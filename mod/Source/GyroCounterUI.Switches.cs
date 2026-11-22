using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Core.Data;
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
        const float KnobGap = 10f;
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
        static string SalvoLabel(TorpedoFireControlBoxes boxes, int v)
        {
            if (v == 0) return Strings.SwitchOff;
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
                specs.Add(new KeyValuePair<string, KnobSpec>(Strings.SwitchSingle, new KnobSpec
                {
                    Values = tubes, Labels = labels, Names = names, InnerTicks = true, Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesTubeIndexKnob>(boxes)),
                    Rots = SpreadRots(tubes.Length, 155f), // off just left of the bottom, tube I next, clockwise
                    Get = b => (int)b.SelectedTorpedoTube,
                    Set = (b, v) => { b.SelectedTorpedoTube = (SelectedTorpedoTube)v; b.ValidateMarkedLaunchersFromSalvoSettings(); }
                }));
            }

            int[] salvos = KnobValues(SceneKnob<TorpedoFireControlBoxesSalvoKnob>(boxes));
            if (salvos != null)
            {
                var labels = new string[salvos.Length];
                for (int i = 0; i < salvos.Length; i++) labels[i] = SalvoLabel(boxes, salvos[i]);
                specs.Add(new KeyValuePair<string, KnobSpec>(Strings.SwitchSalvo, new KnobSpec
                {
                    Values = salvos, Labels = labels, InnerTicks = true, Sound = KnobSound(SceneKnob<TorpedoFireControlBoxesSalvoKnob>(boxes)),
                    Rots = SpreadRots(salvos.Length, 180f), // off at the bottom, clockwise
                    Get = b => b.SelectedTorpedoSalvoPreset,
                    Set = (b, v) => { b.SelectedTorpedoSalvoPreset = v; b.ValidateMarkedLaunchersFromSalvoSettings(); }
                }));
            }

            int[] levers = KnobValues(SceneKnob<TorpedoFireControlBoxesActiveLaunchLeverKnob>(boxes));
            if (levers != null)
            {
                var labels = new string[levers.Length];
                var rots = new float[levers.Length];
                for (int i = 0; i < levers.Length; i++)
                {
                    labels[i] = LeverLabel(levers[i]);
                    // as on the box: bridge at the top, tower at the bottom, off left and right
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

            float x = DialPad;
            Func<string>[] tips = { () => Strings.TipSwitchSingle, () => Strings.TipSwitchSalvo, () => Strings.TipSwitchLever };
            string[] titles = { Strings.SwitchSingle, Strings.SwitchSalvo, Strings.SwitchLever };
            foreach (var kv in specs)
            {
                int tipIndex = Array.IndexOf(titles, kv.Key);
                AddKnob(font, x, kv.Key, tipIndex >= 0 ? tips[tipIndex] : null, kv.Value);
                x += KnobSize + KnobGap;
            }

            float h = DialPad + KnobSize + 8f + 18f + 4f + 18f + DialPad;
            switchesPanel.sizeDelta = new Vector2(x - KnobGap + DialPad, h);
            switchesPanel.localScale = Vector3.one * GyroConfig.Scale;
            RaiseOverlays(); // panel and pop-ups stay on top
        }

        void AddKnob(TMP_FontAsset font, float x, string title, Func<string> tip, KnobSpec spec)
        {
            AddKnob(font, switchesPanel, knobs, x, title, tip, spec);
        }

        /// <summary>A rotary switch (face, labels, handle, name plate, readout) in a window.</summary>
        void AddKnob(TMP_FontAsset font, RectTransform panel, List<KnobSpec> list, float x, string title, Func<string> tip, KnobSpec spec)
        {
            float y = -DialPad;
            var holder = NewRect("Knob", panel);
            PlaceV(holder, x, y, KnobSize, KnobSize);
            // sharp black rim, the face filling it (no soft brass ring)
            Disc("Edge", holder, SharpCircleSprite(), KnobSize + 6f, Color.black);
            var face = Disc("Face", holder, SharpCircleSprite(), KnobSize + 3f, DialFace);
            face.raycastTarget = true;
            spec.Face = face.rectTransform;

            // position labels and marks (inner marks round the hub, as on the boat's tube / salvo switches)
            for (int i = 0; i < spec.Values.Length; i++)
            {
                float rot = spec.Rots[i];
                string text = spec.Labels[i];
                if (spec.InnerTicks || string.IsNullOrEmpty(text)) DrawKnobMark(face.rectTransform, rot);
                if (string.IsNullOrEmpty(text)) continue;
                float r = KnobLabelRadius;
                float a = rot * Mathf.Deg2Rad;
                var label = NewText("Label", face.rectTransform, font, text, 8.5f, DialScale);
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = true;
                label.lineSpacing = -18f;
                var lr = label.rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(40f, 24f);
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
            y -= KnobSize + 8f;

            // name plate
            var plate = NewRect("Plate", panel);
            PlaceV(plate, x + 10f, y, KnobSize - 20f, 18f);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.color = DrumBlack;
            plateImg.raycastTarget = false;
            var plateText = NewText("Text", plate, font, title, CtlFont, DialScale);
            plateText.alignment = TextAlignmentOptions.Center;
            var pr = plateText.rectTransform;
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            y -= 18f + 4f;

            // current position, in full
            spec.Readout = NewText("Readout", panel, font, "", CtlFont, BoxInk);
            spec.Readout.alignment = TextAlignmentOptions.Center;
            PlaceV(spec.Readout.rectTransform, x - KnobGap * 0.5f, y, KnobSize + KnobGap, 18f);

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

        void PositionSwitches()
        {
            if (switchesPanel == null) return;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.SwitchesX, Screen.height * GyroConfig.SwitchesY);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                switchesPanel.anchoredPosition = local + new Vector2(GyroConfig.SwitchesOffsetX, GyroConfig.SwitchesOffsetY);
        }

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

        static UBOAT.Game.Scene.Audio.ISoundEffectsDatabase soundDb;
        static AudioListener listener;

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
            if (string.IsNullOrEmpty(sound)) return;
            try
            {
                if (soundDb == null)
                    soundDb = AccessTools.Field(typeof(PresetDial), "soundEffectsDatabase").GetValue(null) as UBOAT.Game.Scene.Audio.ISoundEffectsDatabase;
                if (soundDb == null) return;
                // the game's clips are 3D: GetClip(tag, shuffle) would put it at the world
                // origin, far out of earshot, so play it right at the listener instead
                if (listener == null || !listener.isActiveAndEnabled) listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
                Vector3 at = listener != null ? listener.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                var clip = soundDb.GetClip(sound, at, false);
                if (clip != null) clip.Play();
                else if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " no sound clip '" + sound + "'");
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
