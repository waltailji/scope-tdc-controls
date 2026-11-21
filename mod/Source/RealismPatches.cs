using System;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Speed-up for the TDC and the tube gyro motors when the "realistic"
    /// options are switched off.
    ///
    /// Both run their motors in fixed sub-steps: every frame they add the frame
    /// time to a simTime accumulator and then step each motor (bearing
    /// follow-up, impact angle, parallax correction, gyro setting...) toward its
    /// target at a limited speed. Adding (Factor - 1) extra frames' worth of
    /// time makes vanilla's own motor logic run Factor times as many steps.
    /// Everything stays damped and stable; it is just faster. (Snapping the
    /// motors instead made the parallax correction overshoot, because it feeds
    /// back into the gyro angle.)
    /// </summary>
    public static class Realism
    {
        public const float Factor = 10f;
        const float MaxExtraPerFrame = 0.5f; // seconds; bounds the work after a long frame

        public static float ExtraTime()
        {
            return Mathf.Min(Time.deltaTime, MaxExtraPerFrame) * (Factor - 1f);
        }
    }

    [HarmonyPatch(typeof(TorpedoCalculator), "SimulateCalculatorStep")]
    static class TorpedoCalculator_FastSolve_Patch
    {
        static AccessTools.FieldRef<TorpedoCalculator, float> simTime;
        static bool resolved, broken;

        static void Prefix(TorpedoCalculator __instance)
        {
            if (GyroConfig.RealisticTdc || broken) return;
            if (!resolved)
            {
                resolved = true;
                try { simTime = AccessTools.FieldRefAccess<TorpedoCalculator, float>("simTime"); }
                catch (Exception e) { broken = true; Debug.LogWarning(ScopeTdcControlsMod.Tag + " fast TDC unavailable: " + e.Message); return; }
            }
            simTime(__instance) += Realism.ExtraTime();
        }
    }

    [HarmonyPatch(typeof(TorpedoLauncherGroup), "UpdateTorpedoSettings")]
    static class TorpedoLauncherGroup_FastSync_Patch
    {
        static AccessTools.FieldRef<TorpedoLauncherGroup, float> simTime;
        static bool resolved, broken;

        static void Prefix(TorpedoLauncherGroup __instance)
        {
            if (GyroConfig.RealisticReceiverSync || broken) return;
            if (!resolved)
            {
                resolved = true;
                try { simTime = AccessTools.FieldRefAccess<TorpedoLauncherGroup, float>("simTime"); }
                catch (Exception e) { broken = true; Debug.LogWarning(ScopeTdcControlsMod.Tag + " fast receiver sync unavailable: " + e.Message); return; }
            }
            if (__instance.AngleSettingMode == TorpedoLauncherGroupAngleSettingMode.GyroManual) return;
            simTime(__instance) += Realism.ExtraTime();
        }
    }
}
