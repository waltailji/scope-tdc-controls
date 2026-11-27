using System;
using System.Collections.Generic;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    public enum DoorState { Shut, Opening, Open, Closing }

    /// <summary>
    /// The torpedo tubes' outer (muzzle) doors, which the base game doesn't model: it only
    /// plays the tube's "Launch" animation (door open and shut) when a torpedo goes.
    ///
    ///  - The door opens only on a flooded tube (right-click a tube: Open outer door), in
    ///    <see cref="OpenTime"/> seconds, driving the tube's own launch animation up to the
    ///    point where the door is fully open, and stays there. Closing runs it back.
    ///  - A tube fires only with its door fully open: the launch levers refuse, and the game's
    ///    fire sequence (its fire key) skips the tube.
    ///  - After a launch the tube stays flooded and empty with its door open: the game's own
    ///    reset to "dry and empty" is taken over. The crew won't reload it (it reads as in use)
    ///    until you close the door and drain it.
    ///  - A tube can't be drained (here, on the game's torpedo panel or by the crew's flood
    ///    valve) while its door is not shut.
    /// The door state is not saved with the game: after loading, all doors are shut.
    /// </summary>
    public static class TubeDoors
    {
        public const float OpenTime = 3f, CloseTime = 3f;
        public const float FiringTime = 4f; // as long as the firing circuit lamp stays lit (FiringLampTime)

        sealed class Tube
        {
            public DoorState Door;
            public float From;           // door position (0 shut .. 1 open) when the move began
            public float Since;          // Time.time the move began
            public bool WetEmpty;        // fired: the tube is flooded, without a torpedo
            public float FiredAt = float.NegativeInfinity;
            // the tube's launch animation, driven to hold the door
            public bool AnimResolved, AnimOk;
            public Animator Anim;
            public int StateHash;
            public float OpenNorm;       // normalized time in the launch animation where the door is fully open
            public float Posed = -1f;    // last door position applied to the animation
        }

        static readonly Dictionary<TorpedoLauncher, Tube> tubes = new Dictionary<TorpedoLauncher, Tube>();
        static readonly AccessTools.FieldRef<TorpedoLauncher, Animator> animatorRef = AccessTools.FieldRefAccess<TorpedoLauncher, Animator>("launcherAnimator");
        static Driver driver;

        public static bool Enabled { get { return GyroConfig.EnhancedTubeHandling; } }

        static Tube Of(TorpedoLauncher l, bool create)
        {
            if (l == null) return null;
            Tube t;
            if (tubes.TryGetValue(l, out t)) return t;
            if (!create) return null;
            t = new Tube();
            tubes[l] = t;
            EnsureDriver();
            return t;
        }

        public static bool IsPlayerTube(TorpedoLauncher l)
        {
            try
            {
                var g = l != null ? l.Group : null;
                return g != null && g.ParentEntity is PlayerShip;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ state

        /// <summary>0 = shut .. 1 = fully open; settles a finished move.</summary>
        public static float Openness(TorpedoLauncher l)
        {
            var t = Of(l, false);
            return t == null ? 0f : Settle(t);
        }

        static float Settle(Tube t)
        {
            switch (t.Door)
            {
                case DoorState.Open: return 1f;
                case DoorState.Opening:
                {
                    float p = Mathf.Clamp01(t.From + (Time.time - t.Since) / OpenTime);
                    if (p >= 1f) t.Door = DoorState.Open;
                    return p;
                }
                case DoorState.Closing:
                {
                    float p = Mathf.Clamp01(t.From - (Time.time - t.Since) / CloseTime);
                    if (p <= 0f) t.Door = DoorState.Shut;
                    return p;
                }
                default: return 0f;
            }
        }

        public static DoorState Door(TorpedoLauncher l)
        {
            var t = Of(l, false);
            if (t == null) return DoorState.Shut;
            Settle(t);
            return t.Door;
        }

        /// <summary>The door is fully open, or doors aren't modelled (option off).</summary>
        public static bool DoorReady(TorpedoLauncher l) { return !Enabled || Door(l) == DoorState.Open; }

        /// <summary>The door is shut (or doors aren't modelled): the tube may be drained.</summary>
        public static bool DoorShut(TorpedoLauncher l) { return !Enabled || Door(l) == DoorState.Shut; }

        /// <summary>Fired and still flooded: no torpedo, water in the tube.</summary>
        public static bool WetEmpty(TorpedoLauncher l)
        {
            var t = Of(l, false);
            return t != null && t.WetEmpty;
        }

        /// <summary>Water in the tube: flooded with a torpedo, or flooded and empty after a shot.</summary>
        public static bool Wet(TorpedoLauncher l) { return l != null && (l.IsFlooded || WetEmpty(l)); }

        /// <summary>A torpedo left this tube less than <see cref="FiringTime"/> seconds ago.</summary>
        public static bool Firing(TorpedoLauncher l)
        {
            var t = Of(l, false);
            return t != null && Time.time < t.FiredAt + FiringTime;
        }

        // ------------------------------------------------------------------ actions

        static bool Busy(TorpedoLauncher l)
        {
            var s = l.CurrentState;
            return s == TorpedoLauncher.State.Flooding || s == TorpedoLauncher.State.Draining || s == TorpedoLauncher.State.Launching;
        }

        public static bool CanOpen(TorpedoLauncher l)
        {
            if (!Enabled || l == null || !Wet(l) || Busy(l)) return false;
            var d = Door(l);
            return d == DoorState.Shut || d == DoorState.Closing;
        }

        public static bool CanClose(TorpedoLauncher l)
        {
            if (!Enabled || l == null) return false;
            var d = Door(l);
            return d == DoorState.Open || d == DoorState.Opening;
        }

        public static void Open(TorpedoLauncher l)
        {
            if (!CanOpen(l)) return;
            var t = Of(l, true);
            t.From = Settle(t);
            t.Since = Time.time;
            t.Door = DoorState.Opening;
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " outer door opening: " + l.name);
        }

        public static void Close(TorpedoLauncher l)
        {
            if (!CanClose(l)) return;
            var t = Of(l, true);
            t.From = Settle(t);
            t.Since = Time.time;
            t.Door = DoorState.Closing;
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " outer door closing: " + l.name);
        }

        /// <summary>Drain a fired (flooded, empty) tube with its door shut.</summary>
        public static bool CanDrainEmpty(TorpedoLauncher l) { return Enabled && WetEmpty(l) && Door(l) == DoorState.Shut; }

        public static void DrainEmpty(TorpedoLauncher l)
        {
            if (!CanDrainEmpty(l)) return;
            Of(l, false).WetEmpty = false;
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " fired tube drained: " + l.name);
        }

        /// <summary>A torpedo left the tube (any launch: the levers, the game's fire key, the crew).</summary>
        internal static void OnLaunched(TorpedoLauncher l)
        {
            var t = Of(l, true);
            t.FiredAt = Time.time;
            if (!Enabled)
            {
                // doors not modelled: let the game's launch animation (just restarted) run as usual
                if (t.Anim != null) { try { t.Anim.speed = 1f; } catch { } }
                t.Posed = -1f;
                return;
            }
            t.WetEmpty = true; // the game makes the tube dry and empty at once; it stays flooded here
            Settle(t);
            if (t.Door != DoorState.Open)
            {
                // fired another way with the door not open (the crew at the torpedo-room lever):
                // the door opened for the shot and stays open
                t.Door = DoorState.Open;
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " tube " + l.name + " fired with its outer door not open; door now open");
            }
            t.Posed = -1f;   // the game just restarted the launch animation: hold it at open again now
            Pose(l, t, 1f);
        }

        // ------------------------------------------------------------------ the 3D door

        static void ResolveAnim(TorpedoLauncher l, Tube t)
        {
            if (t.AnimResolved) return;
            t.AnimResolved = true;
            try
            {
                var a = animatorRef(l);
                if (a == null || a.runtimeAnimatorController == null)
                {
                    if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " tube " + l.name + ": no launch animation");
                    return;
                }
                t.Anim = a;
                t.StateHash = Animator.StringToHash("Launch");
                if (!a.HasState(0, t.StateHash))
                {
                    Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube " + l.name + ": launch animation has no 'Launch' state");
                    return;
                }
                AnimationClip clip = null;
                var clips = a.runtimeAnimatorController.animationClips;
                if (clips != null)
                {
                    foreach (var c in clips)
                        if (c != null && c.name.IndexOf("launch", StringComparison.OrdinalIgnoreCase) >= 0) { clip = c; break; }
                    if (clip == null && clips.Length == 1) clip = clips[0];
                }
                t.OpenNorm = clip != null ? FindOpenTime(a, clip) : 0.5f;
                t.AnimOk = true;
                if (GyroConfig.DebugLog)
                    Debug.Log(ScopeTdcControlsMod.Tag + " tube " + l.name + ": launch clip '" + (clip != null ? clip.name : "?") + "' "
                        + (clip != null ? clip.length.ToString("0.00") : "?") + " s, door fully open at " + t.OpenNorm.ToString("0.00"));
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube launch animation: " + e.Message); }
        }

        /// <summary>
        /// Where in the launch animation the door is fully open: the clip is sampled along its
        /// length, and the first moment the moving parts are (nearly) furthest from the start
        /// pose is taken. The pose is put back afterwards.
        /// </summary>
        static float FindOpenTime(Animator a, AnimationClip clip)
        {
            var trs = a.GetComponentsInChildren<Transform>(true);
            int n = trs.Length;
            var savePos = new Vector3[n];
            var saveRot = new Quaternion[n];
            var saveScale = new Vector3[n];
            for (int i = 0; i < n; i++) { savePos[i] = trs[i].localPosition; saveRot[i] = trs[i].localRotation; saveScale[i] = trs[i].localScale; }
            const int steps = 60;
            var dev = new float[steps + 1];
            try
            {
                clip.SampleAnimation(a.gameObject, 0f);
                var basePos = new Vector3[n];
                var baseRot = new Quaternion[n];
                for (int i = 0; i < n; i++) { basePos[i] = trs[i].localPosition; baseRot[i] = trs[i].localRotation; }
                for (int s = 0; s <= steps; s++)
                {
                    clip.SampleAnimation(a.gameObject, clip.length * s / steps);
                    float d = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float di = Quaternion.Angle(baseRot[i], trs[i].localRotation) + Vector3.Distance(basePos[i], trs[i].localPosition) * 100f;
                        if (di > d) d = di;
                    }
                    dev[s] = d;
                }
            }
            finally
            {
                for (int i = 0; i < n; i++) { trs[i].localPosition = savePos[i]; trs[i].localRotation = saveRot[i]; trs[i].localScale = saveScale[i]; }
            }
            float max = 0f;
            for (int s = 0; s <= steps; s++) max = Mathf.Max(max, dev[s]);
            if (max < 0.5f) return 0.5f;
            for (int s = 0; s <= steps; s++)
                if (dev[s] >= max * 0.97f) return (float)s / steps;
            return 0.5f;
        }

        /// <summary>Hold the launch animation with the door at <paramref name="open"/> (0 shut .. 1 open).</summary>
        static void Pose(TorpedoLauncher l, Tube t, float open)
        {
            if (Mathf.Abs(open - t.Posed) < 0.0005f) return;
            ResolveAnim(l, t);
            t.Posed = open;
            if (!t.AnimOk || t.Anim == null) return;
            try
            {
                t.Anim.speed = 0f;
                t.Anim.Play(t.StateHash, 0, t.OpenNorm * open);
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube door animation: " + e.Message); t.AnimOk = false; }
        }

        // ------------------------------------------------------------------ every frame

        static readonly List<TorpedoLauncher> gone = new List<TorpedoLauncher>();

        static void Tick()
        {
            gone.Clear();
            foreach (var kv in tubes)
            {
                var l = kv.Key;
                var t = kv.Value;
                if (l == null) { gone.Add(l); continue; }
                if (!Enabled)
                {
                    t.WetEmpty = false;
                    if (t.Door != DoorState.Shut) { t.Door = DoorState.Shut; Pose(l, t, 0f); }
                    continue;
                }
                // the crew loaded it anyway (an order to this tube): it was drained for that
                if (t.WetEmpty && (l.Torpedo != null || l.CurrentState == TorpedoLauncher.State.Loading)) t.WetEmpty = false;
                // no water in the tube any more: the door can't be open
                if (!l.IsFlooded && !t.WetEmpty && t.Door != DoorState.Shut && l.CurrentState != TorpedoLauncher.State.Launching)
                    t.Door = DoorState.Shut;
                float open = Settle(t);
                if (t.AnimResolved || t.Door != DoorState.Shut || t.Posed >= 0f) Pose(l, t, open);
            }
            foreach (var l in gone) tubes.Remove(l);
        }

        static void EnsureDriver()
        {
            if (driver != null) return;
            var go = new GameObject("ScopeTdcControls.TubeDoors");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            driver = go.AddComponent<Driver>();
        }

        sealed class Driver : MonoBehaviour
        {
            void Update()
            {
                try { Tick(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    // ---------------------------------------------------------------------- patches

    /// <summary>A torpedo left a player tube (the gyro-angle launch: levers, fire sequence).</summary>
    [HarmonyPatch(typeof(TorpedoLauncher), "Launch", new Type[] {
        typeof(Entity), typeof(float), typeof(float), typeof(float), typeof(float),
        typeof(int), typeof(bool), typeof(bool), typeof(float), typeof(float) })]
    static class TorpedoLauncher_LaunchGyro_DoorPatch
    {
        static bool Prefix(TorpedoLauncher __instance, out bool __state) { __state = __instance.IsFlooded; DoorLaunchHook.Note(__instance); return DoorLaunchHook.Allow(__instance); }
        static void Postfix(TorpedoLauncher __instance, bool __state) { DoorLaunchHook.After(__instance, __state); }
    }

    /// <summary>A torpedo left a player tube (the capture-position launch).</summary>
    [HarmonyPatch(typeof(TorpedoLauncher), "Launch", new Type[] {
        typeof(Entity), typeof(Vector2), typeof(Vector2), typeof(float), typeof(float),
        typeof(int), typeof(bool), typeof(bool), typeof(float), typeof(float) })]
    static class TorpedoLauncher_LaunchCapture_DoorPatch
    {
        static bool Prefix(TorpedoLauncher __instance, out bool __state) { __state = __instance.IsFlooded; DoorLaunchHook.Note(__instance); return DoorLaunchHook.Allow(__instance); }
        static void Postfix(TorpedoLauncher __instance, bool __state) { DoorLaunchHook.After(__instance, __state); }
    }

    static class DoorLaunchHook
    {
        static bool loggedHeld;
        static StoredTorpedo inTube;   // the tube's torpedo just before the launch (it becomes the running torpedo)

        public static void Note(TorpedoLauncher l)
        {
            try { inTube = l != null ? l.Torpedo : null; } catch { inTube = null; }
        }

        /// <summary>
        /// The game's fire sequence (fire key, or the launch lever) skips a tube whose outer
        /// door is not fully open. The torpedo-room levers are not held here.
        /// </summary>
        public static bool Allow(TorpedoLauncher l)
        {
            try
            {
                if (!FireCommand.InSequence || !TubeDoors.Enabled || TubeDoors.DoorReady(l) || !TubeDoors.IsPlayerTube(l)) return true;
                if (!loggedHeld || GyroConfig.DebugLog)
                {
                    loggedHeld = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " tube '" + l.name + "' not fired: outer door not fully open");
                }
                return false;
            }
            catch { return true; }
        }

        public static void After(TorpedoLauncher l, bool wasFlooded)
        {
            try
            {
                // the launch went ahead: the game cleared the flooded flag
                if (wasFlooded && l != null && !l.IsFlooded && TubeDoors.IsPlayerTube(l))
                {
                    TubeDoors.OnLaunched(l);
                    UBOAT.Game.Scene.Entities.Torpedo running = null;
                    try { running = inTube != null ? inTube.GetComponent<UBOAT.Game.Scene.Entities.Torpedo>() : null; } catch { }
                    GyroCounterUI.OnTorpedoLaunched(l, running); // the run timer near the top of the screen
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>No draining while the outer door is not shut (torpedo panel, flood valve, this mod).</summary>
    [HarmonyPatch(typeof(TorpedoLauncher), "Drain", new Type[] { typeof(bool) })]
    static class TorpedoLauncher_Drain_DoorPatch
    {
        static bool Prefix(TorpedoLauncher __instance) { return DoorDrainHook.Allow(__instance); }
    }

    [HarmonyPatch(typeof(TorpedoLauncher), "DrainInstantly")]
    static class TorpedoLauncher_DrainInstantly_DoorPatch
    {
        static bool Prefix(TorpedoLauncher __instance) { return DoorDrainHook.Allow(__instance); }
    }

    static class DoorDrainHook
    {
        public static bool Allow(TorpedoLauncher l)
        {
            try
            {
                if (!TubeDoors.Enabled || TubeDoors.DoorShut(l) || !TubeDoors.IsPlayerTube(l)) return true;
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " drain held: outer door of " + l.name + " not shut");
                return false;
            }
            catch { return true; }
        }
    }

    /// <summary>A fired tube still full of water reads as in use: the crew won't pick it to reload.</summary>
    [HarmonyPatch(typeof(ExtendedTorpedoLauncher), "GetAccessibilityStatus")]
    static class ExtendedTorpedoLauncher_GetAccessibilityStatus_DoorPatch
    {
        static void Postfix(ExtendedTorpedoLauncher __instance, ref ExtendedTorpedoLauncher.TubeAccessStatus __result)
        {
            if (__result == ExtendedTorpedoLauncher.TubeAccessStatus.Accessible && TubeDoors.Enabled && TubeDoors.WetEmpty(__instance))
                __result = ExtendedTorpedoLauncher.TubeAccessStatus.BlockedByOtherTubeUse;
        }
    }
}
