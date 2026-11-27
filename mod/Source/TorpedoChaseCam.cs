using System;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Camera;
using UBOAT.Game.Scene.Entities;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Torpedo view (double-click a torpedo run timer): the game's orbit camera on that torpedo,
    /// the same view as the camera button when a torpedo is selected on the map
    /// (EntitySelectionUI.OnCenterCamera: section view with the torpedo as its target). You orbit
    /// and zoom it as usual. When the torpedo hits, the game itself moves the view to the ship it
    /// hit (Torpedo.Detonate does that when the view is on the torpedo).
    ///
    /// Double-click the row again to go back to the view you came from. If the torpedo ends its
    /// run without a hit, the view goes back there by itself after a few seconds.
    /// </summary>
    public static class TorpedoChaseCam
    {
        const float BackAfterMiss = 4f;  // real seconds after a run ends with nothing to look at

        static Torpedo target;
        static CameraModeDescriptor returnTo;
        static bool haveReturn, tracking;
        static float endedAt = -1f;

        /// <summary>The view is on this torpedo right now.</summary>
        public static bool IsChasing(Torpedo t)
        {
            if (t == null || !t) return false;
            var cam = MainCamera.Instance;
            try
            {
                return cam != null && cam.CurrentMode == CameraMode.SectionView && cam.SectionViewController != null
                       && ReferenceEquals(cam.SectionViewController.Target, t.transform);
            }
            catch { return false; }
        }

        public static bool Active { get { return tracking; } }

        /// <summary>Double-click on a timer: view that torpedo, or go back if the view is already on it.</summary>
        public static void Toggle(Torpedo t)
        {
            var cam = MainCamera.Instance;
            if (cam == null || cam.IsInTransition) return;
            if (IsChasing(t)) { GoBack(cam); return; }
            if (t == null || !t || t.Detonated || !t.gameObject.activeInHierarchy) return;
            try
            {
                if (!tracking)
                {
                    // remember where to go back to (not when already looking at another torpedo)
                    haveReturn = !(cam.CurrentMode == CameraMode.SectionView && cam.SectionViewController != null
                                   && cam.SectionViewController.Target != null
                                   && cam.SectionViewController.Target.GetComponent<Torpedo>() != null);
                    if (haveReturn) returnTo = cam.CurrentModeDescriptor;
                }
                cam.SectionViewController.Target = t.transform;
                if (cam.CurrentMode != CameraMode.SectionView)
                    cam.SetMode(CameraMode.SectionView, CameraTransitionType.FadeInOut, 1f);
                target = t;
                tracking = true;
                endedAt = -1f;
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " torpedo view on " + t.name);
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " torpedo view: " + e.Message); }
        }

        /// <summary>Back to the view the torpedo view was opened from (the own boat in section view, if none).</summary>
        static void GoBack(MainCamera cam)
        {
            tracking = false;
            target = null;
            try
            {
                if (haveReturn && returnTo.Mode != CameraMode.SectionView)
                {
                    ResetSectionTarget(cam);
                    cam.SetMode(returnTo, CameraTransitionType.FadeInOut, 1f);
                }
                else ResetSectionTarget(cam);
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " torpedo view: " + e.Message); }
        }

        /// <summary>The section view back on the own boat (as the game's "leave view" button does).</summary>
        static void ResetSectionTarget(MainCamera cam)
        {
            var ship = ReceiverOverride.Ship();
            if (ship != null && cam.SectionViewController != null) cam.SectionViewController.Target = ship.transform;
        }

        /// <summary>Every frame (from the timer stack): notice the end of the run, or that you left the view.</summary>
        public static void Tick()
        {
            if (!tracking) return;
            var cam = MainCamera.Instance;
            if (cam == null) { tracking = false; return; }
            if (cam.IsInTransition) return;
            bool ended = Ended(target);
            if (!ended)
            {
                // you looked elsewhere (another view, or another object in the section view): let go
                if (!IsChasing(target)) { tracking = false; target = null; }
                return;
            }
            if (endedAt < 0f) endedAt = Time.unscaledTime;
            // a hit: the game has turned the view to the ship that was hit; leave it there
            Transform now = null;
            try { now = cam.CurrentMode == CameraMode.SectionView && cam.SectionViewController != null ? cam.SectionViewController.Target : null; } catch { }
            if (now != null && now.gameObject.activeInHierarchy && now.GetComponent<Torpedo>() == null) { tracking = false; target = null; return; }
            // a miss (or a dud): nothing left to look at, go back after a moment
            if (Time.unscaledTime - endedAt >= BackAfterMiss)
            {
                if (cam.CurrentMode == CameraMode.SectionView) GoBack(cam);
                else { tracking = false; target = null; }
            }
        }

        static bool Ended(Torpedo t)
        {
            try { return t == null || !t || t.Detonated || !t.gameObject.activeInHierarchy; }
            catch { return true; }
        }
    }
}
