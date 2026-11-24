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
    /// TDC target bearing window (Zielrichtung), as on the TDC: two dials, each with a brass
    /// needle (the bearing coming from the bearing source, e.g. the attack periscope:
    /// TorpedoCalculator.TargetBearingInner) and a red flag (the bearing set in the TDC:
    /// TorpedoCalculator.TargetBearing, what the gyro angle is worked out from).
    ///  - fine dial on top: one turn = 10°, marked 0..9 with a mark every 0.1°;
    ///  - coarse dial below: one turn = 360°, a mark every 10°, numbered every 20°.
    /// Turning either dial moves the red flag, like the TDC's own target bearing dial
    /// (TorpedoCalculatorTargetBearingDial). Follow-up is left as it is: while it is on, the
    /// TDC's bearing motor drives the flag back onto the needle, as in the game.
    /// Shown with its check box on the panel, or the small dial icon left of the gyro angle
    /// counter; the x at its top right closes it.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float BearingFinePeriod = 10f;   // degrees of bearing for one turn of the fine dial
        const float CloseIcon = 13f;

        RectTransform bearingPanel;
        CanvasGroup bearingGroup;
        readonly List<DialSpec> bearingDials = new List<DialSpec>();

        // the small dial icon left of the gyro angle counter
        RectTransform bearingIconBtn;
        static Sprite dialIconSprite, closeSprite;

        void BuildBearing(TMP_FontAsset font)
        {
            bearingDials.Clear();
            bearingPanel = NewRect("TdcTargetBearing", transform);
            bearingPanel.anchorMin = bearingPanel.anchorMax = bearingPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(bearingPanel);
            bearingPanel.gameObject.AddComponent<WheelGuard>();
            bearingGroup = bearingPanel.gameObject.AddComponent<CanvasGroup>();
            AddDrag(bearingPanel.gameObject, false).Bearing = true;

            float inner = DialSize + 16f;
            float x0 = DialPad;
            float y = -DialPad;

            // window title (the instrument's name plate), clear of the close button
            var title = ControlTitle(bearingPanel, font, Strings.BearingTitle, x0 + CloseIcon, y, inner - CloseIcon * 2f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 8f;
            title.fontSizeMax = TitleFont;
            AddTooltip(title.gameObject, () => Strings.TipBearingWindow);
            title.raycastTarget = true;
            y -= TitleH + 6f;

            // fine dial: one turn = 10°
            y = AddDial(font, x0, y, inner, Strings.DialBearingFine, () => Strings.TipDialBearingFine, new DialSpec
            {
                Get = t => t.TargetBearing,
                Set = SetTdcBearing,
                Base = TdcBearingBase,   // steps and clicks count from where it is heading
                Get2 = t => t.TargetBearingInner,
                Style = PointerStyle.Flag,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                Period = BearingFinePeriod,
                WheelStep = -0.1f,   // wheel toward you = more, like turning the dial clockwise
                Format = FormatBearing
            }, (spec, f) => DrawBearingFineScale(spec, f), "", bearingPanel, bearingDials);
            // one readout is enough (under the coarse dial): drop this one and its space
            var fine = bearingDials[bearingDials.Count - 1];
            fine.Readout.transform.parent.gameObject.SetActive(false);
            y += ReadoutH + ReadoutGap;

            // coarse dial: one turn = 360°
            y = AddDial(font, x0, y, inner, Strings.DialBearingCoarse, () => Strings.TipDialBearingCoarse, new DialSpec
            {
                Get = t => t.TargetBearing,
                Set = SetTdcBearing,
                Base = TdcBearingBase,   // steps and clicks count from where it is heading
                Get2 = t => t.TargetBearingInner,
                Style = PointerStyle.Flag,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                WheelStep = -1f,
                Format = FormatBearing
            }, (spec, f) => DrawBearingCoarseScale(spec, f), "", bearingPanel, bearingDials);

            bearingPanel.sizeDelta = new Vector2(x0 + inner + DialPad, -y + DialPad - 6f);
            AddCloseButton(bearingPanel, () => SetBearingWindow(false));
            AddResizeGrip(bearingPanel, ResizableWindow.Bearing);
            RaiseOverlays();
        }

        /// <summary>
        /// The dials set where the TDC's bearing should go; it is driven there smoothly (see
        /// <see cref="SlewTdcBearing"/>), as the TDC's gearing would turn it. Follow-up (if on)
        /// pulls it back to the needle afterwards, as in the game.
        /// </summary>
        static void SetTdcBearing(TorpedoCalculator tdc, float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return;
            bearingTarget = Mathf.Repeat(v, 360f);
            bearingTargetTdc = tdc;
        }

        /// <summary>Where the TDC's bearing is being driven to, or where it is if it isn't moving.</summary>
        static float TdcBearingBase(TorpedoCalculator tdc)
        {
            return !float.IsNaN(bearingTarget) && ReferenceEquals(tdc, bearingTargetTdc) ? bearingTarget : tdc.TargetBearing;
        }

        // degrees a second of game time. The TDC's AOB tracking aims at 32x its remaining error (up to
        // 360°/s) but only reaches that speed with about a third of a second's lag: at 80°/s it trails
        // by about 2.5° (4° at worst, just after a click) and settles within half a second of
        // stopping; at 360°/s it fell 15-20° behind. Also the speed the TDC sets the AOB from its dial.
        const float BearingSlewRate = 80f;
        static float bearingTarget = float.NaN;
        static TorpedoCalculator bearingTargetTdc;

        /// <summary>
        /// Each frame: move the TDC's target bearing toward the dialled one. A jump (a click on the
        /// scale, a wheel step) all at once threw the gyro angle off: with AOB tracking on, the TDC
        /// takes each step's bearing change as the target moving and works it off with its AOB motor,
        /// which can't keep up with a big jump, and reads a jump past 180° the short way round (the
        /// wrong way). Driven smoothly, as when the scope turns, the AOB and gyro angle follow.
        /// Paused game: it waits; time compression: it keeps pace with the TDC.
        /// </summary>
        static void SlewTdcBearing(TorpedoCalculator tdc)
        {
            if (float.IsNaN(bearingTarget)) return;
            if (tdc == null || !ReferenceEquals(tdc, bearingTargetTdc)) { bearingTarget = float.NaN; return; } // another boat / TDC
            float now = tdc.TargetBearing;
            float diff = Mathf.DeltaAngle(now, bearingTarget);
            float step = BearingSlewRate * Time.deltaTime;
            if (Mathf.Abs(diff) <= step)
            {
                tdc.TargetBearing = bearingTarget;
                bearingTarget = float.NaN;
                return;
            }
            tdc.TargetBearing = Mathf.Repeat(now + Mathf.Sign(diff) * step, 360f);
        }

        static string FormatBearing(float v)
        {
            float b = Mathf.Repeat(v, 360f);
            if (b >= 359.95f) b = 0f; // 359.96 would print as 360.0
            return b.ToString("000.0", CultureInfo.InvariantCulture) + "°";
        }

        /// <summary>Fine dial, as on the TDC: 0..9 round the face, a mark every 0.1°, longer every 0.5° and every degree.</summary>
        void DrawBearingFineScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int t = 0; t < 100; t++)
            {
                float rot = t * 3.6f;
                bool whole = t % 10 == 0, half = t % 5 == 0;
                DrawTick(spec.Face, rot, whole ? 12f : half ? 9f : 5f, whole ? 2.2f : half ? 1.6f : 1f, DialScale);
                if (whole) ScaleLabel(spec.Face, font, rot, (t / 10).ToString(CultureInfo.InvariantCulture), DialScale, 11f, false, DialSize * 0.5f - 24f);
            }
        }

        /// <summary>Coarse dial, as on the TDC: a mark every 10°, numbered every 20° (0 at the top, clockwise).</summary>
        void DrawBearingCoarseScale(DialSpec spec, TMP_FontAsset font)
        {
            // same spacing as the AOB dial's 20° numbers, which fit three digits round this size of dial
            for (int deg = 0; deg < 360; deg += 5)
            {
                bool ten = deg % 10 == 0;
                DrawTick(spec.Face, deg, ten ? 9f : 4f, ten ? 2.2f : 1f, DialScale);
                if (deg % 20 == 0) ScaleLabel(spec.Face, font, deg, deg.ToString(CultureInfo.InvariantCulture), DialScale, 9f, false, DialSize * 0.5f - 19f);
            }
        }

        void UpdateBearing(TorpedoCalculator tdc, bool manned)
        {
            if (bearingPanel == null) return;
            bearingGroup.alpha = manned ? 1f : 0.45f;
            bearingGroup.interactable = manned;
            bearingGroup.blocksRaycasts = manned;
            foreach (var d in bearingDials) UpdateDialValue(tdc, d);
        }

        void PositionBearing()
        {
            if (bearingPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.BearingX, Screen.height * GyroConfig.BearingY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                bearingPanel.anchoredPosition = local + new Vector2(GyroConfig.BearingOffsetX, GyroConfig.BearingOffsetY);
        }

        void SetBearingWindow(bool on)
        {
            GyroConfig.ShowBearing = on;
            GyroConfig.Save("ShowBearing", on);
            HideTip();
        }

        // ------------------------------------------------------------------ close button

        /// <summary>A small x at the top right of a window.</summary>
        void AddCloseButton(RectTransform panel, System.Action onClose)
        {
            var r = NewRect("Close", panel);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.sizeDelta = new Vector2(CloseIcon, CloseIcon);
            r.anchoredPosition = new Vector2(-5f, -5f);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = CloseSprite();
            img.color = BoxInk;
            img.raycastTarget = true;
            var btn = r.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() =>
            {
                onClose();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            AddTooltip(r.gameObject, () => Strings.TipClose);
        }

        static Sprite CloseSprite()
        {
            if (closeSprite != null) return closeSprite;
            const int n = 40;
            closeSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                if (x < 4f || x > n - 4f || y < 4f || y > n - 4f) return false;
                return Mathf.Abs(x - y) <= 3.2f || Mathf.Abs(x - (n - y)) <= 3.2f;
            });
            return closeSprite;
        }

        // ------------------------------------------------------------------ counter icon

        /// <summary>A small dial icon just left of the gyro angle counter: opens / closes the target bearing window.</summary>
        void BuildBearingIcon()
        {
            if (counterPanel == null) return;
            bearingIconBtn = LeftOfRowIcon(counterPanel, "TargetBearingIcon");
            MakeIconToggle(bearingIconBtn, () => GyroConfig.ShowBearing, () => SetBearingWindow(!GyroConfig.ShowBearing), () => Strings.TipBearingIcon);
        }

        /// <summary>A square just left of a counter row, as tall as the row.</summary>
        static RectTransform LeftOfRowIcon(RectTransform row, string name)
        {
            float h = row.sizeDelta.y;
            var r = NewRect(name, row);
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.sizeDelta = new Vector2(h, h);
            r.anchoredPosition = new Vector2(-4f, 0f);
            return r;
        }

        // ------------------------------------------------------------------ dial icon toggles (shared)

        /// <summary>A small dial icon button that opens / closes a window; lit while the window is open.</summary>
        sealed class IconToggle
        {
            public Image Bg, Img;
            public bool Hover;
            public Func<bool> IsOpen;
        }

        readonly List<IconToggle> iconToggles = new List<IconToggle>();

        /// <summary>Turn an already placed rect into a dial icon toggle.</summary>
        IconToggle MakeIconToggle(RectTransform r, Func<bool> isOpen, Action toggle, Func<string> tip)
        {
            var t = new IconToggle { IsOpen = isOpen };
            t.Bg = r.gameObject.AddComponent<Image>();
            t.Bg.color = BtnIdle;
            t.Bg.raycastTarget = true;
            var edge = r.gameObject.AddComponent<Outline>();
            edge.effectColor = FrameEdge;
            edge.effectDistance = new Vector2(1f, -1f);

            var ir = NewRect("Icon", r);
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(3f, 3f); ir.offsetMax = new Vector2(-3f, -3f);
            t.Img = ir.gameObject.AddComponent<Image>();
            t.Img.sprite = DialIconSprite();
            t.Img.color = DigitLight;
            t.Img.preserveAspect = true;
            t.Img.raycastTarget = false;

            var button = r.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.targetGraphic = t.Bg;
            button.onClick.AddListener(() =>
            {
                try { toggle(); }
                catch (Exception e) { Debug.LogException(e); }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            var relay = AddTooltip(r.gameObject, tip);
            relay.OnHover = v => t.Hover = v;
            iconToggles.Add(t);
            return t;
        }

        /// <summary>Lit while its window is open, as the raise / lower buttons are while running.</summary>
        void PaintIconToggles()
        {
            for (int i = iconToggles.Count - 1; i >= 0; i--)
            {
                var t = iconToggles[i];
                if (t.Bg == null) { iconToggles.RemoveAt(i); continue; } // its window was rebuilt
                if (!t.Bg.isActiveAndEnabled) continue;
                bool open = t.IsOpen();
                t.Bg.color = open ? BtnSelected : (t.Hover ? BtnHover : BtnIdle);
                t.Img.color = open ? DigitDark : DigitLight;
            }
        }

        /// <summary>A dial face: a ring with marks round it and a needle.</summary>
        static Sprite DialIconSprite()
        {
            if (dialIconSprite != null) return dialIconSprite;
            const int n = 48;
            float c = n * 0.5f;
            dialIconSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= 22f && d >= 19.5f) return true;                       // rim
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;                   // clockwise from the top
                float off = Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Repeat(a, 30f) < 15f ? Mathf.Repeat(a, 30f) : Mathf.Repeat(a, 30f) - 30f));
                if (d <= 19.5f && d >= 15f && off * Mathf.Deg2Rad * d <= 1.1f) return true; // marks every 30°
                // needle toward about 40° (from the top, clockwise), with a hub
                if (d <= 3.6f) return true;
                const float na = 40f * Mathf.Deg2Rad;
                float ux = Mathf.Sin(na), uy = Mathf.Cos(na);
                float along = dx * ux + dy * uy, across = Mathf.Abs(dx * uy - dy * ux);
                return along >= 0f && along <= 15f && across <= 1.6f * (1f - along / 18f) + 0.4f;
            });
            return dialIconSprite;
        }
    }
}
