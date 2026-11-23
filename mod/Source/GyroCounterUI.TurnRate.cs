using System.Collections.Generic;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// Own angular speed window: the TDC's rate-of-turn knob (-3..3 °/s, Bb / Stb) on its own,
    /// right of the TDC dials with its bottom level with theirs (the salvo spread window sits
    /// above it, level with their top). Dragged, it keeps its offset from the TDC dials.
    /// Shown with "Show angular speed setting", and only on a TDC that has the knob.
    /// </summary>
    public partial class GyroCounterUI
    {
        RectTransform turnPanel;
        CanvasGroup turnGroup;
        readonly List<DialSpec> turnDials = new List<DialSpec>();

        static bool TurnRateWanted() { return GyroConfig.ShowAngularSpeed && TurnRateAvailable(); }

        void BuildTurnRate(TMP_FontAsset font)
        {
            turnDials.Clear();
            turnPanel = NewRect("OwnAngularSpeed", transform);
            turnPanel.anchorMin = turnPanel.anchorMax = turnPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(turnPanel);
            turnPanel.gameObject.AddComponent<WheelGuard>();
            turnGroup = turnPanel.gameObject.AddComponent<CanvasGroup>();
            AddDrag(turnPanel.gameObject, false).TurnRate = true;

            float inner = DialSize + 16f;
            float x0 = DialPad;
            float y = -DialPad;
            // as on the TDC's knob (TorpedoCalculatorOwnAngularSpeedDial); the TDC leads the gyro
            // angle by it while turning
            y = AddDial(font, x0, y, inner, Strings.DialTurnRate, () => Strings.TipDialTurnRate, new DialSpec
            {
                Get = t => t.OwnAngularSpeed,
                Set = (t, v) => t.OwnAngularSpeed = Mathf.Clamp(Mathf.Round(v * 10f) / 10f, -TurnRateMax, TurnRateMax),
                Style = PointerStyle.Knob,
                Values = new[] { -TurnRateMax, TurnRateMax }, Rots = new[] { -66f, 66f },
                WheelStep = -0.1f,   // wheel toward you = to starboard, like turning the knob clockwise
                Format = FormatTurnRate
            }, (spec, f) => DrawTurnRateScale(spec, f), "", turnPanel, turnDials);

            turnPanel.sizeDelta = new Vector2(x0 + inner + DialPad, -y + DialPad - 6f);
            AddResizeGrip(turnPanel, ResizableWindow.TurnRate);
            AddAnchorIcon(turnPanel, AnchoredWindow.TurnRate); // follow the TDC dials
            RaiseOverlays();
        }

        void UpdateTurnRate(TorpedoCalculator tdc, bool manned)
        {
            if (turnPanel == null) return;
            turnGroup.alpha = manned ? 1f : 0.45f;
            turnGroup.interactable = manned;
            turnGroup.blocksRaycasts = manned;
            foreach (var d in turnDials) UpdateDialValue(tdc, d);
        }

        void PositionTurnRate() { PlaceAnchorable(AnchoredWindow.TurnRate); }
    }
}
