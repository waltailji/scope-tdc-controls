using System;
using System.Collections.Generic;
using System.Reflection;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Optional link to the Torpedo Room mod (Landomatic). Torpedo Room keeps each
    /// torpedo's own settings in a status it attaches to the torpedo
    /// (Landomatic.TorpedoRoom.PreparationStatus.Value: Pistol, Depth + DepthOverride,
    /// Speed + SpeedOverride, Revision) and substitutes them at launch; it also sets
    /// the pistol back from that status when the tube fires.
    ///
    /// So when it is installed, the torpedo settings window reads and writes those
    /// per-torpedo values (otherwise a tube with its own values would fire with them,
    /// and a pistol change made here would be undone at launch). Everything is found by
    /// name; without Torpedo Room (or if it changes), Present is false and nothing here
    /// is used.
    /// </summary>
    static class TorpedoRoomLink
    {
        public const float MaxDepth = 14f;   // Torpedo Room's depth limit

        static bool resolved;
        static MethodInfo statusMethod;     // Runtime.Status(StoredTorpedo item, bool create)
        static MethodInfo setPistolMethod;  // Runtime.SetPistol(Torpedo missile, int mode)
        static FieldInfo valueField;        // PreparationStatus.Value
        static FieldInfo fPistol, fDepth, fDepthOverride, fSpeed, fSpeedOverride, fRevision;
        // Torpedo Room's own edit path (what its depth / speed dial hooks use) and its "applying" guard
        static MethodInfo acceptViewport;   // Runtime.AcceptViewport(TubeSnapshot[] edits, object peer, bool tdc, bool independent)
        static MethodInfo prepCopy;         // Preparation.Copy()
        static Type snapshotType;           // TubeSnapshot
        static FieldInfo snapIndex, snapSettings;
        static FieldInfo applyingField;     // Runtime.Applying: while > 0 its dial hooks stand aside

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                Type runtime = null, status = null, prep = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (snapshotType == null) snapshotType = asm.GetType("Landomatic.TorpedoRoom.TubeSnapshot", false);
                    if (runtime == null) runtime = asm.GetType("Landomatic.TorpedoRoom.Runtime", false);
                    if (status == null) status = asm.GetType("Landomatic.TorpedoRoom.PreparationStatus", false);
                    if (prep == null) prep = asm.GetType("Landomatic.TorpedoRoom.Preparation", false);
                }
                if (runtime == null || status == null || prep == null) return;
                const BindingFlags any = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                statusMethod = runtime.GetMethod("Status", any, null, new[] { typeof(StoredTorpedo), typeof(bool) }, null);
                setPistolMethod = runtime.GetMethod("SetPistol", any, null, new[] { typeof(Torpedo), typeof(int) }, null);
                valueField = status.GetField("Value");
                fPistol = prep.GetField("Pistol");
                fDepth = prep.GetField("Depth");
                fDepthOverride = prep.GetField("DepthOverride");
                fSpeed = prep.GetField("Speed");
                fSpeedOverride = prep.GetField("SpeedOverride");
                fRevision = prep.GetField("Revision");
                prepCopy = prep.GetMethod("Copy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                applyingField = runtime.GetField("Applying", any);
                if (snapshotType != null)
                {
                    snapIndex = snapshotType.GetField("Index");
                    snapSettings = snapshotType.GetField("Settings");
                    foreach (var m in runtime.GetMethods(any))
                    {
                        if (m.Name != "AcceptViewport") continue;
                        var ps = m.GetParameters();
                        if (ps.Length >= 2 && ps[0].ParameterType == snapshotType.MakeArrayType()) { acceptViewport = m; break; }
                    }
                }
                if (Present) Debug.Log(ScopeTdcControlsMod.Tag + " Torpedo Room found: torpedo settings are per torpedo");
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " Torpedo Room link unavailable: " + e.Message);
                statusMethod = null;
            }
        }

        public static bool Present
        {
            get
            {
                Resolve();
                return statusMethod != null && valueField != null && fPistol != null && fDepth != null
                       && fDepthOverride != null && fSpeed != null && fSpeedOverride != null;
            }
        }

        /// <summary>The torpedo's Torpedo Room settings (created if missing and Torpedo Room is active), or null.</summary>
        static object Prep(StoredTorpedo item, bool create)
        {
            if (!Present || item == null) return null;
            try
            {
                var status = statusMethod.Invoke(null, new object[] { item, create });
                return status != null ? valueField.GetValue(status) : null;
            }
            catch { return null; }
        }

        static void Bump(object prep)
        {
            if (fRevision == null) return;
            int r = (int)fRevision.GetValue(prep);
            if (r < int.MaxValue) fRevision.SetValue(prep, r + 1); // Torpedo Room tracks changes by revision
        }

        /// <summary>
        /// Run an action that changes the game's shared torpedo depth / speed (or the TDC's torpedo
        /// speed) with Torpedo Room's dial hooks standing aside: they copy the game dial's value onto
        /// every checked torpedo, and mid-change that can be the old value.
        /// </summary>
        public static void SharedChange(Action change)
        {
            bool guard = Present && applyingField != null;
            if (guard) { try { applyingField.SetValue(null, (int)applyingField.GetValue(null) + 1); } catch { guard = false; } }
            try { change(); }
            finally { if (guard) { try { applyingField.SetValue(null, (int)applyingField.GetValue(null) - 1); } catch { } } }
        }

        /// <summary>
        /// Set depth (or speed) on the checked torpedoes through Torpedo Room's own edit path, as its
        /// depth / speed dials do (validated, revision kept in step); if it turns the edit down (e.g.
        /// its viewport sync is off), write them directly as before.
        /// </summary>
        public static void SetDepthAll(PlayerShip ship, IList<StoredTorpedo> items, float depth)
        {
            depth = Mathf.Clamp(depth, 1f, MaxDepth);
            if (Commit(ship, items, p => { fDepth.SetValue(p, depth); fDepthOverride.SetValue(p, true); })) return;
            foreach (var st in items) SetDepth(st, depth);
        }

        public static void SetSpeedAll(PlayerShip ship, IList<StoredTorpedo> items, float speed)
        {
            if (speed <= 0f) return;
            if (Commit(ship, items, p => { fSpeed.SetValue(p, speed); fSpeedOverride.SetValue(p, true); })) return;
            foreach (var st in items) SetSpeed(st, speed);
        }

        static bool loggedCommit;

        /// <summary>One edit row per checked torpedo (a copy of its settings, changed), handed to AcceptViewport.</summary>
        static bool Commit(PlayerShip ship, IList<StoredTorpedo> items, Action<object> change)
        {
            if (!Present || acceptViewport == null || prepCopy == null || snapshotType == null || snapIndex == null || snapSettings == null) return false;
            if (ship == null || ship.TorpedoLaunchers == null || items == null || items.Count == 0) return false;
            try
            {
                var rows = new List<object>();
                foreach (var st in items)
                {
                    int index = Array.FindIndex(ship.TorpedoLaunchers, t => t != null && ReferenceEquals(t.Torpedo, st));
                    if (index < 0) return false;
                    var current = Prep(st, true);
                    if (current == null) return false;
                    var edit = prepCopy.Invoke(current, null);
                    change(edit);
                    var row = Activator.CreateInstance(snapshotType);
                    snapIndex.SetValue(row, index);
                    snapSettings.SetValue(row, edit);
                    rows.Add(row);
                }
                var arr = Array.CreateInstance(snapshotType, rows.Count);
                for (int i = 0; i < rows.Count; i++) arr.SetValue(rows[i], i);
                var ps = acceptViewport.GetParameters();
                var args = new object[ps.Length];
                args[0] = arr;
                for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                bool ok = (bool)acceptViewport.Invoke(null, args);
                if (!ok && !loggedCommit && GyroConfig.DebugLog)
                {
                    loggedCommit = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " Torpedo Room turned the edit down (viewport sync off?); writing the torpedoes directly");
                }
                return ok;
            }
            catch (Exception e)
            {
                if (!loggedCommit) { loggedCommit = true; Debug.LogWarning(ScopeTdcControlsMod.Tag + " Torpedo Room edit path failed, writing directly: " + e.GetBaseException().Message); }
                return false;
            }
        }

        /// <summary>The torpedo's own depth, if it has one.</summary>
        public static bool TryGetDepth(StoredTorpedo item, out float depth)
        {
            depth = 0f;
            var p = Prep(item, false);
            if (p == null || !(bool)fDepthOverride.GetValue(p)) return false;
            depth = (float)fDepth.GetValue(p);
            return true;
        }

        public static bool TryGetSpeed(StoredTorpedo item, out float speed)
        {
            speed = 0f;
            var p = Prep(item, false);
            if (p == null || !(bool)fSpeedOverride.GetValue(p)) return false;
            speed = (float)fSpeed.GetValue(p);
            return speed > 0f;
        }

        public static bool TryGetPistol(StoredTorpedo item, out int pistol)
        {
            pistol = 0;
            var p = Prep(item, false);
            if (p == null) return false;
            pistol = (int)fPistol.GetValue(p);
            return true;
        }

        public static void SetDepth(StoredTorpedo item, float depth)
        {
            var p = Prep(item, true);
            if (p == null) return;
            fDepth.SetValue(p, Mathf.Clamp(depth, 1f, MaxDepth));
            fDepthOverride.SetValue(p, true);
            Bump(p);
        }

        public static void SetSpeed(StoredTorpedo item, float speed)
        {
            var p = Prep(item, true);
            if (p == null || speed <= 0f) return;
            fSpeed.SetValue(p, speed);
            fSpeedOverride.SetValue(p, true);
            Bump(p);
        }

        /// <summary>Set the pistol in Torpedo Room's record (so it survives the launch) and on the torpedo.</summary>
        public static bool SetPistol(StoredTorpedo item, Torpedo missile, int mode)
        {
            var p = Prep(item, true);
            if (p == null) return false;
            fPistol.SetValue(p, mode);
            Bump(p);
            try
            {
                if (setPistolMethod != null) setPistolMethod.Invoke(null, new object[] { missile, mode });
                else if (missile != null) missile.PistolSetting = (Torpedo.TorpedoPistolSetting)mode;
            }
            catch { if (missile != null) missile.PistolSetting = (Torpedo.TorpedoPistolSetting)mode; }
            return true;
        }
    }
}
