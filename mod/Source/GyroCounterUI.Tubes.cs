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
    ///   blue  = flooded (filling while flooding), green = flooded and selected,
    ///   red   = firing (a few seconds after the launch).
    /// With outer doors (TubeDoors.cs): a grey + over a flooded tube = its door is shut (swept
    /// away clockwise while it opens, back on while it shuts); a grey X on blue = fired, the
    /// tube empty but still flooded.
    /// Click a flooded tube: select it for a single shot (the Einzelschuß switch turns there,
    /// the salvo switch to Aus). Double-click a loaded tube: flood it, or drain it if flooded
    /// or flooding, exactly as the game's torpedo panel does on a click on the tube.
    /// Right-click: actions menu; Ctrl+click: launch with the tube's own lever (GyroCounterUI.TubeMenu.cs).
    /// </summary>
    public partial class GyroCounterUI
    {
        const float TubeSize = 32f, TubeGap = 6f, TubePad = 8f; // first version 20 / 4 / 6, then 40 / 8 / 10
        float tubeLabelW;            // row label column: fits the longer of the two labels
        const float TubeIconSize = 24f; // the gyro setting panel's dial icon, right of the bow tubes
        const float TubeFont = 16f;

        static readonly Color TubeEmpty = new Color(0.05f, 0.05f, 0.05f, 1f);
        static readonly Color TubeLoaded = new Color(0.55f, 0.55f, 0.53f, 1f);
        static readonly Color TubeFlooded = new Color(26f / 255f, 26f / 255f, 1f, 1f);      // #1A1AFF
        static readonly Color TubeMaintenance = new Color(242f / 255f, 204f / 255f, 26f / 255f, 1f); // #F2CC1A: crew servicing the torpedo
        static readonly Color TubeSelected = new Color(46f / 255f, 184f / 255f, 46f / 255f, 1f); // #2EB82E
        static readonly Color TubeFiring = new Color(0.86f, 0.14f, 0.10f, 1f);
        static readonly Color TubeMark = new Color(0.72f, 0.72f, 0.70f, 1f); // the door cross and the empty X

        sealed class TubeDot
        {
            public ExtendedTorpedoLauncher Launcher;
            public int Number;           // 1-based, as on the Einzelschuß switch
            public bool Stern;
            public Image Fill;
            public Image Level;          // the water rising (flooding) or falling (draining), from the bottom
            public Image EmptyMark;      // grey X: fired, empty but still flooded
            public Image DoorMark;       // grey +: outer door shut (radial fill = how shut it is)
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
            CloseTubeMenu();
            tubeDots.Clear();
            tubeLevers.Clear();
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

            tubeLabelW = Mathf.Ceil(Mathf.Max(TextWidth(font, Strings.TubesBowShort.ToUpperInvariant(), TubeFont), TextWidth(font, Strings.TubesSternShort.ToUpperInvariant(), TubeFont))) + 12f;
            int cols = Mathf.Max(bow.Count, stern.Count);
            int rows = stern.Count > 0 && bow.Count > 0 ? 2 : 1;
            float y = -TubePad;
            if (bow.Count > 0) { AddTubeRow(font, bow, Strings.TubesBowShort, y); y -= TubeSize + TubeGap; }
            if (stern.Count > 0) { AddTubeRow(font, stern, Strings.TubesSternShort, y); y -= TubeSize + TubeGap; }

            // a column on the right for the gyro setting panel's dial icon, level with the bow tubes
            float iconX = TubePad + tubeLabelW + cols * TubeSize + (cols - 1) * TubeGap + TubeGap * 2f;
            float w = iconX + TubeIconSize + TubePad + GripSize * 0.6f;
            float h = TubePad * 2f + rows * TubeSize + (rows - 1) * TubeGap;
            tubesPanel.sizeDelta = new Vector2(w, h);
            if (bow.Count > 0)
            {
                var icon = NewRect("GyroSetIcon", tubesPanel);
                PlaceV(icon, iconX, -TubePad - (TubeSize - TubeIconSize) * 0.5f, TubeIconSize, TubeIconSize);
                MakeIconToggle(icon, () => GyroConfig.ShowGyroSet, ToggleGyroSetWindow, () => Strings.TipGyroSetIcon);
            }
            if (stern.Count > 0)
            {
                float sternTop = -TubePad - (bow.Count > 0 ? TubeSize + TubeGap : 0f);
                var icon = NewRect("SternGyroSetIcon", tubesPanel);
                PlaceV(icon, iconX, sternTop - (TubeSize - TubeIconSize) * 0.5f, TubeIconSize, TubeIconSize);
                MakeIconToggle(icon, () => GyroConfig.ShowSternGyroSet, ToggleSternSetWindow, () => Strings.TipSternSetIcon);
            }
            AddResizeGrip(tubesPanel, ResizableWindow.Tubes);
            RaiseOverlays();
            UpdateTubes();
        }

        void AddTubeRow(TMP_FontAsset font, List<TubeDot> row, string label, float top)
        {
            var l = NewText("RowLabel", tubesPanel, font, label.ToUpperInvariant(), TubeFont, BoxInk); // FORE / AFT, like the titles
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
                dot.Level = Disc("Level", holder, SharpCircleSprite(), TubeSize - 5f, TubeFlooded);
                dot.Level.type = Image.Type.Filled;
                dot.Level.fillMethod = Image.FillMethod.Vertical;
                dot.Level.fillOrigin = (int)Image.OriginVertical.Bottom;
                dot.Level.fillAmount = 0f;
                dot.EmptyMark = Disc("EmptyMark", holder, TubeXSprite(), TubeSize - 5f, TubeMark);
                dot.EmptyMark.enabled = false;
                dot.DoorMark = Disc("DoorMark", holder, TubePlusSprite(), TubeSize - 5f, TubeMark);
                dot.DoorMark.type = Image.Type.Filled;
                dot.DoorMark.fillMethod = Image.FillMethod.Radial360;
                dot.DoorMark.fillOrigin = (int)Image.Origin360.Top;
                dot.DoorMark.fillClockwise = false; // swept off clockwise as the door opens
                dot.DoorMark.enabled = false;
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
                float level = 0f; // blue water level, 0 = none .. 1 = full
                bool light;   // light number on a dark fill
                bool wetEmpty = TubeDoors.WetEmpty(l);
                bool firing = TubeDoors.Firing(l);
                if (firing) { fill = TubeFiring; light = true; }           // a torpedo just left
                else if (wetEmpty && l.CurrentState == TorpedoLauncher.State.Draining)
                {
                    // fired tube being drained: empty, the water level falling
                    fill = TubeEmpty; light = true; level = Mathf.Clamp01(l.FloodingProgress);
                }
                else if (wetEmpty) { fill = TubeFlooded; light = true; }   // fired: empty, still flooded
                else if (!TubeHasTorpedo(l)) { fill = TubeEmpty; light = true; }
                else if (l.CurrentState == TorpedoLauncher.State.Preheating) { fill = TubeMaintenance; light = false; } // in maintenance
                else if (l.IsFlooded && l.IsMarkedForLaunch) { fill = TubeSelected; light = false; }
                else if (l.IsFlooded) { fill = TubeFlooded; light = true; }
                else
                {
                    // grey; while flooding (or draining) the blue water level rises (or falls) from the bottom
                    float p = l.CurrentState == TorpedoLauncher.State.Flooding || l.CurrentState == TorpedoLauncher.State.Draining ? Mathf.Clamp01(l.FloodingProgress) : 0f;
                    fill = TubeLoaded;
                    level = p;
                    light = p > 0.5f;
                }
                dot.Fill.color = fill;   // status colours: not tinted by the interior light
                if (dot.Level != null)
                {
                    dot.Level.fillAmount = level;
                    dot.Level.enabled = level > 0.001f;
                }
                dot.Label.color = light ? DigitLight : DigitDark;

                // outer door: the + shows how shut it is; the X marks a flooded empty tube
                bool wet = TubeDoors.Enabled && (l.IsFlooded || wetEmpty);
                float shut = wet && !firing ? 1f - TubeDoors.Openness(l) : 0f;
                if (dot.DoorMark != null)
                {
                    dot.DoorMark.enabled = shut > 0.001f;
                    dot.DoorMark.fillAmount = shut;
                }
                if (dot.EmptyMark != null) dot.EmptyMark.enabled = wetEmpty && !firing;
            }
        }

        static Sprite tubePlusSprite, tubeXSprite;
        const float TubeMarkHalfWidth = 3.4f; // bar half-width in the 64 px sprite (about 3 px on the icon)

        /// <summary>A + across the tube circle: the outer door shut.</summary>
        static Sprite TubePlusSprite()
        {
            if (tubePlusSprite != null) return tubePlusSprite;
            const int n = 64;
            float c = n * 0.5f, r = n * 0.5f - 3f;
            tubePlusSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                if (dx * dx + dy * dy > r * r) return false;
                return Mathf.Abs(dx) <= TubeMarkHalfWidth || Mathf.Abs(dy) <= TubeMarkHalfWidth;
            });
            return tubePlusSprite;
        }

        /// <summary>An X across the tube circle: empty, still flooded.</summary>
        static Sprite TubeXSprite()
        {
            if (tubeXSprite != null) return tubeXSprite;
            const int n = 64;
            float c = n * 0.5f, r = n * 0.5f - 3f;
            const float k = 0.70710678f;
            tubeXSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                if (dx * dx + dy * dy > r * r) return false;
                return Mathf.Abs(dx - dy) * k <= TubeMarkHalfWidth || Mathf.Abs(dx + dy) * k <= TubeMarkHalfWidth;
            });
            return tubeXSprite;
        }

        /// <summary>Single shot from this tube: Einzelschuß to it, Fächerschuß to Aus, as on the switch box.</summary>
        void SelectTube(TubeDot dot)
        {
            var ship = Ship();
            var l = dot.Launcher;
            if (ship == null || l == null || !l.IsFlooded || l.Torpedo == null) return; // a fired tube is flooded but empty
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
            if (l == null) return;
            if (TubeDoors.WetEmpty(l)) { TubeDoors.DrainEmpty(l); return; } // fired: drain it (door shut)
            if (!TubeHasTorpedo(l) || TubeBusy(l.CurrentState)) return;
            var s = l.CurrentState;
            if (s != TorpedoLauncher.State.Loaded && s != TorpedoLauncher.State.Flooded && s != TorpedoLauncher.State.Flooding) return;
            if (!l.IsFlooded && l.FloodingProgress == 0f)
            {
                if (l.Group != null && l.Group.HasDisableFlag((DeviceDisableFlag)1)) return; // tube group damaged
                l.Flood(false);
            }
            else
            {
                if (!TubeDoors.DoorShut(l)) return; // not with the outer door open
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
            bool wetEmpty = TubeDoors.WetEmpty(l);
            if (TubeDoors.Firing(l)) state = Strings.TubeFired;
            else if (wetEmpty) state = Strings.TubeWetEmpty;
            else if (s == TorpedoLauncher.State.Empty) state = Strings.TubeEmpty;
            else if (s == TorpedoLauncher.State.Loading) state = Strings.TubeLoading;
            else if (s == TorpedoLauncher.State.Flooding) state = Strings.TubeFlooding;
            else if (s == TorpedoLauncher.State.Draining) state = Strings.TubeDraining;
            else if (s == TorpedoLauncher.State.Launching) state = Strings.TubeLaunching;
            else if (s == TorpedoLauncher.State.Preheating) state = Strings.TubeMaintenance;
            else if (l.IsFlooded) state = l.IsMarkedForLaunch ? Strings.TubeFloodedSelected : Strings.TubeFlooded;
            else state = Strings.TubeLoaded;
            sb.Append(state);
            var stored = l.Torpedo;
            string kind = stored != null ? TorpedoTypeName(stored.name) : "";
            if (kind.Length > 0) sb.Append('\n').Append(Strings.TorpedoWord).Append(": ").Append(kind);
            if (TubeDoors.Enabled && TubeDoors.Wet(l))
            {
                var door = TubeDoors.Door(l);
                sb.Append('\n').Append(Strings.TubeDoorLabel).Append(": ").Append(
                    door == DoorState.Open ? Strings.DoorOpen : door == DoorState.Opening ? Strings.DoorOpening
                    : door == DoorState.Closing ? Strings.DoorClosing : Strings.DoorShut);
            }
            if (!wetEmpty) sb.Append("\n\n").Append(!l.IsFlooded ? Strings.TubeClickNeedsFlood : l.IsMarkedForLaunch ? Strings.TubeClickDeselect : Strings.TubeClickSelect);
            else sb.Append('\n');
            bool drains = wetEmpty || (TubeHasTorpedo(l) && !TubeBusy(s) && (l.IsFlooded || s == TorpedoLauncher.State.Flooding));
            if (drains && !TubeDoors.DoorShut(l)) sb.Append('\n').Append(Strings.TubeDrainDoorOpen);
            else if (drains) sb.Append('\n').Append(Strings.TubeDoubleDrain);
            else if (TubeHasTorpedo(l) && !TubeBusy(s)) sb.Append('\n').Append(Strings.TubeDoubleFlood);
            if (CanLaunch(l)) sb.Append('\n').Append(Strings.TubeCtrlLaunch);
            sb.Append('\n').Append(Strings.TubeRightMenu);
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
                if (e.button == PointerEventData.InputButton.Right) { Owner.OpenTubeMenu(Dot, e.position); return; } // actions menu
                if (e.button != PointerEventData.InputButton.Left) return;
                Owner.HideTip();
                if (CtrlKey) { Owner.pendingTubeClick = null; Owner.LaunchTube(Dot); return; } // Ctrl+click: the tube's own launch lever
                if (e.clickCount >= 2) { Owner.pendingTubeClick = null; Owner.ToggleTubeFlood(Dot); } // a double-click only
                else { Owner.pendingTubeClick = Dot; Owner.pendingTubeClickAt = Time.unscaledTime + TubeDoubleClickTime; } // single: once no second click follows
            }
        }
    }
}
