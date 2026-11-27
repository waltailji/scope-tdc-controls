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
    ///  - After a launch the tube stays flooded and empty with its door open, in the game too:
    ///    once the launch is complete the tube is set back to flooded (water in, no torpedo), so
    ///    the game's torpedo panel, the torpedo room and the crew all see it flooded. The crew
    ///    won't reload it until you shut the door and drain it; draining is the game's own drain
    ///    (its time and sound), and a drained empty tube is set to empty (the game would call it
    ///    loaded). A flooded empty tube can't be selected or fired. Being the game's own state,
    ///    it is saved with the game; with the option off, such a tube is simply drained.
    ///  - A tube never drains with its door open. Draining it here waits for you to shut the
    ///    door; a drain from the game itself (its torpedo panel, the crew at the flood valve),
    ///    which knows nothing of the door, shuts the door first (3 s) and then goes ahead.
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
            public int PendingDrain;     // a game drain waiting for the door to shut: 0 none, 1 Drain, 2 DrainInstantly
            public bool PendingSilent;
            public float FiredAt = float.NegativeInfinity;
            // the tube's launch animation, driven to hold the door
            public bool AnimResolved, AnimOk;
            public Animator Anim;
            public int StateHash;
            public float OpenNorm;       // normalized time in the launch animation where the door is fully open
            public float Posed = -1f;    // last door position applied to the animation
            public bool Held;            // the tube's animator is held by the door (speed 0); else the game's
        }

        static readonly Dictionary<TorpedoLauncher, Tube> tubes = new Dictionary<TorpedoLauncher, Tube>();
        static readonly AccessTools.FieldRef<TorpedoLauncher, Animator> animatorRef = AccessTools.FieldRefAccess<TorpedoLauncher, Animator>("launcherAnimator");
        static AccessTools.FieldRef<TorpedoLauncher, bool> isFloodedRef;
        static AccessTools.FieldRef<TorpedoLauncher, float> floodingProgressRef;
        static System.Reflection.MethodInfo setStateMethod;
        static bool stateResolved, stateOk;

        static bool ResolveState()
        {
            if (stateResolved) return stateOk;
            stateResolved = true;
            try
            {
                isFloodedRef = AccessTools.FieldRefAccess<TorpedoLauncher, bool>("isFlooded");
                floodingProgressRef = AccessTools.FieldRefAccess<TorpedoLauncher, float>("floodingProgress");
                setStateMethod = AccessTools.PropertySetter(typeof(TorpedoLauncher), "CurrentState");
                stateOk = setStateMethod != null;
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube state fields not found: " + e.Message); stateOk = false; }
            if (!stateOk) Debug.LogWarning(ScopeTdcControlsMod.Tag + " fired tubes can't be shown flooded to the game (state setter not found)");
            return stateOk;
        }

        static void SetState(TorpedoLauncher l, TorpedoLauncher.State s)
        {
            if (l.CurrentState == s || !ResolveState()) return;
            setStateMethod.Invoke(l, new object[] { s }); // the game's setter: its panel and the torpedo room hear of it
        }

        /// <summary>Water in, no torpedo, as the game's own flooded state (after a shot, the launch complete).</summary>
        static void MakeFloodedEmpty(TorpedoLauncher l)
        {
            if (!ResolveState()) return;
            isFloodedRef(l) = true;
            floodingProgressRef(l) = 1f;
            var x = l as ExtendedTorpedoLauncher;
            if (x != null && x.IsMarkedForLaunch) x.IsMarkedForLaunch = false; // nothing in it to fire
            SetState(l, TorpedoLauncher.State.Flooded);
        }

        /// <summary>No water and no torpedo: the game's empty state (a drained empty tube, or the option off).</summary>
        static void MakeDryEmpty(TorpedoLauncher l)
        {
            if (!ResolveState()) return;
            isFloodedRef(l) = false;
            floodingProgressRef(l) = 0f;
            SetState(l, TorpedoLauncher.State.Empty);
        }

        /// <summary>The game holds this tube flooded with no torpedo in it.</summary>
        static bool GameFloodedEmpty(TorpedoLauncher l)
        {
            return l != null && l.IsFlooded && l.Torpedo == null && l.CurrentState == TorpedoLauncher.State.Flooded;
        }

        /// <summary>The game finished draining a tube with nothing in it (it calls that loaded): make it empty.</summary>
        internal static void AfterDrain(TorpedoLauncher l)
        {
            try
            {
                if (l == null || l.IsFlooded || l.Torpedo != null || l.CurrentState != TorpedoLauncher.State.Loaded) return;
                var t = Of(l, false);
                if (t != null) t.WetEmpty = false;
                MakeDryEmpty(l);
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " fired tube drained: " + l.name);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
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

        /// <summary>Fired and still flooded: no torpedo, water in the tube (also one loaded from a save).</summary>
        public static bool WetEmpty(TorpedoLauncher l)
        {
            var t = Of(l, false);
            if (t != null && t.WetEmpty) return true;
            if (GameFloodedEmpty(l))
            {
                if (!Enabled) { MakeDryEmpty(l); return false; } // option off: no tube is left flooded and empty
                Of(l, true).WetEmpty = true; // a saved game kept it flooded and empty
                return true;
            }
            return false;
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

        /// <summary>
        /// The game asked to drain a tube whose door isn't shut (its panel, the crew's valve): the
        /// door shuts, and the drain follows once it is (see Tick).
        /// </summary>
        internal static void DrainAfterClosing(TorpedoLauncher l, bool instant, bool silent)
        {
            var t = Of(l, true);
            if (CanClose(l)) Close(l);
            t.PendingDrain = instant ? 2 : 1;
            t.PendingSilent = silent;
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " drain of " + l.name + " asked with the outer door open: shutting it first");
        }

        /// <summary>Drain a fired (flooded, empty) tube with its door shut.</summary>
        public static bool CanDrainEmpty(TorpedoLauncher l)
        {
            return Enabled && WetEmpty(l) && Door(l) == DoorState.Shut && l.IsFlooded && l.CurrentState == TorpedoLauncher.State.Flooded;
        }

        /// <summary>The game's own drain (time, sound, torpedo room); <see cref="AfterDrain"/> makes it empty when done.</summary>
        public static void DrainEmpty(TorpedoLauncher l)
        {
            if (!CanDrainEmpty(l)) return;
            l.Drain(false);
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " draining fired tube: " + l.name);
        }

        /// <summary>A torpedo left the tube (any launch: the levers, the game's fire key, the crew).</summary>
        internal static void OnLaunched(TorpedoLauncher l)
        {
            var t = Of(l, true);
            t.FiredAt = Time.time;
            if (!Enabled)
            {
                // doors not modelled: let the game's launch animation (just restarted) run as usual
                Release(l, t, false); // the game's launch animation (just restarted) runs as usual
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

        /// <summary>
        /// Hand the tube's animator back to the game (its breech door and launch animations need
        /// it running): the launch animation set to its end (door shut, as after a launch) and its
        /// speed back to normal. toEnd false: leave the animation where the game just put it.
        /// </summary>
        static void Release(TorpedoLauncher l, Tube t, bool toEnd)
        {
            t.Posed = -1f;
            if (!t.Held) return;
            t.Held = false;
            if (t.Anim == null) return;
            try
            {
                if (toEnd && t.AnimOk) t.Anim.Play(t.StateHash, 0, 1f);
                t.Anim.speed = 1f;
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " tube door animation: " + e.Message); }
        }

        /// <summary>Hold the launch animation with the door at <paramref name="open"/> (0 shut .. 1 open).</summary>
        static void Pose(TorpedoLauncher l, Tube t, float open)
        {
            if (t.Held && Mathf.Abs(open - t.Posed) < 0.0005f) return;
            ResolveAnim(l, t);
            t.Posed = open;
            if (!t.AnimOk || t.Anim == null) return;
            try
            {
                t.Held = true;
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
                var state = l.CurrentState;
                if (!Enabled)
                {
                    // option off: a tube held flooded and empty is simply dry and empty again
                    if (t.WetEmpty && state != TorpedoLauncher.State.Draining && GameFloodedEmpty(l)) MakeDryEmpty(l);
                    t.WetEmpty = false;
                    if (t.Door != DoorState.Shut) t.Door = DoorState.Shut;
                    if (t.Held) Release(l, t, true);
                    continue;
                }
                // the crew loaded it anyway (an order to this tube): it was drained for that
                if (t.WetEmpty && (state == TorpedoLauncher.State.Loading || state == TorpedoLauncher.State.Preheating
                                   || (state == TorpedoLauncher.State.Loaded && l.Torpedo != null))) t.WetEmpty = false;
                // the launch is complete (the game made the tube dry and empty): flooded again, as it is
                if (t.WetEmpty && state == TorpedoLauncher.State.Empty && l.Torpedo == null) MakeFloodedEmpty(l);
                // drained by other means (the game's panel, the crew's valve) that ended as "loaded"
                if (t.WetEmpty && state == TorpedoLauncher.State.Loaded && l.Torpedo == null && !l.IsFlooded) AfterDrain(l);
                // no water in the tube any more: the door can't be open
                if (!l.IsFlooded && !t.WetEmpty && t.Door != DoorState.Shut && l.CurrentState != TorpedoLauncher.State.Launching)
                    t.Door = DoorState.Shut;
                // a game drain waiting for the door: go ahead once it is shut; dropped if it is opened again
                if (t.PendingDrain != 0)
                {
                    Settle(t);
                    if (t.Door == DoorState.Opening || t.Door == DoorState.Open) t.PendingDrain = 0;
                    else if (t.Door == DoorState.Shut)
                    {
                        int how = t.PendingDrain;
                        t.PendingDrain = 0;
                        if (l.IsFlooded && l.CurrentState != TorpedoLauncher.State.Draining)
                        {
                            if (how == 2) l.DrainInstantly();
                            else l.Drain(t.PendingSilent);
                        }
                    }
                }
                float open = Settle(t);
                // the animator is the door's only while it is not shut; shut, the game has it back
                if (t.Door != DoorState.Shut) Pose(l, t, open);
                else if (t.Held) Release(l, t, true);
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
                if (!TubeDoors.Enabled || l == null || !TubeDoors.IsPlayerTube(l)) return true; // option off: the game's own launch
                if (l.Torpedo == null) return false;                       // flooded after a shot, nothing in it to fire
                if (l.Torpedo is StoredMinePacket) return true;            // mines are laid as the game does it
                if (TubeDoors.DoorReady(l)) return true;
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
        static bool Prefix(TorpedoLauncher __instance, bool silent) { return DoorDrainHook.Allow(__instance, false, silent); }
    }

    [HarmonyPatch(typeof(TorpedoLauncher), "DrainInstantly")]
    static class TorpedoLauncher_DrainInstantly_DoorPatch
    {
        static bool Prefix(TorpedoLauncher __instance) { return DoorDrainHook.Allow(__instance, true, false); }
    }

    [HarmonyPatch(typeof(TorpedoLauncher), "DrainingUpdate")]
    static class TorpedoLauncher_DrainingUpdate_EmptyPatch
    {
        static void Postfix(TorpedoLauncher __instance) { TubeDoors.AfterDrain(__instance); }
    }

    [HarmonyPatch(typeof(TorpedoLauncher), "DrainInstantly")]
    static class TorpedoLauncher_DrainInstantly_EmptyPatch
    {
        static void Postfix(TorpedoLauncher __instance) { TubeDoors.AfterDrain(__instance); }
    }

    /// <summary>A flooded tube with nothing in it (fired, still flooded) can't be selected for a shot.</summary>
    [HarmonyPatch(typeof(ExtendedTorpedoLauncher), "IsMarkedForLaunch", MethodType.Setter)]
    static class ExtendedTorpedoLauncher_MarkEmpty_Patch
    {
        static bool Prefix(ExtendedTorpedoLauncher __instance, bool value)
        {
            try { return !(value && TubeDoors.Enabled && __instance.IsFlooded && __instance.Torpedo == null); }
            catch { return true; }
        }
    }

    /// <summary>
    /// The crew's launch lever in the torpedo room: with the option on, it can't be pulled until
    /// the tube's outer door is fully open (it would fire into a shut door); its tooltip says why.
    /// </summary>
    [HarmonyPatch(typeof(UBOAT.Game.Scene.Characters.Actions.UseTorpedoLauncherLaunchLever), "GetState")]
    static class UseTorpedoLauncherLaunchLever_GetState_DoorPatch
    {
        static void Postfix(UBOAT.Game.Scene.Characters.Activators.Activator activator,
                            ref UBOAT.Game.Scene.Characters.Actions.ActionStateData __result)
        {
            try
            {
                if (__result.ActionState != UBOAT.Game.Scene.Characters.Actions.ActionState.Enabled || !TubeDoors.Enabled) return;
                var lever = activator != null ? activator.GetComponent<TorpedoLauncherLaunchLever>() : null;
                var l = lever != null ? lever.Launcher : null;
                if (l == null || !TubeDoors.IsPlayerTube(l) || l.Torpedo is StoredMinePacket || TubeDoors.DoorReady(l)) return;
                __result = new UBOAT.Game.Scene.Characters.Actions.ActionStateData(UBOAT.Game.Scene.Characters.Actions.ActionState.Disabled);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(UBOAT.Game.Scene.Characters.Actions.UseTorpedoLauncherLaunchLever), "GetTooltipContents")]
    static class UseTorpedoLauncherLaunchLever_Tooltip_DoorPatch
    {
        static void Postfix(UBOAT.Game.Scene.Characters.Actions.UseTorpedoLauncherLaunchLever __instance, ref string __result)
        {
            try
            {
                if (!TubeDoors.Enabled) return;
                var lever = Traverse.Create(__instance).Field("hoveredActivator").GetValue<Component>(); // the lever the tooltip is for
                var tl = lever != null ? lever.GetComponent<TorpedoLauncherLaunchLever>() : null;
                var l = tl != null ? tl.Launcher : null;
                if (l == null || l.Torpedo is StoredMinePacket || TubeDoors.DoorReady(l)) return;
                __result = (__result ?? "") + "\n" + Strings.LeverDoorNotOpen;
            }
            catch { }
        }
    }

    static class DoorDrainHook
    {
        /// <summary>
        /// A drain with the outer door not shut: held, the door shut, and the drain started again
        /// once it is (the game, which has no outer doors, asked for it: its panel or the crew).
        /// </summary>
        public static bool Allow(TorpedoLauncher l, bool instant, bool silent)
        {
            try
            {
                if (!TubeDoors.Enabled || TubeDoors.DoorShut(l) || !TubeDoors.IsPlayerTube(l)) return true;
                if (!l.IsFlooded) return true; // nothing to drain: the game returns at once anyway
                TubeDoors.DrainAfterClosing(l, instant, silent);
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
