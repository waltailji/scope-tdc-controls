using System;
using HarmonyLib;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// "TDC operator required" (on by default = vanilla).
    ///
    /// A device's crew station (Interactable) switches the device off with its
    /// TurnedOff disable flag whenever nobody works it (and when crew
    /// performance drops to zero). A disabled TDC stops calculating, and its
    /// button on the attack-station bar is greyed out.
    ///
    /// With the option off, writes of that flag coming from the crew station
    /// are ignored for the TDC only. Real power-off, damage and the other
    /// flags still disable it.
    /// </summary>
    public static class TdcOperator
    {
        // the flag the crew station uses for "nobody here" (DeviceDisableFlag.TurnedOff = 2)
        public const DeviceDisableFlag StationFlag = DeviceDisableFlag.TurnedOff;

        public static int FromStation; // >0 while an Interactable method is running

        public static bool Required { get { return GyroConfig.TdcOperatorRequired; } }

        /// <summary>Apply the current setting to the boat's TDC right away.</summary>
        public static void Apply()
        {
            try
            {
                var ship = ReceiverOverride.Ship();
                var tdc = ship != null ? ship.GetEquipment<TorpedoCalculator>() : null;
                if (tdc == null) return;
                if (!Required)
                {
                    if (tdc.HasDisableFlag(StationFlag)) tdc.SetDisableFlag(StationFlag, false);
                }
                else
                {
                    // back to vanilla: off again if nobody is at the station
                    var station = tdc.GetComponent<Interactable>();
                    if (station == null) station = tdc.GetComponentInParent<Interactable>();
                    if (station != null)
                    {
                        var workers = station.Workers;
                        tdc.SetDisableFlag(StationFlag, workers == null || workers.Count == 0);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not apply TDC operator setting: " + e.Message); }
        }
    }

    /// <summary>Marks code running inside the crew station's own methods.</summary>
    [HarmonyPatch]
    static class Interactable_StationScope_Patch
    {
        static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var t = typeof(Interactable);
            foreach (var name in new[] { "Start", "OnDisable", "UpdatePerformance", "UpdateTargetPerformance" })
            {
                var m = AccessTools.Method(t, name);
                if (m != null) yield return m;
            }
            var setter = AccessTools.PropertySetter(t, "Device");
            if (setter != null) yield return setter;
        }

        static void Prefix() { TdcOperator.FromStation++; }
        static void Finalizer() { if (TdcOperator.FromStation > 0) TdcOperator.FromStation--; }
    }

    /// <summary>With "TDC operator required" off, the crew station can't switch the TDC off.</summary>
    [HarmonyPatch(typeof(Device), "SetDisableFlag", new Type[] { typeof(DeviceDisableFlag), typeof(bool) })]
    static class Device_SetDisableFlag_Patch
    {
        static void Prefix(Device __instance, DeviceDisableFlag disableFlag, ref bool state)
        {
            if (!state || TdcOperator.FromStation == 0 || TdcOperator.Required) return;
            if (disableFlag == TdcOperator.StationFlag && __instance is TorpedoCalculator) state = false;
        }
    }
}
