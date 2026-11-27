using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Forward torpedo room gyro setting panel (the bow tube group's Schusswinkeleinstellung):
    ///  - gyro angle, fine (10° a turn) and coarse (360°): brass needle = the gyro angle received
    ///    from the TDC (TorpedoLauncherGroup.ReceivedGyroAngle), red flag = the angle set on the
    ///    tubes (SetGyroAngle);
    ///  - spread angle: needle = received, flag = set (Received / SetSalvoSpreadAngle);
    ///  - hand wheel: as TorpedoLauncherGroupAngleSettingHandWheel, it turns the gyro angle flag
    ///    on "manual gyro" (180° a turn, half the game's rate, Shift 18°), otherwise the spread
    ///    angle flag (20° a turn);
    ///  - salvo size switch, 1 2 3 4 from the top clockwise (SelectedSalvoSize);
    ///  - setting mode switch: manual salvo + auto gyro / manual salvo / manual gyro
    ///    (AngleSettingMode). The game does the rest: on "manual salvo, auto gyro" it drives
    ///    every flag back onto its needle;
    ///  - fast synchronization lever (FastSynchronizationMode): held to - or +, it drives the gyro
    ///    angle flag that way (3°/s more), in every setting mode; its lamps show which way the flag lags
    ///    (as the boat's indicator lights: more than 3.5° off).
    /// Every flag can also be set by turning or clicking its own dial. Shown with its check box,
    /// or the dial icon beside the bow receiver counter or in the torpedo tube window.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float GyroSetColGap = 8f;
        const float GroupSpreadMax = 20f;
        const float WheelGyroPerTurn = 180f; // degrees of gyro angle per turn of the hand wheel (Shift: a tenth)
        const float WheelScale = 1.7f;       // the hand wheel, as a multiple of a dial's size
        const float FastSyncColW = 100f;     // the fast sync lever's column
        const float FastSyncLag = 3.5f;   // the boat's indicator lights: lit beyond this many degrees

        RectTransform gyroSetPanel;
        readonly List<DialSpec> gyroSetDials = new List<DialSpec>();
        readonly List<KnobSpec> gyroSetKnobs = new List<KnobSpec>();
        DialSpec gyroSetWheel;
        static float gyroSetWheelAngle;   // the hand wheel's own angle, kept when the window is rebuilt
        RectTransform fastHandle;
        Lamp fastLampMinus, fastLampPlus;

        /// <summary>The forward tube group (the one the bow gyro angle receiver feeds).</summary>
        static TorpedoLauncherGroup BowGroup()
        {
            TorpedoLauncherGroup bow, stern;
            ReceiverGroups(out bow, out stern);
            return bow;
        }

        static bool ManualGyro(TorpedoLauncherGroup g) { return g != null && g.AngleSettingMode == TorpedoLauncherGroupAngleSettingMode.GyroManual; }

        void BuildGyroSet(TMP_FontAsset font)
        {
            gyroSetDials.Clear();
            gyroSetKnobs.Clear();
            gyroSetPanel = NewRect("BowGyroSetting", transform);
            gyroSetPanel.anchorMin = gyroSetPanel.anchorMax = gyroSetPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(gyroSetPanel);
            gyroSetPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(gyroSetPanel.gameObject, false).GyroSet = true;

            // laid out as the boat's panel:
            //   spread angle | gyro coarse | gyro fine | fast sync lever
            //   salvo size   | setting mode | hand wheel (filling the last two columns)
            float inner = DialSize + 16f;
            float col = inner + GyroSetColGap;
            float x0 = DialPad;
            // the lever's column is only as wide as the lever
            float width = DialPad * 2f + inner * 3f + FastSyncColW + GyroSetColGap * 3f;
            float y = -DialPad;

            var title = ControlTitle(gyroSetPanel, font, Strings.GyroSetTitle, x0 + CloseIcon, y, width - DialPad * 2f - CloseIcon * 2f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 8f;
            title.fontSizeMax = TitleFont;
            title.raycastTarget = true;
            AddTooltip(title.gameObject, () => Strings.TipGyroSetWindow);
            y -= TitleH + 6f;

            // ---- row 1: spread angle, gyro angle coarse, gyro angle fine, fast sync lever
            float row = y;
            float yFine = AddDial(font, x0 + col * 2f, row, inner, Strings.DialGyroFine, () => Strings.TipDialGyroFine, GyroDialSpec(BowGroup, true),
                                  (spec, f) => DrawBearingFineScale(spec, f), "", gyroSetPanel, gyroSetDials);
            var fine = gyroSetDials[gyroSetDials.Count - 1];
            fine.Readout.transform.parent.gameObject.SetActive(false); // one readout, under the coarse dial

            float yCoarse = AddDial(font, x0 + col, row, inner, Strings.DialGyroCoarse, () => Strings.TipDialGyroCoarse, GyroDialSpec(BowGroup, false),
                                    (spec, f) => DrawBearingCoarseScale(spec, f), "", gyroSetPanel, gyroSetDials);
            AddTypedEntry(gyroSetDials[gyroSetDials.Count - 1], font, ParseTypedAngle, () => Strings.RangeAngle360, () => Strings.TypeGyro);

            float ySpread = AddDial(font, x0, row, inner, Strings.DialGroupSpread, () => Strings.TipDialGroupSpread, new DialSpec
            {
                Get = t => { var g = BowGroup(); return g != null ? g.SetSalvoSpreadAngle : float.NaN; },
                Set = (t, v) => { var g = BowGroup(); if (g != null && !float.IsNaN(v)) g.SetSalvoSpreadAngle = Mathf.Clamp(v, 0f, GroupSpreadMax); },
                Get2 = t => { var g = BowGroup(); return g != null ? g.ReceivedSalvoSpreadAngle : float.NaN; },
                Locked = t => BowGroup() == null,
                Style = PointerStyle.Flag,
                Values = new[] { 0f, GroupSpreadMax }, Rots = new[] { 0f, 180f },
                WheelStep = -0.5f,
                Format = v => v.ToString("0.0", CultureInfo.InvariantCulture) + "°"
            }, (spec, f) => DrawSpreadAngleScale(spec, f), "", gyroSetPanel, gyroSetDials);
            AddTypedEntry(gyroSetDials[gyroSetDials.Count - 1], font, TypedNumber(0f, GroupSpreadMax, 0.1f), () => Strings.RangeSpread, () => Strings.TypeSpread);
            BuildFastSyncLever(font, x0 + col * 3f, FastSyncColW, row);
            y = Mathf.Min(yFine, Mathf.Min(yCoarse, ySpread));

            // ---- row 2: salvo size switch, setting mode switch, hand wheel
            row = y;
            gyroSetWheel = new DialSpec
            {
                Get = t => { var g = BowGroup(); return g == null ? float.NaN : ManualGyro(g) ? g.SetGyroAngle : g.SetSalvoSpreadAngle; },
                Set = (t, v) =>
                {
                    var g = BowGroup();
                    if (g == null || float.IsNaN(v)) return;
                    if (ManualGyro(g)) g.SetGyroAngle = Mathf.Repeat(v, 360f);
                    else g.SetSalvoSpreadAngle = Mathf.Clamp(v, 0f, GroupSpreadMax);
                },
                Locked = t => BowGroup() == null,
                Style = PointerStyle.Wheel,
                NoClick = true,
                FreeSpin = true,   // it turns freely and drives the flags through clutches: it stays wherever it was turned to
                FreeAngle = gyroSetWheelAngle,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                WheelStep = -1f,
                Format = v =>
                {
                    var g = BowGroup();
                    return ManualGyro(g) ? Strings.WheelGyro + " " + FormatBearing(v)
                                         : Strings.WheelSpread + " " + Mathf.Clamp(v, 0f, GroupSpreadMax).ToString("0.0", CultureInfo.InvariantCulture) + "°";
                }
            };
            // the big wheel fills the last two columns: built at dial size, then scaled up about its
            // centre (turning reads the angle round that centre, so it works at any size)
            float wheelW = inner + GyroSetColGap + FastSyncColW;
            float yWheel = AddDial(font, x0 + col * 2f, row, wheelW, Strings.DialHandWheel, () => Strings.TipDialHandWheel, gyroSetWheel,
                                   (spec, f) => { }, "", gyroSetPanel, gyroSetDials);
            float k = WheelScale;
            var wheelHolder = (RectTransform)gyroSetWheel.Face.parent;
            Vector2 topLeft = wheelHolder.anchoredPosition;           // placed by its top-left corner
            wheelHolder.pivot = new Vector2(0.5f, 0.5f);
            wheelHolder.anchoredPosition = new Vector2(topLeft.x + DialSize * 0.5f, topLeft.y - DialSize * k * 0.5f - 3f * (k - 1f));
            wheelHolder.localScale = Vector3.one * k;
            float grow = DialSize * (k - 1f) + 6f * (k - 1f);
            var wheelReadout = (RectTransform)gyroSetWheel.Readout.transform.parent;
            wheelReadout.anchoredPosition += new Vector2(0f, -grow);   // under the bigger wheel
            yWheel -= grow;

            // the knobs lay out from their parent's top: give each row its own holder
            var knobRow = NewRect("KnobRow", gyroSetPanel);
            knobRow.anchorMin = knobRow.anchorMax = knobRow.pivot = new Vector2(0f, 1f);
            knobRow.sizeDelta = Vector2.zero;
            knobRow.anchoredPosition = new Vector2(0f, row + DialPad);

            var sizeKnob = GroupKnob<TorpedoLauncherGroupSalvoSizeSwitch>();
            AddKnob(font, knobRow, gyroSetKnobs, x0 + (inner - KnobSize) * 0.5f, Strings.SwitchSalvoSize, () => Strings.TipSwitchSalvoSize, new KnobSpec
            {
                Values = new[] { 1, 2, 3, 4 },
                Labels = new[] { "1", "2", "3", "4" },
                Rots = new[] { 0f, 60f, 120f, 180f },  // from the top, clockwise, to the bottom
                LabelSize = 13f,
                InnerTicks = true,
                Sound = KnobSound(sizeKnob),
                Get = b => { var g = BowGroup(); return g != null ? g.SelectedSalvoSize : -1; },
                Set = (b, v) => { var g = BowGroup(); if (g != null) g.SelectedSalvoSize = v; }
            });

            var modeKnob = GroupKnob<TorpedoLauncherGroupAngleSettingModeSwitch>();
            AddKnob(font, knobRow, gyroSetKnobs, x0 + col + (inner - KnobSize) * 0.5f, Strings.SwitchAngleMode, () => Strings.TipSwitchAngleMode, new KnobSpec
            {
                Values = new[] { 0, 1, 2 },
                Labels = Strings.AngleModeLabels,
                Names = Strings.AngleModeNames,
                Rots = new[] { 0f, 90f, 180f },  // as on the panel: auto at the top, salvo to the right, gyro at the bottom
                LabelSize = 8.5f,
                InnerTicks = true,
                Sound = KnobSound(modeKnob),
                Get = b => { var g = BowGroup(); return g != null ? (int)g.AngleSettingMode : -1; },
                Set = (b, v) => { var g = BowGroup(); if (g != null) g.AngleSettingMode = (TorpedoLauncherGroupAngleSettingMode)v; }
            });
            y = Mathf.Min(yWheel, row - ControlColumnH - 12f);

            gyroSetPanel.sizeDelta = new Vector2(width, -y + DialPad - 6f);
            AddCloseButton(gyroSetPanel, () => SetGyroSetWindow(false));
            AddResizeGrip(gyroSetPanel, ResizableWindow.GyroSet);
            RaiseOverlays();
        }

        /// <summary>
        /// A tube group's gyro angle dial: brass needle = received from the TDC, red flag = set on the
        /// tubes. Fine: one turn = 10°; coarse: 360°.
        /// </summary>
        static DialSpec GyroDialSpec(Func<TorpedoLauncherGroup> group, bool fine)
        {
            return new DialSpec
            {
                Get = t => { var g = group(); return g != null ? g.SetGyroAngle : float.NaN; },
                Set = (t, v) => { var g = group(); if (g != null && !float.IsNaN(v)) g.SetGyroAngle = Mathf.Repeat(v, 360f); },
                Get2 = t => { var g = group(); return g != null ? g.ReceivedGyroAngle : float.NaN; },
                Locked = t => group() == null,
                Style = PointerStyle.Flag,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                Period = fine ? 10f : 360f,
                WheelStep = fine ? -0.1f : -1f,
                Format = FormatBearing
            };
        }

        /// <summary>The boat's own switch of this kind on the bow tube group, for its click sound.</summary>
        static T GroupKnob<T>() where T : Component
        {
            try
            {
                var bow = BowGroup();
                var f = HarmonyLib.AccessTools.Field(typeof(T), "torpedoLauncherGroup");
                T any = null;
                foreach (var k in Resources.FindObjectsOfTypeAll<T>())
                {
                    if (k == null || !k.gameObject.scene.IsValid()) continue;
                    if (any == null) any = k;
                    if (f != null && ReferenceEquals(f.GetValue(k), bow)) return k;
                }
                return any;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ fast synchronization lever

        /// <summary>
        /// The lever in its own column, as one block centred in the dials' band: its name, then the
        /// lamps over the ends of the plate, the handle standing up between them from the plate.
        /// </summary>
        void BuildFastSyncLever(TMP_FontAsset font, float x, float colW, float top)
        {
            const float plateW = 84f, plateH = 24f, lampSize = 10f;
            const float handleLen = 40f;                                // from the pivot in the plate's middle, upward
            float headroom = handleLen - plateH * 0.5f + 2f;            // the handle's reach above the plate

            float cx = x + colW * 0.5f;
            float blockH = TitleH + TitleGap + headroom + plateH;
            float areaH = TitleH + TitleGap + ControlH;                 // the dials' name and dial band
            float y = top - (areaH - blockH) * 0.5f;                    // the block's top, centred

            var t = ControlTitle(gyroSetPanel, font, Strings.FastSync, x - GyroSetColGap * 0.5f, y, colW + GyroSetColGap);
            t.raycastTarget = true;
            AddTooltip(t.gameObject, () => Strings.TipFastSync);
            y -= TitleH + TitleGap;

            // lamps over each end: lit when the flag lags behind the needle that way (push toward the lit lamp)
            // as on the boat: red on the left (-), green on the right (+)
            float lampY = y - headroom + lampSize * 0.5f + 3f;          // just above the plate
            fastLampMinus = MakeLamp(gyroSetPanel, new Vector2(0f, 1f), new Vector2(cx - plateW * 0.5f + 11f, lampY), lampSize, LampRed);
            fastLampPlus = MakeLamp(gyroSetPanel, new Vector2(0f, 1f), new Vector2(cx + plateW * 0.5f - 11f, lampY), lampSize, LampGreen);
            y -= headroom;

            var plate = NewRect("FastSyncLever", gyroSetPanel);
            PlaceV(plate, cx - plateW * 0.5f, y, plateW, plateH);
            var pi = plate.gameObject.AddComponent<Image>();
            pi.color = DrumBlack;
            pi.raycastTarget = true;
            var edge = plate.gameObject.AddComponent<Outline>();
            edge.effectColor = FrameEdge;
            edge.effectDistance = new Vector2(1f, -1f);

            // - and + drawn as bars (the game's font has no minus sign: it showed as a box)
            SignBar(plate, 11f, plateH, false);
            SignBar(plate, plateW - 11f, plateH, true);

            // the handle: a long bar up from the middle of the plate, tilting to the side held
            fastHandle = NewRect("Handle", plate);
            fastHandle.anchorMin = fastHandle.anchorMax = new Vector2(0.5f, 0.5f);
            fastHandle.pivot = new Vector2(0.5f, 0f);
            fastHandle.sizeDelta = new Vector2(7f, handleLen);
            fastHandle.anchoredPosition = Vector2.zero;
            var hi = fastHandle.gameObject.AddComponent<Image>();
            hi.color = KnobHandle;
            hi.raycastTarget = false;
            var he = fastHandle.gameObject.AddComponent<Outline>();
            he.effectColor = KnobHandleEdge;
            he.effectDistance = new Vector2(0.8f, -0.8f);
            var tip = Disc("Tip", fastHandle, CircleSprite(), 12f, KnobHandle); // a ball on the end to hold it by
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tip.rectTransform.anchoredPosition = Vector2.zero;
            Disc("Pivot", plate, CircleSprite(), 9f, DialNeedle);

            plate.gameObject.AddComponent<FastSyncRelay>().Owner = this;
            AddTooltip(plate.gameObject, () => Strings.TipFastSync);
        }

        /// <summary>A minus (or plus) sign drawn with bars, centred at x on the plate.</summary>
        static void SignBar(RectTransform plate, float x, float plateH, bool plus)
        {
            const float len = 10f, thick = 2.4f;
            for (int i = 0; i < (plus ? 2 : 1); i++)
            {
                var b = NewRect(plus ? "Plus" : "Minus", plate);
                b.anchorMin = b.anchorMax = b.pivot = new Vector2(0f, 1f);
                b.pivot = new Vector2(0.5f, 0.5f);
                b.sizeDelta = i == 0 ? new Vector2(len, thick) : new Vector2(thick, len);
                b.anchoredPosition = new Vector2(x, -plateH * 0.5f);
                var img = b.gameObject.AddComponent<Image>();
                img.color = DigitLight;
                img.raycastTarget = false;
            }
        }

        /// <summary>Held: the bow group's fast synchronization one way; let go: back to the middle.</summary>
        sealed class FastSyncRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
        {
            public GyroCounterUI Owner;
            bool held;

            public void OnPointerDown(PointerEventData e)
            {
                if (e.button != PointerEventData.InputButton.Left) return;
                var g = BowGroup();
                if (g == null) return;
                if (Owner != null) Owner.HideTip();
                // which half was pressed: measured from the plate's middle (its pivot is its top-left corner)
                var rt = (RectTransform)transform;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, null, out local);
                g.FastSynchronizationMode = local.x < rt.rect.center.x ? -1 : 1;
                held = true;
            }

            public void OnPointerUp(PointerEventData e) { Release(); }
            public void OnBeginDrag(PointerEventData e) { } // a held lever doesn't move the window
            public void OnDrag(PointerEventData e) { }
            void OnDisable() { Release(); }

            void Release()
            {
                if (!held) return;
                held = false;
                var g = BowGroup();
                if (g != null) g.FastSynchronizationMode = 0;
            }
        }

        // ------------------------------------------------------------------ runtime

        void UpdateGyroSet()
        {
            if (gyroSetPanel == null) return;
            var g = BowGroup();
            if (g == null) return;
            var tdc = Tdc();
            // the wheel turns the gyro flag (180° a turn, geared down from the game's 360 to set it
            // finely; Shift 18°) on manual gyro, else the spread flag (20° a turn)
            bool gyro = ManualGyro(g);
            gyroSetWheel.Period = gyro ? WheelGyroPerTurn : GroupSpreadMax;
            gyroSetWheel.WheelStep = gyro ? -0.5f : -0.1f;
            if (tdc != null) foreach (var d in gyroSetDials) UpdateDialValue(tdc, d);
            UpdateKnobs(gyroSetKnobs, Boxes());

            int fast = g.FastSynchronizationMode;
            fastHandle.localEulerAngles = new Vector3(0f, 0f, fast > 0 ? -28f : fast < 0 ? 28f : 0f);
            float lag = Mathf.DeltaAngle(g.ReceivedGyroAngle, g.SetGyroAngle); // flag past the needle (+) or short of it (-)
            fastLampMinus.Set(lag > FastSyncLag, LampRed);
            fastLampPlus.Set(lag < -FastSyncLag, LampGreen);
        }

        void PositionGyroSet()
        {
            if (gyroSetPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.GyroSetX, Screen.height * GyroConfig.GyroSetY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                gyroSetPanel.anchoredPosition = local + new Vector2(GyroConfig.GyroSetOffsetX, GyroConfig.GyroSetOffsetY);
        }

        void SetGyroSetWindow(bool on)
        {
            GyroConfig.ShowGyroSet = on;
            GyroConfig.Save("ShowGyroSet", on);
            HideTip();
        }

        void ToggleGyroSetWindow() { SetGyroSetWindow(!GyroConfig.ShowGyroSet); }

        // ------------------------------------------------------------------ hand wheel artwork

        static Sprite wheelRimSprite;

        /// <summary>A hand wheel: rim, five spokes, hub, and a grip on the rim; returns the turning holder.</summary>
        static RectTransform MakeHandWheel(RectTransform face)
        {
            var holder = NewRect("HandWheel", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            float d = DialSize - 12f, r = d * 0.5f;
            Color steel = new Color(0.40f, 0.38f, 0.35f, 1f);

            for (int i = 0; i < 5; i++)
            {
                var sh = NewRect("SpokeHolder", holder);
                sh.anchorMin = sh.anchorMax = sh.pivot = new Vector2(0.5f, 0.5f);
                sh.sizeDelta = Vector2.zero;
                sh.localEulerAngles = new Vector3(0f, 0f, -72f * i);
                var s = NewRect("Spoke", sh);
                s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
                s.pivot = new Vector2(0.5f, 0f);
                s.sizeDelta = new Vector2(6f, r - 3f);
                var si = s.gameObject.AddComponent<Image>();
                si.color = steel;
                si.raycastTarget = false;
            }
            var rim = Disc("Rim", holder, WheelRimSprite(), d, steel);
            rim.raycastTarget = false;
            Disc("Hub", holder, SharpCircleSprite(), 26f, steel);
            Disc("HubCap", holder, SharpCircleSprite(), 12f, new Color(0.18f, 0.18f, 0.18f, 1f));
            // grip on the rim, at the top when the value is 0
            var grip = Disc("Grip", holder, SharpCircleSprite(), 15f, DialNeedle);
            grip.rectTransform.anchoredPosition = new Vector2(0f, r - 5f);
            var ge = grip.gameObject.AddComponent<Outline>();
            ge.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ge.effectDistance = new Vector2(0.8f, -0.8f);
            return holder;
        }

        static Sprite WheelRimSprite()
        {
            if (wheelRimSprite != null) return wheelRimSprite;
            const int n = 256;
            float c = n * 0.5f, outer = c - 1f, inner = outer - n * 0.075f;
            wheelRimSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float d2 = (x - c) * (x - c) + (y - c) * (y - c);
                return d2 <= outer * outer && d2 >= inner * inner;
            });
            return wheelRimSprite;
        }
    }
}
