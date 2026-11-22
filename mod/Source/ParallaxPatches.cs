using System;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Utilities;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// TDC parallax direction (TorpedoCalculator.ParallaxMultiplier):
    /// +1 = forward (bow tubes), -1 = backward (stern tubes).
    ///
    /// The game only sets it from the periscope torpedo panel, from the checked
    /// tubes. This mod takes over:
    ///   Auto (default), first rule that applies:
    ///     - tubes checked (flooding/flooded) at one end: bow -> forward,
    ///       stern -> backward;
    ///     - gyro receiver on: follows the receiving tube group, Bug -> forward,
    ///       Heck -> backward;
    ///     - otherwise: follows the TDC's target bearing (relative to the
    ///       boat): forward while the target is ahead of the beam, backward
    ///       while it is abaft, with a small dead band on the beam so it doesn't
    ///       flicker. Uses the TDC's bearing rather than the raw scope, so with
    ///       follow-up off it holds the bearing the TDC uses.
    ///   Manual: only the Forward / Backward buttons (or the physical switch on
    ///     the TDC) change it.
    /// The torpedo panel's own write is blocked in both modes so nothing fights.
    /// </summary>
    public static class Parallax
    {
        public const float Forward = 1f;
        public const float Backward = -1f;
        const float DeadBand = 5f; // degrees either side of the beam

        public static bool IsForward(TorpedoCalculator tdc) { return tdc.ParallaxMultiplier >= 0f; }

        /// <summary>Signed parallax correction in degrees, the amount the TDC adds to the gyro angle.</summary>
        public static float CorrectionDegrees(TorpedoCalculator tdc)
        {
            return TorpedoFiringSolution.GetProcessedParallaxCorrectionInDegrees(tdc.ParallaxCorrection1, tdc.ParallaxMultiplier);
        }

        /// <summary>Set the direction on the TDC and on its switch target, so the switch doesn't animate it back.</summary>
        public static void Set(TorpedoCalculator tdc, float direction)
        {
            if (tdc == null) return;
            if (tdc.ParallaxMultiplierTarget != direction) tdc.ParallaxMultiplierTarget = direction;
            if (tdc.ParallaxMultiplier != direction) SwitchKeepingAob(tdc, direction);
        }

        static Action<TorpedoCalculator> updateAngleOnBow;
        static bool updateResolved;

        /// <summary>
        /// Switch the parallax direction without changing the target's course on the TDC.
        /// The TDC's AOB includes the parallax correction, so flipping its sign makes the AOB
        /// jump by twice the correction (largest with the target on the beam, which is where
        /// auto parallax switches); AOB tracking then took that jump for a change of course
        /// and the AOB came out wrong after a sweep (e.g. S 120 instead of S 90). So, while
        /// the TDC is tracking, the impact angle is reset so the AOB stays what it was.
        /// </summary>
        static void SwitchKeepingAob(TorpedoCalculator tdc, float direction)
        {
            bool tracking = tdc.ApplyCourseChanges && tdc.IsAOBMotorOn && !tdc.IsLockingMechanismTriggered;
            float aob = tdc.AngleOnBow;
            tdc.ParallaxMultiplier = direction;
            if (!tracking || float.IsNaN(aob) || float.IsInfinity(aob)) return;
            try
            {
                if (!updateResolved)
                {
                    updateResolved = true;
                    var m = AccessTools.Method(typeof(TorpedoCalculator), "UpdateAngleOnBow");
                    if (m != null) updateAngleOnBow = AccessTools.MethodDelegate<Action<TorpedoCalculator>>(m);
                }
                if (updateAngleOnBow == null) return;
                updateAngleOnBow(tdc); // the AOB with the new direction (else the impact search below returns at once)
                float impact = tdc.ComputeImpactAngleForDesiredAOB(aob, tdc.ImpactAngle);
                if (float.IsNaN(impact) || float.IsInfinity(impact)) return;
                tdc.ImpactAngle = impact;
                tdc.AngleOnBow = aob;
                tdc.AOBBearingSum = aob - tdc.TargetBearing; // nothing for the tracking to correct
            }
            catch (Exception e)
            {
                updateAngleOnBow = null;
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " parallax switch: AOB not kept (" + e.GetBaseException().Message + ")");
            }
        }

        public static void AutoTick(TorpedoCalculator tdc)
        {
            if (!GyroConfig.AutoParallax || tdc == null) return;

            // a receiver double-click aim is running (receiver disconnected meanwhile):
            // hold the parallax of the end being aimed for, so the TDC angle comes out right
            if (ScopeAim.AimingStern.HasValue)
            {
                Set(tdc, ScopeAim.AimingStern.Value ? Backward : Forward);
                return;
            }

            // checked tubes come first: parallax for the end you are going to fire from
            bool bowChecked, sternChecked;
            ReceiverOverride.CheckedEnds(out bowChecked, out sternChecked);
            if (bowChecked != sternChecked)
            {
                Set(tdc, sternChecked ? Backward : Forward);
                return;
            }

            // then a selected receiver: parallax for the tubes that get the gyro angle
            var group = tdc.GyroAngleReceiver;
            if (group != null)
            {
                var ship = ReceiverOverride.Ship();
                var boxes = ship != null ? ship.GetEquipment<TorpedoFireControlBoxes>() : null;
                Set(tdc, ReceiverOverride.IsStern(boxes, group) ? Backward : Forward);
                return;
            }

            // no receiver: go by where the target is
            float bearing = tdc.TargetBearing; // relative to the bow
            if (float.IsNaN(bearing) || float.IsInfinity(bearing)) return;
            float offBow = Mathf.Abs(Mathf.DeltaAngle(0f, bearing)); // 0 = dead ahead, 180 = dead astern

            if (offBow > 90f + DeadBand) Set(tdc, Backward);
            else if (offBow < 90f - DeadBand) Set(tdc, Forward);
            // on the beam (within the dead band): keep the current direction
        }
    }

    [HarmonyPatch(typeof(TorpedoCalculator), "Update")]
    static class TorpedoCalculator_AutoParallax_Patch
    {
        static bool failed;

        static void Postfix(TorpedoCalculator __instance)
        {
            if (failed) return;
            try
            {
                ReceiverOverride.AutoTick(__instance); // receiver first: auto parallax follows it
                Parallax.AutoTick(__instance);
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " auto receiver/parallax disabled after error: " + e.GetBaseException().Message);
            }
        }
    }

    /// <summary>Block the torpedo panel's per-frame parallax write; this mod decides the direction.</summary>
    [HarmonyPatch(typeof(TorpedoCalculator), "ParallaxMultiplier", MethodType.Setter)]
    static class TorpedoCalculator_SetParallax_Patch
    {
        static bool Prefix()
        {
            return !ReceiverOverride.InLaunchersUpdate;
        }
    }
}
