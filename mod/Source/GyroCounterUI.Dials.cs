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
        const float AobWheelCommitDelay = 0.6f; // wheel on AOB: commit after the wheel stops
        const float GhostTimeout = 20f;

        static readonly Color DialFace = new Color(0.07f, 0.07f, 0.07f, 1f);
        static readonly Color DialScale = new Color(0.93f, 0.90f, 0.82f, 1f);
        static readonly Color DialPort = new Color(0.95f, 0.35f, 0.30f, 1f);
        static readonly Color DialStarboard = new Color(0.40f, 0.90f, 0.45f, 1f);
        static readonly Color DialNeedle = new Color(0.86f, 0.62f, 0.32f, 1f);   // brass, like the TDC's pointers
        static readonly Color DialGhost = new Color(0.97f, 0.95f, 0.88f, 0.55f);

        // the real distance dial: hm -> degrees clockwise from the top (read off the TDC face)
        static readonly float[] DistanceHm = { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f };
        static readonly float[] DistanceRot = { 12f, 38f, 60f, 78f, 93f, 106f, 117f, 127f, 182f, 218f, 245f, 268f, 289f, 306f, 321f, 345f };

        RectTransform dialsPanel;
        CanvasGroup dialsGroup;
        readonly List<DialSpec> dials = new List<DialSpec>();

        /// <summary>One TDC dial: how it reads/writes the TDC and how it is drawn.</summary>
        sealed class DialSpec
        {
            public Func<TorpedoCalculator, float> Get;
            public Action<TorpedoCalculator, float> Set;      // direct dials
            public Action<TorpedoCalculator, float> Commit;   // AOB: send the ghost pointer's value on release
            public Func<TorpedoCalculator, bool> Locked;
            public float[] Values, Rots;                      // value -> pointer angle (clockwise from the top), increasing
            public bool Wrap;                                 // AOB: ±180 is the same place
            public float WheelStep;
            public Func<float, string> Format;
            public RectTransform Needle, Ghost;
            public TextMeshProUGUI Readout;
            public RectTransform Face;

            // ghost pointer (AOB)
            public float Target = float.NaN;
            public bool Dragging;
            public float CommitAt = float.NaN;
            public float GhostUntil;

            public float Min { get { return Values[0]; } }
            public float Max { get { return Values[Values.Length - 1]; } }

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
                if (x <= from[0]) return to[0];
                for (int i = 1; i < from.Length; i++)
                    if (x <= from[i]) return Mathf.Lerp(to[i - 1], to[i], (x - from[i - 1]) / (from[i] - from[i - 1]));
                return to[to.Length - 1];
            }

            /// <summary>Turn by a pointer angle; the value follows the scale under the pointer.</summary>
            public float Turn(float v, float degrees)
            {
                float r = ToRot(v) + degrees;
                if (!Wrap) r = Mathf.Clamp(r, Rots[0], Rots[Rots.Length - 1]);
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
            Frame(dialsPanel, true);
            dialsPanel.gameObject.AddComponent<WheelGuard>();
            dialsGroup = dialsPanel.gameObject.AddComponent<CanvasGroup>();
            AddDrag(dialsPanel.gameObject, false).Dials = true;

            float inner = DialSize + 16f;
            float y = -DialPad;

            // AOB: full circle, 0 at the top, starboard clockwise (green), port anticlockwise (red)
            y = AddDial(font, y, inner, Strings.DialAob, () => Strings.TipDialAob, new DialSpec
            {
                Get = t => t.AngleOnBow,
                Commit = CommitAob,
                Locked = t => t.IsLockingMechanismTriggered,
                Values = new[] { -180f, 180f }, Rots = new[] { -180f, 180f }, Wrap = true,
                WheelStep = 1f,
                Format = FormatAob
            }, (spec, f) => DrawAobScale(spec, f), "");

            // target speed: 0..55 kn, clockwise from just right of the top round to just left of it
            y = AddDial(font, y, inner, Strings.DialSpeed, () => Strings.TipDialSpeed, new DialSpec
            {
                Get = t => t.TargetSpeed,
                Set = (t, v) => t.TargetSpeed = v,
                Values = new[] { 0f, 55f }, Rots = new[] { 10f, 350f },
                WheelStep = -0.5f,   // wheel toward you = more, like turning the dial clockwise
                Format = v => v.ToString("0.0", CultureInfo.InvariantCulture) + " " + Strings.UnitKnots
            }, (spec, f) => DrawSpeedScale(spec, f), Strings.UnitKnots);

            // target distance: 3..100 hm on the real dial's stretched scale
            float[] hm, rot;
            DistanceScale(out hm, out rot);
            var meters = new float[hm.Length];
            for (int i = 0; i < hm.Length; i++) meters[i] = hm[i] * 100f;
            y = AddDial(font, y, inner, Strings.DialDistance, () => Strings.TipDialDistance, new DialSpec
            {
                Get = t => t.DistanceToTarget,
                Set = (t, v) => t.DistanceToTarget = v,
                Values = meters, Rots = rot,
                WheelStep = -100f,   // wheel toward you = more
                Format = v => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) + " m"
            }, (spec, f) => DrawDistanceScale(spec, f), "hm");

            dialsPanel.sizeDelta = new Vector2(inner + DialPad * 2f, -y + DialPad - 6f);
            dialsPanel.localScale = Vector3.one * GyroConfig.Scale;
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

        float AddDial(TMP_FontAsset font, float y, float inner, string title, Func<string> tip, DialSpec spec,
                      Action<DialSpec, TMP_FontAsset> drawScale, string unit)
        {
            var titleText = NewText("Title", dialsPanel, font, title, HeaderFont, LabelColor);
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 2f;
            titleText.alignment = TextAlignmentOptions.Center;
            PlaceV(titleText.rectTransform, DialPad, y, inner, 14f);
            y -= 14f + 3f;

            // dial: rim, dark face, scale, pointers, hub
            var holder = NewRect("Dial", dialsPanel);
            PlaceV(holder, DialPad + (inner - DialSize) * 0.5f, y, DialSize, DialSize);
            Disc("Bezel", holder, CircleSprite(), DialSize + 6f, FrameEdge);
            var face = Disc("Face", holder, CircleSprite(), DialSize, DialFace);
            face.raycastTarget = true; // the grab area
            spec.Face = face.rectTransform;
            drawScale(spec, font);

            if (unit.Length > 0)
            {
                var u = NewText("Unit", face.rectTransform, font, unit, 9f, DialScale);
                u.alignment = TextAlignmentOptions.Center;
                var ur = u.rectTransform;
                ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(0.5f, 0.5f);
                ur.sizeDelta = new Vector2(50f, 12f);
                ur.anchoredPosition = new Vector2(0f, -DialSize * 0.2f);
            }

            if (spec.Commit != null) spec.Ghost = MakePointer(face.rectTransform, "Ghost", DialGhost, 3f);
            spec.Needle = MakePointer(face.rectTransform, "Needle", DialNeedle, 4f);
            if (spec.Ghost != null) spec.Ghost.gameObject.SetActive(false);
            Disc("Hub", face.rectTransform, CircleSprite(), 14f, FrameEdge);

            var relay = face.gameObject.AddComponent<DialRelay>();
            relay.Owner = this;
            relay.Spec = spec;
            AddTooltip(face.gameObject, tip);
            y -= DialSize + 6f;

            // readout
            var box = NewRect("Readout", dialsPanel);
            PlaceV(box, DialPad + 14f, y, inner - 28f, 20f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = DrumBlack;
            boxImg.raycastTarget = false;
            spec.Readout = NewText("Text", box, font, "", CtlFont + 2f, DigitLight);
            spec.Readout.alignment = TextAlignmentOptions.Center;
            var rr = spec.Readout.rectTransform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = rr.offsetMax = Vector2.zero;
            y -= 20f + 12f;

            dials.Add(spec);
            return y;
        }

        /// <summary>A pointer turning about the face centre; returns the rotating holder.</summary>
        static RectTransform MakePointer(RectTransform face, string name, Color color, float width)
        {
            var holder = NewRect(name, face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            var hand = NewRect("Hand", holder);
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.pivot = new Vector2(0.5f, 0.12f);
            hand.sizeDelta = new Vector2(width, DialSize * 0.5f - 10f);
            hand.anchoredPosition = Vector2.zero;
            var img = hand.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return holder;
        }

        /// <summary>A tick on the face at a pointer angle (clockwise from the top).</summary>
        void DrawTick(RectTransform face, float rot, float length, float width, Color color)
        {
            var holder = NewRect("TickHolder", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            holder.localEulerAngles = new Vector3(0f, 0f, -rot);
            var t = NewRect("Tick", holder);
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 1f);
            t.sizeDelta = new Vector2(width, length);
            t.anchoredPosition = new Vector2(0f, DialSize * 0.5f - 3f);
            var img = t.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        void ScaleLabel(RectTransform face, TMP_FontAsset font, float rot, string text, Color color, float size = 10f)
        {
            float r = DialSize * 0.5f - 22f;
            float a = rot * Mathf.Deg2Rad;
            var label = NewText("Label", face, font, text, size, color);
            label.alignment = TextAlignmentOptions.Center;
            var lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(28f, 14f);
            lr.anchoredPosition = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
        }

        void DrawAobScale(DialSpec spec, TMP_FontAsset font)
        {
            for (int deg = -170; deg <= 180; deg += 10)
            {
                bool major = deg % 30 == 0;
                Color c = deg == 0 || deg == 180 ? DialScale : (deg < 0 ? DialPort : DialStarboard);
                DrawTick(spec.Face, deg, major ? 10f : 5f, major ? 2f : 1.2f, c);
                if (major) ScaleLabel(spec.Face, font, deg, Mathf.Abs(deg).ToString(), c);
            }
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

        void DrawDistanceScale(DialSpec spec, TMP_FontAsset font)
        {
            // fine marks: every 0.5 hm up to 10, every 5 hm above
            for (float hm = 3f; hm <= 100.01f; hm += hm < 10f ? 0.5f : 5f)
                DrawTick(spec.Face, spec.ToRot(hm * 100f), 4f, 1.2f, DialScale);
            foreach (float hm in new[] { 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 100f })
            {
                float r = spec.ToRot(hm * 100f);
                DrawTick(spec.Face, r, 9f, 2f, DialScale);
                ScaleLabel(spec.Face, font, r, ((int)hm).ToString(), DialScale, hm < 10f ? 9f : 10f);
            }
        }

        static string FormatAob(float v)
        {
            float a = Mathf.Abs(v);
            string side = a < 0.5f || a > 179.5f ? "" : (v < 0f ? Strings.Port : Strings.Starboard) + " ";
            return side + Mathf.RoundToInt(a).ToString("000") + "°";
        }

        /// <summary>The stock AOB dial's release: let the TDC's AOB motor turn to the new AOB.</summary>
        static void CommitAob(TorpedoCalculator tdc, float aob)
        {
            float impact = tdc.ComputeImpactAngleForDesiredAOB(Mathf.DeltaAngle(0f, aob), tdc.ImpactAngle);
            if (float.IsNaN(impact) || float.IsInfinity(impact)) return;
            tdc.DesiredImpactAngle = impact;
        }

        // ------------------------------------------------------------------ runtime

        void UpdateDials(TorpedoCalculator tdc, bool manned)
        {
            if (dialsPanel == null) return;
            dialsGroup.alpha = manned ? 1f : 0.45f;
            dialsGroup.interactable = manned;
            dialsGroup.blocksRaycasts = manned;
            foreach (var d in dials)
            {
                float v = d.Get(tdc);
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                d.Needle.localEulerAngles = new Vector3(0f, 0f, -d.ToRot(v));

                if (d.Ghost != null)
                {
                    // mouse-wheel turns are sent once the wheel stops
                    if (!d.Dragging && !float.IsNaN(d.CommitAt) && Time.unscaledTime >= d.CommitAt)
                    {
                        d.CommitAt = float.NaN;
                        if (!float.IsNaN(d.Target)) { d.Commit(tdc, d.Target); d.GhostUntil = Time.unscaledTime + GhostTimeout; }
                    }
                    bool pending = d.Dragging || !float.IsNaN(d.CommitAt);
                    // after release the ghost stays until the AOB motor gets there
                    bool ghost = !float.IsNaN(d.Target)
                                 && (pending || (Time.unscaledTime < d.GhostUntil && Mathf.Abs(Mathf.DeltaAngle(v, d.Target)) > 0.5f));
                    if (!ghost && !pending) d.Target = float.NaN;
                    if (d.Ghost.gameObject.activeSelf != ghost) d.Ghost.gameObject.SetActive(ghost);
                    if (ghost) d.Ghost.localEulerAngles = new Vector3(0f, 0f, -d.ToRot(d.Target));
                    d.Readout.text = ghost ? d.Format(v) + " > " + d.Format(d.Target) : d.Format(v);
                }
                else d.Readout.text = d.Format(v);
            }
        }

        void PositionDials()
        {
            if (dialsPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.DialsX, Screen.height * GyroConfig.DialsY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                dialsPanel.anchoredPosition = local + new Vector2(GyroConfig.DialsOffsetX, GyroConfig.DialsOffsetY);
        }

        /// <summary>Starting value for a turn: the ghost's target while one is pending, else the TDC.</summary>
        float DialBase(DialSpec spec, TorpedoCalculator tdc)
        {
            if (spec.Commit != null && !float.IsNaN(spec.Target)) return spec.Target;
            return spec.Get(tdc);
        }

        void SetDial(DialSpec spec, TorpedoCalculator tdc, float v)
        {
            if (spec.Commit != null) spec.Target = v; // ghost pointer; sent on release
            else spec.Set(tdc, v);
        }

        void TurnDial(DialSpec spec, float degrees)
        {
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            float v = DialBase(spec, tdc);
            if (float.IsNaN(v)) return;
            SetDial(spec, tdc, spec.Turn(v, degrees));
        }

        void StepDial(DialSpec spec, float delta)
        {
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            float v = DialBase(spec, tdc);
            if (float.IsNaN(v)) return;
            SetDial(spec, tdc, spec.Step(v, delta));
            if (spec.Commit != null && !spec.Dragging) spec.CommitAt = Time.unscaledTime + AobWheelCommitDelay;
        }

        void BeginDialDrag(DialSpec spec)
        {
            if (spec.Commit == null) return;
            var tdc = Tdc();
            if (tdc == null || (spec.Locked != null && spec.Locked(tdc))) return;
            spec.Dragging = true;
            spec.CommitAt = float.NaN;
            if (float.IsNaN(spec.Target)) spec.Target = spec.Get(tdc);
        }

        void EndDialDrag(DialSpec spec)
        {
            if (spec.Commit == null || !spec.Dragging) return;
            spec.Dragging = false;
            var tdc = Tdc();
            if (tdc == null || float.IsNaN(spec.Target)) return;
            spec.Commit(tdc, spec.Target);
            spec.GhostUntil = Time.unscaledTime + GhostTimeout;
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
        sealed class DialRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
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
