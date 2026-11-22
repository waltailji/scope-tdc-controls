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

        // the real distance dial: hm -> degrees clockwise from the top (read off the TDC face)
        static readonly float[] DistanceHm = { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f };
        static readonly float[] DistanceRot = { 12f, 38f, 60f, 78f, 93f, 106f, 117f, 127f, 182f, 218f, 245f, 268f, 289f, 306f, 321f, 345f };

        RectTransform dialsPanel;
        CanvasGroup dialsGroup;
        float dialsFullH;                // window height with every dial shown
        readonly List<DialSpec> dials = new List<DialSpec>();

        enum PointerStyle
        {
            Needle,   // thin brass needle (speed)
            Arrow,    // needle with an arrow tip (AOB)
            Knob,     // turning knob grip with a painted arrow (distance)
            Ring      // the scale ring turns under a fixed index at the top (target length)
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
            public float WheelStep;
            public Func<float, string> Format;
            public Action<DialSpec, float> Extra;             // per-frame extras (AOB: the rolling ones digits)
            public Func<bool> Visible;                        // null = always shown
            public RectTransform Group;                       // title, dial and readout
            public float Height;                              // its share of the window height
            public PointerStyle Style;                        // how the value is shown
            public RectTransform Needle;                      // Ring style: the turning scale ring
            public TextMeshProUGUI Readout;
            public RectTransform Face;

            public bool Dragging;
            public float DragValue = float.NaN;               // the value being dragged (the TDC can read back differently)

            public float Min { get { return Mathf.Min(Values[0], Values[Values.Length - 1]); } }
            public float Max { get { return Mathf.Max(Values[0], Values[Values.Length - 1]); } }

            public float ToRot(float v)
            {
                if (Wrap) return Mathf.DeltaAngle(0f, v);
                return Map(v, Values, Rots);
            }

            public float ToValue(float rot)
            {
                if (Wrap) return Mathf.DeltaAngle(0f, rot);
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
                float r = ToRot(v) + (Style == PointerStyle.Ring ? -degrees : degrees);
                if (!Wrap) r = Mathf.Clamp(r, Mathf.Min(Rots[0], Rots[Rots.Length - 1]), Mathf.Max(Rots[0], Rots[Rots.Length - 1]));
                return ToValue(r);
            }

            public float Step(float v, float delta)
            {
                return Wrap ? Mathf.DeltaAngle(0f, v + delta) : Mathf.Clamp(v + delta, Min, Max);
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

            // target speed: 0..55 kn, clockwise from just right of the top round to just left of it
            y = AddDial(font, x0, y, inner, Strings.DialSpeed, () => Strings.TipDialSpeed, new DialSpec
            {
                Get = t => t.TargetSpeed,
                Set = (t, v) => t.TargetSpeed = v,
                Announce = VoiceCallouts.AnnounceSpeed,
                Values = new[] { 0f, 55f }, Rots = new[] { 10f, 350f },
                WheelStep = -0.5f,   // wheel toward you = more, like turning the dial clockwise
                Format = v => v.ToString("0.0", CultureInfo.InvariantCulture) + " " + Strings.UnitKnots
            }, (spec, f) => DrawSpeedScale(spec, f), Strings.UnitKnots);

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
                Format = v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) + " m"
            }, (spec, f) => DrawDistanceScale(spec, f), ""); // "hm" is printed on the rim

            // target length (Streubreite): 100..200 m printed round the right half of a turning ring;
            // the value under the fixed index at the top is the one set (as on the TDC)
            y = AddDial(font, x0, y, inner, Strings.DialLength, () => Strings.TipDialLength, new DialSpec
            {
                Get = t => t.TargetLength,
                Set = (t, v) => t.TargetLength = v,
                Style = PointerStyle.Ring,
                Visible = SpreadSelected,   // only needed for a spread (Fächer)
                Values = new[] { 100f, 200f }, Rots = new[] { 180f, 0f },
                WheelStep = -1f,     // wheel toward you = more
                Format = v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) + " m"
            }, (spec, f) => DrawLengthScale(spec, f), "");

            dialsPanel.sizeDelta = new Vector2(x0 + inner + DialPad, -y + DialPad - 6f);
            dialsFullH = dialsPanel.sizeDelta.y;
            AddResizeGrip(dialsPanel, ResizableWindow.Dials);
        }

        /// <summary>
        /// The distance scale: the boat's own TDC distance dial when it can be
        /// found (its value/angle table, stretched over the same arc), else the
        /// table read off the real dial's face.
        /// </summary>
        static void DistanceScale(out float[] hm, out float[] rot)
        {
            hm = DistanceHm;
            rot = DistanceRot;
            try
            {
                var dial = UnityEngine.Object.FindObjectOfType<TorpedoCalculatorDistanceToTargetDial>();
                if (dial == null) return;
                var table = AccessTools.Field(typeof(TorpedoCalculatorDistanceToTargetDial), "values").GetValue(dial) as Array;
                if (table == null || table.Length < 2) return;
                var type = table.GetValue(0).GetType();
                var fv = AccessTools.Field(type, "Value");
                var fa = AccessTools.Field(type, "Angle");
                int n = table.Length;
                var v = new float[n];
                var a = new float[n];
                for (int i = 0; i < n; i++)
                {
                    object e = table.GetValue(i);
                    v[i] = (float)fv.GetValue(e);
                    a[i] = (float)fa.GetValue(e);
                }
                float span = a[n - 1] - a[0];
                if (Mathf.Abs(span) < 1f) return;
                var r = new float[n];
                for (int i = 0; i < n; i++)
                {
                    if (i > 0 && (v[i] <= v[i - 1] || (a[i] - a[i - 1]) * span <= 0f)) return; // not monotonic: keep ours
                    r[i] = Mathf.Lerp(DistanceRot[0], DistanceRot[DistanceRot.Length - 1], (a[i] - a[0]) / span);
                }
                if (Mathf.Abs(v[0] - 3f) > 0.5f || Mathf.Abs(v[n - 1] - 100f) > 5f) return; // not hm 3..100
                hm = v;
                rot = r;
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " distance dial scale taken from the TDC (" + n + " points)");
            }
            catch (Exception e)
            {
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " distance dial scale: using built-in table (" + e.Message + ")");
            }
        }

        float AddDial(TMP_FontAsset font, float x, float y, float inner, string title, Func<string> tip, DialSpec spec,
                      Action<DialSpec, TMP_FontAsset> drawScale, string unit)
        {
            // everything of one dial in a group, so it can be hidden as a whole
            spec.Group = NewRect("DialGroup", dialsPanel);
            spec.Group.anchorMin = spec.Group.anchorMax = spec.Group.pivot = new Vector2(0f, 1f);
            spec.Group.sizeDelta = Vector2.zero;
            float yStart = y;
            var titleText = NewText("Title", spec.Group, font, title, HeaderFont, BoxInk);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 2f;
            titleText.alignment = TextAlignmentOptions.Center;
            PlaceV(titleText.rectTransform, x, y, inner, 14f);
            y -= 14f + 3f;

            // dial: rim, dark face, scale, pointers, hub
            var holder = NewRect("Dial", spec.Group);
            PlaceV(holder, x + (inner - DialSize) * 0.5f, y, DialSize, DialSize);
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

                default:
                    bool arrow = spec.Style == PointerStyle.Arrow;
                    spec.Needle = MakePointer(face.rectTransform, "Needle", DialNeedle, 4f, arrow);
                    if (arrow) { Disc("Hub", face.rectTransform, SharpCircleSprite(), 22f, Color.black); Disc("HubCap", face.rectTransform, SharpCircleSprite(), 10f, new Color(0.18f, 0.18f, 0.18f, 1f)); }
                    else Disc("Hub", face.rectTransform, CircleSprite(), 14f, FrameEdge);
                    break;
            }

            var relay = face.gameObject.AddComponent<DialRelay>();
            relay.Owner = this;
            relay.Spec = spec;
            AddTooltip(face.gameObject, tip);
            y -= DialSize + 6f;

            // readout
            var box = NewRect("Readout", spec.Group);
            PlaceV(box, x + 14f, y, inner - 28f, 20f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = DrumBlack;
            boxImg.raycastTarget = false;
            spec.Readout = NewText("Text", box, font, "", CtlFont + 2f, DigitLight);
            spec.Readout.alignment = TextAlignmentOptions.Center;
            var rr = spec.Readout.rectTransform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = rr.offsetMax = Vector2.zero;
            y -= 20f + 12f;

            spec.Height = yStart - y;
            dials.Add(spec);
            return y;
        }

        /// <summary>A pointer turning about the face centre; returns the rotating holder.</summary>
        static RectTransform MakePointer(RectTransform face, string name, Color color, float width, bool arrowTip)
        {
            const float headLen = 13f;
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
                head.sizeDelta = new Vector2(width * 3.2f, headLen);
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
            tri.sizeDelta = new Vector2(11f, 10f);
            tri.anchoredPosition = new Vector2(0f, DistKnob * 0.46f - 1f);
            var ti = tri.gameObject.AddComponent<Image>();
            ti.sprite = TriangleSprite();
            ti.color = DialScale;
            ti.raycastTarget = false;
            tri.SetAsLastSibling(); // over the knob
            return holder;
        }

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
                DrawTick(face, deg, 9f, 2.4f, DialScale);
                if (deg % 20 == 0) ScaleLabel(face, font, deg, Mathf.Abs(deg).ToString(), DialScale, 9f, false, DialSize * 0.5f - 19f);
            }

            // inner disc: thin white ring, red / green halves, black window at the bottom
            float inner = DialSize * 0.58f;
            Disc("InnerRing", face, SharpCircleSprite(), inner + 2f, DialScale);
            Disc("Starboard", face, HalfDiscSprite(), inner, AobGreen);
            var port = Disc("Port", face, HalfDiscSprite(), inner, AobRed);
            port.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            Disc("WindowEdge", face, SectorSprite(), inner + 2f, DialScale);
            Disc("Window", face, SectorSprite(), inner - 1f, Color.black);

            AobLabel(face, font, Strings.AobDegrees, 0f, inner * 0.42f, 0f);
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
            // fixed index at the bottom of the window, pointing in
            var index = NewRect("FineIndexHolder", face);
            index.anchorMin = index.anchorMax = index.pivot = new Vector2(0.5f, 0.5f);
            index.sizeDelta = Vector2.zero;
            index.localEulerAngles = new Vector3(0f, 0f, 180f);
            var idx = NewRect("FineIndex", index);
            idx.anchorMin = idx.anchorMax = new Vector2(0.5f, 0.5f);
            idx.pivot = new Vector2(0.5f, 1f);
            idx.sizeDelta = new Vector2(7f, 6f);
            idx.anchoredPosition = new Vector2(0f, inner * 0.5f - 1f);
            idx.localEulerAngles = new Vector3(0f, 0f, 180f);
            var idxImg = idx.gameObject.AddComponent<Image>();
            idxImg.sprite = TriangleSprite();
            idxImg.color = DialScale;
            idxImg.raycastTarget = false;

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
                    Color rc = AobFineRed, gc = DialStarboard;
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

        void DrawSpeedScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int kn = 0; kn <= 55; kn++)
            {
                bool major = kn % 5 == 0;
                DrawTick(spec.Face, spec.ToRot(kn), major ? 11f : 7f, major ? 2.2f : 1.4f, DialScale);
                if (major) ScaleLabel(spec.Face, font, spec.ToRot(kn), kn.ToString(), DialScale);
            }
        }

        // distance dial layout (as on the TDC): numbers round the rim, marks inside them pointing in
        const float DistLabelR = DialSize * 0.5f - 9f;
        const float DistTickR = DialSize * 0.5f - 17f;
        const float DistMinorInset = 3f;               // minor marks start this far inside the numbered ones
        const float DistKnob = DialSize * 0.5f;      // knob sprite size, corner to corner about 2 * DistTickR - marks

        void DrawDistanceScale(DialSpec spec, TMP_FontAsset font)
        {
            float[] labels = { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f };
            // numbered marks
            foreach (float hm in labels)
            {
                float r = spec.ToRot(hm * 100f);
                DrawTick(spec.Face, r, 7f, 2f, DialScale, DistTickR);
                ScaleLabel(spec.Face, font, r, ((int)hm).ToString(), DialScale, hm < 10f ? 9f : 10f, false, DistLabelR);
            }
            // half-way marks with a dot on their tip: x.5 up to 10, then 15, 25 ... 75 and 90
            for (int i = 0; i + 1 < labels.Length; i++)
                DrawDotTick(spec.Face, spec.ToRot((labels[i] + labels[i + 1]) * 50f));
            // very thin marks every 1 hm from 10 to 30, where the scale is widest: four each side of the dotted half-way marks
            for (int hm = 11; hm < 30; hm++)
                if (hm % 5 != 0) DrawTick(spec.Face, spec.ToRot(hm * 100f), 3f, 0.7f, DialScale, DistTickR - DistMinorInset);
            // faint circle just inside the marks: the base of the knob
            FaintRing(spec.Face, DistTickR - 9f);
            ScaleLabel(spec.Face, font, 0f, "hm", DialScale, 9f, false, DistLabelR);
        }

        /// <summary>A mark pointing in from the scale with a small dot on its inner tip.</summary>
        void DrawDotTick(RectTransform face, float rot)
        {
            const float len = 5f;
            DrawTick(face, rot, len, 1.2f, DialScale, DistTickR - DistMinorInset); // minor marks start a little inside the numbered ones
            float a = rot * Mathf.Deg2Rad, r = DistTickR - DistMinorInset - len * 0.5f + 1.5f; // a little out from half way along the mark
            var dot = Disc("Dot", face, CircleSprite(), 3.6f, DialScale);
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
        static void ApplyAobNow(TorpedoCalculator tdc, float aob)
        {
            aob = Mathf.DeltaAngle(0f, aob);
            float impact = tdc.ComputeImpactAngleForDesiredAOB(aob, tdc.ImpactAngle);
            if (float.IsNaN(impact) || float.IsInfinity(impact)) return;
            tdc.DesiredImpactAngle = impact;
            tdc.ImpactAngle = impact;
            tdc.AngleOnBow = aob; // shown right away; the TDC keeps it in step from here
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
            foreach (var d in dials)
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
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                if (d.Announce != null && !d.Dragging && !float.IsNaN(d.AnnounceAt) && Time.unscaledTime >= d.AnnounceAt)
                {
                    d.AnnounceAt = float.NaN;
                    d.Announce(tdc, v);
                }
                // a pointer turns to the value; a ring turns the value to the top
                // while dragging, show the dial's own value so it stays under the pointer (the TDC may lag or round it)
                float shown = d.Dragging && !float.IsNaN(d.DragValue) ? d.DragValue : v;
                d.Needle.localEulerAngles = new Vector3(0f, 0f, d.Style == PointerStyle.Ring ? d.ToRot(shown) : -d.ToRot(shown));

                d.Readout.text = d.Format(shown);
                if (d.Extra != null) d.Extra(d, shown);
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
            float v = spec.Get(tdc);
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
            if (spec.Dragging) spec.DragValue = next;
            spec.Set(tdc, next);
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
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, faceTransform.position);
            Vector2 d = screenPos - centre;
            float scale = faceTransform.lossyScale.x > 0f ? faceTransform.lossyScale.x : 1f;
            if (d.magnitude / scale < DialSize * 0.5f * 0.58f) return;   // inside the scale: not a click on it
            float a = Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f);

            float v;
            if (spec.Wrap) v = Mathf.DeltaAngle(0f, a);
            else
            {
                float rot = a;
                if (spec.Style == PointerStyle.Ring) rot = Mathf.Repeat(a + spec.ToRot(DialBase(spec, tdc)), 360f); // ring coordinates
                float lo = Mathf.Min(spec.Rots[0], spec.Rots[spec.Rots.Length - 1]);
                float hi = Mathf.Max(spec.Rots[0], spec.Rots[spec.Rots.Length - 1]);
                if (rot < lo - 2f || rot > hi + 2f) return;                 // off the printed scale
                v = spec.ToValue(Mathf.Clamp(rot, lo, hi));
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
                Owner.StepDial(Spec, Mathf.Sign(steps) * Mathf.Ceil(Mathf.Abs(steps)) * Spec.WheelStep * (ShiftHeld ? 0.1f : 1f));
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
