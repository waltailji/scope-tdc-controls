using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Stern torpedo room gyro setting panel: simpler than the bow's, as on the boat
    /// ("Torp. Schussw.-Empf."): the gyro angle coarse (360° a turn) and fine (10° a turn) dials,
    /// brass needle = received from the TDC, red flag = set on the tubes, and a small hand crank
    /// that turns the gyro angle flag (180° a turn, Shift 18°, like the bow's wheel on manual gyro).
    /// The crank spins freely (it works through a clutch) and stays where it was turned to. The
    /// game's own setting logic is left alone: if the stern group is on automatic, it drives the
    /// flag back onto the needle. Shown with its check box, or the dial icon beside the stern
    /// receiver counter or at the end of the Aft row in the torpedo tube window.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float CrankGyroPerTurn = 180f;

        RectTransform sternSetPanel;
        readonly List<DialSpec> sternSetDials = new List<DialSpec>();
        DialSpec sternCrank;
        static float sternCrankAngle;   // the crank's own angle, kept when the window is rebuilt

        /// <summary>The aft tube group (the one the stern gyro angle receiver feeds).</summary>
        static TorpedoLauncherGroup SternGroup()
        {
            TorpedoLauncherGroup bow, stern;
            ReceiverGroups(out bow, out stern);
            return stern;
        }

        void BuildSternGyroSet(TMP_FontAsset font)
        {
            sternSetDials.Clear();
            sternSetPanel = NewRect("SternGyroSetting", transform);
            sternSetPanel.anchorMin = sternSetPanel.anchorMax = sternSetPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(sternSetPanel);
            sternSetPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(sternSetPanel.gameObject, false).SternGyroSet = true;

            // as on the boat: coarse | fine | hand crank
            float inner = DialSize + 16f;
            float col = inner + GyroSetColGap;
            float x0 = DialPad;
            float width = DialPad * 2f + inner * 3f + GyroSetColGap * 2f;
            float y = -DialPad;

            var title = ControlTitle(sternSetPanel, font, Strings.SternSetTitle, x0 + CloseIcon, y, width - DialPad * 2f - CloseIcon * 2f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 8f;
            title.fontSizeMax = TitleFont;
            title.raycastTarget = true;
            AddTooltip(title.gameObject, () => Strings.TipSternSetWindow);
            y -= TitleH + 6f;

            float row = y;
            float yCoarse = AddDial(font, x0, row, inner, Strings.DialGyroCoarse, () => Strings.TipDialGyroCoarse, GyroDialSpec(SternGroup, false),
                                    (spec, f) => DrawBearingCoarseScale(spec, f), "", sternSetPanel, sternSetDials);
            float yFine = AddDial(font, x0 + col, row, inner, Strings.DialGyroFine, () => Strings.TipDialGyroFine, GyroDialSpec(SternGroup, true),
                                  (spec, f) => DrawBearingFineScale(spec, f), "", sternSetPanel, sternSetDials);
            sternSetDials[sternSetDials.Count - 1].Readout.transform.parent.gameObject.SetActive(false); // one readout, under the coarse dial

            sternCrank = new DialSpec
            {
                Get = t => { var g = SternGroup(); return g != null ? g.SetGyroAngle : float.NaN; },
                Set = (t, v) => { var g = SternGroup(); if (g != null && !float.IsNaN(v)) g.SetGyroAngle = Mathf.Repeat(v, 360f); },
                Locked = t => SternGroup() == null,
                Style = PointerStyle.Crank,
                NoClick = true,
                FreeSpin = true,          // turns freely through its clutch: stays wherever it was turned to
                FreeAngle = sternCrankAngle,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                Period = CrankGyroPerTurn,
                WheelStep = -0.5f,
                Format = FormatBearing
            };
            float yCrank = AddDial(font, x0 + col * 2f, row, inner, Strings.DialCrank, () => Strings.TipDialCrank, sternCrank,
                                   (spec, f) => { }, "", sternSetPanel, sternSetDials);
            sternCrank.Readout.transform.parent.gameObject.SetActive(false); // the coarse dial's readout shows the angle
            y = Mathf.Min(yCoarse, Mathf.Min(yFine, yCrank));

            sternSetPanel.sizeDelta = new Vector2(width, -y + DialPad - 6f);
            AddCloseButton(sternSetPanel, () => SetSternSetWindow(false));
            AddResizeGrip(sternSetPanel, ResizableWindow.SternGyroSet);
            RaiseOverlays();
        }

        void UpdateSternGyroSet()
        {
            if (sternSetPanel == null || SternGroup() == null) return;
            var tdc = Tdc();
            if (tdc != null) foreach (var d in sternSetDials) UpdateDialValue(tdc, d);
        }

        void PositionSternGyroSet()
        {
            if (sternSetPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.SternSetX, Screen.height * GyroConfig.SternSetY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                sternSetPanel.anchoredPosition = local + new Vector2(GyroConfig.SternSetOffsetX, GyroConfig.SternSetOffsetY);
        }

        void SetSternSetWindow(bool on)
        {
            GyroConfig.ShowSternGyroSet = on;
            GyroConfig.Save("ShowSternGyroSet", on);
            HideTip();
        }

        void ToggleSternSetWindow() { SetSternSetWindow(!GyroConfig.ShowSternGyroSet); }

        // ------------------------------------------------------------------ crank artwork

        /// <summary>A hand crank: a light arm from the hub with a dark knob at its end; returns the turning holder.</summary>
        static RectTransform MakeHandCrank(RectTransform face)
        {
            var holder = NewRect("HandCrank", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            Color arm = new Color(0.68f, 0.67f, 0.64f, 1f);   // light cast metal, as on the boat
            float len = DialSize * 0.5f - 22f;

            var a = NewRect("Arm", holder);
            a.anchorMin = a.anchorMax = new Vector2(0.5f, 0.5f);
            a.pivot = new Vector2(0.5f, 0f);
            a.sizeDelta = new Vector2(20f, len);
            var ai = a.gameObject.AddComponent<Image>();
            ai.color = arm;
            ai.raycastTarget = false;
            var ae = a.gameObject.AddComponent<Outline>();
            ae.effectColor = new Color(0f, 0f, 0f, 0.7f);
            ae.effectDistance = new Vector2(0.8f, -0.8f);

            // rounded ends: discs at the hub and the tip
            Disc("HubEnd", holder, SharpCircleSprite(), 30f, arm);
            var tipEnd = Disc("TipEnd", holder, SharpCircleSprite(), 20f, arm);
            tipEnd.rectTransform.anchoredPosition = new Vector2(0f, len);
            Disc("HubCap", holder, SharpCircleSprite(), 12f, new Color(0.18f, 0.18f, 0.18f, 1f));
            // the knob at the end of the arm
            var knob = Disc("Knob", holder, SharpCircleSprite(), 17f, new Color(0.10f, 0.10f, 0.10f, 1f));
            knob.rectTransform.anchoredPosition = new Vector2(0f, len);
            var ke = knob.gameObject.AddComponent<Outline>();
            ke.effectColor = new Color(1f, 1f, 1f, 0.15f);
            ke.effectDistance = new Vector2(0.8f, -0.8f);
            return holder;
        }
    }
}
