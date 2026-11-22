using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// SH3-style torpedo settings window, shown while a checked tube holds a torpedo:
    ///  - running depth: a dial scaled 0..15 m (red below 1 m and beyond 14 m), set 1..14 m in 0.25 m steps
    ///    (PlayerShipTorpedoAttackSettings.TorpedoDepth, as the stock depth dial does);
    ///  - speed: a knob with only the speeds every checked torpedo supports (slow /
    ///    medium / fast from StoredTorpedo.Speed1..3), the readout in knots; set like the
    ///    stock speed dial (TorpedoAttackSettings.TorpedoSpeed in m/s, and the TDC's
    ///    torpedo speed in knots);
    ///  - pistol: impact (AZ) or magnetic (MZ), set on every checked torpedo
    ///    (Torpedo.PistolSetting, as the stock tube panel's pistol button does).
    /// With the Torpedo Room mod installed, all three are also written to each checked
    /// torpedo's own Torpedo Room settings (which it applies at launch), and read back
    /// from the first one.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float MsToKnots = 1.943844f;
        const float DepthMin = 1f, DepthMax = 14f;   // what can be set (Torpedo Room's limit too)
        const float DepthScaleMax = 15f;            // what the scale shows
        const float DepthStep = 0.25f;              // wheel, click and drag all set quarter metres
        const float TorpScaleFont = 11f;            // numbers on the depth scale (larger than the TDC dials')
        const float TorpKnobFont = 13f;             // S / M / F and AZ / MZ on the knob faces

        RectTransform torpedoPanel;
        string torpedoSignature;
        DialSpec depthSpec;
        readonly List<KnobSpec> torpKnobs = new List<KnobSpec>();
        float[] torpSpeeds = new float[0];          // the speeds every checked torpedo has, m/s, ascending
        readonly List<Torpedo> torpSelected = new List<Torpedo>();
        readonly List<StoredTorpedo> torpStored = new List<StoredTorpedo>();

        // ------------------------------------------------------------------ what is selected

        /// <summary>The torpedoes in the checked tubes that can still be set (loaded, flooding or flooded).</summary>
        void CollectSelectedTorpedoes()
        {
            torpSelected.Clear();
            torpStored.Clear();
            var ship = Ship();
            if (ship == null || ship.TorpedoLaunchers == null) return;
            foreach (var l in ship.TorpedoLaunchers)
            {
                if (l == null || !l.IsMarkedForLaunch) continue;
                var st = l.Torpedo;
                if (st == null) continue;
                var s = l.CurrentState;
                if (s != TorpedoLauncher.State.Loaded && s != TorpedoLauncher.State.Flooding && s != TorpedoLauncher.State.Flooded) continue;
                var t = st.GetComponent<Torpedo>();
                if (t == null) continue; // mines and the like
                torpSelected.Add(t);
                torpStored.Add(st);
            }
        }

        static float[] SpeedsOf(StoredTorpedo st)
        {
            var list = new List<float>();
            foreach (float v in new[] { (float)st.Speed1, (float)st.Speed2, (float)st.Speed3 })
            {
                if (v <= 0.01f) continue;
                bool dup = false;
                foreach (var x in list) if (Mathf.Abs(x - v) < 0.01f) { dup = true; break; }
                if (!dup) list.Add(v);
            }
            list.Sort();
            return list.ToArray();
        }

        /// <summary>Speeds every checked torpedo supports (as the stock speed dial works it out).</summary>
        float[] CommonSpeeds()
        {
            if (torpStored.Count == 0) return new float[0];
            var common = new List<float>(SpeedsOf(torpStored[0]));
            for (int i = 1; i < torpStored.Count; i++)
            {
                var other = SpeedsOf(torpStored[i]);
                common.RemoveAll(v => Array.FindIndex(other, o => Mathf.Abs(o - v) < 0.01f) < 0);
            }
            return common.ToArray();
        }

        /// <summary>S / M / F (L / M / S) by where the speed sits among that torpedo's own speeds.</summary>
        string SpeedLetter(float v)
        {
            var own = torpStored.Count > 0 ? SpeedsOf(torpStored[0]) : torpSpeeds;
            int i = Array.FindIndex(own, o => Mathf.Abs(o - v) < 0.01f);
            int n = own.Length;
            if (n <= 1 || i < 0) return Strings.SpeedMedium;
            if (i == 0) return Strings.SpeedSlow;
            if (i == n - 1) return Strings.SpeedFast;
            return Strings.SpeedMedium;
        }

        static string Knots(float ms) { return Mathf.RoundToInt(ms * MsToKnots).ToString(CultureInfo.InvariantCulture) + " kn"; }

        // ------------------------------------------------------------------ setting

        void SetTorpedoSpeed(float ms)
        {
            var ship = Ship();
            if (ship == null) return;
            ship.TorpedoAttackSettings.TorpedoSpeed = ms;
            foreach (var st in torpStored) TorpedoRoomLink.SetSpeed(st, ms);
            var tdc = Tdc();
            if (tdc != null && tdc.enabled)
            {
                tdc.TorpedoSpeed = ms * MsToKnots;
                tdc.TorpedoSpeedTarget = ms * MsToKnots;
            }
        }

        int CurrentSpeedIndex()
        {
            var ship = Ship();
            if (ship == null || torpSpeeds.Length == 0) return 0;
            float cur = ship.TorpedoAttackSettings.TorpedoSpeed;
            float own;
            if (torpStored.Count > 0 && TorpedoRoomLink.TryGetSpeed(torpStored[0], out own)) cur = own;
            int best = 0;
            for (int i = 1; i < torpSpeeds.Length; i++)
                if (Mathf.Abs(torpSpeeds[i] - cur) < Mathf.Abs(torpSpeeds[best] - cur)) best = i;
            return best;
        }

        int CurrentPistol()
        {
            int p;
            if (torpStored.Count > 0 && TorpedoRoomLink.TryGetPistol(torpStored[0], out p)) return p;
            return torpSelected.Count > 0 && torpSelected[0] != null ? (int)torpSelected[0].PistolSetting : 0;
        }

        void SetPistol(int v)
        {
            for (int i = 0; i < torpSelected.Count; i++)
            {
                var t = torpSelected[i];
                if (t == null) continue;
                // Torpedo Room puts its own pistol back on the torpedo at launch, so set it there too
                if (!TorpedoRoomLink.SetPistol(torpStored[i], t, v)) t.PistolSetting = (Torpedo.TorpedoPistolSetting)v;
            }
        }

        float CurrentDepth()
        {
            float d;
            if (torpStored.Count > 0 && TorpedoRoomLink.TryGetDepth(torpStored[0], out d)) return d;
            var s = Ship();
            return s != null ? s.TorpedoAttackSettings.TorpedoDepth : DepthMin;
        }

        /// <summary>Depth in 0.25 m steps, within 1..14 m.</summary>
        static float SnapDepth(float v)
        {
            return Mathf.Clamp(Mathf.Round(v / DepthStep) * DepthStep, DepthMin, DepthMax);
        }

        void SetDepth(float v)
        {
            float d = SnapDepth(v);
            var s = Ship();
            if (s != null) s.TorpedoAttackSettings.TorpedoDepth = d;
            foreach (var st in torpStored) TorpedoRoomLink.SetDepth(st, d);
        }

        // ------------------------------------------------------------------ build

        void BuildTorpedo(TMP_FontAsset font)
        {
            torpKnobs.Clear();
            torpSpeeds = CommonSpeeds();
            string click = AnyBoxKnobSound(); // the boat has no knob for these: borrow the fire-control box click

            torpedoPanel = NewRect("TorpedoSettings", transform);
            torpedoPanel.anchorMin = torpedoPanel.anchorMax = torpedoPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(torpedoPanel);
            torpedoPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(torpedoPanel.gameObject, false).Torpedo = true;

            float x = DialPad;
            BuildDepthDial(font, x);
            x += DialSize + 16f + KnobGap;

            // speed: only the speeds every checked torpedo supports
            if (torpSpeeds.Length > 0)
            {
                int n = torpSpeeds.Length;
                var values = new int[n];
                var rots = new float[n];
                var labels = new string[n];
                var names = new string[n];
                float step = n >= 3 ? 60f : 45f;
                for (int i = 0; i < n; i++)
                {
                    values[i] = i;
                    rots[i] = Mathf.Repeat((i - (n - 1) * 0.5f) * step * (n == 2 ? 2f : 1f), 360f);
                    labels[i] = SpeedLetter(torpSpeeds[i]);
                    names[i] = Knots(torpSpeeds[i]);
                }
                x += KnobRim;
                AddKnob(font, torpedoPanel, torpKnobs, x, Strings.TorpSpeed, () => Strings.TipTorpSpeed, new KnobSpec
                {
                    Values = values, Rots = rots, Labels = labels, Names = names, LabelSize = TorpKnobFont, Sound = click,
                    Get = b => CurrentSpeedIndex(),
                    Set = (b, v) => { if (v >= 0 && v < torpSpeeds.Length) SetTorpedoSpeed(torpSpeeds[v]); }
                });
                x += KnobSize + KnobGap;
            }

            // pistol: impact or magnetic, on every checked torpedo
            if (torpSelected.Count > 0)
            {
                x += torpSpeeds.Length > 0 ? 0f : KnobRim;
                AddKnob(font, torpedoPanel, torpKnobs, x, Strings.TorpPistol, () => Strings.TipTorpPistol, new KnobSpec
                {
                    Values = new[] { (int)Torpedo.TorpedoPistolSetting.Contact, (int)Torpedo.TorpedoPistolSetting.Magnetic },
                    Rots = new[] { 315f, 45f },
                    Labels = new[] { Strings.PistolImpactShort, Strings.PistolMagneticShort },
                    Names = new[] { Strings.PistolImpact, Strings.PistolMagnetic },
                    LabelSize = TorpKnobFont, Sound = click,
                    Get = b => CurrentPistol(),
                    Set = (b, v) => SetPistol(v)
                });
                x += KnobSize + KnobGap;
            }

            float h = DialPad + ControlColumnH + DialPad;
            torpedoPanel.sizeDelta = new Vector2(x - KnobGap + KnobRim + DialPad, h);
            AddResizeGrip(torpedoPanel, ResizableWindow.Torpedo);
            RaiseOverlays();
        }

        /// <summary>The running depth dial: like the TDC dials, scaled 0..15 m, settable 1..14 m.</summary>
        void BuildDepthDial(TMP_FontAsset font, float x)
        {
            float inner = DialSize + 16f;
            float y = -DialPad;
            ControlTitle(torpedoPanel, font, Strings.TorpDepth, x, y, inner);
            y -= TitleH + TitleGap;

            var holder = NewRect("Dial", torpedoPanel);
            PlaceV(holder, x + (inner - DialSize) * 0.5f, y - 3f, DialSize, DialSize); // the black rim reaches 3 beyond
            Disc("Edge", holder, SharpCircleSprite(), DialSize + 6f, Color.black);
            var face = Disc("Face", holder, SharpCircleSprite(), DialSize + 3f, DialFace);
            face.raycastTarget = true;
            face.alphaHitTestMinimumThreshold = 0.5f;

            depthSpec = new DialSpec
            {
                Get = t => CurrentDepth(),
                Set = (t, v) => SetDepth(v),
                Values = new[] { 0f, DepthScaleMax }, Rots = new[] { -135f, 135f },
                LimitMin = DepthMin, LimitMax = DepthMax,
                WheelStep = -DepthStep,   // wheel toward you = deeper
                Format = v => v.ToString("0.00", CultureInfo.InvariantCulture) + " m",
                Style = PointerStyle.Arrow,
                Face = face.rectTransform
            };

            // red where the torpedo may not run: above 1 m and below 14 m
            DrawArc(face.rectTransform, depthSpec.ToRot(0f), depthSpec.ToRot(DepthMin), AobRed);
            DrawArc(face.rectTransform, depthSpec.ToRot(DepthMax), depthSpec.ToRot(DepthScaleMax), AobRed);
            // the settable range 1..14 m: a numbered mark every metre, a short mark at each half metre
            for (int h = (int)(DepthMin * 2f); h <= (int)(DepthMax * 2f); h++)
            {
                float m = h * 0.5f;
                bool whole = h % 2 == 0;
                DrawTick(face.rectTransform, depthSpec.ToRot(m), whole ? 9f : 5f, whole ? 2f : 1.2f, DialScale);
                if (whole) ScaleLabel(face.rectTransform, font, depthSpec.ToRot(m), ((int)m).ToString(), DialScale, TorpScaleFont, false, DialSize * 0.5f - 20f);
            }
            var unit = NewText("Unit", face.rectTransform, font, "m", TorpScaleFont, DialScale);
            unit.alignment = TextAlignmentOptions.Center;
            var ur = unit.rectTransform;
            ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(0.5f, 0.5f);
            ur.sizeDelta = new Vector2(30f, 12f);
            ur.anchoredPosition = new Vector2(0f, -DialSize * 0.22f);

            depthSpec.Needle = MakePointer(face.rectTransform, "Needle", DialNeedle, 4f, true);
            Disc("Hub", face.rectTransform, SharpCircleSprite(), 16f, Color.black);

            var relay = face.gameObject.AddComponent<DialRelay>();
            relay.Owner = this;
            relay.Spec = depthSpec;
            AddTooltip(face.gameObject, () => Strings.TipTorpDepth);
            y -= ControlH + ReadoutGap;

            depthSpec.Readout = ReadoutBox(torpedoPanel, font, x + inner * 0.5f, y);
        }

        static readonly Dictionary<string, Sprite> arcSprites = new Dictionary<string, Sprite>();

        /// <summary>A coloured band round the rim of a dial face between two pointer angles.</summary>
        void DrawArc(RectTransform face, float fromRot, float toRot, Color color)
        {
            string key = Mathf.RoundToInt(fromRot) + "_" + Mathf.RoundToInt(toRot);
            Sprite sprite;
            if (!arcSprites.TryGetValue(key, out sprite))
            {
                const int n = 260;
                float c = n * 0.5f, outer = c - 6f, inner = c - 22f;
                float a0 = Mathf.Min(fromRot, toRot), a1 = Mathf.Max(fromRot, toRot);
                sprite = MakeShapeSprite(n, n, (px, py) =>
                {
                    float dx = px - c, dy = py - c;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > outer * outer || d2 < inner * inner) return false;
                    float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // clockwise from the top
                    return a >= a0 && a <= a1;
                });
                arcSprites[key] = sprite;
            }
            var r = NewRect("Arc", face);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(DialSize, DialSize);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = new Color(color.r, color.g, color.b, 0.85f);
            img.raycastTarget = false;
        }

        // ------------------------------------------------------------------ runtime

        /// <summary>Shown while a checked tube holds a torpedo; rebuilt when the choice of speeds changes.</summary>
        void ValidateTorpedo(bool wanted)
        {
            CollectSelectedTorpedoes();
            bool have = wanted && GyroConfig.ShowTorpedo && torpSelected.Count > 0;
            if (!have)
            {
                if (torpedoPanel != null && torpedoPanel.gameObject.activeSelf) torpedoPanel.gameObject.SetActive(false);
                return;
            }
            var speeds = CommonSpeeds();
            string sig = speeds.Length + ":" + string.Join(",", Array.ConvertAll(speeds, v => v.ToString("0.00", CultureInfo.InvariantCulture)))
                         + ":" + (torpStored.Count > 0 ? string.Join(",", Array.ConvertAll(SpeedsOf(torpStored[0]), v => v.ToString("0.00", CultureInfo.InvariantCulture))) : "");
            if (torpedoPanel == null || sig != torpedoSignature)
            {
                if (torpedoPanel != null) Destroy(torpedoPanel.gameObject);
                torpedoSignature = sig;
                BuildTorpedo(ResolveFont());
            }
            if (!torpedoPanel.gameObject.activeSelf) torpedoPanel.gameObject.SetActive(true);

            // depth needle and readout (the dial's own value while it is dragged)
            if (depthSpec != null)
            {
                var tdc = Tdc();
                float v = depthSpec.Get(tdc);
                float shown = depthSpec.Dragging && !float.IsNaN(depthSpec.DragValue) ? SnapDepth(depthSpec.DragValue) : v;
                depthSpec.Needle.localEulerAngles = new Vector3(0f, 0f, -depthSpec.ToRot(shown));
                depthSpec.Readout.text = depthSpec.Format(shown);
            }
            UpdateKnobs(torpKnobs, Boxes());
            PositionTorpedo();
        }

        void PositionTorpedo()
        {
            if (torpedoPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.TorpedoX, Screen.height * GyroConfig.TorpedoY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                torpedoPanel.anchoredPosition = local + new Vector2(GyroConfig.TorpedoOffsetX, GyroConfig.TorpedoOffsetY);
        }
    }
}
