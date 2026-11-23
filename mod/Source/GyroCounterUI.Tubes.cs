using System.Collections.Generic;
using System.Text;
using TMPro;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// SH3-style torpedo tube indicator: a compact window with one numbered circle per tube,
    /// bow tubes on the first row, stern tubes (if the boat has any) on the second.
    ///   black = empty (or loading), grey = loaded but not flooded,
    ///   blue  = flooded (filling while flooding), green = flooded and selected.
    /// Click a flooded tube: select it for a single shot (the Einzelschuß switch turns there,
    /// the salvo switch to Aus). Double-click a loaded tube: flood it, or drain it if flooded
    /// or flooding, exactly as the game's torpedo panel does on a click on the tube.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float TubeSize = 32f, TubeGap = 6f, TubePad = 8f; // first version 20 / 4 / 6, then 40 / 8 / 10
        float tubeLabelW;            // row label column: fits the longer of the two labels
        const float TubeFont = 16f;

        static readonly Color TubeEmpty = new Color(0.05f, 0.05f, 0.05f, 1f);
        static readonly Color TubeLoaded = new Color(0.55f, 0.55f, 0.53f, 1f);
        static readonly Color TubeFlooded = new Color(26f / 255f, 26f / 255f, 1f, 1f);      // #1A1AFF
        static readonly Color TubeMaintenance = new Color(242f / 255f, 204f / 255f, 26f / 255f, 1f); // #F2CC1A: crew servicing the torpedo
        static readonly Color TubeSelected = new Color(46f / 255f, 184f / 255f, 46f / 255f, 1f); // #2EB82E

        sealed class TubeDot
        {
            public ExtendedTorpedoLauncher Launcher;
            public int Number;           // 1-based, as on the Einzelschuß switch
            public bool Stern;
            public Image Fill;
            public TextMeshProUGUI Label;
        }

        RectTransform tubesPanel;
        readonly List<TubeDot> tubeDots = new List<TubeDot>();
        ExtendedTorpedoLauncher[] tubesFor;  // the boat's tubes the window was built for
        float tubesRetryAt;
        TubeDot pendingTubeClick;     // clicked once: selected unless a second click makes it a double-click
        float pendingTubeClickAt;
        const float TubeDoubleClickTime = 0.3f;

        /// <summary>(Re)build when the boat's tubes change (another boat, or not loaded yet).</summary>
        void ValidateTubes()
        {
            var ship = Ship();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubesPanel != null && ReferenceEquals(tubes, tubesFor)) return;
            if (tubesPanel == null && Time.unscaledTime < tubesRetryAt) return;
            tubesRetryAt = Time.unscaledTime + 2f;
            if (tubesPanel != null) { Destroy(tubesPanel.gameObject); tubesPanel = null; }
            tubeDots.Clear();
            tubesFor = null;
            if (tubes == null || tubes.Length == 0) return;
            BuildTubes(ResolveFont(), ship, tubes);
        }

        void BuildTubes(TMP_FontAsset font, PlayerShip ship, ExtendedTorpedoLauncher[] tubes)
        {
            tubesFor = tubes;
            var boxes = ship.GetEquipment<TorpedoFireControlBoxes>();
            var bow = new List<TubeDot>();
            var stern = new List<TubeDot>();
            for (int i = 0; i < tubes.Length; i++)
            {
                var l = tubes[i];
                if (l == null) continue;
                bool isStern = ReceiverOverride.IsStern(boxes, l.Group);
                (isStern ? stern : bow).Add(new TubeDot { Launcher = l, Number = i + 1, Stern = isStern });
            }

            tubesPanel = NewRect("TorpedoTubes", transform);
            tubesPanel.anchorMin = tubesPanel.anchorMax = tubesPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(tubesPanel);
            tubesPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(tubesPanel.gameObject, false).Tubes = true;

            tubeLabelW = Mathf.Ceil(Mathf.Max(TextWidth(font, Strings.TubesBowShort, TubeFont), TextWidth(font, Strings.TubesSternShort, TubeFont))) + 12f;
            int cols = Mathf.Max(bow.Count, stern.Count);
            int rows = stern.Count > 0 && bow.Count > 0 ? 2 : 1;
            float y = -TubePad;
            if (bow.Count > 0) { AddTubeRow(font, bow, Strings.TubesBowShort, y); y -= TubeSize + TubeGap; }
            if (stern.Count > 0) { AddTubeRow(font, stern, Strings.TubesSternShort, y); y -= TubeSize + TubeGap; }

            float w = TubePad + tubeLabelW + cols * TubeSize + (cols - 1) * TubeGap + TubePad + GripSize * 0.6f;
            float h = TubePad * 2f + rows * TubeSize + (rows - 1) * TubeGap;
            tubesPanel.sizeDelta = new Vector2(w, h);
            AddResizeGrip(tubesPanel, ResizableWindow.Tubes);
            RaiseOverlays();
            UpdateTubes();
        }

        void AddTubeRow(TMP_FontAsset font, List<TubeDot> row, string label, float top)
        {
            var l = NewText("RowLabel", tubesPanel, font, label, TubeFont, BoxInk);
            l.fontStyle = FontStyles.Bold;
            l.alignment = TextAlignmentOptions.Center;
            PlaceV(l.rectTransform, TubePad, top - (TubeSize - 22f) * 0.5f, tubeLabelW - 6f, 22f);

            float x = TubePad + tubeLabelW;
            foreach (var dot in row)
            {
                var holder = NewRect("TubeDot", tubesPanel); // not light-tinted (see InLamp)
                PlaceV(holder, x, top, TubeSize, TubeSize);
                var rim = Disc("Rim", holder, SharpCircleSprite(), TubeSize, Color.black);
                rim.raycastTarget = true;
                rim.alphaHitTestMinimumThreshold = 0.5f;
                dot.Fill = Disc("Fill", holder, SharpCircleSprite(), TubeSize - 5f, TubeEmpty);
                dot.Label = NewText("Number", holder, font, dot.Number.ToString(), TubeFont, DigitLight);
                dot.Label.fontStyle = FontStyles.Bold;
                dot.Label.alignment = TextAlignmentOptions.Center;
                var lr = dot.Label.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = lr.offsetMax = Vector2.zero;

                var relay = rim.gameObject.AddComponent<TubeRelay>();
                relay.Owner = this;
                relay.Dot = dot;
                var d = dot;
                AddTooltip(rim.gameObject, () => TubeTip(d));
                tubeDots.Add(dot);
                x += TubeSize + TubeGap;
            }
        }

        static bool TubeBusy(TorpedoLauncher.State s)
        {
            return s == TorpedoLauncher.State.Loading || s == TorpedoLauncher.State.Preheating
                || s == TorpedoLauncher.State.Unloading || s == TorpedoLauncher.State.Launching;
        }

        static bool TubeHasTorpedo(TorpedoLauncher l)
        {
            return l != null && l.Torpedo != null && l.CurrentState != TorpedoLauncher.State.Empty && l.CurrentState != TorpedoLauncher.State.Loading;
        }

        void UpdateTubes()
        {
            if (tubesPanel == null) return;
            // a single click acts once the double-click time has passed without a second click
            if (pendingTubeClick != null && Time.unscaledTime >= pendingTubeClickAt)
            {
                var d = pendingTubeClick;
                pendingTubeClick = null;
                SelectTube(d);
            }
            foreach (var dot in tubeDots)
            {
                var l = dot.Launcher;
                if (l == null || dot.Fill == null) continue;
                Color fill;
                bool light;   // light number on a dark fill
                if (!TubeHasTorpedo(l)) { fill = TubeEmpty; light = true; }
                else if (l.CurrentState == TorpedoLauncher.State.Preheating) { fill = TubeMaintenance; light = false; } // in maintenance
                else if (l.IsFlooded && l.IsMarkedForLaunch) { fill = TubeSelected; light = false; }
                else if (l.IsFlooded) { fill = TubeFlooded; light = true; }
                else
                {
                    // grey, turning blue as it floods (and back as it drains)
                    float p = l.CurrentState == TorpedoLauncher.State.Flooding || l.CurrentState == TorpedoLauncher.State.Draining ? Mathf.Clamp01(l.FloodingProgress) : 0f;
                    fill = Color.Lerp(TubeLoaded, TubeFlooded, p);
                    light = p > 0.5f;
                }
                dot.Fill.color = fill;   // status colours: not tinted by the interior light
                dot.Label.color = light ? DigitLight : DigitDark;
            }
        }

        /// <summary>Single shot from this tube: Einzelschuß to it, Fächerschuß to Aus, as on the switch box.</summary>
        void SelectTube(TubeDot dot)
        {
            var ship = Ship();
            var l = dot.Launcher;
            if (ship == null || l == null || !l.IsFlooded) return;
            if (FiringCircuitLive || ship.IsPreparingToLaunchTorpedoes) return; // switches can't be changed now
            var boxes = ship.GetEquipment<TorpedoFireControlBoxes>();
            if (boxes == null) return;
            if (boxes.SelectedTorpedoSalvoPreset != 0) boxes.SelectedTorpedoSalvoPreset = 0;
            // a click on a tube that is already selected deselects it (Einzelschuß to Aus)
            boxes.SelectedTorpedoTube = l.IsMarkedForLaunch ? (SelectedTorpedoTube)0 : (SelectedTorpedoTube)dot.Number;
            boxes.ValidateMarkedLaunchersFromSalvoSettings();
            SyncTorpedoPanel();
            PlayGameSound(AnyBoxKnobSound());
        }

        /// <summary>A selected tube was drained: the switch box lets go of it (Einzelschuß / Fächerschuß to Aus).</summary>
        void DeselectTube()
        {
            var ship = Ship();
            if (ship == null || FiringCircuitLive || ship.IsPreparingToLaunchTorpedoes) return;
            var boxes = ship.GetEquipment<TorpedoFireControlBoxes>();
            if (boxes == null) return;
            if (boxes.SelectedTorpedoSalvoPreset != 0) boxes.SelectedTorpedoSalvoPreset = 0;
            boxes.SelectedTorpedoTube = (SelectedTorpedoTube)0;
            boxes.ValidateMarkedLaunchersFromSalvoSettings();
            SyncTorpedoPanel();
        }

        /// <summary>Flood a loaded tube, or drain a flooded / flooding one (as the game's torpedo panel does).</summary>
        void ToggleTubeFlood(TubeDot dot)
        {
            var l = dot.Launcher;
            if (l == null || !TubeHasTorpedo(l) || TubeBusy(l.CurrentState)) return;
            var s = l.CurrentState;
            if (s != TorpedoLauncher.State.Loaded && s != TorpedoLauncher.State.Flooded && s != TorpedoLauncher.State.Flooding) return;
            if (!l.IsFlooded && l.FloodingProgress == 0f)
            {
                if (l.Group != null && l.Group.HasDisableFlag((DeviceDisableFlag)1)) return; // tube group damaged
                l.Flood(false);
            }
            else
            {
                bool wasSelected = l.IsMarkedForLaunch;
                l.DrainInstantly();
                if (wasSelected) DeselectTube(); // a drained tube can't stay selected
            }
        }

        /// <summary>The game's torpedo panel check boxes follow the selection.</summary>
        static void SyncTorpedoPanel()
        {
            try
            {
                var ui = Get<UBOAT.Game.UI.GameUI>();
                var panel = ui != null ? ui.CurrentTorpedoLaunchersUI : null;
                if (panel != null) TubeMarkMemory.SyncWidgets(panel);
            }
            catch { }
        }

        string TubeTip(TubeDot dot)
        {
            var l = dot.Launcher;
            var sb = new StringBuilder();
            sb.Append("<b>").Append(Strings.TubeWord).Append(' ').Append(dot.Number)
              .Append(" (").Append(dot.Stern ? Strings.Stern : Strings.Bow).Append(")</b>\n");
            if (l == null) return sb.ToString();
            var s = l.CurrentState;
            string state;
            if (s == TorpedoLauncher.State.Empty) state = Strings.TubeEmpty;
            else if (s == TorpedoLauncher.State.Loading) state = Strings.TubeLoading;
            else if (s == TorpedoLauncher.State.Flooding) state = Strings.TubeFlooding;
            else if (s == TorpedoLauncher.State.Draining) state = Strings.TubeDraining;
            else if (s == TorpedoLauncher.State.Launching) state = Strings.TubeLaunching;
            else if (s == TorpedoLauncher.State.Preheating) state = Strings.TubeMaintenance;
            else if (l.IsFlooded) state = l.IsMarkedForLaunch ? Strings.TubeFloodedSelected : Strings.TubeFlooded;
            else state = Strings.TubeLoaded;
            sb.Append(state);
            sb.Append("\n\n").Append(!l.IsFlooded ? Strings.TubeClickNeedsFlood : l.IsMarkedForLaunch ? Strings.TubeClickDeselect : Strings.TubeClickSelect);
            if (TubeHasTorpedo(l) && !TubeBusy(s))
                sb.Append('\n').Append(l.IsFlooded || s == TorpedoLauncher.State.Flooding ? Strings.TubeDoubleDrain : Strings.TubeDoubleFlood);
            return sb.ToString();
        }

        void PositionTubes()
        {
            if (tubesPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.TubesX, Screen.height * GyroConfig.TubesY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                tubesPanel.anchoredPosition = local + new Vector2(GyroConfig.TubesOffsetX, GyroConfig.TubesOffsetY);
        }

        sealed class TubeRelay : MonoBehaviour, IPointerClickHandler
        {
            public GyroCounterUI Owner;
            public TubeDot Dot;

            public void OnPointerClick(PointerEventData e)
            {
                if (e.button != PointerEventData.InputButton.Left) return;
                Owner.HideTip();
                if (e.clickCount >= 2) { Owner.pendingTubeClick = null; Owner.ToggleTubeFlood(Dot); } // a double-click only
                else { Owner.pendingTubeClick = Dot; Owner.pendingTubeClickAt = Time.unscaledTime + TubeDoubleClickTime; } // single: once no second click follows
            }
        }
    }
}
