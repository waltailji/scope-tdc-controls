using System;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.UI;
using UBOAT.Game.UI.Periscope;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Torpedo tube check boxes: one end of the boat at a time.
    ///   - Checking a stern tube unchecks the bow tubes, and vice versa (the
    ///     TDC can only serve one receiver anyway).
    ///   - When the game auto-checks a tube because it finished flooding, and
    ///     tubes at the other end are already checked, that auto-check is
    ///     undone instead, so a tube finishing flooding never steals the
    ///     selection you made.
    /// </summary>
    public static class TubeSelection
    {
        /// <summary>
        /// keepChosen = true: the player checked it, so clear the other end.
        /// keepChosen = false: the game auto-checked it, so drop it if the other end is in use.
        /// </summary>
        public static void OneEnd(ExtendedTorpedoLauncher chosen, bool keepChosen, TorpedoLaunchersUI ui)
        {
            if (chosen == null || !chosen.IsMarkedForLaunch) return;
            var ship = ReceiverOverride.Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return;
            var boxes = ship.GetEquipment<TorpedoFireControlBoxes>();
            bool chosenStern = ReceiverOverride.IsStern(boxes, chosen.Group);

            bool changed = false, otherEndInUse = false;
            foreach (var t in tubes)
            {
                if (t == null || t == chosen || !t.IsMarkedForLaunch) continue;
                if (ReceiverOverride.IsStern(boxes, t.Group) == chosenStern) continue;
                if (keepChosen) { t.IsMarkedForLaunch = false; changed = true; }
                else otherEndInUse = true;
            }
            if (!keepChosen && otherEndInUse)
            {
                chosen.IsMarkedForLaunch = false;
                changed = true;
            }
            if (changed && ui != null) TubeMarkMemory.SyncWidgets(ui);
        }

        /// <summary>Unchecks the tubes at the other end (used when switching receiver by double-click).</summary>
        public static void KeepOnlyEnd(bool stern)
        {
            var ship = ReceiverOverride.Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return;
            var boxes = ship.GetEquipment<TorpedoFireControlBoxes>();
            bool changed = false;
            foreach (var t in tubes)
            {
                if (t == null || !t.IsMarkedForLaunch) continue;
                if (ReceiverOverride.IsStern(boxes, t.Group) == stern) continue;
                t.IsMarkedForLaunch = false;
                changed = true;
            }
            if (!changed) return;
            try
            {
                var ui = InjectionGet<UBOAT.Game.UI.GameUI>();
                var panel = ui != null ? ui.CurrentTorpedoLaunchersUI : null;
                if (panel != null) TubeMarkMemory.SyncWidgets(panel);
            }
            catch { }
            SyncSelector();
        }

        static T InjectionGet<T>() where T : class
        {
            try
            {
                var fw = DWS.Common.InjectionFramework.InjectionFramework.Instance;
                return fw != null ? fw.GetInstance<T>() : null;
            }
            catch { return null; }
        }

        static bool loggedSyncError;

        /// <summary>
        /// Puts the fire-control box's tube selector / salvo preset in step with
        /// the checked tubes (the game's own mapping; None when nothing is
        /// checked). Other code, e.g. the Torpedo Room mod while the torpedo
        /// panel is closed, re-derives the tube checks from that selector, so
        /// it has to agree with what the player picked in the scope.
        /// </summary>
        public static void SyncSelector()
        {
            try
            {
                var ship = ReceiverOverride.Ship();
                var boxes = ship != null ? ship.GetEquipment<TorpedoFireControlBoxes>() : null;
                if (boxes == null || ship.IsPreparingToLaunchTorpedoes || ship.IsLaunchingTorpedoes) return;
                boxes.ValidateSalvoSettingsFromMarkedLaunchers();
            }
            catch (Exception e)
            {
                if (loggedSyncError) return;
                loggedSyncError = true;
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not set the fire-control box tube selector: " + e.GetBaseException().Message);
            }
        }
    }

    /// <summary>
    /// A tube's check box changed. Only a real click on that check box may
    /// change the tube's selection.
    ///
    /// When the torpedo panel is hidden or shown, Unity switches the tube
    /// widgets off/on and their toggles fire change events on the way (the
    /// toggle group turning another tube's box on when the checked one goes
    /// off, typically the stern tube). Those events used to really check that
    /// tube; "one end at a time" then unchecked the bow tube and auto receiver /
    /// parallax switched to stern while the panel was hidden. A widget can still
    /// be active when this happens, so "is it visible" is not enough: a real
    /// click is a mouse release, this frame, over that tube's check box.
    /// </summary>
    [HarmonyPatch(typeof(TorpedoLauncherUI), "ToFireChanged")]
    static class TorpedoLauncherUI_ToFireChanged_Patch
    {
        static bool loggedIgnored;
        static AccessTools.FieldRef<TorpedoLauncherUI, UnityEngine.UI.Toggle> checkbox;
        static bool resolved;

        static bool IsRealClick(TorpedoLauncherUI w)
        {
            if (!w.gameObject.activeInHierarchy || !Input.GetMouseButtonUp(0)) return false;
            if (!resolved)
            {
                resolved = true;
                try { checkbox = AccessTools.FieldRefAccess<TorpedoLauncherUI, UnityEngine.UI.Toggle>("markedForLaunchCheckbox"); }
                catch { }
            }
            // over this tube's widget (the game also toggles the box when the tube itself is clicked)
            // or over its check box
            var c = w.GetComponentInParent<Canvas>();
            Canvas root = c != null ? c.rootCanvas : null;
            Camera cam = (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) ? null : root.worldCamera;
            var widget = w.transform as RectTransform;
            if (widget != null && RectTransformUtility.RectangleContainsScreenPoint(widget, Input.mousePosition, cam)) return true;
            var box = checkbox != null ? checkbox(w) : null;
            var boxRect = box != null ? box.transform as RectTransform : null;
            return boxRect != null && RectTransformUtility.RectangleContainsScreenPoint(boxRect, Input.mousePosition, cam);
        }

        // set by the prefix, read by the postfix (same call)
        static bool lastWasReal;

        static bool Prefix(TorpedoLauncherUI __instance)
        {
            lastWasReal = IsRealClick(__instance);
            if (lastWasReal) return true;
            if (!loggedIgnored)
            {
                loggedIgnored = true;
                Debug.Log(ScopeTdcControlsMod.Tag + " ignored a tube check-box change that was not a click (torpedo panel hiding/showing).");
            }
            return false;
        }

        static void Postfix(TorpedoLauncherUI __instance)
        {
            if (!lastWasReal) return; // ignored above
            try
            {
                var l = __instance.Launcher;
                TubeSelection.OneEnd(l, true, __instance.GetComponentInParent<TorpedoLaunchersUI>());
                TubeSelection.SyncSelector(); // the fire-control box follows the scope selection
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// The game auto-checks a tube when it starts flooding (autoMarkForLaunch, on a tube
    /// state change), and the checked tubes then set the firing selector switches (several
    /// tubes flooded one after another turn them to a spread / salvo).
    ///   - "Auto select tube when flooding" off (default): no auto-check at all, so the
    ///     tube selection and the switches are left alone.
    ///   - On (base game): the auto-check happens, then "one end at a time" applies.
    /// </summary>
    [HarmonyPatch(typeof(TorpedoLauncherUI), "ValidateState")]
    static class TorpedoLauncherUI_ValidateState_Patch
    {
        static void Prefix(ref bool autoMarkForLaunch)
        {
            if (!GyroConfig.AutoSelectFloodedTube) autoMarkForLaunch = false;
        }

        static void Postfix(TorpedoLauncherUI __instance, bool autoMarkForLaunch)
        {
            if (!autoMarkForLaunch) return;
            try
            {
                var l = __instance.Launcher;
                TubeSelection.OneEnd(l, false, __instance.GetComponentInParent<TorpedoLaunchersUI>());
                TubeSelection.SyncSelector();
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
