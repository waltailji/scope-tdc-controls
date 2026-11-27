using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// TDC dial window on the left of the scope: AOB, target speed and target
    /// distance, shown/hidden with the sidebar button together with the
    /// control panel.
    ///
    /// The dials behave like the TDC's own ones:
    ///  - Speed and distance write straight into the TDC (TargetSpeed,
    ///    DistanceToTarget), like TorpedoCalculatorTargetSpeedDial /
    ///    TorpedoCalculatorDistanceToTargetDial. Their faces follow the real
    ///    dials: both read clockwise from just right of the top, speed 0-55 kn
    ///    linear, distance 3-100 hm on the real dial's stretched scale (taken
    ///    from the boat's own dial when it can be found).
    ///  - AOB can't be written directly: the TDC recomputes it from the impact
    ///    angle every step. Like TorpedoCalculatorAngleOnBowDial, turning the
    ///    dial only moves a ghost pointer; on release the TDC gets
    ///    DesiredImpactAngle = ComputeImpactAngleForDesiredAOB(target), and its
    ///    AOB motor turns there (the game switches "AOB tracking" off for that
    ///    and back on afterwards). Locked while the locking mechanism is
    ///    triggered, as on the TDC.
    /// Extras: Shift turns 10x finer, the mouse wheel steps.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float DialSize = 130f;
        const float DialPad = 8f;
        const float WheelAnnounceDelay = 0.6f;  // mouse wheel: call the value out once the wheel stops

        static readonly Color DialFace = new Color(0.07f, 0.07f, 0.07f, 1f);
        static readonly Color DialScale = new Color(0.93f, 0.90f, 0.82f, 1f);
        static readonly Color DialPort = new Color(0.95f, 0.35f, 0.30f, 1f);
        static readonly Color DialStarboard = new Color(0.40f, 0.90f, 0.45f, 1f);
        static readonly Color DialNeedle = new Color(0.86f, 0.62f, 0.32f, 1f);   // brass, like the TDC's pointers

        // the real distance dial is logarithmic: measured off the TDC's 3D face, every mark sits at
        // 12 deg + 331 deg * ln(hm / 3) / ln(100 / 3) clockwise from the top (3 hm at 12, 10 at 125, 100 at 343)
        const float DistRot3 = 12f, DistRot100 = 343f;

        static float DistanceRotOf(float hm)
        {
            return DistRot3 + (DistRot100 - DistRot3) * Mathf.Log(hm / 3f) / Mathf.Log(100f / 3f);
        }

        RectTransform dialsPanel;
        CanvasGroup dialsGroup;
        float dialsFullH;                // window height with every dial shown
        readonly List<DialSpec> dials = new List<DialSpec>();
        FlipSwitch aobMotorSwitch;

        enum PointerStyle
        {
            Needle,   // thin brass needle (speed)
            Arrow,    // needle with an arrow tip (AOB)
            Knob,     // turning knob grip with a painted arrow (distance)
            Ring,     // the scale ring turns under a fixed index at the top (target length)
            Flag,     // a red flag set by hand (or by a motor), and a brass needle for a second value (spread angle)
            Wheel,    // a hand wheel with spokes and a grip, turning with the value (tube group angle setting wheel)
            Crank     // a hand crank: an arm with a knob at its end (stern gyro setting)
        }

        /// <summary>One TDC dial: how it reads/writes the TDC and how it is drawn.</summary>
        sealed class DialSpec
        {
            public Func<TorpedoCalculator, float> Get;
            public Action<TorpedoCalculator, float> Set;      // direct dials
            public Action<TorpedoCalculator, float> Announce; // direct dials: voice callout after release
            public float AnnounceAt = float.NaN;              // wheel: announce once the wheel stops
            public float DragStartValue = float.NaN;
            public Func<TorpedoCalculator, bool> Locked;
            public float[] Values, Rots;                      // value -> pointer angle (clockwise from the top), increasing
            public bool Wrap;                                 // AOB: ±180 is the same place
            public float Period = 360f;                       // Wrap: the value change for one full turn (target bearing fine dial: 10°)
            public bool NoClick;                              // no scale to click on (hand wheel): only turning sets it
            public Func<TorpedoCalculator, float> Base;       // the value to turn / step from, when that isn't Get (TDC bearing: where it is being driven to)
            public bool FreeSpin;                             // hand wheel: shown where the hand turned it, not at the value (it works through clutches)
            public float FreeAngle;                           // FreeSpin: the wheel's own angle, clockwise
            public float WheelStep;
            public Func<float, float, bool, float> WheelStepAt; // (value, direction +-1, Shift) -> wheel step size, where it depends on the value
            public float RoundTo;                            // drag and click set values rounded to this (target distance: 10 m)
            public Func<float, string> Format;
            public Action<DialSpec, float> Extra;             // per-frame extras (AOB: the rolling ones digits)
            public Func<bool> Visible;                        // null = always shown
            public RectTransform Group;                       // title, dial and readout
            public float Height;                              // its share of the window height
            public PointerStyle Style;                        // how the value is shown
            public float NeedleWidth = 4f;                    // Needle / Arrow: the hand's width
            public RectTransform Needle;                      // Ring style: the turning scale ring
            public RectTransform Needle2;                     // Flag style: the brass needle
            public Func<TorpedoCalculator, float> Get2;        // Flag style: the value the brass needle shows
            public TextMeshProUGUI Readout;
            public RectTransform Face;

            public bool Dragging;
            public float DragValue = float.NaN;               // the value being dragged (the TDC can read back differently)
            public float LimitMin = float.NegativeInfinity;   // the scale may show more than can be set (torpedo depth: 1..14 m on 0..15)
            public float LimitMax = float.PositiveInfinity;

            public float Min { get { return Mathf.Min(Values[0], Values[Values.Length - 1]); } }
            public float Max { get { return Mathf.Max(Values[0], Values[Values.Length - 1]); } }

            public float ToRot(float v)
            {
                if (Wrap) return Mathf.DeltaAngle(0f, v * 360f / Period);
                return Map(v, Values, Rots);
            }

            public float ToValue(float rot)
            {
                if (Wrap) return Mathf.DeltaAngle(0f, rot) * Period / 360f;
                return Map(rot, Rots, Values);
            }

            static float Map(float x, float[] from, float[] to)
            {
                if (from.Length > 1 && from[0] > from[from.Length - 1])
                {
                    // descending table (e.g. a scale that reads anticlockwise): walk it backwards
                    var f = (float[])from.Clone(); Array.Reverse(f);
                    var t = (float[])to.Clone(); Array.Reverse(t);
                    from = f; to = t;
                }
                if (x <= from[0]) return to[0];
                for (int i = 1; i < from.Length; i++)
                    if (x <= from[i]) return Mathf.Lerp(to[i - 1], to[i], (x - from[i - 1]) / (from[i] - from[i - 1]));
                return to[to.Length - 1];
            }

            /// <summary>Turn by a pointer angle; the value follows the scale under the pointer.</summary>
            public float Turn(float v, float degrees)
            {
                // a turning scale under a fixed index reads the other way round
                if (Wrap) return Mathf.DeltaAngle(0f, v + (Style == PointerStyle.Ring ? -degrees : degrees) * Period / 360f); // a fine dial turns the value on by a fraction of a turn
                float r = ToRot(v) + (Style == PointerStyle.Ring ? -degrees : degrees);
                if (!Wrap) r = Mathf.Clamp(r, Mathf.Min(Rots[0], Rots[Rots.Length - 1]), Mathf.Max(Rots[0], Rots[Rots.Length - 1]));
                return Wrap ? ToValue(r) : Mathf.Clamp(ToValue(r), LimitMin, LimitMax);
            }

            /// <summary>A dragged or clicked value rounded to RoundTo (0 = as it is), kept in the limits.</summary>
            public float Round(float v)
            {
                if (RoundTo <= 0f || Wrap || float.IsNaN(v)) return v;
                return Mathf.Clamp(Mathf.Round(v / RoundTo) * RoundTo, LimitMin, LimitMax);
            }

            public float Step(float v, float delta)
            {
                return Wrap ? Mathf.DeltaAngle(0f, v + delta) : Mathf.Clamp(Mathf.Clamp(v + delta, Min, Max), LimitMin, LimitMax);
            }
        }

        // ------------------------------------------------------------------ build

        void BuildDials(TMP_FontAsset font)
        {
            dials.Clear();
            dialsPanel = NewRect("TdcDials", transform);
            dialsPanel.anchorMin = dialsPanel.anchorMax = dialsPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(dialsPanel);
            dialsPanel.gameObject.AddComponent<WheelGuard>();
            dialsGroup = dialsPanel.gameObject.AddComponent<CanvasGroup>();
            AddDrag(dialsPanel.gameObject, false).Dials = true;

            float inner = DialSize + 16f;
            float x0 = DialPad;
            float y = -DialPad;

            // AOB: full circle, 0 at the top, starboard clockwise (green), port anticlockwise (red)
            y = AddDial(font, x0, y, inner, Strings.DialAob, () => Strings.TipDialAob, new DialSpec
            {
                Get = t => t.AngleOnBow,
                Set = ApplyAobNow,                       // follows the dial at once
                Announce = VoiceCallouts.AnnounceAob,    // and is called out after you let go
                Locked = t => t.IsLockingMechanismTriggered,
                Style = PointerStyle.Arrow,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                WheelStep = 1f,
                Format = FormatAob
            }, (spec, f) => DrawAobScale(spec, f), "");
            AddTypedEntry(dials[dials.Count - 1], font, ParseTypedAob, () => Strings.RangeAob, () => Strings.TypeAob);

            // AOB motor (Lagenwinkelmotor): on = the TDC keeps the AOB in step with own course and
            // bearing changes (needed for AOB tracking). Only on a TDC that has the switch (the
            // Type II's has no AOB motor); decided when the window is built, on entering the scope.
            if (AobMotorAvailable())
            {
                y += 12f - 4f; // AddDial leaves 12 below the readout
                aobMotorSwitch = AddFlipSwitch(dialsPanel, font, x0, ref y, inner, Strings.AobMotor, () => Strings.TipAobMotor,
                    t => t.IsAOBMotorOn, (t, v) => t.IsAOBMotorOn = v, typeof(TorpedoCalculatorAOBMotorSwitch));
            }

            // target speed: 0..55 kn, clockwise from just right of the top round to just left of it
            y = AddDial(font, x0, y, inner, Strings.DialSpeed, () => Strings.TipDialSpeed, new DialSpec
            {
                Get = t => t.TargetSpeed,
                Set = (t, v) => t.TargetSpeed = v,
                Announce = VoiceCallouts.AnnounceSpeed,
                Style = PointerStyle.Arrow, NeedleWidth = 3f, // a slimmer needle with an arrowhead, like the AOB's
                Values = new[] { 0f, 55f }, Rots = new[] { 10f, 350f },
                WheelStep = -0.5f,   // wheel toward you = more, like turning the dial clockwise
                Format = v => v.ToString("0.0", CultureInfo.InvariantCulture) + " " + Strings.UnitKnots
            }, (spec, f) => DrawSpeedScale(spec, f), Strings.UnitKnots);
            AddTypedEntry(dials[dials.Count - 1], font, TypedNumber(0f, 55f, 0.1f, "knots", "kts", "kn", "kt"), () => Strings.RangeSpeed, () => Strings.TypeSpeed);

            // target distance: 3..100 hm on the real dial's stretched scale
            float[] hm, rot;
            DistanceScale(out hm, out rot);
            var meters = new float[hm.Length];
            for (int i = 0; i < hm.Length; i++) meters[i] = hm[i] * 100f;
            y = AddDial(font, x0, y, inner, Strings.DialDistance, () => Strings.TipDialDistance, new DialSpec
            {
                Get = t => t.DistanceToTarget,
                Set = (t, v) => t.DistanceToTarget = v,
                Announce = VoiceCallouts.AnnounceDistance,
                Style = PointerStyle.Knob,
                Values = meters, Rots = rot,
                WheelStep = -100f,   // wheel toward you = more
                // 50 m a notch from 3 to 10 hm, where the scale is open, 100 m above; Shift: 10 m.
                // Each notch lands on the next round value that way (910 up -> 950, down -> 900).
                WheelStepAt = (v, dir, fine) =>
                {
                    float s = fine ? 10f : (dir > 0f ? v < 1000f - 0.5f : v <= 1000f + 0.5f) ? 50f : 100f;
                    float target = dir > 0f ? (Mathf.Floor(v / s + 0.001f) + 1f) * s
                                            : (Mathf.Ceil(v / s - 0.001f) - 1f) * s;
                    return Mathf.Abs(target - v);
                },
                RoundTo = 10f,      // drag and click: to the nearest 10 m
                Format = v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) + " m"
            }, (spec, f) => DrawDistanceScale(spec, f), ""); // "hm" is printed on the rim
            AddTypedEntry(dials[dials.Count - 1], font, ParseTypedDistance, () => Strings.RangeDistance, () => Strings.TypeDistance);
            // target length: in the salvo spread window (GyroCounterUI.Spread.cs)

            // own angular speed: its own window beside the TDC dials (GyroCounterUI.TurnRate.cs)

            // time to impact: three drums at the bottom (GyroCounterUI.ImpactTime.cs)
            y = BuildImpactTime(font, x0, y, inner);

            dialsPanel.sizeDelta = new Vector2(x0 + inner + DialPad, -y + DialPad - 6f);
            dialsFullH = dialsPanel.sizeDelta.y;
            AddResizeGrip(dialsPanel, ResizableWindow.Dials);
        }

        /// <summary>
        /// The distance scale as value / angle points for the dial: the log curve sampled closely
        /// (every 0.1 hm up to 10, then every 1 hm), so the straight pieces between them follow it.
        /// (The game's own dial table bunches 9 and 10 together, so it isn't used.)
        /// </summary>
        static void DistanceScale(out float[] hm, out float[] rot)
        {
            var h = new List<float>();
            for (int i = 30; i < 100; i++) h.Add(i / 10f);
            for (int i = 10; i <= 100; i++) h.Add(i);
            hm = h.ToArray();
            rot = new float[hm.Length];
            for (int i = 0; i < hm.Length; i++) rot[i] = DistanceRotOf(hm[i]);
        }

        // ------------------------------------------------------------------ shared control layout
        // Every dial and knob window uses the TDC dials' layout: the name above the control,
        // the current value below it in a black box, at the same heights in every window.
        const float TitleFont = 12f;          // control names
        const float TitleH = 16f;
        const float TitleGap = 3f;            // name to the control's black rim
        const float ControlH = DialSize + 6f; // a control's band: the dial with its rim (knobs are centred in it)
        const float ReadoutGap = 5f;          // control band to the readout
        const float ReadoutW = DialSize - 12f, ReadoutH = 20f;
        /// <summary>Height of one name / control / readout column.</summary>
        const float ControlColumnH = TitleH + TitleGap + ControlH + ReadoutGap + ReadoutH;

        /// <summary>A control's name, centred above it.</summary>
        TextMeshProUGUI ControlTitle(RectTransform parent, TMP_FontAsset font, string text, float left, float top, float width)
        {
            var t = NewText("Title", parent, font, text, TitleFont, BoxInk);
            t.fontStyle = FontStyles.Bold | FontStyles.UpperCase; // every window's control names in capitals
            t.characterSpacing = 1.5f;
            t.alignment = TextAlignmentOptions.Center;
            PlaceV(t.rectTransform, left, top, width, TitleH);
            return t;
        }

        /// <summary>The black value box under a control; returns its text.</summary>
        TextMeshProUGUI ReadoutBox(RectTransform parent, TMP_FontAsset font, float centreX, float top)
        {
            var box = NewRect("Readout", parent);
            PlaceV(box, centreX - ReadoutW * 0.5f, top, ReadoutW, ReadoutH);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = DrumBlack;
            RoundValueBox(boxImg);
            boxImg.raycastTarget = false;
            var text = NewText("Text", box, font, "", CtlFont + 2f, DigitLight);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;      // long switch names shrink to fit
            text.fontSizeMin = 8f;
            text.fontSizeMax = CtlFont + 2f;
            text.overflowMode = TextOverflowModes.Truncate;
            var rr = text.rectTransform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(3f, 0f); rr.offsetMax = new Vector2(-3f, 0f);
            return text;
        }

        float AddDial(TMP_FontAsset font, float x, float y, float inner, string title, Func<string> tip, DialSpec spec,
                      Action<DialSpec, TMP_FontAsset> drawScale, string unit,
                      RectTransform parent = null, List<DialSpec> list = null)
        {
            // everything of one dial in a group, so it can be hidden as a whole
            spec.Group = NewRect("DialGroup", parent ?? dialsPanel);
            spec.Group.anchorMin = spec.Group.anchorMax = spec.Group.pivot = new Vector2(0f, 1f);
            spec.Group.sizeDelta = Vector2.zero;
            float yStart = y;
            ControlTitle(spec.Group, font, title, x, y, inner);
            y -= TitleH + TitleGap;

            // dial: rim, dark face, scale, pointers, hub (the black rim reaches 3 beyond the holder)
            var holder = NewRect("Dial", spec.Group);
            PlaceV(holder, x + (inner - DialSize) * 0.5f, y - 3f, DialSize, DialSize);
            // sharp black rim, the face filling it (no soft brass ring)
            Disc("Edge", holder, SharpCircleSprite(), DialSize + 6f, Color.black);
            var face = Disc("Face", holder, SharpCircleSprite(), DialSize + 3f, DialFace);
            face.raycastTarget = true; // the grab area
            face.alphaHitTestMinimumThreshold = 0.5f; // only the round face catches the mouse; drags just outside it move the window
            RectTransform ring = null;
            if (spec.Style == PointerStyle.Ring)
            {
                // the scale is drawn on a ring that turns; the face stays put
                ring = NewRect("Ring", face.rectTransform);
                ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
                ring.sizeDelta = new Vector2(DialSize, DialSize);
                spec.Face = ring;
            }
            else spec.Face = face.rectTransform;
            drawScale(spec, font);
            spec.Face = face.rectTransform;

            if (unit.Length > 0)
            {
                var u = NewText("Unit", face.rectTransform, font, unit, 9f, DialScale);
                u.alignment = TextAlignmentOptions.Center;
                var ur = u.rectTransform;
                ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(0.5f, 0.5f);
                ur.sizeDelta = new Vector2(50f, 12f);
                ur.anchoredPosition = new Vector2(0f, -DialSize * 0.2f);
            }

            switch (spec.Style)
            {
                case PointerStyle.Ring:
                    // the same four-lobed knob as the distance dial, smaller so it clears the
                    // numbers, turning with the ring; and the fixed index triangle at the top
                    AddCrossKnob(ring, DialSize * 0.4f);
                    var index = NewRect("Index", holder);
                    index.anchorMin = index.anchorMax = index.pivot = new Vector2(0.5f, 0.5f);
                    index.sizeDelta = new Vector2(13f, 11f);
                    index.anchoredPosition = new Vector2(0f, DialSize * 0.5f - 2f);
                    index.localEulerAngles = new Vector3(0f, 0f, 180f); // points down at the scale
                    var ii = index.gameObject.AddComponent<Image>();
                    ii.sprite = TriangleSprite();
                    ii.color = DialScale;
                    ii.raycastTarget = false;
                    spec.Needle = ring;
                    break;

                case PointerStyle.Knob:
                    spec.Needle = MakeKnobPointer(face.rectTransform);
                    break;

                case PointerStyle.Wheel:
                    spec.Needle = MakeHandWheel(face.rectTransform);
                    break;

                case PointerStyle.Crank:
                    spec.Needle = MakeHandCrank(face.rectTransform);
                    break;

                case PointerStyle.Flag:
                    // brass needle for the computed value, the red flag (the value set) over it at the rim
                    spec.Needle2 = MakePointer(face.rectTransform, "Needle", DialNeedle, 4f, true);
                    Disc("Hub", face.rectTransform, SharpCircleSprite(), 22f, Color.black);
                    Disc("HubCap", face.rectTransform, SharpCircleSprite(), 10f, new Color(0.18f, 0.18f, 0.18f, 1f));
                    spec.Needle = MakeFlagPointer(face.rectTransform);
                    break;

                default:
                    bool arrow = spec.Style == PointerStyle.Arrow;
                    spec.Needle = MakePointer(face.rectTransform, "Needle", DialNeedle, spec.NeedleWidth, arrow);
                    if (arrow) { Disc("Hub", face.rectTransform, SharpCircleSprite(), 22f, Color.black); Disc("HubCap", face.rectTransform, SharpCircleSprite(), 10f, new Color(0.18f, 0.18f, 0.18f, 1f)); }
                    else Disc("Hub", face.rectTransform, CircleSprite(), 14f, FrameEdge);
                    break;
            }

            if (spec.Set != null) // read-only dials (spread angle) don't turn
            {
                var relay = face.gameObject.AddComponent<DialRelay>();
                relay.Owner = this;
                relay.Spec = spec;
            }
            AddTooltip(face.gameObject, tip);
            y -= ControlH + ReadoutGap;

            // readout
            spec.Readout = ReadoutBox(spec.Group, font, x + inner * 0.5f, y);
            y -= ReadoutH + 12f;

            spec.Height = yStart - y;
            (list ?? dials).Add(spec);
            return y;
        }

        /// <summary>A pointer turning about the face centre; returns the rotating holder.</summary>
        static RectTransform MakePointer(RectTransform face, string name, Color color, float width, bool arrowTip)
        {
            const float headLen = 17f;
            var holder = NewRect(name, face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            var hand = NewRect("Hand", holder);
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.pivot = new Vector2(0.5f, 0.12f);
            float len = DialSize * 0.5f - 10f;
            hand.sizeDelta = new Vector2(width, arrowTip ? len - headLen : len);
            hand.anchoredPosition = Vector2.zero;
            var img = hand.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (arrowTip)
            {
                // solid arrowhead at the end of the needle, the tip at the needle's full length
                var head = NewRect("Tip", hand);
                head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f);
                head.pivot = new Vector2(0.5f, 0f);
                head.sizeDelta = new Vector2(Mathf.Max(width * 2.5f, 8f), headLen); // long and slender
                head.anchoredPosition = new Vector2(0f, -1f);
                var hi = head.gameObject.AddComponent<Image>();
                hi.sprite = TriangleSprite();
                hi.color = color;
                hi.raycastTarget = false;
            }
            return holder;
        }

        /// <summary>
        /// The distance knob, as on the TDC: a four-lobed grip with a brass screw in
        /// the middle and a white triangle in one of its hollows pointing at the value.
        /// </summary>
        static RectTransform MakeKnobPointer(RectTransform face)
        {
            var holder = NewRect("Knob", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;

            AddCrossKnob(holder, DistKnob);

            // pointer: white triangle in the top hollow, pointing out at the scale
            var tri = NewRect("Pointer", holder);
            tri.anchorMin = tri.anchorMax = tri.pivot = new Vector2(0.5f, 0.5f);
            const float triH = 10f;
            tri.sizeDelta = new Vector2(11f, triH);
            // tip on the knob's outer circle (the faint ring round its base)
            tri.anchoredPosition = new Vector2(0f, DistKnobRingR - triH * 0.5f);
            var ti = tri.gameObject.AddComponent<Image>();
            ti.sprite = TriangleSprite();
            ti.color = DialScale;
            ti.raycastTarget = false;
            tri.SetAsLastSibling(); // over the knob
            return holder;
        }

        /// <summary>A red flag at the rim pointing in at the scale (the TDC's spread angle flag); returns the turning holder.</summary>
        static RectTransform MakeFlagPointer(RectTransform face)
        {
            var holder = NewRect("Flag", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            var flag = NewRect("Pointer", holder);
            flag.anchorMin = flag.anchorMax = flag.pivot = new Vector2(0.5f, 0.5f);
            flag.sizeDelta = new Vector2(14f, 13f);
            flag.anchoredPosition = new Vector2(0f, DialSize * 0.5f - 8f);
            flag.localEulerAngles = new Vector3(0f, 0f, 180f); // points in, at the marks
            var img = flag.gameObject.AddComponent<Image>();
            img.sprite = TriangleSprite();
            img.color = FlagRed;
            img.raycastTarget = false;
            var edge = flag.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.8f);
            edge.effectDistance = new Vector2(0.8f, -0.8f);
            return holder;
        }

        static readonly Color FlagRed = new Color(0.90f, 0.16f, 0.12f, 1f);

        /// <summary>The TDC's four-lobed knob with a slotted brass screw in the middle.</summary>
        static void AddCrossKnob(RectTransform parent, float size)
        {
            var knob = NewRect("Grip", parent);
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(size, size);
            var ki = knob.gameObject.AddComponent<Image>();
            ki.sprite = CrossKnobSprite();
            ki.color = new Color(0.10f, 0.10f, 0.10f, 1f);
            ki.raycastTarget = false;
            var ke = knob.gameObject.AddComponent<Outline>();
            ke.effectColor = KnobHandleEdge;
            ke.effectDistance = new Vector2(0.8f, -0.8f);

            float screw = Mathf.Max(7f, size * 0.15f);
            Disc("Screw", parent, CircleSprite(), screw, DialNeedle);
            var slot = NewRect("Slot", parent);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = new Vector2(screw * 0.7f, 1.4f);
            var si = slot.gameObject.AddComponent<Image>();
            si.color = new Color(0.25f, 0.18f, 0.08f, 1f);
            si.raycastTarget = false;
        }

        static Sprite triangleSprite;

        /// <summary>A solid triangle pointing up.</summary>
        static Sprite TriangleSprite()
        {
            if (triangleSprite != null) return triangleSprite;
            const int w = 32, h = 32;
            triangleSprite = MakeShapeSprite(w, h, (x, y) => y >= 1f && y <= h - 1f && Mathf.Abs(x - w * 0.5f) <= (w * 0.5f - 1f) * (h - 1f - y) / (h - 2f));
            return triangleSprite;
        }

        /// <summary>A tick on the face at a pointer angle (clockwise from the top).</summary>
        void DrawTick(RectTransform face, float rot, float length, float width, Color color, float outerRadius = -1f)
        {
            var holder = NewRect("TickHolder", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            holder.localEulerAngles = new Vector3(0f, 0f, -rot);
            var t = NewRect("Tick", holder);
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(width, length);
            t.anchoredPosition = new Vector2(0f, outerRadius > 0f ? outerRadius : DialSize * 0.5f - 3f);
            var img = t.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        void ScaleLabel(RectTransform face, TMP_FontAsset font, float rot, string text, Color color, float size = 10f, bool radial = false, float radius = -1f)
        {
            float r = radius > 0f ? radius : DialSize * 0.5f - 22f;
            float a = rot * Mathf.Deg2Rad;
            var label = NewText("Label", face, font, text, size, color);
            label.alignment = TextAlignmentOptions.Center;
            var lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(28f, 14f);
            lr.anchoredPosition = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
            if (radial) lr.localEulerAngles = new Vector3(0f, 0f, -rot); // printed round the ring, upright at the top
        }

        static readonly Color AobRed = new Color(0.80f, 0.29f, 0.26f, 1f);      // the TDC's port half
        static readonly Color AobFineRed = new Color(1.00f, 0.47f, 0.42f, 1f);  // the red ones digits, brighter to read on black
        static readonly Color AobGreen = new Color(0.20f, 0.56f, 0.30f, 1f);    // the TDC's starboard half
        const float AobFineSpacing = 16f;   // degrees of arc between the fine (ones) digits
        const float AobWindowHalf = 52f;    // half the angle of the black window at the bottom

        /// <summary>
        /// The TDC's Lagenwinkel dial: a black rim with a mark every 10° and numbers every 20°
        /// (0 at the top, 180 at the bottom), an inner disc with the port half solid red and
        /// the starboard half solid green ("Bug links" / "Bug rechts", "Grad"), and a black
        /// window at the bottom where the ones digits roll by (red for port, green for starboard).
        /// </summary>
        void DrawAobScale(DialSpec spec, TMP_FontAsset font)
        {
            var face = spec.Face;
            for (int deg = -170; deg <= 180; deg += 10)
            {
                DrawTick(face, deg, 9f, 2.4f, DialScale, RimTickR);
                if (deg % 20 == 0) ScaleLabel(face, font, deg, Mathf.Abs(deg).ToString(), DialScale, 9f, false, DialSize * 0.5f - 17f);
            }

            // inner disc: thin white ring, red / green halves, black window at the bottom
            float inner = DialSize * 0.58f;
            Disc("InnerRing", face, SharpCircleSprite(), inner + 2f, DialScale);
            Disc("Starboard", face, HalfDiscSprite(), inner, AobGreen);
            var port = Disc("Port", face, HalfDiscSprite(), inner, AobRed);
            port.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            Disc("WindowEdge", face, SectorSprite(), inner + 2f, DialScale);
            Disc("Window", face, SectorSprite(), inner - 1f, Color.black);

            AobLabel(face, font, Strings.AobDegrees, 0f, inner * 0.36f, 0f);
            AobLabel(face, font, Strings.AobBowLeft, -68f, inner * 0.30f, 58f);
            AobLabel(face, font, Strings.AobBowRight, 68f, inner * 0.30f, -58f);

            // the ones ring: a full turn of it is 10° of AOB. Ten positions 36° apart, each with
            // a mark and a pair of digits, red d (port) beside green (10 - d) % 10 (starboard):
            // red 0 / green 0, red 9 / green 1, red 8 / green 2 ... Read at the index at the
            // bottom: green while the needle is to starboard, red while it is to port.
            float fineR = inner * 0.5f * 0.70f;
            var red = new TextMeshProUGUI[10];
            var green = new TextMeshProUGUI[10];
            var marks = new RectTransform[10];
            for (int g = 0; g < 10; g++)
            {
                red[g] = FineDigit(face, font, ((10 - g) % 10).ToString(), AobFineRed);
                green[g] = FineDigit(face, font, g.ToString(), DialStarboard);
                marks[g] = NewRect("FineMarkHolder" + g, face);
                marks[g].anchorMin = marks[g].anchorMax = marks[g].pivot = new Vector2(0.5f, 0.5f);
                marks[g].sizeDelta = Vector2.zero;
                var t = NewRect("FineMark", marks[g]);
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
                t.pivot = new Vector2(0.5f, 1f);
                t.sizeDelta = new Vector2(1.2f, 4f);
                t.anchoredPosition = new Vector2(0f, fineR + 9f);
                var ti = t.gameObject.AddComponent<Image>();
                ti.color = DialScale;
                ti.raycastTarget = false;
            }
            spec.Extra = (d, v) =>
            {
                // position of the ring: continuous through 0, since green g pairs with red (10 - g)
                float u = Mathf.Repeat(v, 10f);
                bool toPort = v < -0.05f;
                for (int g = 0; g < 10; g++)
                {
                    float rel = Mathf.DeltaAngle(0f, (u - g) * 36f);   // from the bottom index: 0-0, then 9-1, 8-2 ... anticlockwise
                    float edge = Mathf.Abs(rel);
                    bool show = edge <= AobWindowHalf - 6f;
                    red[g].enabled = green[g].enabled = show;
                    marks[g].gameObject.SetActive(show);
                    if (!show) continue;
                    float rot = 180f + rel;
                    float ra = rot * Mathf.Deg2Rad;
                    var c = new Vector2(Mathf.Sin(ra) * fineR, Mathf.Cos(ra) * fineR);
                    // printed to be read from the dial's edge: the tops of the digits face the centre,
                    // the red / green pair side by side along the ring
                    float tilt = 180f - rot;
                    var turn = Quaternion.Euler(0f, 0f, tilt);
                    Vector2 along = turn * new Vector3(3.2f, 0f, 0f);
                    red[g].rectTransform.anchoredPosition = c - along;
                    green[g].rectTransform.anchoredPosition = c + along;
                    red[g].rectTransform.localEulerAngles = green[g].rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt);
                    marks[g].localEulerAngles = new Vector3(0f, 0f, -rot);
                    float fade = Mathf.Clamp01((AobWindowHalf - 6f - edge) / 10f); // fade at the window's sides
                    Color rc = Tinted(AobFineRed, lastTint), gc = Tinted(DialStarboard, lastTint); // set every frame: tint here
                    rc.a = fade * (toPort ? 1f : 0.45f); // the side the needle is on reads bright
                    gc.a = fade * (toPort ? 0.45f : 1f);
                    red[g].color = rc;
                    green[g].color = gc;
                }
            };
        }

        TextMeshProUGUI FineDigit(RectTransform face, TMP_FontAsset font, string text, Color color)
        {
            var t = NewText("Fine", face, font, text, 9f, color);
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(9f, 12f);
            t.enabled = false;
            return t;
        }

        void AobLabel(RectTransform face, TMP_FontAsset font, string text, float rot, float radius, float tilt)
        {
            var t = NewText("Label", face, font, text, 8f, DialScale);
            t.alignment = TextAlignmentOptions.Center;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(50f, 11f);
            float a = rot * Mathf.Deg2Rad;
            r.anchoredPosition = new Vector2(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius);
            r.localEulerAngles = new Vector3(0f, 0f, tilt);
        }

        static Sprite halfDiscSprite, sectorSprite;

        /// <summary>The right half of a crisp disc.</summary>
        static Sprite HalfDiscSprite()
        {
            if (halfDiscSprite != null) return halfDiscSprite;
            const int n = 256;
            float c = n * 0.5f, rr = (n * 0.5f - 1f) * (n * 0.5f - 1f);
            halfDiscSprite = MakeShapeSprite(n, n, (x, y) => x >= c && (x - c) * (x - c) + (y - c) * (y - c) <= rr);
            return halfDiscSprite;
        }

        /// <summary>A crisp wedge of a disc pointing down (the AOB dial's window).</summary>
        static Sprite SectorSprite()
        {
            if (sectorSprite != null) return sectorSprite;
            const int n = 256;
            float c = n * 0.5f, rr = (n * 0.5f - 1f) * (n * 0.5f - 1f);
            sectorSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                if (dx * dx + dy * dy > rr) return false;
                return Mathf.Abs(Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg) <= AobWindowHalf;
            });
            return sectorSprite;
        }

        const float RimTickR = DialSize * 0.5f - 1f; // AOB and speed marks: out to just inside the dial's edge

        void DrawSpeedScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int kn = 0; kn <= 55; kn++)
            {
                bool major = kn % 5 == 0;
                DrawTick(spec.Face, spec.ToRot(kn), major ? 11f : 7f, major ? 2.2f : 1.4f, DialScale, RimTickR);
                if (major) ScaleLabel(spec.Face, font, spec.ToRot(kn), kn.ToString(), DialScale, 10f, false, DialSize * 0.5f - 20f);
            }
        }

        // distance dial layout (as on the TDC): numbers round the rim, marks inside them pointing in
        // (shared with the own angular speed knob)
        const float DistLabelR = DialSize * 0.5f - 4f;   // numbers near the rim
        const float DistTickR = DialSize * 0.5f - 11f;   // numbered marks just inside the numbers
        const float DistKnob = DialSize * 0.5f + 12f;  // knob sprite size: its corners on the faint ring (DistKnobRingR)
        const float DistMajorLen = 7f;                 // numbered marks
        const float DistKnobRingR = DistTickR - DistMajorLen - 1f; // the faint ring round the knob's base, just inside the marks

        void DrawDistanceScale(DialSpec spec, TMP_FontAsset font)
        {
            // as on the TDC's face, every mark at its place on the log scale (DistanceRotOf)
            float[] labels = { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f };
            foreach (float hm in labels)
            {
                float r = DistanceRotOf(hm);
                DrawTick(spec.Face, r, DistMajorLen, 2f, DialScale, DistTickR);
                ScaleLabel(spec.Face, font, r, ((int)hm).ToString(), DialScale, hm < 10f ? 9f : 10f, false, DistLabelR);
            }
            DrawTick(spec.Face, DistanceRotOf(90f), DistMajorLen, 2f, DialScale, DistTickR); // 90: a plain mark, no number
            // half-way marks with a dot: x.5 up to 10, then 15, 25 ... 75, 85 and 95
            for (int i = 7; i < 20; i++) DrawDotTick(spec.Face, DistanceRotOf(i * 0.5f));
            for (int hm = 15; hm < 100; hm += 10) DrawDotTick(spec.Face, DistanceRotOf(hm));
            // very thin marks every 1 hm from 10 to 30, where the scale is widest; their inner ends in line with the numbered marks'
            const float thinLen = 3f;
            for (int hm = 11; hm < 30; hm++)
                if (hm % 5 != 0) DrawTick(spec.Face, DistanceRotOf(hm), thinLen, 0.7f, DialScale, DistTickR - DistMajorLen + thinLen);
            // faint circle just inside the marks: the base of the knob
            FaintRing(spec.Face, DistKnobRingR);
            // "hm" at the top, down among the marks in the gap between 100 and 3
            ScaleLabel(spec.Face, font, 0f, "hm", DialScale, 9f, false, DistTickR - 3.5f);
        }

        const float TurnRateMax = 3f; // °/s either way, as on the TDC's knob

        static TorpedoCalculator turnRateCheckedFor;
        static bool turnRateAvailable;

        /// <summary>
        /// Whether this boat's TDC has the own angular speed knob. Not every TDC model has
        /// one (the smaller boats' calculator does not), and which model a boat carries is in
        /// its 3D model, so this looks for a TorpedoCalculatorOwnAngularSpeedDial wired to this
        /// TDC. Searched once per TDC (the scene search is slow); the result is logged.
        /// </summary>
        static TorpedoCalculator aobMotorCheckedFor;
        static bool aobMotorAvailable;

        /// <summary>Whether this boat's TDC has an AOB motor switch (looked up once per TDC; the result is logged).</summary>
        static bool AobMotorAvailable()
        {
            var tdc = Tdc();
            if (tdc == null) return true; // can't tell yet: show it
            if (ReferenceEquals(tdc, aobMotorCheckedFor)) return aobMotorAvailable;
            aobMotorCheckedFor = tdc;
            aobMotorAvailable = false;
            try
            {
                var calc = AccessTools.Field(typeof(TorpedoCalculatorAOBMotorSwitch), "calculator");
                foreach (var s in Resources.FindObjectsOfTypeAll<TorpedoCalculatorAOBMotorSwitch>())
                {
                    if (s == null || !s.gameObject.scene.IsValid()) continue;
                    var c = calc != null ? calc.GetValue(s) as TorpedoCalculator : null;
                    if (c == null || ReferenceEquals(c, tdc)) { aobMotorAvailable = true; break; }
                }
                Debug.Log(ScopeTdcControlsMod.Tag + " TDC '" + tdc.name + "': AOB motor switch " + (aobMotorAvailable ? "found" : "not on this TDC, hidden"));
            }
            catch (Exception e)
            {
                aobMotorAvailable = true;
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " AOB motor switch check failed: " + e.Message);
            }
            return aobMotorAvailable;
        }

        static bool TurnRateAvailable()
        {
            var tdc = Tdc();
            if (tdc == null) return false;
            if (ReferenceEquals(tdc, turnRateCheckedFor)) return turnRateAvailable;
            turnRateCheckedFor = tdc;
            turnRateAvailable = false;
            try
            {
                var calc = AccessTools.Field(typeof(TorpedoCalculatorOwnAngularSpeedDial), "calculator");
                foreach (var d in Resources.FindObjectsOfTypeAll<TorpedoCalculatorOwnAngularSpeedDial>())
                {
                    if (d == null || !d.gameObject.scene.IsValid()) continue;
                    var c = calc != null ? calc.GetValue(d) as TorpedoCalculator : null;
                    if (c == null || ReferenceEquals(c, tdc)) { turnRateAvailable = true; break; }
                }
                bool c36 = false;
                try { c36 = (bool)AccessTools.Field(typeof(TorpedoCalculator), "isC36Calculator").GetValue(tdc); } catch { }
                Debug.Log(ScopeTdcControlsMod.Tag + " TDC '" + tdc.name + "' (C36 calculator: " + c36 + "): own angular speed knob "
                          + (turnRateAvailable ? "found" : "not on this TDC, setting disabled"));
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " angular speed knob check failed: " + e.Message);
            }
            return turnRateAvailable;
        }

        static string FormatTurnRate(float v)
        {
            float a = Mathf.Abs(v);
            string side = a < 0.05f ? "" : (v < 0f ? Strings.Port : Strings.Starboard) + " ";
            return side + a.ToString("0.0", CultureInfo.InvariantCulture) + " °/s";
        }

        /// <summary>As on the TDC's angular speed knob: 3 2 1 0 1 2 3 over the top, half marks between, Bb / Stb at the sides.</summary>
        void DrawTurnRateScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int h = -6; h <= 6; h++)
            {
                float v = h * 0.5f;
                float r = spec.ToRot(v);
                if (h % 2 != 0) { DrawTick(spec.Face, r, 4f, 1.2f, DialScale, DistTickR); continue; }
                DrawTick(spec.Face, r, 7f, 2f, DialScale, DistTickR);
                ScaleLabel(spec.Face, font, r, Mathf.Abs(h / 2).ToString(CultureInfo.InvariantCulture), DialScale, 10f, false, DistLabelR);
            }
            FaintRing(spec.Face, DistKnobRingR);
            ScaleLabel(spec.Face, font, -97f, Strings.PortLong, DialScale, 10f, false, DistLabelR - 5f);
            ScaleLabel(spec.Face, font, 97f, Strings.StarboardLong, DialScale, 10f, false, DistLabelR - 5f);
        }

        /// <summary>A mark pointing in from the scale with a small dot on its inner tip.</summary>
        void DrawDotTick(RectTransform face, float rot)
        {
            const float len = 5f;
            float outer = DistTickR - DistMajorLen + len; // inner end in line with the numbered marks'
            DrawTick(face, rot, len, 0.9f, DialScale, outer);
            float a = rot * Mathf.Deg2Rad, r = outer - len * 0.5f + 1.5f; // a little out from half way along the mark
            var dot = Disc("Dot", face, CircleSprite(), 1.8f, DialScale);
            dot.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
        }

        static readonly Color FaintLine = new Color(0.93f, 0.90f, 0.82f, 0.16f);
        static readonly Dictionary<int, Sprite> ringSprites = new Dictionary<int, Sprite>();

        /// <summary>A very thin, faint circle of the given radius (about one pixel wide), e.g. a knob's base.</summary>
        static void FaintRing(RectTransform parent, float radius)
        {
            int diameter = Mathf.Max(8, Mathf.RoundToInt(radius * 2f));
            Sprite sprite;
            if (!ringSprites.TryGetValue(diameter, out sprite))
            {
                // drawn at 4x the shown size so the line stays crisp; one shown pixel = 4 texels
                int n = diameter * 4;
                float c = n * 0.5f, outer = c - 1f, inner = outer - 2.5f; // about 0.6 px
                sprite = MakeShapeSprite(n, n, (x, y) =>
                {
                    float d2 = (x - c) * (x - c) + (y - c) * (y - c);
                    return d2 <= outer * outer && d2 >= inner * inner;
                });
                ringSprites[diameter] = sprite;
            }
            var r = NewRect("FaintRing", parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(diameter, diameter);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = FaintLine;
            img.raycastTarget = false;
        }

        static Sprite sharpCircleSprite;

        /// <summary>A crisp filled circle (anti-aliased over about one pixel, no soft glow) for dial faces and rims.</summary>
        static Sprite SharpCircleSprite()
        {
            if (sharpCircleSprite != null) return sharpCircleSprite;
            const int n = 256;
            float c = n * 0.5f, r = n * 0.5f - 1f;
            sharpCircleSprite = MakeShapeSprite(n, n, (x, y) => (x - c) * (x - c) + (y - c) * (y - c) <= r * r);
            return sharpCircleSprite;
        }

        static Sprite crossKnobSprite;

        /// <summary>The distance knob: a square with deeply hollowed sides and rounded corners (four lobes).</summary>
        static Sprite CrossKnobSprite()
        {
            if (crossKnobSprite != null) return crossKnobSprite;
            const int n = 128;
            float c = n * 0.5f, s = n * 0.46f, depth = n * 0.11f, bigR = n * 0.62f, cornerR = n * 0.09f;
            crossKnobSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c);
                if (dx > s || dy > s) return false;
                // rounded corners
                if (dx > s - cornerR && dy > s - cornerR && new Vector2(dx - (s - cornerR), dy - (s - cornerR)).magnitude > cornerR) return false;
                // hollowed sides: outside a big circle beyond each side
                float off = s + bigR - depth;
                if (new Vector2(dx, dy - off).magnitude < bigR) return false;
                if (new Vector2(dx - off, dy).magnitude < bigR) return false;
                return true;
            });
            return crossKnobSprite;
        }

        void DrawLengthScale(DialSpec spec, TMP_FontAsset font)
        {
            // as on the TDC: a mark every 10 m, longer every 50 m, numbered 100 / 150 / 200
            for (int m = 100; m <= 200; m += 5)
            {
                bool major = m % 50 == 0, mid = m % 10 == 0;
                if (!mid && !major) { DrawTick(spec.Face, spec.ToRot(m), 4f, 1.2f, DialScale); continue; }
                DrawTick(spec.Face, spec.ToRot(m), major ? 11f : 7f, major ? 2.2f : 1.6f, DialScale);
                if (major) ScaleLabel(spec.Face, font, spec.ToRot(m), m.ToString(), DialScale, 10f, true);
            }
        }

        /// <summary>The Einzelschuß switch is on a spread position (straight course / while turning).</summary>
        static bool SpreadSelected()
        {
            var b = Boxes();
            if (b == null) return true;
            var t = b.SelectedTorpedoTube;
            return t == SelectedTorpedoTube.SalvoCourse || t == SelectedTorpedoTube.SalvoTurning;
        }

        static string FormatAob(float v)
        {
            float a = Mathf.Abs(v);
            string side = a < 0.5f || a > 179.5f ? "" : (v < 0f ? Strings.Port : Strings.Starboard) + " ";
            return side + Mathf.RoundToInt(a).ToString("000") + "°";
        }

        /// <summary>
        /// Set the AOB at once (as the game does when a crew callout ends): the impact angle
        /// that gives this AOB, both as the target of the AOB motor and as its position, so
        /// the TDC follows the dial while it is turned.
        /// </summary>
        /// <remarks>
        /// Not through DesiredImpactAngle: its setter switches AOB tracking (ApplyCourseChanges)
        /// off and remembers the old state to restore when the motor arrives, so setting it
        /// again every frame of a drag remembered "off" and left tracking off for good (the
        /// AOB then stopped following the bearing). Instead the impact angle is set directly,
        /// and the TDC's AOB-bearing sum with it, so the tracking sees no course change to
        /// undo. Like the crew's AOB report it switches AOB tracking on; the AOB motor is left
        /// as set on its switch (tracking only follows the bearing while the motor is on).
        /// </remarks>
        static void ApplyAobNow(TorpedoCalculator tdc, float aob)
        {
            aob = Mathf.DeltaAngle(0f, aob);
            float impact = tdc.ComputeImpactAngleForDesiredAOB(aob, tdc.ImpactAngle);
            if (float.IsNaN(impact) || float.IsInfinity(impact)) return;

            // AOB tracking on, as the crew's AOB report does (Periscope.CourseTicket_Stopped); the
            // AOB motor stays as it is on the AOB motor switch
            if (!tdc.ApplyCourseChanges) tdc.ApplyCourseChanges = true;
            ResolveAobFields();
            if (restoreTracking != null) restoreTracking(tdc) = false; // nothing left to restore
            if (desiredImpact != null) desiredImpact(tdc) = float.NaN; // cancel any motor run (no side effects)
            tdc.ImpactAngle = impact;
            tdc.AngleOnBow = aob; // shown right away; the TDC keeps it in step from here
            tdc.AOBBearingSum = aob - tdc.TargetBearing; // so AOB tracking doesn't count this as a course change
        }

        static AccessTools.FieldRef<TorpedoCalculator, bool> restoreTracking;
        static AccessTools.FieldRef<TorpedoCalculator, float> desiredImpact;
        static bool aobFieldsResolved;

        static void ResolveAobFields()
        {
            if (aobFieldsResolved) return;
            aobFieldsResolved = true;
            try { restoreTracking = AccessTools.FieldRefAccess<TorpedoCalculator, bool>("restoreApplyCourseChangesFlagAfterAOBChange"); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " AOB: restore flag not found: " + e.Message); }
            try { desiredImpact = AccessTools.FieldRefAccess<TorpedoCalculator, float>("desiredImpactAngle"); }
            catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " AOB: desired impact angle not found: " + e.Message); }
        }

        // ------------------------------------------------------------------ runtime

        void UpdateDials(TorpedoCalculator tdc, bool manned)
        {
            if (dialsPanel == null) return;
            dialsGroup.alpha = manned ? 1f : 0.45f;
            dialsGroup.interactable = manned;
            dialsGroup.blocksRaycasts = manned;

            // dials shown only in some cases (target length: spreads only); the window fits what is shown
            bool layout = false;
            foreach (var d in dials)
            {
                bool on = d.Visible == null || d.Visible();
                if (d.Group.gameObject.activeSelf != on) { d.Group.gameObject.SetActive(on); layout = true; }
            }
            if (layout)
            {
                float h = dialsFullH;
                foreach (var d in dials) if (!d.Group.gameObject.activeSelf) h -= d.Height;
                dialsPanel.sizeDelta = new Vector2(dialsPanel.sizeDelta.x, h);
            }
            foreach (var d in dials) UpdateDialValue(tdc, d);
            UpdateFlipSwitch(aobMotorSwitch, tdc);
            UpdateImpactTime(tdc);
        }

        /// <summary>One dial's needle (or ring) and readout from the TDC.</summary>
        void UpdateDialValue(TorpedoCalculator tdc, DialSpec d)
        {
            {
                float v = d.Get(tdc);
                // a value outside the dial's (the game's) range, e.g. from an old save, would
                // leave the dial with nothing to turn from: bring it into range
                if (d.Set != null && !d.Wrap && (float.IsNaN(v) || float.IsInfinity(v) || v < d.Min - 0.01f || v > d.Max + 0.01f))
                {
                    float fixedV = float.IsNaN(v) || float.IsInfinity(v) ? (d.Min + d.Max) * 0.5f : Mathf.Clamp(v, d.Min, d.Max);
                    if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " TDC value " + v + " out of the dial's range, set to " + fixedV);
                    d.Set(tdc, fixedV);
                    v = d.Get(tdc);
                }
                if (float.IsNaN(v) || float.IsInfinity(v)) return;
                if (d.Announce != null && !d.Dragging && !float.IsNaN(d.AnnounceAt) && Time.unscaledTime >= d.AnnounceAt)
                {
                    d.AnnounceAt = float.NaN;
                    d.Announce(tdc, v);
                }
                // a pointer turns to the value; a ring turns the value to the top
                // while dragging, show the dial's own value so it stays under the pointer (the TDC may lag or round it)
                float shown = d.Dragging && !float.IsNaN(d.DragValue) ? d.DragValue : v;
                d.Needle.localEulerAngles = new Vector3(0f, 0f, d.FreeSpin ? -d.FreeAngle : d.Style == PointerStyle.Ring ? d.ToRot(shown) : -d.ToRot(shown));

                d.Readout.text = d.Format(shown);
                if (d.Extra != null) d.Extra(d, shown);
                if (d.Needle2 != null && d.Get2 != null)
                {
                    float v2 = d.Get2(tdc);
                    if (!float.IsNaN(v2) && !float.IsInfinity(v2)) d.Needle2.localEulerAngles = new Vector3(0f, 0f, -d.ToRot(d.Wrap ? v2 : Mathf.Clamp(v2, d.Min, d.Max)));
                }
            }
        }

        void PositionDials()
        {
            if (dialsPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.DialsX, Screen.height * GyroConfig.DialsY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                dialsPanel.anchoredPosition = local + new Vector2(GyroConfig.DialsOffsetX, GyroConfig.DialsOffsetY)
                    + new Vector2(0f, (dialsFullH - dialsPanel.sizeDelta.y) * 0.5f * EffectiveScale(ResizableWindow.Dials)); // hidden dials shrink it from the bottom; the top stays put
        }

        /// <summary>Starting value for a turn: the TDC's value, brought into the dial's range.</summary>
        float DialBase(DialSpec spec, TorpedoCalculator tdc)
        {
            float v = spec.Base != null ? spec.Base(tdc) : spec.Get(tdc);
            if (spec.Wrap) return v;
            if (float.IsNaN(v) || float.IsInfinity(v)) return (spec.Min + spec.Max) * 0.5f; // nothing set yet
            return Mathf.Clamp(v, spec.Min, spec.Max);                                  // out of range: start from the end
        }

        void TurnDial(DialSpec spec, float degrees)
        {
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            float v = spec.Dragging && !float.IsNaN(spec.DragValue) ? spec.DragValue : DialBase(spec, tdc);
            if (float.IsNaN(v)) return;
            float next = spec.Turn(v, degrees);
            if (spec.Dragging) spec.DragValue = next; // the drag keeps the exact value, so small moves add up
            spec.Set(tdc, spec.Round(next));
        }

        void StepDial(DialSpec spec, float delta)
        {
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            float v = DialBase(spec, tdc);
            if (float.IsNaN(v)) return;
            spec.Set(tdc, spec.Step(v, delta));
            if (spec.Announce != null && !spec.Dragging) spec.AnnounceAt = Time.unscaledTime + WheelAnnounceDelay;
        }

        /// <summary>
        /// Click on a dial's scale: set the value printed where the pointer is. Only the outer
        /// ring (the scale) responds; clicks on positions the scale doesn't cover are ignored.
        /// A turning ring (target length) brings the clicked number up to the index.
        /// </summary>
        void ClickDial(DialSpec spec, Vector2 screenPos, Transform faceTransform)
        {
            var tdc = Tdc();
            if (spec.NoClick || tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, faceTransform.position);
            Vector2 d = screenPos - centre;
            float scale = faceTransform.lossyScale.x > 0f ? faceTransform.lossyScale.x : 1f;
            if (d.magnitude / scale < DialSize * 0.5f * 0.58f) return;   // inside the scale: not a click on it
            float a = Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);

            float v;
            if (spec.Wrap)
            {
                // the nearest value that sits under the click (a fine dial keeps the tens it is on)
                float b = DialBase(spec, tdc), p = spec.Period;
                float delta = Mathf.Repeat(a * p / 360f - Mathf.Repeat(b, p) + p * 0.5f, p) - p * 0.5f;
                v = Mathf.DeltaAngle(0f, b + delta);
            }
            else
            {
                float rot = a;
                if (spec.Style == PointerStyle.Ring) rot = Mathf.Repeat(a + spec.ToRot(DialBase(spec, tdc)), 360f); // ring coordinates
                float lo = Mathf.Min(spec.Rots[0], spec.Rots[spec.Rots.Length - 1]);
                float hi = Mathf.Max(spec.Rots[0], spec.Rots[spec.Rots.Length - 1]);
                if (rot > hi + 2f && rot - 360f >= lo - 2f) rot -= 360f;    // scales given in -180..180 (torpedo depth)
                if (rot < lo - 2f || rot > hi + 2f) return;                 // off the printed scale
                v = spec.Round(Mathf.Clamp(spec.ToValue(Mathf.Clamp(rot, lo, hi)), spec.LimitMin, spec.LimitMax));
            }

            HideTip();
            spec.Set(tdc, v);
            if (spec.Announce != null) spec.AnnounceAt = Time.unscaledTime;
        }

        /// <summary>Every dial sets the TDC as it turns; the callout comes after you let go.</summary>
        void BeginDialDrag(DialSpec spec)
        {
            spec.Dragging = true;
            spec.AnnounceAt = float.NaN;
            var t0 = Tdc();
            spec.DragStartValue = t0 != null ? spec.Get(t0) : float.NaN;
            spec.DragValue = t0 != null ? DialBase(spec, t0) : float.NaN; // turn from here, not from what the TDC reads back
        }

        void EndDialDrag(DialSpec spec)
        {
            if (!spec.Dragging) return;
            spec.Dragging = false;
            spec.DragValue = float.NaN;
            var t1 = Tdc();
            bool changed = t1 == null || float.IsNaN(spec.DragStartValue)
                           || Mathf.Abs(spec.Wrap ? Mathf.DeltaAngle(spec.Get(t1), spec.DragStartValue)
                                                  : spec.Get(t1) - spec.DragStartValue) > 0.01f;
            if (spec.Announce != null && changed) spec.AnnounceAt = Time.unscaledTime; // next update, with the final value
        }

        /// <summary>True while the pointer is over the dial window: the game's own wheel use is held off.</summary>
        public static bool WheelOverDials { get { return WheelGuard.Hovered; } }

        /// <summary>
        /// Tracks the pointer over the dial window (enter/exit reach this parent
        /// from every child), so scrolling there doesn't also zoom the camera out
        /// of the scope view.
        /// </summary>
        sealed class WheelGuard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public static bool Hovered;
            public void OnPointerEnter(PointerEventData e) { Hovered = true; }
            public void OnPointerExit(PointerEventData e) { Hovered = false; }
            void OnDisable() { Hovered = false; }
        }

        static bool ShiftHeld { get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }
        static bool CtrlKey { get { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); } }

        /// <summary>
        /// Grab and turn, like the TDC's dials: the value follows the change in
        /// pointer angle around the dial centre. Shift = 10x finer, wheel = steps,
        /// Ctrl+drag moves the window instead.
        /// </summary>
        sealed class DialRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
        {
            public GyroCounterUI Owner;
            public DialSpec Spec;
            float lastAngle = float.NaN;
            DragRelay windowDrag;
            bool movingWindow;

            DragRelay WindowDrag
            {
                get
                {
                    if (windowDrag == null && transform.parent != null) windowDrag = transform.parent.GetComponentInParent<DragRelay>();
                    return windowDrag;
                }
            }

            float PointerAngle(Vector2 screenPos)
            {
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, transform.position);
                Vector2 d = screenPos - centre;
                if (d.sqrMagnitude < 36f) return float.NaN; // too close to the centre to tell
                return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            }

            public void OnPointerDown(PointerEventData e)
            {
                lastAngle = PointerAngle(e.position);
                if (Owner != null) Owner.HideTip();
            }

            public void OnBeginDrag(PointerEventData e)
            {
                movingWindow = CtrlKey && WindowDrag != null;
                if (movingWindow) WindowDrag.OnBeginDrag(e);
                else Owner.BeginDialDrag(Spec);
            }

            public void OnDrag(PointerEventData e)
            {
                if (movingWindow) { WindowDrag.OnDrag(e); return; }
                float a = PointerAngle(e.position);
                if (float.IsNaN(a)) return;
                if (float.IsNaN(lastAngle)) { lastAngle = a; return; }
                float turned = Mathf.DeltaAngle(lastAngle, a);
                lastAngle = a;
                if (Spec.FreeSpin) Spec.FreeAngle = Mathf.Repeat(Spec.FreeAngle + turned, 360f); // the wheel goes round with the hand (Shift only gears the value down)
                Owner.TurnDial(Spec, turned * (ShiftHeld ? 0.1f : 1f));
            }

            public void OnEndDrag(PointerEventData e)
            {
                if (movingWindow) WindowDrag.OnEndDrag(e);
                else Owner.EndDialDrag(Spec);
                movingWindow = false;
                lastAngle = float.NaN;
            }

            void OnDisable()
            {
                // window hidden mid-drag: still send what was dialled
                if (Owner != null && Spec != null) Owner.EndDialDrag(Spec);
                movingWindow = false;
                lastAngle = float.NaN;
            }

            // a single click on the scale (outer ring) sets the value under the pointer
            public void OnPointerClick(PointerEventData e)
            {
                if (e.dragging || e.button != PointerEventData.InputButton.Left || CtrlKey) return;
                Owner.ClickDial(Spec, e.position, transform);
            }

            public void OnScroll(PointerEventData e)
            {
                float steps = e.scrollDelta.y;
                if (Mathf.Abs(steps) < 0.01f) return;
                float delta = Mathf.Sign(steps) * Mathf.Ceil(Mathf.Abs(steps)) * Spec.WheelStep * (ShiftHeld ? 0.1f : 1f);
                if (Spec.WheelStepAt != null)
                {
                    // the step depends on where the dial is (target distance: 50 m below 10 hm): one notch at a time
                    float dir = Mathf.Sign(delta);
                    for (int i = 0; i < Mathf.CeilToInt(Mathf.Abs(steps)); i++)
                    {
                        var tdc = Tdc();
                        if (tdc == null) return;
                        Owner.StepDial(Spec, dir * Spec.WheelStepAt(Owner.DialBase(Spec, tdc), dir, ShiftHeld));
                    }
                    return;
                }
                if (Spec.FreeSpin) Spec.FreeAngle = Mathf.Repeat(Spec.FreeAngle + Mathf.Sign(delta) * 15f * Mathf.Ceil(Mathf.Abs(steps)), 360f); // a bit of a turn per notch, the way the value goes
                Owner.StepDial(Spec, delta);
            }
        }
    }

    /// <summary>
    /// The camera reads the zoom wheel (scrolling out leaves the scope view)
    /// only when this says the wheel is free; it isn't while the pointer is
    /// over the TDC dial window.
    /// </summary>
    [HarmonyPatch(typeof(UBOAT.Game.UI.GameInputModule), "IsReadyToCaptureMouseWheelInputs")]
    static class GameInputModule_IsReadyToCaptureMouseWheelInputs_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (__result && GyroCounterUI.WheelOverDials) __result = false;
        }
    }
}
