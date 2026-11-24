using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// The fire-control switch box (Schaltkasten T-Abf.-Anl.) as a window near
    /// the bottom of the scope view: Einzelschuß (single tube / spread), Fächerschuß
    /// (salvo preset) and T.Abf. Schalter (launch station: bridge / tower / off).
    ///
    /// Each switch offers exactly the positions of the boat's own knob
    /// (TorpedoFireControlBoxes*Knob.presets), so it adapts to the sub type, and
    /// turning it does what the knob does: set the box value (plus
    /// ValidateMarkedLaunchersFromSalvoSettings for tube and salvo). The 3D knobs
    /// follow through the box's change events. Salvo labels come from the
    /// game's own (localized) knob tooltips.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float KnobSize = 112f;
        const float KnobGap = 24f;
        const float KnobRim = 10f;   // the black face reaches this far beyond the knob's layout square
        const float KnobLabelRadius = KnobSize * 0.5f - 13f;
        const float KnobHandleLength = KnobSize * 0.52f;
        const float SwitchRetry = 2f;

        static readonly Color KnobHandle = new Color(0.04f, 0.04f, 0.04f, 1f);
        static readonly Color KnobHandleEdge = new Color(0.30f, 0.30f, 0.30f, 1f);
        static readonly Color KnobArrow = new Color(0.95f, 0.95f, 0.93f, 1f);

        static readonly string[] Roman = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        static readonly Regex RomanToken = new Regex(@"\b(VIII|VII|VI|IV|V|III|II|I)\b");
        static readonly Regex Tags = new Regex("<[^>]*>");

        RectTransform switchesPanel;
        TorpedoFireControlBoxes switchesBoxes;
        float switchesRetryAt;
        readonly List<KnobSpec> knobs = new List<KnobSpec>();

        sealed class KnobSpec
        {
            public int[] Values;
            public float[] Rots;              // clockwise from the top
            public string[] Labels;           // printed on the face
            public string[] Names;            // full names for the readout (null = Labels)
            public bool InnerTicks;           // marks round the hub at every position
            public float LabelSize = 10f;     // face label font size (switch box knobs)
            public Func<bool> Locked;         // can't be turned while this is true
            public string Sound;              // the boat's knob click (its PresetDial audio keyword)
            public Func<TorpedoFireControlBoxes, int> Get;
            public Action<TorpedoFireControlBoxes, int> Set;
            public RectTransform Handle;
            public TextMeshProUGUI Readout;
            public RectTransform Face;

            public int IndexOf(int v) { return Array.IndexOf(Values, v); }

            public int Nearest(float rot)
            {
                int best = 0;
                float bestD = 999f;
                for (int i = 0; i < Rots.Length; i++)
                {
                    float d = Mathf.Abs(Mathf.DeltaAngle(rot, Rots[i]));
                    if (d < bestD) { bestD = d; best = i; }
                }
                return best;
            }
        }

        // ------------------------------------------------------------------ knob data from the boat

        static T SceneKnob<T>(TorpedoFireControlBoxes boxes) where T : Component
        {
            try
            {
                var f = AccessTools.Field(typeof(T), "torpedoFireControlBoxes");
                foreach (var k in Resources.FindObjectsOfTypeAll<T>())
                {
                    if (k == null || !k.gameObject.scene.IsValid()) continue;
                    if (f == null || ReferenceEquals(f.GetValue(k), boxes)) return k;
                }
            }
            catch { }
            return null;
        }

        /// <summary>The preset values the boat's knob offers, in ascending order.</summary>
        static int[] KnobValues(Component knob)
        {
            if (knob == null) return null;
            try
            {
                var arr = AccessTools.Field(typeof(PresetDial), "presets").GetValue(knob) as Array;
                if (arr == null || arr.Length == 0) return null;
                var fv = AccessTools.Field(arr.GetType().GetElementType(), "Value");
                var list = new List<int>();
                foreach (var p in arr)
                {
                    int v = (int)fv.GetValue(p);
                    if (!list.Contains(v)) list.Add(v);
                }
                list.Sort();
                return list.ToArray();
            }
            catch { return null; }
        }

        static Locale gameLocale;

        /// <summary>A game localization string, or null if it isn't there.</summary>
        static string GameText(string key)
        {
            try
            {
                if (gameLocale == null)
                {
                    var fw = DWS.Common.InjectionFramework.InjectionFramework.Instance;
                    if (fw != null) gameLocale = fw.GetInstance<Locale>();
                }
                if (gameLocale == null) return null;
                string s = gameLocale[key];
                if (string.IsNullOrEmpty(s) || s == key || s.Contains(key)) return null;
                return Tags.Replace(s, "").Trim();
            }
            catch { return null; }
        }

        /// <summary>Tube switch position name; <paramref name="onDial"/> = the short form printed on the face.</summary>
        static string TubeLabel(int v, bool onDial)
        {
            if (v >= 1 && v <= 8) return Strings.TubeWord + " " + Roman[v];
            if (v == (int)SelectedTorpedoTube.SalvoCourse) return onDial ? Strings.SpreadCourseShort : Strings.SpreadCourse;
            if (v == (int)SelectedTorpedoTube.SalvoTurning) return onDial ? Strings.SpreadTurningShort : Strings.SpreadTurning;
            return Strings.SwitchOff;
        }

        /// <summary>"Rohr I II" from the game's tooltip for that salvo preset.</summary>
        // Type IX salvo presets 1..8, as its box marks them (the game fires: 1 = all four bow tubes,
        // 2 = I II IV, 3 = I III IV, 4 = I IV, 5 = II III, 6 = I III, 7 = II IV, 8 = the stern tubes)
        static readonly string[] TypeIXSalvos = { "I III II IV", "I II IV", "I III IV", "I IV", "II III", "I III", "II IV", "V VI" };

        static bool IsTypeIX(TorpedoFireControlBoxes boxes)
        {
            try { return boxes != null && boxes.TubesSetup == ShipTubesSetup.TypeIX; }
            catch { return false; }
        }

        /// <summary>Type IX salvo knob: preset 1 at the bottom, the rest clockwise, evenly (it has no Off).</summary>
        static float[] TypeIXSalvoRots(int[] values)
        {
            int n = values.Length;
            var order = new List<int>();
            foreach (var v in values) if (v != 0) order.Add(v);
            order.Sort();
            if (Array.IndexOf(values, 0) >= 0) order.Add(0);
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = Mathf.Repeat(180f + order.IndexOf(values[i]) * 360f / Mathf.Max(1, n), 360f);
            return r;
        }

        static string SalvoLabel(TorpedoFireControlBoxes boxes, int v)
        {
            if (v == 0) return Strings.SwitchOff;
            if (IsTypeIX(boxes) && v >= 1 && v <= TypeIXSalvos.Length) return Strings.TubeWord + " " + TypeIXSalvos[v - 1];
            string key = (boxes.TubesSetup != ShipTubesSetup.TypeVII ? "Tooltips/Torpedo Fire Control Boxes Salvo Type II " : "Tooltips/Torpedo Fire Control Boxes Salvo ") + v;
            string text = GameText(key);
            if (text != null)
            {
                var sb = new StringBuilder();
                foreach (Match m in RomanToken.Matches(text)) sb.Append(' ').Append(m.Value);
                if (sb.Length > 0) return Strings.TubeWord + sb;
            }
            return "#" + v;
        }

        static string LeverLabel(int v)
        {
            switch ((ActiveLaunchLever)v)
            {
                case ActiveLaunchLever.UZO: return Strings.LeverBridge;
                case ActiveLaunchLever.ConningTowerInterior: return Strings.LeverTower;
                default: return Strings.SwitchOff;
            }
        }

        /// <summary>The Type II's switch box (its knobs are laid out differently).</summary>
        static bool IsTypeII(TorpedoFireControlBoxes boxes)
        {
            try { return boxes != null && boxes.TubesSetup == ShipTubesSetup.TypeII; }
            catch { return false; }
        }

        /// <summary>
        /// Whether a Type II fires from the bridge: the IID does, the IIA (no UZO on the bridge)
        /// doesn't. Told by the boat's name ("Type IIA ..." / "Type IID ..."): the IID's 3D box is
        /// painted Aus / Turm, and neither its switch box's UZO reference nor its knob's positions
        /// tell the two apart.
        /// </summary>
        bool HasUzo(TorpedoFireControlBoxes boxes, int[] levers)
        {
            // the interior the switch box is in ("Type IIA (Interior)"), then the boat ("Type IIA (Player)")
            var names = new List<string>();
            try { if (boxes != null) names.Add(boxes.transform.root.name); } catch { }
            try { var ship = Ship(); if (ship != null) names.Add(ship.name); } catch { }
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var m = System.Text.RegularExpressions.Regex.Match(name, @"II\s*([A-D])\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                bool iia = m.Groups[1].Value.ToUpperInvariant() == "A";
                if (GyroConfig.DebugLog)
                    Debug.Log(ScopeTdcControlsMod.Tag + " Type II firing station: '" + name + "' -> " + (iia ? "IIA (Off / Tower)" : "bridge / Tower"));
                return !iia;
            }
            if (GyroConfig.DebugLog)
                Debug.Log(ScopeTdcControlsMod.Tag + " Type II firing station: boat not named (" + string.Join(", ", names.ToArray()) + "), going by the knob");
            return Array.IndexOf(levers, (int)ActiveLaunchLever.UZO) >= 0;
        }

        /// <summary>Off (the first value) at the bottom, the rest evenly round the dial, clockwise.</summary>
        static float[] EvenRots(int n)
        {
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = Mathf.Repeat(180f + i * 360f / Mathf.Max(1, n), 360f);
            return r;
        }

        static float[] SpreadRots(int n, float start)
        {
            float step = Mathf.Min(45f, 360f / Mathf.Max(1, n));
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = Mathf.Repeat(start + i * step, 360f);
            return r;
        }

        // ------------------------------------------------------------------ build

        void BuildSwitches(TMP_FontAsset font)
        {
            knobs.Clear();
            switchesRetryAt = Time.unscaledTime + SwitchRetry;
            var boxes = Boxes();
            if (boxes == null) return;

            var specs = new List<KeyValuePair<string, KnobSpec>>();

            int[] tubes = KnobValues(SceneKnob<TorpedoFireControlBoxesTubeIndexKnob>(boxes));
            if (tubes != null)
            {
                var labels = new string[tubes.Length];
                var names = new string[tubes.Length];
                for (int i = 0; i < tubes.Length; i++) { labels[i] = TubeLabel(tubes[i], true); names[i] = TubeLabel(tubes[i], false); }
                // the Type II's box has one salvo position: just "Salvo"
                if (IsTypeII(boxes))
                    for (int i = 0; i < tubes.Length; i++)
                        if (tubes[i] == (int)SelectedTorpedoTube.SalvoCourse) labels[i] = names[i] = Strings.SpreadPlain;
                specs.Add(new KeyValuePair<string, KnobSpec>(Strings.SwitchSingle, new KnobSpec
                {
                    Values = tubes, Labels = labels, Names = names, InnerTicks = true, Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesTubeIndexKnob>(boxes)),
                    Rots = IsTypeII(boxes) ? EvenRots(tubes.Length) : SpreadRots(tubes.Length, 155f), // off just left of the bottom, tube I next, clockwise (Type II: evenly round)
                    Get = b => (int)b.SelectedTorpedoTube,
                    Set = (b, v) => { b.SelectedTorpedoTube = (SelectedTorpedoTube)v; b.ValidateMarkedLaunchersFromSalvoSettings(); }
                }));
            }

            int[] salvos = KnobValues(SceneKnob<TorpedoFireControlBoxesSalvoKnob>(boxes));
            // the Type IX's salvo knob has no Off position: eight salvos, 45 degrees apart
            if (salvos != null && IsTypeIX(boxes)) salvos = Array.FindAll(salvos, v => v != 0);
            if (salvos != null)
            {
                var labels = new string[salvos.Length];
                for (int i = 0; i < salvos.Length; i++) labels[i] = SalvoLabel(boxes, salvos[i]);
                specs.Add(new KeyValuePair<string, KnobSpec>(Strings.SwitchSalvo, new KnobSpec
                {
                    Values = salvos, Labels = labels, InnerTicks = true, Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesSalvoKnob>(boxes)),
                    Rots = IsTypeIX(boxes) ? TypeIXSalvoRots(salvos) : IsTypeII(boxes) ? EvenRots(salvos.Length) : SpreadRots(salvos.Length, 180f), // off at the bottom, clockwise (Type II: evenly round; Type IX: as its box)
                    Get = b => b.SelectedTorpedoSalvoPreset,
                    Set = (b, v) => { b.SelectedTorpedoSalvoPreset = v; b.ValidateMarkedLaunchersFromSalvoSettings(); }
                }));
            }

            int[] levers = KnobValues(SceneKnob<TorpedoFireControlBoxesActiveLaunchLeverKnob>(boxes));
            // Type II box, two positions with the tower at the bottom: the IID (it has a UZO)
            // has the bridge (UZO) at the top, the IIA (no UZO on the bridge) has Off at the top
            if (levers != null && IsTypeII(boxes))
            {
                int top = HasUzo(boxes, levers) ? (int)ActiveLaunchLever.UZO : (int)ActiveLaunchLever.Off;
                levers = new[] { top, (int)ActiveLaunchLever.ConningTowerInterior };
            }
            if (levers != null)
            {
                var labels = new string[levers.Length];
                var rots = new float[levers.Length];
                for (int i = 0; i < levers.Length; i++)
                {
                    labels[i] = LeverLabel(levers[i]);
                    // as on the box: bridge at the top, tower at the bottom, off left and right
                    if (IsTypeII(boxes))
                    {
                        var lv = (ActiveLaunchLever)levers[i];
                        rots[i] = lv == ActiveLaunchLever.ConningTowerInterior ? 180f : 0f;
                        continue;
                    }
                    switch ((ActiveLaunchLever)levers[i])
                    {
                        case ActiveLaunchLever.UZO: rots[i] = 0f; break;
                        case ActiveLaunchLever.Off2: rots[i] = 270f; break;   // the boat's knob: Off2 on the left
                        case ActiveLaunchLever.ConningTowerInterior: rots[i] = 180f; break;
                        default: rots[i] = 90f; break;
                    }
                }
                specs.Add(new KeyValuePair<string, KnobSpec>(Strings.SwitchLever, new KnobSpec
                {
                    Values = levers, Labels = labels, Rots = rots, Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesActiveLaunchLeverKnob>(boxes)),
                    Get = b => (int)b.ActiveLaunchLever,
                    Set = (b, v) => b.ActiveLaunchLever = (ActiveLaunchLever)v
                }));
            }

            if (specs.Count == 0) return; // knobs not loaded yet: LateUpdate retries
            switchesBoxes = boxes;
            if (GyroConfig.DebugLog)
                foreach (var kv in specs)
                    Debug.Log(ScopeTdcControlsMod.Tag + " switch box " + boxes.TubesSetup + " '" + kv.Key + "': "
                              + string.Join(", ", Array.ConvertAll(kv.Value.Values, v => v.ToString())) + " -> "
                              + string.Join(" | ", kv.Value.Labels));

            switchesPanel = NewRect("TdcSwitches", transform);
            switchesPanel.anchorMin = switchesPanel.anchorMax = switchesPanel.pivot = new Vector2(0.5f, 0.5f);
            BoxFrame(switchesPanel);
            switchesPanel.gameObject.AddComponent<WheelGuard>();
            AddDrag(switchesPanel.gameObject, false).Switches = true;

            float x = DialPad + KnobRim;
            Func<string>[] tips = { () => Strings.TipSwitchSingle, () => Strings.TipSwitchSalvo, () => Strings.TipSwitchLever };
            string[] titles = { Strings.SwitchSingle, Strings.SwitchSalvo, Strings.SwitchLever };
            foreach (var kv in specs)
            {
                int tipIndex = Array.IndexOf(titles, kv.Key);
                kv.Value.Locked = () => FiringCircuitLive; // not while the firing circuit is energized
                AddKnob(font, x, kv.Key, tipIndex >= 0 ? tips[tipIndex] : null, kv.Value);
                x += KnobSize + KnobGap;
            }

            // firing circuit lamp in a narrow column on the right, level with the knobs' centres
            float lampX = x - KnobGap + KnobRim + FiringLampCol * 0.5f;
            float lampY = -DialPad - TitleH - TitleGap - ControlH * 0.5f;
            firingLamp = MakeLamp(switchesPanel, new Vector2(0f, 1f), new Vector2(lampX, lampY), 12f, LampRed);
            var lampHover = NewRect("FiringLampHover", switchesPanel);
            PlaceV(lampHover, lampX - 14f, lampY + 14f, 28f, 28f);
            var hoverImg = lampHover.gameObject.AddComponent<Image>();
            hoverImg.color = Color.clear;   // invisible, only catches the mouse for the pop-up
            AddTooltip(lampHover.gameObject, () => FiringCircuitLive ? Strings.TipFiringLampOn : Strings.TipFiringLampOff);
            firingLamp.Set(FiringCircuitLive, LampRed);

            float h = DialPad + ControlColumnH + DialPad;
            switchesPanel.sizeDelta = new Vector2(x - KnobGap + KnobRim + FiringLampCol + DialPad, h);
            AddResizeGrip(switchesPanel, ResizableWindow.Switches);
            AddAnchorIcon(switchesPanel, AnchoredWindow.Switches); // follow the direction selector
            RaiseOverlays(); // panel and pop-ups stay on top
        }

        void AddKnob(TMP_FontAsset font, float x, string title, Func<string> tip, KnobSpec spec)
        {
            AddKnob(font, switchesPanel, knobs, x, title, tip, spec);
        }

        /// <summary>A rotary switch (face, labels, handle, name plate, readout) in a window.</summary>
        void AddKnob(TMP_FontAsset font, RectTransform panel, List<KnobSpec> list, float x, string title, Func<string> tip, KnobSpec spec)
        {
            // name above, as on the TDC dials
            float top = -DialPad;
            ControlTitle(panel, font, title, x - KnobGap * 0.5f, top, KnobSize + KnobGap);
            float band = top - TitleH - TitleGap;                        // top of the control band
            float y = band - (ControlH - (KnobSize + KnobRim * 2f)) * 0.5f - KnobRim; // knob centred in the band
            var holder = NewRect("Knob", panel);
            PlaceV(holder, x, y, KnobSize, KnobSize);
            // sharp black rim, the face filling it (no soft brass ring)
            Disc("Edge", holder, SharpCircleSprite(), KnobSize + KnobRim * 2f, Color.black);
            var face = Disc("Face", holder, SharpCircleSprite(), KnobSize + KnobRim * 2f - 3f, DialFace); // larger, so the labels sit well inside
            face.raycastTarget = true;
            face.alphaHitTestMinimumThreshold = 0.5f; // only the round face catches the mouse; drags just outside it move the window
            spec.Face = face.rectTransform;
            FaintRing(face.rectTransform, KnobSize * 0.5f - 37f); // round the hub, under the handle

            // position labels and marks (inner marks round the hub, as on the boat's tube / salvo switches)
            for (int i = 0; i < spec.Values.Length; i++)
            {
                float rot = spec.Rots[i];
                string text = spec.Labels[i];
                if (spec.InnerTicks || string.IsNullOrEmpty(text)) DrawKnobMark(face.rectTransform, rot);
                if (string.IsNullOrEmpty(text)) continue;
                float r = KnobLabelRadius;
                float a = rot * Mathf.Deg2Rad;
                var label = NewText("Label", face.rectTransform, font, text, spec.LabelSize, DialScale);
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = true;
                label.lineSpacing = -18f;
                var lr = label.rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(44f, 26f); // room for the larger labels (they wrap onto two lines)
                lr.anchoredPosition = new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
            }

            // the handle: black bar with a white arrow at the pointing end
            var turn = NewRect("Handle", face.rectTransform);
            turn.anchorMin = turn.anchorMax = turn.pivot = new Vector2(0.5f, 0.5f);
            turn.sizeDelta = Vector2.zero;
            float len = KnobHandleLength;
            float wid = len * 0.27f;
            var bar = NewRect("Bar", turn);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(wid, len);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = HandleSprite();
            barImg.color = KnobHandle;
            barImg.raycastTarget = false;
            var edge = bar.gameObject.AddComponent<Outline>();
            edge.effectColor = KnobHandleEdge;
            edge.effectDistance = new Vector2(0.8f, -0.8f);
            var arrow = NewRect("Arrow", turn);
            arrow.anchorMin = arrow.anchorMax = arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(wid, len);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = ArrowSprite();
            arrowImg.color = KnobArrow;
            arrowImg.raycastTarget = false;
            spec.Handle = turn;

            var relay = face.gameObject.AddComponent<KnobRelay>();
            relay.Owner = this;
            relay.Spec = spec;
            if (tip != null) AddTooltip(face.gameObject, tip);

            // current position, in full, in the black box below (as on the TDC dials)
            spec.Readout = ReadoutBox(panel, font, x + KnobSize * 0.5f, band - ControlH - ReadoutGap);

            list.Add(spec);
        }

        // ------------------------------------------------------------------ handle artwork

        const int HandleTexW = 48, HandleTexH = 176;
        static Sprite handleSprite, arrowSprite;

        /// <summary>A white sprite from a coverage function (x, y in texture pixels), 4x4 supersampled.</summary>
        static Sprite MakeShapeSprite(int w, int h, Func<float, float, bool> inside)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hits++;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>The knob's grip: a long lozenge, widest in the middle, with rounded tapering ends.</summary>
        static Sprite HandleSprite()
        {
            if (handleSprite != null) return handleSprite;
            float cx = HandleTexW * 0.5f, half = HandleTexW * 0.5f - 1f;
            handleSprite = MakeShapeSprite(HandleTexW, HandleTexH, (x, y) =>
            {
                float t = Mathf.Clamp01((y - 1f) / (HandleTexH - 2f));      // 0 tail .. 1 tip
                float taper = Mathf.Sin(Mathf.PI * t);                       // 0 at the ends, 1 in the middle
                float hw = half * (0.62f + 0.38f * taper);
                float endR = half * 0.62f;                                   // rounded ends
                float yb = 1f + endR, yt = HandleTexH - 1f - endR;
                if (y < yb) return (new Vector2(x - cx, y - yb)).magnitude <= endR;
                if (y > yt) return (new Vector2(x - cx, y - yt)).magnitude <= endR;
                return Mathf.Abs(x - cx) <= hw;
            });
            return handleSprite;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + t * ab)).magnitude;
        }

        /// <summary>The white arrow painted on the grip: solid head at the tip, one solid notched feather at the tail.</summary>
        static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            float cx = HandleTexW * 0.5f;
            float tip = HandleTexH - 10f, headBase = tip - 30f, headHalf = 10f;
            float tail = 10f, shaftHalf = 2.2f;
            float featherLen = 28f, featherSpread = 9f, notch = 9f;  // a little longer than the old two-stroke tail
            arrowSprite = MakeShapeSprite(HandleTexW, HandleTexH, (x, y) =>
            {
                float w = Mathf.Abs(x - cx);
                // head
                if (y >= headBase && y <= tip && w <= headHalf * (tip - y) / (tip - headBase)) return true;
                // shaft
                if (y >= tail + notch && y <= headBase + 1f && w <= shaftHalf) return true;
                // feather: a solid chevron, widest at the tail end, with a V notch cut into its back
                float u = y - tail;
                if (u >= 0f && u <= featherLen && w <= featherSpread * (1f - u / featherLen) && u >= notch * (1f - w / featherSpread)) return true;
                return false;
            });
            return arrowSprite;
        }

        void DrawKnobMark(RectTransform face, float rot)
        {
            var holder = NewRect("MarkHolder", face);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.sizeDelta = Vector2.zero;
            holder.localEulerAngles = new Vector3(0f, 0f, -rot);
            var t = NewRect("Mark", holder);
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 0f);
            t.sizeDelta = new Vector2(1.5f, 6f);
            t.anchoredPosition = new Vector2(0f, KnobSize * 0.5f - 37f); // round the hub, well inside the labels
            var img = t.gameObject.AddComponent<Image>();
            img.color = DialScale;
            img.raycastTarget = false;
        }

        // ------------------------------------------------------------------ runtime

        void UpdateSwitches()
        {
            if (switchesPanel == null || switchesBoxes == null) return;
            UpdateKnobs(knobs, switchesBoxes);
            if (firingLamp != null) firingLamp.Set(FiringCircuitLive, LampRed);
        }

        // ------------------------------------------------------------------ firing circuit lamp

        const float FiringLampTime = 4f;   // seconds the circuit stays energized after a fire command
        const float FiringLampCol = 28f;   // width of the lamp's column
        Lamp firingLamp;
        static float firingCircuitUntil = float.NegativeInfinity;

        /// <summary>The firing circuit is energized: lamp lit, the three switches can't be turned.</summary>
        static bool FiringCircuitLive { get { return Time.time < firingCircuitUntil; } }

        /// <summary>
        /// A fire command was given (launch lever or the game's fire key). If the launch
        /// lever's lamp would have been green (every circuit energized), the firing circuit
        /// lamp lights for <see cref="FiringLampTime"/> seconds of game time.
        /// </summary>
        public static void OnFireCommand(PlayerShip ship)
        {
            if (instance == null || ship == null) return;
            string ready;
            bool blocking;
            if (instance.LeverProblem(ship, out ready, out blocking, true) != null) return;
            firingCircuitUntil = Time.time + FiringLampTime;
            if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " firing circuit energized (" + ready + ")");
        }

        /// <summary>Turn each knob's handle to the box's current value and show its name.</summary>
        static void UpdateKnobs(List<KnobSpec> list, TorpedoFireControlBoxes boxes)
        {
            if (boxes == null) return;
            foreach (var k in list)
            {
                int i = k.IndexOf(k.Get(boxes));
                if (i < 0) { k.Readout.text = ""; continue; }
                k.Handle.localEulerAngles = new Vector3(0f, 0f, -k.Rots[i]);
                string name = k.Names != null ? k.Names[i] : k.Labels[i];
                k.Readout.text = string.IsNullOrEmpty(name) ? Strings.SwitchOff : name;
            }
        }

        /// <summary>Free: its screen position plus offset; anchored: left of the direction selector (GyroCounterUI.Anchor.cs).</summary>
        void PositionSwitches() { PlaceAnchorable(AnchoredWindow.Switches); }

        /// <summary>Boat changed, or the knobs weren't loaded when the scope opened: (re)build.</summary>
        void ValidateSwitches()
        {
            if (!GyroConfig.ShowSwitches) return;
            var boxes = Boxes();
            if (switchesPanel != null && ReferenceEquals(boxes, switchesBoxes)) return;
            if (switchesPanel == null && Time.unscaledTime < switchesRetryAt) return;
            if (switchesPanel != null) { Destroy(switchesPanel.gameObject); switchesPanel = null; }
            switchesBoxes = null;
            BuildSwitches(ResolveFont());
        }

        /// <summary>The sound name the boat's knob plays when turned (PresetDial.AudioKeyword).</summary>
        static string KnobSound(Component knob)
        {
            try
            {
                var dial = knob as PresetDial;
                return dial != null ? dial.AudioKeyword : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// A knob click for switches the boat has no knob for (torpedo speed and pistol): the
        /// first of the fire-control box's knobs that has a sound.
        /// </summary>
        static string AnyBoxKnobSound()
        {
            var boxes = Boxes();
            if (boxes == null) return null;
            // the scene search is slow and the torpedo window is rebuilt often: once per boat
            if (ReferenceEquals(boxes, boxKnobSoundFor)) return boxKnobSound;
            boxKnobSoundFor = boxes;
            boxKnobSound = FindBoxKnobSound(boxes);
            return boxKnobSound;
        }

        static TorpedoFireControlBoxes boxKnobSoundFor;
        static string boxKnobSound;

        static string FindBoxKnobSound(TorpedoFireControlBoxes boxes)
        {
            Component[] candidates =
            {
                SceneKnob<TorpedoFireControlBoxesTubeIndexKnob>(boxes),
                SceneKnob<TorpedoFireControlBoxesSalvoKnob>(boxes),
                SceneKnob<TorpedoFireControlBoxesActiveLaunchLeverKnob>(boxes),
                SceneKnob<TorpedoFireControlBoxesGyroAngleReceiverKnob>(boxes),
                SceneKnob<TorpedoFireControlBoxesBearingTransmitterKnob>(boxes)
            };
            foreach (var k in candidates)
            {
                string s = KnobSound(k);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        static UBOAT.Game.Scene.Audio.ISoundEffectsDatabase soundDb;
        static AudioListener listener;
        static AudioSource uiAudio;
        const float SwitchVolume = 0.25f; // well below the clip itself
        static bool soundLogged;

        static void LogSoundOnce(string what)
        {
            if (soundLogged) return;
            soundLogged = true;
            Debug.Log(ScopeTdcControlsMod.Tag + " switch sounds: " + what);
        }

        /// <summary>
        /// Play the boat's knob click for our switch, through the game's sound database
        /// (the same clip PresetDial.OnSwitch plays), unpositioned so it is heard at the scope.
        /// </summary>
        static void PlayKnobClick(KnobSpec spec)
        {
            PlayGameSound(spec.Sound);
        }

        /// <summary>Play one of the game's sound effects by name, unpositioned.</summary>
        static void PlayGameSound(string sound)
        {
            if (string.IsNullOrEmpty(sound)) { LogSoundOnce("this knob has no sound name"); return; }
            try
            {
                if (soundDb == null)
                    soundDb = AccessTools.Field(typeof(PresetDial), "soundEffectsDatabase").GetValue(null) as UBOAT.Game.Scene.Audio.ISoundEffectsDatabase;
                if (soundDb == null) { LogSoundOnce("no sound database"); return; }
                // take the clip the game would play for that knob, but play it ourselves as a
                // plain 2D UI sound: the game's own pooled 3D sources are placed in the boat
                // (and its interior mix is quiet or muted while looking through the scope)
                if (listener == null || !listener.isActiveAndEnabled) listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
                Vector3 at = listener != null ? listener.transform.position : Vector3.zero;
                var effect = soundDb.GetClip(sound, at, false);
                var clip = effect != null ? effect.clip : null;
                if (clip == null) { LogSoundOnce("no clip for '" + sound + "'"); return; }
                if (uiAudio == null && instance != null)
                {
                    uiAudio = instance.gameObject.AddComponent<AudioSource>();
                    uiAudio.playOnAwake = false;
                    uiAudio.spatialBlend = 0f;     // 2D: same loudness wherever the camera is
                    uiAudio.ignoreListenerPause = true;
                }
                if (uiAudio != null) uiAudio.PlayOneShot(clip, SwitchVolume);
            }
            catch (Exception e)
            {
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " knob click failed: " + e.Message);
            }
        }

        void SelectKnob(KnobSpec spec, int index)
        {
            var boxes = Boxes();
            if (boxes == null || index < 0 || index >= spec.Values.Length) return;
            if (spec.Locked != null && spec.Locked()) return;
            if (spec.Get(boxes) == spec.Values[index]) return;
            spec.Set(boxes, spec.Values[index]);
            PlayKnobClick(spec);
        }

        void StepKnob(KnobSpec spec, int dir)
        {
            var boxes = Boxes();
            if (boxes == null) return;
            int i = spec.IndexOf(spec.Get(boxes));
            if (i < 0) i = 0;
            int n = spec.Values.Length;
            SelectKnob(spec, ((i + dir) % n + n) % n);
        }

        /// <summary>
        /// Click a position's label to turn the switch there; click the handle
        /// (left = clockwise, right = back) or use the mouse wheel to step; drag
        /// to turn it, snapping to the positions. Ctrl+drag moves the window.
        /// </summary>
        sealed class KnobRelay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
        {
            public GyroCounterUI Owner;
            public KnobSpec Spec;
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

            Vector2 FromCentre(Vector2 screenPos)
            {
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, transform.position);
                return screenPos - centre;
            }

            float Rot(Vector2 d) { return Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f); }

            public void OnPointerClick(PointerEventData e)
            {
                if (e.dragging) return;
                Owner.HideTip();
                Vector2 d = FromCentre(e.position);
                float scale = transform.lossyScale.x > 0f ? transform.lossyScale.x : 1f;
                float radius = d.magnitude / scale; // in panel units
                if (radius > KnobHandleLength * 0.25f + 8f && e.button == PointerEventData.InputButton.Left)
                    Owner.SelectKnob(Spec, Spec.Nearest(Rot(d)));
                else
                    Owner.StepKnob(Spec, e.button == PointerEventData.InputButton.Right ? -1 : 1);
            }

            public void OnBeginDrag(PointerEventData e)
            {
                movingWindow = CtrlKey && WindowDrag != null;
                if (movingWindow) WindowDrag.OnBeginDrag(e);
            }

            public void OnDrag(PointerEventData e)
            {
                if (movingWindow) { WindowDrag.OnDrag(e); return; }
                Vector2 d = FromCentre(e.position);
                if (d.sqrMagnitude < 100f) return;
                Owner.SelectKnob(Spec, Spec.Nearest(Rot(d)));
            }

            public void OnEndDrag(PointerEventData e)
            {
                if (movingWindow) WindowDrag.OnEndDrag(e);
                movingWindow = false;
            }

            public void OnScroll(PointerEventData e)
            {
                if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
                Owner.StepKnob(Spec, e.scrollDelta.y < 0f ? 1 : -1); // toward you = clockwise
            }
        }
    }
}
