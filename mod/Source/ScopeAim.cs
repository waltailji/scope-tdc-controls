using System;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Scene.Camera;
using UBOAT.Game.Scene.Camera.ExternalControllers;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Turns the periscope / UZO so that the TDC's gyro angle comes out at a
    /// wanted value (e.g. the angle a tube group's gyros are already set to).
    ///
    /// The TDC's gyro angle = its target bearing + lead + deflection + parallax.
    /// With follow-up on, its target bearing follows the scope. So the scope has
    /// to point at (wanted gyro - current offset), where offset is the TDC's
    /// present gyro angle minus its bearing. The offset shifts a little as the
    /// bearing changes (angle on bow moves with it), so this re-aims every
    /// 0.6 s until the TDC's gyro angle matches. It uses the game's own
    /// PeriscopeController.LookAt (the "center view" call), correcting for any
    /// small difference between where it was asked to point and where it ended up.
    /// </summary>
    public static class ScopeAim
    {
        const float LookInterval = 0.6f;   // LookAt animates for about 0.5 s
        const float Timeout = 20f;
        const float GyroTolerance = 0.3f;  // degrees
        const float ScopeTolerance = 0.3f;

        static bool active;
        static float wantedGyro, startTime, nextLook, bias, lastDesired;
        static bool looked;
        static PeriscopeController controller;
        static Action finish;

        public static bool Active { get { return active; } }

        /// <summary>While aiming: the end (true = stern) the aim is for, so auto parallax can hold it.</summary>
        public static bool? AimingStern { get; private set; }

        static T Get<T>() where T : class
        {
            try
            {
                var fw = InjectionFramework.Instance;
                return fw != null ? fw.GetInstance<T>() : null;
            }
            catch { return null; }
        }

        public static PeriscopeController CurrentController()
        {
            var cam = Get<MainCamera>();
            var gen = cam != null ? cam.GenericController : null;
            return gen != null ? gen.ExternalController as PeriscopeController : null;
        }

        /// <summary>
        /// Start turning the scope so the TDC's gyro angle becomes <paramref name="gyro"/>.
        /// <paramref name="onFinished"/> runs once when the aim ends (matched, cancelled,
        /// timed out or the scope view closed), e.g. to connect the receiver.
        /// </summary>
        public static bool Start(float gyro, bool stern, Action onFinished)
        {
            Stop(); // finish any previous aim first
            controller = CurrentController();
            if (controller == null || controller.Periscope == null)
            {
                if (onFinished != null) onFinished();
                return false;
            }

            // a locked scope keeps tracking its target; release it so it can be turned
            if (controller.LockTarget != null) controller.LockTarget = null;

            wantedGyro = gyro;
            startTime = Time.unscaledTime;
            nextLook = 0f;
            bias = 0f;
            looked = false;
            finish = onFinished;
            AimingStern = stern;
            active = true;
            return true;
        }

        public static void Stop()
        {
            active = false;
            controller = null;
            AimingStern = null;
            var f = finish;
            finish = null;
            if (f != null)
            {
                try { f(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Called every frame by the overlay while the scope view is open.</summary>
        public static void Tick(TorpedoCalculator tdc)
        {
            if (!active) return;
            float now = Time.unscaledTime;
            if (tdc == null || controller == null || controller.Periscope == null || now - startTime > Timeout) { Stop(); return; }

            // the player takes over: any mouse button outside the first moment cancels
            if (now - startTime > 0.3f && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) { Stop(); return; }

            var scope = controller.Periscope;
            float offset = Mathf.DeltaAngle(tdc.TargetBearing, tdc.GyroAngle);
            float desired = Normalize(wantedGyro - offset);
            float current = scope.LocalBearing;

            bool scopeThere = Mathf.Abs(Mathf.DeltaAngle(current, desired)) < ScopeTolerance;
            bool gyroThere = Mathf.Abs(Mathf.DeltaAngle(tdc.GyroAngle, wantedGyro)) < GyroTolerance;
            if (scopeThere && gyroThere) { Stop(); return; }

            if (now < nextLook) return;

            // where the last LookAt actually ended up vs. where it was asked: fold the error in
            if (looked)
            {
                float residual = Mathf.DeltaAngle(current, lastDesired);
                if (Mathf.Abs(residual) > 0.2f) bias += residual;
            }
            if (!scopeThere || !looked)
            {
                controller.LookAt(Normalize(desired + bias));
                looked = true;
                lastDesired = desired;
            }
            nextLook = now + LookInterval;
        }

        static float Normalize(float a)
        {
            a %= 360f;
            if (a < 0f) a += 360f;
            return a;
        }
    }
}
