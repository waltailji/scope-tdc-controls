using System;
using System.Reflection;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.UI;
using UBOAT.Game.UI.Periscope;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Gyro receiver selection.
    ///
    /// Vanilla TorpedoLaunchersUI.Update (the periscope torpedo panel) sets the
    /// receiver every frame: BOW if any bow tube is flooding/flooded, otherwise
    /// STERN if a stern tube is. The tube check boxes are ignored. This mod
    /// replaces that write, and with "Auto set receiver" on also drives the
    /// receiver every frame (panel open or not):
    ///   1. the player's manual pick (this mod's panel or the control-room
    ///      knob), held until the set of checked flooded tubes changes;
    ///   2. otherwise the group of the CHECKED flooded tubes; with bow AND stern
    ///      checked, the end the TDC's bearing points to while follow-up is on
    ///      (ahead of the beam = Bug, abaft = Heck), else the current one;
    ///   3. otherwise (nothing checked) OFF.
    /// With "Auto set receiver" off, the torpedo panel no longer changes the
    /// receiver at all; only the player does.
    /// OFF set by the game in direct-gyro mode is left alone either way.
    /// </summary>
    public static class ReceiverOverride
    {
        public static bool InLaunchersUpdate;
        public static bool KnobWrite;
        const float DeadBand = 5f; // degrees either side of the beam (both-ends case)

        static GyroAngleReceiverSetting? manual;
        static int lastMask = -1;
        static bool loggedOverride;

        static AccessTools.FieldRef<TorpedoFireControlBoxes, TorpedoLauncherGroup> bowRef;
        static AccessTools.FieldRef<TorpedoFireControlBoxes, TorpedoLauncherGroup> sternRef;
        static bool resolved;

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try { bowRef = AccessTools.FieldRefAccess<TorpedoFireControlBoxes, TorpedoLauncherGroup>("bowReceiver"); } catch { }
            try { sternRef = AccessTools.FieldRefAccess<TorpedoFireControlBoxes, TorpedoLauncherGroup>("sternReceiver"); } catch { }
        }

        public static PlayerShip Ship()
        {
            try
            {
                var fw = InjectionFramework.Instance;
                return fw != null ? fw.GetInstance<PlayerShip>() : null;
            }
            catch { return null; }
        }

        public static bool Ready(TorpedoLauncher l)
        {
            var s = l.CurrentState;
            return s == TorpedoLauncher.State.Flooding || s == TorpedoLauncher.State.Flooded;
        }

        public static bool IsStern(TorpedoFireControlBoxes boxes, TorpedoLauncherGroup group)
        {
            if (group == null) return false;
            Resolve();
            if (boxes != null)
            {
                if (sternRef != null && ReferenceEquals(sternRef(boxes), group)) return true;
                if (bowRef != null && ReferenceEquals(bowRef(boxes), group)) return false;
            }
            return !group.name.Contains("Bow");
        }

        /// <summary>Bit mask of tubes that are checked AND flooding/flooded.</summary>
        static int MarkedReadyMask(TorpedoFireControlBoxes boxes, out bool bow, out bool stern)
        {
            bow = stern = false;
            int mask = 0;
            var ship = Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return 0;
            for (int i = 0; i < tubes.Length && i < 31; i++)
            {
                var t = tubes[i];
                if (t == null || !t.IsMarkedForLaunch || !Ready(t)) continue;
                mask |= 1 << i;
                if (IsStern(boxes, t.Group)) stern = true; else bow = true;
            }
            return mask;
        }

        /// <summary>Which ends have checked, flooding/flooded tubes.</summary>
        public static void CheckedEnds(out bool bow, out bool stern)
        {
            var ship = Ship();
            var boxes = ship != null ? ship.GetEquipment<TorpedoFireControlBoxes>() : null;
            MarkedReadyMask(boxes, out bow, out stern);
        }

        /// <summary>Called by the mod's AUS / BUG / HECK buttons.</summary>
        public static void SetManual(TorpedoFireControlBoxes boxes, GyroAngleReceiverSetting setting)
        {
            bool b, s;
            lastMask = MarkedReadyMask(boxes, out b, out s);
            manual = setting;
        }

        /// <summary>The player turned the control-room receiver knob: treat it like a manual pick.</summary>
        public static void NoteKnob(TorpedoFireControlBoxes boxes, GyroAngleReceiverSetting setting)
        {
            SetManual(boxes, setting);
        }

        static bool IsOff(GyroAngleReceiverSetting s)
        {
            return s == GyroAngleReceiverSetting.Off || s == GyroAngleReceiverSetting.Off2;
        }

        /// <summary>Receiver auto mode wants right now.</summary>
        static GyroAngleReceiverSetting Wanted(TorpedoFireControlBoxes boxes, TorpedoCalculator tdc)
        {
            bool bow, stern;
            int mask = MarkedReadyMask(boxes, out bow, out stern);
            if (mask != lastMask)
            {
                lastMask = mask;
                manual = null; // tube selection changed: follow the check boxes again
            }
            if (manual.HasValue) return manual.Value;
            if (bow && stern) return BothEnds(boxes, tdc);
            if (bow) return GyroAngleReceiverSetting.Bow;
            if (stern) return GyroAngleReceiverSetting.Stern;
            return GyroAngleReceiverSetting.Off;
        }

        /// <summary>Bow and stern tubes both checked: the end the scope is looking toward.</summary>
        static GyroAngleReceiverSetting BothEnds(TorpedoFireControlBoxes boxes, TorpedoCalculator tdc)
        {
            var current = boxes != null ? boxes.GyroAngleReceiver : GyroAngleReceiverSetting.Off;
            bool haveCurrent = current == GyroAngleReceiverSetting.Bow || current == GyroAngleReceiverSetting.Stern;

            if (tdc != null && tdc.BearingFollowUpEnabled)
            {
                float bearing = tdc.TargetBearing; // relative to the bow, follows the scope
                if (!float.IsNaN(bearing) && !float.IsInfinity(bearing))
                {
                    float offBow = Mathf.Abs(Mathf.DeltaAngle(0f, bearing));
                    if (offBow > 90f + DeadBand) return GyroAngleReceiverSetting.Stern;
                    if (offBow < 90f - DeadBand) return GyroAngleReceiverSetting.Bow;
                }
            }
            // follow-up off, or on the beam: keep what is selected
            return haveCurrent ? current : GyroAngleReceiverSetting.Bow;
        }

        static TorpedoCalculator Tdc()
        {
            var ship = Ship();
            return ship != null ? ship.GetEquipment<TorpedoCalculator>() : null;
        }

        /// <summary>Replaces vanilla's BOW/STERN pick inside TorpedoLaunchersUI.Update.</summary>
        public static GyroAngleReceiverSetting Decide(TorpedoFireControlBoxes boxes, GyroAngleReceiverSetting vanilla)
        {
            // manual mode: the torpedo panel doesn't touch the receiver
            if (!GyroConfig.AutoReceiver) return boxes.GyroAngleReceiver;

            var result = Wanted(boxes, Tdc());
            if (result != vanilla && !loggedOverride)
            {
                loggedOverride = true;
                Debug.Log(ScopeTdcControlsMod.Tag + " torpedo panel wanted receiver " + vanilla + ", using " + result
                          + (manual.HasValue ? " (manual pick)" : " (checked tubes)"));
            }
            return result;
        }

        /// <summary>Auto mode, every frame: keep the receiver on what the checked tubes call for.</summary>
        public static void AutoTick(TorpedoCalculator tdc)
        {
            if (!GyroConfig.AutoReceiver || InLaunchersUpdate) return;
            var ship = Ship();
            var boxes = ship != null ? ship.GetEquipment<TorpedoFireControlBoxes>() : null;
            if (boxes == null) return;
            var wanted = Wanted(boxes, tdc);
            var cur = boxes.GyroAngleReceiver;
            if (wanted == cur || (IsOff(wanted) && IsOff(cur))) return;
            boxes.GyroAngleReceiver = wanted;
        }
    }

    /// <summary>
    /// Keeps the tube check boxes as the player left them when the periscope
    /// torpedo panel is hidden and shown again. While the panel is open the
    /// selection is recorded every frame. For a few frames after it reopens,
    /// the selection is put back, unless a tube changed state (fired, drained)
    /// or the tube/salvo knob was turned while the panel was closed.
    /// </summary>
    public static class TubeMarkMemory
    {
        const int RestoreFrameCount = 3;

        static bool[] marks;
        static TorpedoLauncher.State[] states;
        static int knobTube = int.MinValue, knobSalvo = int.MinValue;
        static int restoreFrames;
        static bool loggedRestore, loggedGroup;

        static AccessTools.FieldRef<TorpedoLaunchersUI, TorpedoLauncherUI[]> widgetsRef;
        static AccessTools.FieldRef<TorpedoLauncherUI, Toggle> checkboxRef;
        static MethodInfo widgetLauncher;
        static bool resolved;

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try { widgetsRef = AccessTools.FieldRefAccess<TorpedoLaunchersUI, TorpedoLauncherUI[]>("launchers"); } catch { }
            try { checkboxRef = AccessTools.FieldRefAccess<TorpedoLauncherUI, Toggle>("markedForLaunchCheckbox"); } catch { }
            widgetLauncher = AccessTools.PropertyGetter(typeof(TorpedoLauncherUI), "Launcher");
        }

        public static bool Restoring { get { return restoreFrames > 0; } }

        public static void PanelOpened() { restoreFrames = RestoreFrameCount; }

        static void KnobState(TorpedoFireControlBoxes boxes, out int tube, out int salvo)
        {
            tube = salvo = int.MinValue;
            if (boxes == null) return;
            try { tube = (int)boxes.SelectedTorpedoTube; salvo = boxes.SelectedTorpedoSalvoPreset; } catch { }
        }

        public static void Record()
        {
            var ship = ReceiverOverride.Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return;
            if (marks == null || marks.Length != tubes.Length)
            {
                marks = new bool[tubes.Length];
                states = new TorpedoLauncher.State[tubes.Length];
            }
            for (int i = 0; i < tubes.Length; i++)
            {
                if (tubes[i] == null) continue;
                marks[i] = tubes[i].IsMarkedForLaunch;
                states[i] = tubes[i].CurrentState;
            }
            KnobState(ship.GetEquipment<TorpedoFireControlBoxes>(), out knobTube, out knobSalvo);
        }

        public static void RestoreStep(TorpedoLaunchersUI ui)
        {
            if (restoreFrames <= 0) return;
            restoreFrames--;

            var ship = ReceiverOverride.Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null || marks == null || marks.Length != tubes.Length) return;

            int t, s;
            KnobState(ship.GetEquipment<TorpedoFireControlBoxes>(), out t, out s);
            if (t != knobTube || s != knobSalvo) { restoreFrames = 0; return; } // knobs turned: game re-derived the marks on purpose

            bool changed = false;
            for (int i = 0; i < tubes.Length; i++)
            {
                var tube = tubes[i];
                if (tube == null || tube.CurrentState != states[i]) continue; // tube changed meanwhile: leave vanilla's choice
                if (tube.IsMarkedForLaunch != marks[i])
                {
                    tube.IsMarkedForLaunch = marks[i];
                    changed = true;
                }
            }
            if (changed)
            {
                SyncWidgets(ui);
                TubeSelection.SyncSelector();
                if (!loggedRestore)
                {
                    loggedRestore = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " torpedo panel reopened with different tube check boxes; restored the previous selection.");
                }
            }
        }

        /// <summary>Make the check boxes show the tubes' state without firing their change events.</summary>
        public static void SyncWidgets(TorpedoLaunchersUI ui)
        {
            Resolve();
            if (ui == null || widgetsRef == null || checkboxRef == null || widgetLauncher == null) return;
            var widgets = widgetsRef(ui);
            if (widgets == null) return;
            foreach (var w in widgets)
            {
                if (w == null) continue;
                Toggle box = checkboxRef(w);
                ExtendedTorpedoLauncher l = null;
                try { l = widgetLauncher.Invoke(w, null) as ExtendedTorpedoLauncher; } catch { }
                if (box == null || l == null) continue;
                if (box.group != null && !loggedGroup)
                {
                    loggedGroup = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " tube check boxes belong to ToggleGroup '" + box.group.name + "' (allowSwitchOff=" + box.group.allowSwitchOff + ").");
                }
                if (box.isOn != l.IsMarkedForLaunch) box.SetIsOnWithoutNotify(l.IsMarkedForLaunch);
            }
        }
    }

    [HarmonyPatch(typeof(TorpedoLaunchersUI), "Open")]
    static class TorpedoLaunchersUI_Open_Patch
    {
        static void Postfix() { TubeMarkMemory.PanelOpened(); }
    }

    [HarmonyPatch(typeof(TorpedoLaunchersUI), "Update")]
    static class TorpedoLaunchersUI_Update_Patch
    {
        static void Prefix(TorpedoLaunchersUI __instance)
        {
            try { TubeMarkMemory.RestoreStep(__instance); }
            catch (Exception e) { Debug.LogException(e); }
            ReceiverOverride.InLaunchersUpdate = true;
        }

        static void Postfix()
        {
            ReceiverOverride.InLaunchersUpdate = false;
            try
            {
                if (!TubeMarkMemory.Restoring) TubeMarkMemory.Record();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // finalizer always runs, even if Update throws, so the flag never sticks
        static void Finalizer() { ReceiverOverride.InLaunchersUpdate = false; }
    }

    [HarmonyPatch(typeof(TorpedoFireControlBoxes), "GyroAngleReceiver", MethodType.Setter)]
    static class TorpedoFireControlBoxes_SetGyroAngleReceiver_Patch
    {
        static void Prefix(TorpedoFireControlBoxes __instance, ref GyroAngleReceiverSetting value)
        {
            try
            {
                if (ReceiverOverride.KnobWrite) { ReceiverOverride.NoteKnob(__instance, value); return; }
                if (!ReceiverOverride.InLaunchersUpdate) return;
                if (value != GyroAngleReceiverSetting.Bow && value != GyroAngleReceiverSetting.Stern) return;
                value = ReceiverOverride.Decide(__instance, value);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Flags writes coming from the physical receiver knob in the control room.</summary>
    [HarmonyPatch(typeof(TorpedoFireControlBoxesGyroAngleReceiverKnob), "Value", MethodType.Setter)]
    static class GyroAngleReceiverKnob_SetValue_Patch
    {
        static void Prefix() { ReceiverOverride.KnobWrite = true; }
        static void Finalizer() { ReceiverOverride.KnobWrite = false; }
    }

    /// <summary>Optional diagnostics (DebugLog=true): log every tube check-box change and who made it.</summary>
    [HarmonyPatch(typeof(ExtendedTorpedoLauncher), "IsMarkedForLaunch", MethodType.Setter)]
    static class ExtendedTorpedoLauncher_SetMarked_Patch
    {
        static void Prefix(ExtendedTorpedoLauncher __instance, bool value)
        {
            if (!GyroConfig.DebugLog || __instance.IsMarkedForLaunch == value) return;
            string trace = Environment.StackTrace;
            if (trace.Length > 1500) trace = trace.Substring(0, 1500);
            Debug.Log(ScopeTdcControlsMod.Tag + "[debug] tube '" + __instance.name + "' marked " + __instance.IsMarkedForLaunch + " -> " + value
                      + (TubeMarkMemory.Restoring ? " (restore window)" : "") + "\n" + trace);
        }
    }

    /// <summary>Optional diagnostics (DebugLog=true): log every gyro receiver change and who made it.</summary>
    [HarmonyPatch(typeof(TorpedoFireControlBoxes), "GyroAngleReceiver", MethodType.Setter)]
    [HarmonyPriority(Priority.Last)] // after this mod's own filter, so it logs the value actually applied
    static class TorpedoFireControlBoxes_SetReceiverDebug_Patch
    {
        static void Prefix(TorpedoFireControlBoxes __instance, GyroAngleReceiverSetting value)
        {
            if (!GyroConfig.DebugLog || __instance.GyroAngleReceiver == value) return;
            string marks = "";
            try
            {
                var ship = ReceiverOverride.Ship();
                if (ship != null && ship.TorpedoLaunchers != null)
                    foreach (var t in ship.TorpedoLaunchers)
                        if (t != null) marks += " " + t.name + "=" + (t.IsMarkedForLaunch ? "checked" : "-") + "/" + t.CurrentState;
            }
            catch { }
            string trace = Environment.StackTrace;
            if (trace.Length > 1500) trace = trace.Substring(0, 1500);
            Debug.Log(ScopeTdcControlsMod.Tag + "[debug] receiver " + __instance.GyroAngleReceiver + " -> " + value
                      + (ReceiverOverride.InLaunchersUpdate ? " (inside torpedo panel update)" : "")
                      + "\n tubes:" + marks + "\n" + trace);
        }
    }
}
