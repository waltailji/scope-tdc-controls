using System.Collections.Generic;
using HarmonyLib;
using UBOAT.Game.UI.Periscope;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// "Hide periscope raise/lower control": makes the game's raise/lower control on the
    /// periscope screen (its up / down buttons, the height scale and its pointer) invisible
    /// and click-through. It is faded out with a CanvasGroup rather than switched off: the game
    /// switches it on and off itself, and this mod still reads where it is to place its panel.
    /// The raise / lower keys keep working. The raise fully / lower fully buttons hide with it.
    /// </summary>
    public partial class GyroCounterUI
    {
        static readonly string[] StockElevatorFields = { "elevatorUp", "elevatorDown", "waterLevel", "periscopeElevationPointer" };
        // other parts of the periscope screen: a shared parent that holds any of these is too big to hide
        static readonly string[] StockOtherFields = { "topPanel", "bottomPanel", "summaryCenter", "leaveStationButton", "centerViewButton", "helpButton", "targetParametersWindow", "lockMarker" };

        static PeriscopeUI stockElevatorUI;
        static readonly List<CanvasGroup> stockElevatorGroups = new List<CanvasGroup>();
        static readonly List<CanvasGroup> stockElevatorAdded = new List<CanvasGroup>();
        static bool stockElevatorHidden;

        static Transform StockPart(PeriscopeUI ui, string field)
        {
            try
            {
                var f = AccessTools.Field(typeof(PeriscopeUI), field);
                if (f == null) return null;
                var v = f.GetValue(ui);
                var c = v as Component;
                if (c != null) return c.transform;
                var g = v as GameObject;
                return g != null ? g.transform : null;
            }
            catch { return null; }
        }

        /// <summary>The objects to fade: the control's own parent if it holds nothing else of the screen, else each part.</summary>
        static List<Transform> StockElevatorTargets(PeriscopeUI ui)
        {
            var parts = new List<Transform>();
            foreach (var n in StockElevatorFields) { var t = StockPart(ui, n); if (t != null) parts.Add(t); }
            if (parts.Count == 0) return parts;

            // lowest common parent of all the parts
            Transform common = parts[0];
            foreach (var p in parts)
                while (common != null && !p.IsChildOf(common)) common = common.parent;

            bool shared = common == null || common == ui.transform;
            if (!shared)
                foreach (var n in StockOtherFields)
                {
                    var o = StockPart(ui, n);
                    if (o != null && o.IsChildOf(common)) { shared = true; break; }
                }
            if (shared) return parts;
            return new List<Transform> { common };
        }

        static void SetStockElevatorHidden(bool hide)
        {
            var ui = periscopeUI;
            if (hide && ui != null && (ui != stockElevatorUI || stockElevatorGroups.Count == 0))
            {
                SetStockElevatorHidden(false); // a different periscope screen: let go of the old one
                stockElevatorUI = ui;
                foreach (var t in StockElevatorTargets(ui))
                {
                    var g = t.GetComponent<CanvasGroup>();
                    if (g == null) { g = t.gameObject.AddComponent<CanvasGroup>(); stockElevatorAdded.Add(g); }
                    stockElevatorGroups.Add(g);
                }
                if (GyroConfig.DebugLog)
                    Debug.Log(ScopeTdcControlsMod.Tag + " hiding the stock raise/lower control: " + stockElevatorGroups.Count + " object(s)");
            }

            if (hide)
            {
                foreach (var g in stockElevatorGroups)
                {
                    if (g == null) continue;
                    g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false;
                }
                stockElevatorHidden = true;
                return;
            }

            if (!stockElevatorHidden && stockElevatorGroups.Count == 0) return;
            foreach (var g in stockElevatorGroups)
            {
                if (g == null) continue;
                if (stockElevatorAdded.Contains(g)) Object.Destroy(g);
                else { g.alpha = 1f; g.interactable = true; g.blocksRaycasts = true; }
            }
            stockElevatorGroups.Clear();
            stockElevatorAdded.Clear();
            stockElevatorUI = null;
            stockElevatorHidden = false;
        }
    }
}
