using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Salvo spread window, stacked vertically, by default just right of the TDC dials:
    ///  - target length (Streubreite): the turning-ring dial, moved here from the TDC dials;
    ///  - spread angle (Streuwinkel): read-only, the angle the TDC computes for the spread
    ///    (TorpedoCalculator.SalvoSpreadAngle, as the TDC's own Streuwinkel dial shows it),
    ///    0° at the top clockwise to 20° at the bottom, "Grad" on the left.
    /// Shown with "Auto display spread settings" only while the Einzelschuß switch is on a
    /// spread position (straight course / while turning); otherwise the window check box
    /// shows or hides it.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float SpreadAngleMax = 20f;
        const float SpreadGap = 8f; // between the TDC dials and this window

        RectTransform spreadPanel;
        CanvasGroup spreadGroup;
        readonly List<DialSpec> spreadDials = new List<DialSpec>();
        FlipSwitch spreadMotorSwitch;

        /// <summary>Whether the window should be up: automatically by the Einzelschuß switch, or by its check box.</summary>
        static bool SpreadWanted()
        {
            return GyroConfig.ShowSpread || (GyroConfig.AutoSpread && SpreadSelected()); // checked = always
        }

        void BuildSpread(TMP_FontAsset font)
        {
            spreadDials.Clear();
            spreadPanel = NewRect("SalvoSpread", transform);
            spreadPanel.anchorMin = spreadPanel.anchorMax = spreadPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(spreadPanel);
            spreadPanel.gameObject.AddComponent<WheelGuard>();
            spreadGroup = spreadPanel.gameObject.AddComponent<CanvasGroup>();
            AddDrag(spreadPanel.gameObject, false).Spread = true;

            float inner = DialSize + 16f;
            float x0 = DialPad;
            float y = -DialPad;

            // target length (Streubreite): 100..200 m round the right half of a turning ring;
            // the value under the fixed index at the top is the one set (as on the TDC)
            y = AddDial(font, x0, y, inner, Strings.DialLength, () => Strings.TipDialLength, new DialSpec
            {
                Get = t => t.TargetLength,
                Set = (t, v) => t.TargetLength = v,
                Style = PointerStyle.Ring,
                Values = new[] { 100f, 200f }, Rots = new[] { 180f, 0f },
                WheelStep = -1f,     // wheel toward you = more
                Format = v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) + " m"
            }, (spec, f) => DrawLengthScale(spec, f), "", spreadPanel, spreadDials);

            // spread angle (Streuwinkel), 0° at the top clockwise to 20° at the bottom: the brass
            // needle is the angle the TDC computes, the red flag the angle set (SalvoSpreadAngleOuter,
            // what goes to the tubes). Turning the dial moves the flag; with the spread motor on, the
            // TDC drives the flag to the computed angle itself (as on the TDC).
            y = AddDial(font, x0, y, inner, Strings.DialSpreadAngle, () => Strings.TipDialSpreadAngle, new DialSpec
            {
                Get = t => t.SalvoSpreadAngleOuter,
                Set = (t, v) => t.SalvoSpreadAngleOuter = Mathf.Clamp(v, 0f, SpreadAngleMax),
                Get2 = t => t.SalvoSpreadAngle,
                Style = PointerStyle.Flag,
                Values = new[] { 0f, SpreadAngleMax }, Rots = new[] { 0f, 180f },
                WheelStep = -0.5f,   // wheel toward you = wider
                Format = v => v.ToString("0.0", CultureInfo.InvariantCulture) + "°"
            }, (spec, f) => DrawSpreadAngleScale(spec, f), "", spreadPanel, spreadDials);

            // spread motor (Streuwinkelmotor): on = the TDC drives the red flag to the computed angle
            y += 12f - 4f; // AddDial leaves 12 below the readout
            spreadMotorSwitch = AddFlipSwitch(spreadPanel, font, x0, ref y, inner, Strings.SpreadMotor, () => Strings.TipSpreadMotor,
                t => t.IsSalvoSpreadAngleMotorOn, (t, v) => t.IsSalvoSpreadAngleMotorOn = v,
                typeof(TorpedoCalculatorSalvoSpreadAngleMotorSwitch));

            spreadPanel.sizeDelta = new Vector2(x0 + inner + DialPad, -y + DialPad - 6f);
            AddResizeGrip(spreadPanel, ResizableWindow.Spread);
            AddAnchorIcon(spreadPanel, AnchoredWindow.Spread); // follow the TDC dials
            RaiseOverlays();
        }

        /// <summary>As on the TDC's Streuwinkel dial: a mark every degree down the right half, numbered every 5°, "Grad" on the left.</summary>
        void DrawSpreadAngleScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int a = 0; a <= (int)SpreadAngleMax; a++)
            {
                bool end = a == 0 || a == (int)SpreadAngleMax, major = a % 5 == 0;
                DrawTick(spec.Face, spec.ToRot(a), end ? 14f : major ? 10f : 7f, end || major ? 2.2f : 1.6f, DialScale);
                if (major) ScaleLabel(spec.Face, font, spec.ToRot(a), a.ToString(CultureInfo.InvariantCulture), DialScale, 11f);
            }
            var grad = NewText("Grad", spec.Face, font, Strings.SpreadUnit, 11f, DialScale);
            grad.alignment = TextAlignmentOptions.Center;
            var gr = grad.rectTransform;
            gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(44f, 14f);
            gr.anchoredPosition = new Vector2(-DialSize * 0.27f, 0f);
        }

        void UpdateSpread(TorpedoCalculator tdc, bool manned)
        {
            if (spreadPanel == null) return;
            spreadGroup.alpha = manned ? 1f : 0.45f;
            spreadGroup.interactable = manned;
            spreadGroup.blocksRaycasts = manned;
            foreach (var d in spreadDials) UpdateDialValue(tdc, d);
            UpdateFlipSwitch(spreadMotorSwitch, tdc);
        }

        /// <summary>Top-aligned just right of the TDC dials window (where it is, or would be), plus the dragged offset.</summary>
        void PositionSpread()
        {
            PlaceAnchorable(AnchoredWindow.Spread);
        }

        void ToggleSpreadWindow()
        {
            GyroConfig.ShowSpread = !GyroConfig.ShowSpread;
            GyroConfig.Save("ShowSpread", GyroConfig.ShowSpread);
            HideTip();
        }
    }
}
