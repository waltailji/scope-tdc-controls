using System;
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

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                Type runtime = null, status = null, prep = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
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
