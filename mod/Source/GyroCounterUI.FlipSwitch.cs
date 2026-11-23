using System;
using System.Collections.Generic;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// A small two-position flip switch for a TDC motor (AOB motor, spread motor): the name
    /// on the left, a black plate with a toggle lever (up = Ein, down = Aus) and the Ein /
    /// Aus marks beside it. Click to flip; it plays the click of the TDC's own switch.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float FlipSwitchW = 22f, FlipSwitchH = 32f;

        sealed class FlipSwitch
        {
            public RectTransform Lever;
            public float Shown = -1f;     // animated, 0 = Aus (down) .. 1 = Ein (up)
            public Func<TorpedoCalculator, bool> Get;
            public Action<TorpedoCalculator, bool> Set;
            public Type SoundSource;      // the TDC's own switch component, for its click
        }

        /// <summary>Add a flip switch row at <paramref name="y"/> (moved down past it).</summary>
        FlipSwitch AddFlipSwitch(RectTransform parent, TMP_FontAsset font, float x, ref float y, float inner, string title, Func<string> tip,
                                 Func<TorpedoCalculator, bool> get, Action<TorpedoCalculator, bool> set, Type soundSource)
        {
            var sw = new FlipSwitch { Get = get, Set = set, SoundSource = soundSource };
            float rowTop = y - 2f;

            // name, left of the switch
            var label = NewText("Title", parent, font, title, TitleFont - 1f, BoxInk);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 1f;
            label.alignment = TextAlignmentOptions.Right;
            float plateX = x + inner * 0.62f;
            PlaceV(label.rectTransform, x, rowTop - (FlipSwitchH - TitleH) * 0.5f, plateX - x - 8f, TitleH);

            var plate = NewRect("FlipSwitch", parent);
            PlaceV(plate, plateX, rowTop, FlipSwitchW, FlipSwitchH);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.color = DrumBlack;
            plateImg.raycastTarget = true;
            var plateEdge = plate.gameObject.AddComponent<Outline>();
            plateEdge.effectColor = FrameEdge;
            plateEdge.effectDistance = new Vector2(1f, -1f);
            Disc("Pivot", plate, SharpCircleSprite(), 8f, new Color(0.25f, 0.25f, 0.25f, 1f));

            sw.Lever = NewRect("Lever", plate);
            sw.Lever.anchorMin = sw.Lever.anchorMax = new Vector2(0.5f, 0.5f);
            sw.Lever.pivot = new Vector2(0.5f, 0f);
            sw.Lever.sizeDelta = new Vector2(7f, FlipSwitchH * 0.46f);
            var grip = sw.Lever.gameObject.AddComponent<Image>();
            grip.sprite = HandleSprite();
            grip.color = new Color(0.55f, 0.55f, 0.53f, 1f);
            grip.raycastTarget = false;

            // Ein at the top, Aus at the bottom, right of the plate
            var on = NewText("Ein", parent, font, Strings.SwitchOn, CtlFont - 1f, BoxInk);
            on.alignment = TextAlignmentOptions.Left;
            PlaceV(on.rectTransform, plateX + FlipSwitchW + 4f, rowTop, 40f, 13f);
            var off = NewText("Aus", parent, font, Strings.SwitchOff, CtlFont - 1f, BoxInk);
            off.alignment = TextAlignmentOptions.Left;
            PlaceV(off.rectTransform, plateX + FlipSwitchW + 4f, rowTop - FlipSwitchH + 13f, 40f, 13f);

            var relay = plate.gameObject.AddComponent<FlipSwitchRelay>();
            relay.Owner = this;
            relay.Switch = sw;
            AddTooltip(plate.gameObject, tip);
            AddTooltip(label.gameObject, tip);
            label.raycastTarget = true;

            y = rowTop - FlipSwitchH - 10f;
            return sw;
        }

        void FlipFlipSwitch(FlipSwitch sw)
        {
            var tdc = Tdc();
            if (tdc == null || sw == null) return;
            sw.Set(tdc, !sw.Get(tdc));
            PlayGameSound(TdcSwitchSound(tdc, sw.SoundSource));
        }

        static void UpdateFlipSwitch(FlipSwitch sw, TorpedoCalculator tdc)
        {
            if (sw == null || sw.Lever == null || tdc == null) return;
            float target = sw.Get(tdc) ? 1f : 0f;
            sw.Shown = sw.Shown < 0f ? target : Mathf.MoveTowards(sw.Shown, target, Time.unscaledDeltaTime * 8f);
            sw.Lever.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(180f, 0f, sw.Shown)); // up = Ein
        }

        static TorpedoCalculator switchSoundsFor;
        static readonly Dictionary<Type, string> switchSounds = new Dictionary<Type, string>();

        /// <summary>The click of the TDC's own switch of this kind (looked up once per TDC: the scene search is slow).</summary>
        static string TdcSwitchSound(TorpedoCalculator tdc, Type source)
        {
            if (!ReferenceEquals(tdc, switchSoundsFor)) { switchSoundsFor = tdc; switchSounds.Clear(); }
            string s;
            if (source != null && switchSounds.TryGetValue(source, out s)) return s;
            s = null;
            if (source != null)
                foreach (var o in Resources.FindObjectsOfTypeAll(source))
                {
                    var c = o as Component;
                    if (c != null && c.gameObject.scene.IsValid()) { s = KnobSound(c); break; }
                }
            if (string.IsNullOrEmpty(s)) s = AnyBoxKnobSound();
            if (source != null) switchSounds[source] = s;
            return s;
        }

        sealed class FlipSwitchRelay : MonoBehaviour, IPointerClickHandler
        {
            public GyroCounterUI Owner;
            public FlipSwitch Switch;

            public void OnPointerClick(PointerEventData e)
            {
                if (e.button != PointerEventData.InputButton.Left) return;
                Owner.HideTip();
                Owner.FlipFlipSwitch(Switch);
            }
        }
    }
}
