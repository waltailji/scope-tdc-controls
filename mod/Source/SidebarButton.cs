using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UBOAT.Game.UI;
using UBOAT.Game.UI.Periscope;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// A button in the periscope / UZO screen's stock left sidebar that shows
    /// or hides this mod's control panel.
    ///
    /// It is a copy of the sidebar's chronometer button, so it gets the stock
    /// size, layout and hover/pressed behaviour. That button is a
    /// SpriteSwapToggle whose artwork (normal, hover, pressed, selected,
    /// disabled, each for off and on) has the chronometer icon baked in. Every
    /// one of those sprites is redrawn at runtime: the stock pixels are read
    /// back, the centre is painted over with the button's own background colour
    /// and the counter icon is drawn in the button's own icon colour. So the
    /// frame, colours and states stay stock and only the icon changes.
    /// </summary>
    public static class SidebarButton
    {
        const string CloneName = "ScopeTdcControlsButton";
        static readonly string[] Templates = { "chronometerButton", "chronometerButton2" };

        static readonly List<Toggle> buttons = new List<Toggle>();
        static readonly Dictionary<Toggle, Toggle> clones = new Dictionary<Toggle, Toggle>(); // stock button -> our copy
        static readonly Dictionary<Sprite, Sprite> redrawn = new Dictionary<Sprite, Sprite>();
        static MethodInfo swapToggleRefresh;
        static bool loggedLayout;
        static int debugIndex;

        /// <summary>Make sure the button exists on this periscope screen and shows the current state.</summary>
        public static void Ensure(PeriscopeUI ui, Action<bool> onChanged)
        {
            buttons.RemoveAll(b => b == null);
            if (ui == null) return;

            foreach (var name in Templates)
            {
                Toggle template = null;
                try
                {
                    var f = AccessTools.Field(typeof(PeriscopeUI), name);
                    if (f != null) template = f.GetValue(ui) as Toggle;
                }
                catch { }
                if (template == null) continue;

                Toggle button;
                if (!clones.TryGetValue(template, out button) || button == null)
                {
                    button = Create(template, onChanged);
                    clones[template] = button;
                }
                if (button == null) continue;
                if (!buttons.Contains(button)) buttons.Add(button);
                button.gameObject.SetActive(GyroConfig.ShowControls && !hidden);
                Show(button, GyroConfig.ControlsVisible);
            }
        }

        static bool hidden;

        /// <summary>Take the button off the sidebar (bridge binoculars) or put it back.</summary>
        public static void SetHidden(bool hide)
        {
            if (hide == hidden) return;
            hidden = hide;
            foreach (var b in buttons)
                if (b != null) b.gameObject.SetActive(GyroConfig.ShowControls && !hide);
        }

        /// <summary>Show the panel state on every copy of the button (without firing their click handlers).</summary>
        public static void SetState(bool on)
        {
            foreach (var b in buttons)
                if (b != null) Show(b, on);
        }

        static void Show(Toggle button, bool on)
        {
            if (button.isOn != on) button.SetIsOnWithoutNotify(on);
            // SetIsOnWithoutNotify skips the sprite swap; run it ourselves
            var swap = button as SpriteSwapToggle;
            if (swap == null) return;
            try
            {
                if (swapToggleRefresh == null)
                    swapToggleRefresh = AccessTools.Method(typeof(SpriteSwapToggle), "OnValueChanged", new[] { typeof(bool) });
                if (swapToggleRefresh != null) swapToggleRefresh.Invoke(swap, new object[] { on });
            }
            catch { }
        }

        static Toggle Create(Toggle template, Action<bool> onChanged)
        {
            try
            {
                var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                go.name = CloneName;
                go.SetActive(true);
                Place(go.transform as RectTransform, template.transform as RectTransform);

                var toggle = go.GetComponent<Toggle>();
                if (toggle == null) { UnityEngine.Object.Destroy(go); return null; }

                // drop the chronometer's wiring that was copied with it (inspector listeners,
                // event triggers, toggle group), but keep SpriteSwapToggle's own runtime
                // listener so the "on" artwork still shows
                for (int i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                    toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
                toggle.group = null;
                foreach (var trigger in go.GetComponentsInChildren<EventTrigger>(true)) UnityEngine.Object.DestroyImmediate(trigger);

                // keep the stock tooltip placement, then replace the text with ours
                Vector3 offset = Vector3.zero;
                Vector2 pivot = new Vector2(0f, 0.5f);
                float interval = 0f;
                foreach (var tip in go.GetComponentsInChildren<Tooltip>(true))
                {
                    try
                    {
                        var tr = Traverse.Create(tip);
                        offset = tr.Field("offset").GetValue<Vector3>();
                        pivot = tr.Field("pivot").GetValue<Vector2>();
                        interval = tr.Field("updateInterval").GetValue<float>();
                    }
                    catch { }
                    UnityEngine.Object.DestroyImmediate(tip);
                }

                ReplaceArtwork(toggle);

                toggle.onValueChanged.AddListener(v =>
                {
                    try { onChanged(v); }
                    catch (Exception e) { Debug.LogException(e); }
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                });

                var rect = go.transform as RectTransform;
                if (rect != null) Tooltip.SetTooltip(rect, () => Strings.TipSidebarButton, interval, offset, pivot);

                if (!loggedLayout)
                {
                    loggedLayout = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " sidebar button added next to '" + go.transform.parent.name + "/chronometer' ("
                              + toggle.GetType().Name + ", transition " + toggle.transition + ")");
                }
                return toggle;
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not add the sidebar button: " + e.GetBaseException().Message);
                return null;
            }
        }

        /// <summary>
        /// Put our button in the toolbar row, right after the stock button's own item. The
        /// chronometer (velocity tool) button sits inside "Velocity Group" with the velocity
        /// input, which doesn't lay out its children, so a copy left in there landed on top
        /// of it. So: climb to the item that the nearest layout group arranges and insert
        /// after it; with no layout group up there, place it beside the stock button by hand.
        /// </summary>
        static void Place(RectTransform rect, RectTransform template)
        {
            if (rect == null || template == null) return;
            Transform item = template, container = template.parent;
            for (int depth = 0; depth < 4 && container != null; depth++)
            {
                if (container.GetComponent<LayoutGroup>() != null)
                {
                    rect.SetParent(container, false);
                    rect.SetSiblingIndex(item.GetSiblingIndex() + 1);
                    Log("in layout '" + container.name + "' after '" + item.name + "'");
                    return;
                }
                item = container;
                container = container.parent;
            }

            // no layout: same parent, shifted one button along the toolbar (the way its buttons run)
            rect.SetSiblingIndex(template.GetSiblingIndex() + 1);
            bool vertical = RunsVertically(template);
            float gap = 4f;
            Vector2 step = vertical ? new Vector2(0f, -(template.rect.height + gap)) : new Vector2(template.rect.width + gap, 0f);
            rect.anchoredPosition = template.anchoredPosition + step;
            Log("beside '" + template.name + "' in '" + (template.parent != null ? template.parent.name : "?") + "' (" + (vertical ? "below" : "to the right") + ")");
        }

        /// <summary>Whether the stock buttons next to this one are stacked rather than in a row.</summary>
        static bool RunsVertically(RectTransform template)
        {
            var parent = template.parent;
            if (parent == null) return false;
            float dx = 0f, dy = 0f;
            foreach (Transform sibling in parent)
            {
                var r = sibling as RectTransform;
                if (r == null || r == template || r.name == CloneName || r.GetComponent<Selectable>() == null) continue;
                dx = Mathf.Max(dx, Mathf.Abs(r.anchoredPosition.x - template.anchoredPosition.x));
                dy = Mathf.Max(dy, Mathf.Abs(r.anchoredPosition.y - template.anchoredPosition.y));
            }
            return dy > dx;
        }

        static void Log(string where)
        {
            if (loggedPlace) return;
            loggedPlace = true;
            Debug.Log(ScopeTdcControlsMod.Tag + " sidebar button placed " + where);
        }

        static bool loggedPlace;

        // ------------------------------------------------------------------ artwork

        /// <summary>Redraw every sprite the button can show with the counter icon.</summary>
        static void ReplaceArtwork(Toggle toggle)
        {
            var image = toggle.targetGraphic as Image;
            if (image == null) image = toggle.GetComponent<Image>();
            if (image == null) return;

            var swap = toggle as SpriteSwapToggle;
            if (swap != null)
            {
                // read before changing the image: these fall back to the image's sprite when unset
                var baseSprite = swap.BaseSprite;
                var baseState = swap.BaseSpriteState;
                var toggledSprite = swap.ToggledSprite;
                var toggledState = swap.ToggledSpriteState;
                if (baseSprite != null) swap.BaseSprite = Redraw(baseSprite);
                swap.BaseSpriteState = Redraw(baseState);
                if (toggledSprite != null) swap.ToggledSprite = Redraw(toggledSprite);
                swap.ToggledSpriteState = Redraw(toggledState);
            }

            image.sprite = Redraw(image.sprite);
            image.overrideSprite = null;
            toggle.spriteState = Redraw(toggle.spriteState);
        }

        static SpriteState Redraw(SpriteState s)
        {
            s.highlightedSprite = Redraw(s.highlightedSprite);
            s.pressedSprite = Redraw(s.pressedSprite);
            s.selectedSprite = Redraw(s.selectedSprite);
            s.disabledSprite = Redraw(s.disabledSprite);
            return s;
        }

        static Sprite Redraw(Sprite src)
        {
            if (src == null) return null;
            Sprite done;
            if (redrawn.TryGetValue(src, out done)) return done;
            if (redrawn.ContainsValue(src)) return src; // already one of ours

            Sprite result = src;
            try
            {
                var tex = ReadPixels(src);
                if (tex != null)
                {
                    if (GyroConfig.DebugLog) SaveDebug(tex, "original");
                    int w = tex.width, h = tex.height;
                    var px = tex.GetPixels();

                    Color bg = SampleBackground(px, w, h);
                    Color ink = SampleInk(px, w, h, bg);

                    // paint over the stock icon in the middle, then draw ours
                    int ex0 = Mathf.RoundToInt(w * 0.16f), ex1 = Mathf.RoundToInt(w * 0.84f);
                    int ey0 = Mathf.RoundToInt(h * 0.16f), ey1 = Mathf.RoundToInt(h * 0.84f);
                    for (int y = ey0; y < ey1; y++)
                        for (int x = ex0; x < ex1; x++)
                            px[y * w + x] = bg;
                    DrawIcon(px, w, h, ink);

                    tex.SetPixels(px);
                    tex.Apply();
                    if (GyroConfig.DebugLog) SaveDebug(tex, "redrawn");

                    var r = src.rect;
                    var pivot = new Vector2(src.pivot.x / r.width, src.pivot.y / r.height);
                    result = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
                    result.name = src.name + " (Scope TDC)";
                    result.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " could not redraw sidebar icon '" + src.name + "': " + e.Message);
            }
            redrawn[src] = result;
            return result;
        }

        /// <summary>Copies a sprite's pixels via the GPU (works for textures that aren't CPU-readable).</summary>
        static Texture2D ReadPixels(Sprite src)
        {
            var source = src.texture;
            if (source == null) return null;
            var r = src.textureRect;
            int w = Mathf.Max(1, Mathf.RoundToInt(r.width)), h = Mathf.Max(1, Mathf.RoundToInt(r.height));

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt,
                    new Vector2(r.width / source.width, r.height / source.height),
                    new Vector2(r.x / source.width, r.y / source.height));
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.HideAndDontSave;
                return tex;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>The button face colour just inside its border (alpha-weighted average).</summary>
        static Color SampleBackground(Color[] px, int w, int h)
        {
            float r = 0, g = 0, b = 0, aSum = 0; int n = 0;
            int a0 = Mathf.RoundToInt(Mathf.Min(w, h) * 0.10f), a1 = Mathf.RoundToInt(Mathf.Min(w, h) * 0.15f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (edge < a0 || edge > a1) continue;
                    // middle stretches of each side only (corners are often rounded)
                    bool midX = x > w * 0.3f && x < w * 0.7f, midY = y > h * 0.3f && y < h * 0.7f;
                    if (!midX && !midY) continue;
                    var c = px[y * w + x];
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; aSum += c.a; n++;
                }
            if (n == 0) return new Color(0, 0, 0, 0);
            float alpha = aSum / n;
            return aSum > 0.0001f ? new Color(r / aSum, g / aSum, b / aSum, alpha) : new Color(0, 0, 0, 0);
        }

        /// <summary>The stock icon's colour: the centre pixels that stand out most from the background.</summary>
        static Color SampleInk(Color[] px, int w, int h, Color bg)
        {
            float r = 0, g = 0, b = 0, a = 0, wSum = 0;
            for (int y = Mathf.RoundToInt(h * 0.25f); y < Mathf.RoundToInt(h * 0.75f); y++)
                for (int x = Mathf.RoundToInt(w * 0.25f); x < Mathf.RoundToInt(w * 0.75f); x++)
                {
                    var c = px[y * w + x];
                    float diff = Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) + Mathf.Abs(c.a - bg.a);
                    if (diff < 0.35f || c.a < 0.25f) continue;
                    float wt = diff * diff;
                    r += c.r * wt; g += c.g * wt; b += c.b * wt; a += c.a * wt; wSum += wt;
                }
            return wSum > 0 ? new Color(r / wSum, g / wSum, b / wSum, a / wSum) : new Color(0.95f, 0.92f, 0.85f, 1f);
        }

        /// <summary>The counter icon (frame + three drums + seam) in the middle of the sprite.</summary>
        static void DrawIcon(Color[] px, int w, int h, Color ink)
        {
            float size = Mathf.Min(w, h) * 0.56f;
            float cx = w * 0.5f, cy = h * 0.5f, u = size / 56f; // icon designed on a 56 x 32 grid
            float fx0 = cx - 28 * u, fx1 = cx + 28 * u, fy0 = cy - 16 * u, fy1 = cy + 16 * u;
            float ring = 3.5f * u;
            float[] drumX = { -20, -6, 8 };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float d = RoundRectDist(px0, py0, fx0, fy0, fx1, fy1, 5 * u);
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + ring + 0.5f);
                    foreach (var dx in drumX)
                    {
                        float box = RoundRectDist(px0, py0, cx + dx * u, cy - 10 * u, cx + (dx + 12) * u, cy + 10 * u, 1.8f * u);
                        a = Mathf.Max(a, Mathf.Clamp01(0.5f - box));
                    }
                    if (Mathf.Abs(py0 - cy) < 0.8f * u && px0 > cx - 20 * u && px0 < cx + 20 * u) a *= 0.25f;
                    if (a <= 0f) continue;
                    var dst = px[y * w + x];
                    var col = Color.Lerp(dst, ink, a);
                    col.a = Mathf.Max(dst.a, ink.a * a);
                    px[y * w + x] = col;
                }
        }

        /// <summary>Signed distance to a rounded rectangle (negative inside).</summary>
        static float RoundRectDist(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>DebugLog=true: save before/after images next to the config file.</summary>
        static void SaveDebug(Texture2D tex, string kind)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "scope-tdc-sidebar-" + (debugIndex / 2) + "-" + kind + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                debugIndex++;
            }
            catch { }
        }
    }
}
