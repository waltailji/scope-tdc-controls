using System;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// "Keep my scope bearing": the game turns a periscope away from where you left it in two
    /// places that mods blocking its automatic rotation (in Periscope.Update) don't cover:
    ///   - Periscope_TargetChanged: a new target (another contact selected, or picked / locked by
    ///     the crew) wipes the last direction you aimed it, so it swings to the new target;
    ///   - ResetManualRotation: clicking a ship (3D view, map) or a contact list row snaps the
    ///     scope back onto its target.
    /// With the setting on, the aim survives a target change, and a reset only happens while
    /// you are at the scope itself (where clicking a ship to aim at it is wanted).
    /// </summary>
    static class ScopeBearing
    {
        static AccessTools.FieldRef<Periscope, Vector3> lastUserRotation;
        static bool resolved, broken;

        public static bool Ready()
        {
            if (resolved) return !broken;
            resolved = true;
            try { lastUserRotation = AccessTools.FieldRefAccess<Periscope, Vector3>("lastUserRotation"); }
            catch (Exception e) { broken = true; Debug.LogWarning(ScopeTdcControlsMod.Tag + " keep scope bearing unavailable: " + e.Message); }
            return !broken;
        }

        public static Vector3 Get(Periscope p) { return lastUserRotation(p); }
        public static void Set(Periscope p, Vector3 v) { lastUserRotation(p) = v; }

        public static bool IsSet(Vector3 v) { return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z); }

        /// <summary>
        /// The bridge binoculars (the "Binocular" item, B key) are a Periscope too, but the game
        /// aims them where the camera looks each time you raise them, and mouse look drives them
        /// freely; keeping / restoring a bearing on them fights that. Leave them to the game.
        /// (The UZO is also IsBinocular, but it is a sight you return to, so it keeps the feature.)
        /// </summary>
        public static bool Applies(Periscope p)
        {
            if (!GyroConfig.KeepScopeBearing || p == null) return false;
            try { return !(p.IsBinocular && p.name != null && p.name.StartsWith("Binocular", StringComparison.Ordinal)); }
            catch { return true; }
        }
    }

    /// <summary>
    /// Entering the scope turns it to its "last user rotation", which only manual turning
    /// updates. A lock turns the scope with the target and an unlock leaves it there, but the
    /// stored aim stays the pre-lock one, so coming back to the scope jumped (e.g. 90 degrees).
    /// So: on an unlock and on leaving the scope, the direction it points in becomes the stored aim.
    /// </summary>
    static class ScopeAimSync
    {
        public static void Store(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController controller)
        {
            if (!GyroConfig.KeepScopeBearing || controller == null) return;
            var scope = controller.Periscope;
            if (!ScopeBearing.Applies(scope) || controller.LockTarget != null) return; // locked: the lock decides where it points
            var offset = scope.PeriscopeEulersOffset;
            if (GyroConfig.DebugLog)
                Debug.Log(ScopeTdcControlsMod.Tag + " keep scope bearing: '" + scope.name + "' bearing " + scope.LocalBearing.ToString("0.0")
                          + ", stored aim was " + scope.LastUserRotation + ", now " + offset);
            if (ScopeBearing.IsSet(offset)) scope.LastUserRotation = offset;
        }

        // ---- bearing restore: the view is the game's base direction (which it moves while you are
        // away, e.g. back to the bow after an unlock) plus your offset, so restoring only the offset
        // can land somewhere else. The bearing (relative to the bow) is saved on leaving and the
        // offset adjusted on coming back until the scope is on it again.

        static readonly System.Collections.Generic.Dictionary<Periscope, float> saved = new System.Collections.Generic.Dictionary<Periscope, float>();
        static Periscope restoring;
        static float restoreTo, lastError = float.PositiveInfinity, sign = 1f;
        static int framesLeft, waitFrames;
        static Vector3 lastSet = new Vector3(float.NaN, float.NaN, float.NaN); // the aim this restore last set

        public static void SaveBearing(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController controller)
        {
            if (!GyroConfig.KeepScopeBearing || controller == null) return;
            var scope = controller.Periscope;
            if (!ScopeBearing.Applies(scope)) return;
            if (controller.LockTarget != null) { saved.Remove(scope); return; } // locked: the game re-locks on return
            saved[scope] = scope.LocalBearing;
        }

        /// <summary>You asked for the scope to go back on its target: don't put it back on the old bearing next time.</summary>
        public static void Forget(Periscope scope)
        {
            if (scope == null) return;
            saved.Remove(scope);
            if (restoring == scope) restoring = null;
        }

        public static void BeginRestore(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController controller)
        {
            restoring = null;
            if (!GyroConfig.KeepScopeBearing || controller == null) return;
            var scope = controller.Periscope;
            float bearing;
            if (!ScopeBearing.Applies(scope) || controller.LockTarget != null || !saved.TryGetValue(scope, out bearing)) return;
            restoring = scope;
            restoreTo = bearing;
            lastError = float.PositiveInfinity;
            sign = 1f;
            framesLeft = 90;   // about a second and a half at most
            lastSet = new Vector3(float.NaN, float.NaN, float.NaN);
            waitFrames = 1;
        }

        /// <summary>Each frame at the scope: nudge the offset until the scope is back on the saved bearing.</summary>
        public static void Step(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController controller)
        {
            if (restoring == null || controller == null || controller.Periscope != restoring) return;
            if (controller.LockTarget != null || --framesLeft <= 0) { Finish(controller.Periscope, "gave up"); return; }
            if (--waitFrames > 0) return; // let the game apply the last change and refresh the bearing
            var scope = restoring;
            // you turned the scope yourself meanwhile (the game stores that as your aim): leave it to you
            if (ScopeBearing.IsSet(lastSet) && (scope.LastUserRotation - lastSet).sqrMagnitude > 0.0001f) { Finish(scope, "stopped (scope turned by hand)"); return; }
            float error = Mathf.DeltaAngle(scope.LocalBearing, restoreTo);
            if (Mathf.Abs(error) < 0.1f) { Finish(scope, "done"); return; }
            if (Mathf.Abs(error) > lastError * 0.9f && !float.IsInfinity(lastError)) sign = -sign; // went the wrong way: turn the other way
            lastError = Mathf.Abs(error);
            var offset = scope.PeriscopeEulersOffset;
            if (!ScopeBearing.IsSet(offset)) offset = Vector3.zero;
            offset.y += sign * error;
            scope.SetPeriscopeEulersOffset(offset, true);
            scope.LastUserRotation = offset;
            lastSet = offset;
            waitFrames = 2;
        }

        static void Finish(Periscope scope, string how)
        {
            if (GyroConfig.DebugLog && scope != null)
                Debug.Log(ScopeTdcControlsMod.Tag + " keep scope bearing: '" + scope.name + "' restore " + how + ", bearing " + scope.LocalBearing.ToString("0.0") + " (wanted " + restoreTo.ToString("0.0") + ")");
            restoring = null;
        }
    }

    [HarmonyPatch(typeof(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController), "UpdateController")]
    static class PeriscopeController_UpdateController_Patch
    {
        static void Postfix(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController __instance)
        {
            try { ScopeAimSync.Step(__instance); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " keep scope bearing (restore): " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController), "StopController")]
    static class PeriscopeController_StopController_Patch
    {
        static void Prefix(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController __instance)
        {
            try { ScopeAimSync.SaveBearing(__instance); ScopeAimSync.Store(__instance); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " keep scope bearing (leaving): " + e.Message); }
        }
    }

    /// <summary>Coming back to the scope: start putting it back on the saved bearing (and, with DebugLog, log what it came back to).</summary>
    [HarmonyPatch(typeof(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController), "StartController")]
    static class PeriscopeController_StartController_Patch
    {
        static void Postfix(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController __instance)
        {
            try { ScopeAimSync.BeginRestore(__instance); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " keep scope bearing (entering): " + e.Message); }
            if (!GyroConfig.DebugLog) return;
            try
            {
                var scope = __instance.Periscope;
                if (scope == null) return;
                Debug.Log(ScopeTdcControlsMod.Tag + " entering '" + scope.name + "': stored aim " + scope.LastUserRotation + ", aim now " + scope.PeriscopeEulersOffset
                          + ", bearing " + scope.LocalBearing.ToString("0.0") + (__instance.LockTarget != null ? ", locked" : ""));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController), "LockTarget", MethodType.Setter)]
    static class PeriscopeController_SetLockTarget_Patch
    {
        static void Prefix(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController __instance, out bool __state)
        {
            __state = false;
            try { __state = __instance.LockTarget != null; } catch { }
        }

        static void Postfix(UBOAT.Game.Scene.Camera.ExternalControllers.PeriscopeController __instance, UBOAT.Game.Scene.Entities.Entity value, bool __state)
        {
            // unlocked: the scope stays where the lock left it, and that is now the aim to come back to
            try { if (__state && value == null) ScopeAimSync.Store(__instance); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " keep scope bearing (unlock): " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Periscope), "Periscope_TargetChanged")]
    static class Periscope_TargetChanged_Patch
    {
        static void Prefix(Periscope __instance, out Vector3 __state)
        {
            __state = new Vector3(float.NaN, float.NaN, float.NaN);
            try { if (ScopeBearing.Applies(__instance) && ScopeBearing.Ready()) __state = ScopeBearing.Get(__instance); }
            catch { }
        }

        static void Postfix(Periscope __instance, Vector3 __state)
        {
            try
            {
                // the game set "no user aim" (NaN): put back the direction you had aimed it
                if (ScopeBearing.Applies(__instance) && ScopeBearing.IsSet(__state) && ScopeBearing.Ready())
                    ScopeBearing.Set(__instance, __state);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Periscope), "ResetManualRotation")]
    static class Periscope_ResetManualRotation_Patch
    {
        static bool loggedSkip;

        /// <summary>Set while the torpedo panel's "reset manual rotation" button runs (it can be pressed away from the scope).</summary>
        internal static bool explicitReset;

        static bool Prefix(Periscope __instance)
        {
            if (explicitReset) { ScopeAimSync.Forget(__instance); return true; } // asked for by you: always reset
            if (!ScopeBearing.Applies(__instance) || GyroCounterUI.ScopeViewActive) return true; // at the scope: aim at the clicked ship
            if (GyroConfig.DebugLog && !loggedSkip)
            {
                loggedSkip = true;
                Debug.Log(ScopeTdcControlsMod.Tag + " kept the scope bearing (a ship / contact was clicked away from the scope)");
            }
            return false;
        }
    }

    /// <summary>
    /// The torpedo panel's "reset manual rotation" button (shown with the "scope turned by hand"
    /// warning, also outside the scope view) is an explicit request: let its ResetManualRotation
    /// through even away from the scope, and drop the saved bearing so entering the scope
    /// doesn't swing it back off the target.
    /// </summary>
    [HarmonyPatch(typeof(UBOAT.Game.UI.Periscope.TorpedoLaunchersUI), "OnResetManualRotation")]
    static class TorpedoLaunchersUI_OnResetManualRotation_Patch
    {
        static void Prefix() { Periscope_ResetManualRotation_Patch.explicitReset = true; }
        static void Finalizer() { Periscope_ResetManualRotation_Patch.explicitReset = false; }
    }
}
