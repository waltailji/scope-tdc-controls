using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UBOAT.Game.Core.Mods;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Torpedo run timers, Silent Hunter style: when a torpedo leaves one of our tubes, a row with
    /// a torpedo picture and a countdown from the time-to-impact counter's value at that moment
    /// appears near the top of the screen. Several torpedoes stack, newest at the bottom. At 0:00
    /// the row fades out over a minute and leaves the stack.
    ///
    /// The stack has its own overlay, so it stays on screen in every view (map, interior, scope),
    /// hidden only with the game's menus, loading screens and with the game UI hidden. The
    /// countdown runs in game time (time compression speeds it up, pause stops it); the fade runs
    /// in real time. Drag the stack to move it (Ctrl+drag with "Ctrl+drag to move" on).
    /// </summary>
    public partial class GyroCounterUI
    {
        /// <summary>A torpedo left a player tube (called from the launch patch).</summary>
        public static void OnTorpedoLaunched(TorpedoLauncher tube, Torpedo torpedo)
        {
            if (!GyroConfig.Enabled || !GyroConfig.TorpedoTimers) return;
            // worked out now for this torpedo (whether its tube was selected or not): the TDC's
            // entries as they are, the torpedo's own speed and the gyro angle its tube was set to.
            // If that can't be done, what the time-to-impact drums show.
            float seconds;
            if (!LaunchTime(tube, torpedo, out seconds)) seconds = impactSeconds;
            if (!(seconds >= 1f)) return;    // no time to count down
            TorpedoTimerHud.Ensure().Add(Mathf.Min(seconds, ImpactTimeMax), torpedo);
        }

        static bool LaunchTime(TorpedoLauncher tube, Torpedo torpedo, out float seconds)
        {
            seconds = 0f;
            try
            {
                var tdc = Tdc();
                if (tdc == null) return false;
                float speed = 0f;
                if (torpedo != null && torpedo) speed = torpedo.Velocity; // what it was launched at, m/s
                if (!(speed > 1f && speed < 40f)) speed = 0f;              // not a plausible m/s: the TDC's speed
                float gyro = float.NaN;
                var group = tube != null ? tube.Group : null;
                if (group != null && !float.IsNaN(group.SetGyroAngle)) gyro = group.SetGyroAngle;
                return TryImpactTimeFor(tdc, speed, gyro, out seconds);
            }
            catch { return false; }
        }

        /// <summary>
        /// A short torpedo type from the game's name for it: the body (G7a, G7e, G7es), its T number
        /// and a FAT / LUT program ("G7e Torpedo T3 - Pi2 - FAT" -> "G7e T3 FAT"; Zaunkoenig -> "G7es T5").
        /// Anything else (mines): the name itself, tidied. Empty for none.
        /// </summary>
        public static string TorpedoTypeName(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return "";
                name = name.Replace("(Clone)", "").Trim();
                var parts = name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "";
                string body = parts[0], tNum = null, program = null;
                foreach (var p in parts)
                {
                    if (tNum == null && p.Length >= 2 && (p[0] == 'T' || p[0] == 't') && char.IsDigit(p[1])) tNum = p.ToUpperInvariant();
                    string u = p.ToUpperInvariant();
                    if (u == "FAT" || u == "LUT") program = u;
                }
                if (tNum == null && name.IndexOf("Zaunk", StringComparison.OrdinalIgnoreCase) >= 0) tNum = "T5";
                if (!body.StartsWith("G7", StringComparison.OrdinalIgnoreCase) && tNum == null) return name; // mines and the like
                return body + (tNum != null ? " " + tNum : "") + (program != null ? " " + program : "");
            }
            catch { return ""; }
        }

        sealed class TorpedoTimerHud : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            const float RowH = 22f, RowGap = 3f, IconH = 13f, TextW = 46f, PadX = 6f;
            const float FadeTime = 60f;      // real seconds after 0:00
            static readonly Color PlateColor = new Color(0.03f, 0.03f, 0.03f, 0.62f);
            static readonly Color ChasePlateColor = new Color(0.10f, 0.22f, 0.30f, 0.80f); // the torpedo the camera follows
            static readonly Color TimeColor = new Color(0.95f, 0.93f, 0.86f, 1f);
            static readonly Color ExpiredColor = new Color(0.95f, 0.55f, 0.45f, 1f);

            static TorpedoTimerHud hud;
            static Sprite iconSprite;
            static bool iconTried;

            sealed class Row
            {
                public RectTransform Rect;
                public CanvasGroup Group;
                public Image Plate;
                public Torpedo Torpedo;       // the running torpedo (null if it couldn't be found)
                public string Type = "";      // its type (G7e T3 FAT...), for the hover popup
                public bool Chased;
                public TextMeshProUGUI Time;
                public float EndsAt;          // game time of the expected hit
                public float ExpiredAt = -1f; // real time it reached 0:00
                public int Shown = -1;
            }

            readonly List<Row> rows = new List<Row>();
            RectTransform tip;                 // hover popup: the torpedo's type
            TextMeshProUGUI tipText;
            Row tipRow;
            Canvas canvas;
            RectTransform stack;
            TMP_FontAsset font;
            Material fontMaterial;
            bool dragging;

            public static TorpedoTimerHud Ensure()
            {
                if (hud != null) return hud;
                var go = new GameObject("ScopeTdcTorpedoTimers");
                DontDestroyOnLoad(go);
                hud = go.AddComponent<TorpedoTimerHud>();
                hud.Build();
                return hud;
            }

            void Build()
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = GyroConfig.SortingOrder;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                gameObject.AddComponent<GraphicRaycaster>();

                stack = NewRect("Stack", transform);
                stack.anchorMin = stack.anchorMax = new Vector2(0f, 0f);
                stack.pivot = new Vector2(0.5f, 1f);   // the top centre sits at the saved point
                var hit = stack.gameObject.AddComponent<Image>();
                hit.color = Color.clear;               // catches the drag between and around the rows
                var relay = stack.gameObject.AddComponent<DragForward>();
                relay.Hud = this;

                // hover popup (the panel's tooltips live on its own canvas, shown only at the scope)
                tip = NewRect("Tip", transform);
                tip.anchorMin = tip.anchorMax = new Vector2(0f, 0f);
                tip.pivot = new Vector2(0f, 0.5f);
                var tipBg = tip.gameObject.AddComponent<Image>();
                tipBg.color = new Color(0.05f, 0.06f, 0.08f, 0.94f);
                RoundValueBox(tipBg);
                tipBg.raycastTarget = false;
                tipText = NewText("Text", tip, ResolveFont(), "", 13f, TimeColor);
                tipText.alignment = TextAlignmentOptions.MidlineLeft;
                var ttr = tipText.rectTransform;
                ttr.anchorMin = Vector2.zero; ttr.anchorMax = Vector2.one;
                ttr.offsetMin = new Vector2(8f, 4f); ttr.offsetMax = new Vector2(-8f, -4f);
                tip.gameObject.SetActive(false);

                var panelFont = ResolveFont();
                font = ImpactFont(panelFont);
                try { if (font != null) fontMaterial = PlainMaterial(font, panelFont); } catch { fontMaterial = null; }
            }

            public void Add(float seconds, Torpedo torpedo)
            {
                var r = new Row();
                r.Torpedo = torpedo;
                try { if (torpedo != null && torpedo) r.Type = TorpedoTypeName(torpedo.gameObject.name); } catch { }
                r.Rect = NewRect("Torpedo", stack);
                r.Group = r.Rect.gameObject.AddComponent<CanvasGroup>();
                var plate = r.Rect.gameObject.AddComponent<Image>();
                plate.color = PlateColor;
                RoundValueBox(plate);
                r.Plate = plate;
                var relay = r.Rect.gameObject.AddComponent<RowRelay>(); // double-click: chase camera; drag: move the stack
                relay.Hud = this;
                relay.Row = r;

                var sprite = IconSprite();
                float iconW = sprite != null ? IconH * sprite.rect.width / sprite.rect.height : IconH * 9f;
                var icon = NewRect("Icon", r.Rect);
                PlaceH(icon, PadX, iconW, IconH);
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;

                r.Time = NewText("Time", r.Rect, font, "", 15f, TimeColor);
                if (fontMaterial != null) r.Time.fontSharedMaterial = fontMaterial;
                r.Time.alignment = TextAlignmentOptions.MidlineRight;
                PlaceH(r.Time.rectTransform, PadX + iconW + 6f, TextW, RowH);

                float w = PadX + iconW + 6f + TextW + PadX;

                r.Rect.anchorMin = r.Rect.anchorMax = new Vector2(0.5f, 1f);
                r.Rect.pivot = new Vector2(0.5f, 1f);
                r.Rect.sizeDelta = new Vector2(w, RowH);

                r.EndsAt = Time.time + seconds;
                rows.Add(r);
                Layout();
                if (GyroConfig.DebugLog) Debug.Log(ScopeTdcControlsMod.Tag + " torpedo timer: " + seconds.ToString("0", CultureInfo.InvariantCulture) + " s");
            }

            void Layout()
            {
                float w = 0f, y = 0f;
                foreach (var r in rows)
                {
                    r.Rect.anchoredPosition = new Vector2(0f, -y);
                    y += RowH + RowGap;
                    w = Mathf.Max(w, r.Rect.sizeDelta.x);
                }
                stack.sizeDelta = new Vector2(w, Mathf.Max(0f, y - RowGap));
            }

            void Update()
            {
                bool hidden = quitting || GameLoading() || GameMenuOpen() || !GyroConfig.Enabled || !GyroConfig.TorpedoTimers
                              || (GyroConfig.HideWithGameUI && GameUiHidden() && !TorpedoChaseCam.Active); // kept while chasing, to double-click back
                bool removed = false;
                for (int i = rows.Count - 1; i >= 0; i--)
                {
                    var r = rows[i];
                    float left = r.EndsAt - Time.time;
                    if (left <= 0f && r.ExpiredAt < 0f)
                    {
                        r.ExpiredAt = Time.unscaledTime;
                        r.Time.color = ExpiredColor;
                    }
                    int secs = Mathf.Max(0, Mathf.CeilToInt(left));
                    if (secs != r.Shown)
                    {
                        r.Shown = secs;
                        r.Time.text = (secs / 60).ToString(CultureInfo.InvariantCulture) + ":" + (secs % 60).ToString("00", CultureInfo.InvariantCulture);
                    }
                    if (r.ExpiredAt >= 0f)
                    {
                        float f = (Time.unscaledTime - r.ExpiredAt) / FadeTime;
                        if (f >= 1f) { Destroy(r.Rect.gameObject); rows.RemoveAt(i); removed = true; continue; }
                        r.Group.alpha = 1f - f;
                    }
                }
                if (removed) Layout();
                if (tipRow != null && (!rows.Contains(tipRow) || dragging)) HideTip();
                if (tipRow != null) PlaceTip(tipRow);
                TorpedoChaseCam.Tick();
                foreach (var r in rows)
                {
                    bool chased = TorpedoChaseCam.IsChasing(r.Torpedo);
                    if (chased != r.Chased) { r.Chased = chased; r.Plate.color = chased ? ChasePlateColor : PlateColor; }
                }
                canvas.enabled = rows.Count > 0 && !hidden;
                if (!dragging) Place();
            }

            void Place()
            {
                var cr = ((RectTransform)transform).rect;
                Vector2 p = new Vector2(cr.width * GyroConfig.TimersX, cr.height * GyroConfig.TimersY);
                // kept on screen whatever the saved point (the top centre of the stack sits there)
                Vector2 size = stack.sizeDelta;
                p.x = Mathf.Clamp(p.x, Mathf.Min(size.x * 0.5f, cr.width * 0.5f), Mathf.Max(cr.width - size.x * 0.5f, cr.width * 0.5f));
                p.y = Mathf.Clamp(p.y, Mathf.Min(size.y, cr.height), cr.height);
                stack.anchoredPosition = p;
            }

            public void OnBeginDrag(PointerEventData e)
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                dragging = ctrl || !GyroConfig.CtrlDragToMove;
                if (!dragging) e.pointerDrag = null;
            }

            public void OnDrag(PointerEventData e)
            {
                if (!dragging) return;
                float k = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
                stack.anchoredPosition += e.delta / k;
                var cr = ((RectTransform)transform).rect;
                if (cr.width > 0f && cr.height > 0f)
                {
                    GyroConfig.TimersX = Mathf.Clamp01(stack.anchoredPosition.x / cr.width);
                    GyroConfig.TimersY = Mathf.Clamp01(stack.anchoredPosition.y / cr.height);
                }
            }

            public void OnEndDrag(PointerEventData e)
            {
                if (!dragging) return;
                dragging = false;
                GyroConfig.SavePrecise("TimersX", GyroConfig.TimersX);
                GyroConfig.SavePrecise("TimersY", GyroConfig.TimersY);
            }

            void ShowTip(Row r)
            {
                if (tip == null || r == null) return;
                string type = r.Type.Length > 0 ? "<b>" + r.Type + "</b>" : "<b>" + Strings.TorpedoWord + "</b>";
                string text = type + "\n<size=11>" + Strings.TimerDoubleClick + "</size>";
                tipText.text = text;
                Vector2 pref = tipText.GetPreferredValues(text);
                tip.sizeDelta = new Vector2(Mathf.Ceil(pref.x) + 16f, Mathf.Ceil(pref.y) + 8f);
                tip.gameObject.SetActive(true);
                tip.SetAsLastSibling();
                tipRow = r;
                PlaceTip(r);
            }

            void HideTip()
            {
                tipRow = null;
                if (tip != null) tip.gameObject.SetActive(false);
            }

            /// <summary>Right of the row (left of it if that runs off the screen), level with it.</summary>
            void PlaceTip(Row r)
            {
                var cr = ((RectTransform)transform).rect;
                Vector2 top = stack.anchoredPosition + r.Rect.anchoredPosition;       // the row's top centre
                float halfW = r.Rect.sizeDelta.x * 0.5f;
                float y = top.y - RowH * 0.5f;
                float x = top.x + halfW + 6f;
                if (x + tip.sizeDelta.x > cr.width) x = top.x - halfW - 6f - tip.sizeDelta.x;
                tip.anchoredPosition = new Vector2(Mathf.Max(0f, x), y);
            }

            /// <summary>A timer row: hover shows its type, double-click chases its torpedo with the camera; drags move the stack.</summary>
            sealed class RowRelay : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
            {
                public TorpedoTimerHud Hud;
                public Row Row;
                public void OnPointerEnter(PointerEventData e) { if (Hud != null && !Hud.dragging) Hud.ShowTip(Row); }
                public void OnPointerExit(PointerEventData e) { if (Hud != null && Hud.tipRow == Row) Hud.HideTip(); }
                public void OnPointerClick(PointerEventData e)
                {
                    if (e.button != PointerEventData.InputButton.Left || e.clickCount != 2 || Row == null) return;
                    if (Row.Torpedo == null || !Row.Torpedo) return; // gone, or never found
                    TorpedoChaseCam.Toggle(Row.Torpedo);
                }
                public void OnBeginDrag(PointerEventData e) { if (Hud != null) Hud.OnBeginDrag(e); }
                public void OnDrag(PointerEventData e) { if (Hud != null) Hud.OnDrag(e); }
                public void OnEndDrag(PointerEventData e) { if (Hud != null) Hud.OnEndDrag(e); }
            }

            /// <summary>The stack's background passes drags to the HUD.</summary>
            sealed class DragForward : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
            {
                public TorpedoTimerHud Hud;
                public void OnBeginDrag(PointerEventData e) { if (Hud != null) Hud.OnBeginDrag(e); }
                public void OnDrag(PointerEventData e) { if (Hud != null) Hud.OnDrag(e); }
                public void OnEndDrag(PointerEventData e) { if (Hud != null) Hud.OnEndDrag(e); }
            }

            // ---------------------------------------------------------- the torpedo picture

            const string IconFile = "torpedo_icon.png";

            /// <summary>Textures/torpedo_icon.png from the mod's folder; a drawn torpedo if it can't be read.</summary>
            static Sprite IconSprite()
            {
                if (iconTried) return iconSprite;
                iconTried = true;
                try
                {
                    foreach (var dir in ModFolders())
                    {
                        string path = Path.Combine(Path.Combine(dir, "Textures"), IconFile);
                        if (!File.Exists(path)) continue;
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if (!tex.LoadImage(File.ReadAllBytes(path))) continue;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Trilinear;
                        tex.anisoLevel = 2;
                        iconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        Debug.Log(ScopeTdcControlsMod.Tag + " torpedo timer icon: " + path);
                        return iconSprite;
                    }
                }
                catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " torpedo timer icon: " + e.Message); }
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " torpedo timer icon not found; a drawn one is used");
                iconSprite = DrawnTorpedo();
                return iconSprite;
            }

            /// <summary>Where this mod may live: the game's record of it, else the usual folders.</summary>
            static IEnumerable<string> ModFolders()
            {
                var list = new List<string>();
                try
                {
                    var mm = Get<ModManager>();
                    if (mm == null)
                        foreach (var m in Resources.FindObjectsOfTypeAll<ModManager>()) { mm = m; break; }
                    var mod = mm != null ? mm.GetMod(typeof(GyroCounterUI).Assembly) : null;
                    if (mod != null && !string.IsNullOrEmpty(mod.Path)) list.Add(mod.Path);
                }
                catch { }
                try { list.Add(Path.Combine(Path.Combine(Application.persistentDataPath, "Mods"), "scope-tdc-controls")); } catch { }
                try
                {
                    // Steam Workshop copy: <steamapps>/workshop/content/494840/<id>
                    var steamapps = Directory.GetParent(Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName).FullName;
                    list.Add(Path.Combine(steamapps, Path.Combine("workshop", Path.Combine("content", Path.Combine("494840", "3843459058")))));
                }
                catch { }
                return list;
            }

            /// <summary>A plain torpedo shape (grey body, dark nose) for when the picture is missing.</summary>
            static Sprite DrawnTorpedo()
            {
                const int w = 180, h = 20;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float cy = (y + 0.5f) - h * 0.5f;
                        float r = h * 0.32f;
                        bool body = x >= 8 && x < w - 22 && Mathf.Abs(cy) <= r;
                        float nx = x - 8f;
                        bool nose = x < 8 + r && nx * nx + cy * cy <= r * r * 1.0f && x >= 8 - r;
                        bool tail = x >= w - 22 && x < w - 6 && Mathf.Abs(cy) <= r * (1f - (x - (w - 22f)) / 24f);
                        bool fin = x >= w - 12 && x < w - 4 && Mathf.Abs(cy) <= h * 0.45f && Mathf.Abs(cy) >= h * 0.30f;
                        Color32 c = new Color32(0, 0, 0, 0);
                        if (body || tail || fin) c = new Color32(176, 170, 150, 255);
                        if ((body && x < 40) || nose) c = new Color32(70, 70, 66, 255);
                        px[y * w + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
