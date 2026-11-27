using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Anchoring: an anchor icon at the top right of some windows switches them between
    /// following another window and standing on their own.
    ///   - salvo spread: right of the TDC dials, top level with theirs;
    ///   - own angular speed: right of the TDC dials, bottom level with theirs;
    ///   - firing selector: right of the direction selector, bottom level with it.
    /// Each mode keeps its own offset (dragging changes the one in use), and switching
    /// converts between them so the window stays where it is.
    /// </summary>
    public partial class GyroCounterUI
    {
        enum AnchoredWindow { Spread, TurnRate, Switches }

        const float AnchorIcon = 14f;
        const float AnchorGap = 8f;   // between a window and the one it follows

        readonly Dictionary<AnchoredWindow, Image> anchorIcons = new Dictionary<AnchoredWindow, Image>();
        static Sprite anchorSprite;

        static bool IsAnchored(AnchoredWindow w)
        {
            switch (w)
            {
                case AnchoredWindow.Spread: return GyroConfig.SpreadAnchored;
                case AnchoredWindow.TurnRate: return GyroConfig.TurnRateAnchored;
                default: return GyroConfig.SwitchesAnchored;
            }
        }

        RectTransform AnchoredPanel(AnchoredWindow w)
        {
            switch (w)
            {
                case AnchoredWindow.Spread: return spreadPanel;
                case AnchoredWindow.TurnRate: return turnPanel;
                default: return switchesPanel;
            }
        }

        static ResizableWindow ResizableOf(AnchoredWindow w)
        {
            switch (w)
            {
                case AnchoredWindow.Spread: return ResizableWindow.Spread;
                case AnchoredWindow.TurnRate: return ResizableWindow.TurnRate;
                default: return ResizableWindow.Switches;
            }
        }

        static Vector2 GetOffset(AnchoredWindow w, bool anchored)
        {
            switch (w)
            {
                case AnchoredWindow.Spread:
                    return anchored ? new Vector2(GyroConfig.SpreadOffsetX, GyroConfig.SpreadOffsetY) : new Vector2(GyroConfig.SpreadFreeX, GyroConfig.SpreadFreeY);
                case AnchoredWindow.TurnRate:
                    return anchored ? new Vector2(GyroConfig.TurnRateOffsetX, GyroConfig.TurnRateOffsetY) : new Vector2(GyroConfig.TurnRateFreeX, GyroConfig.TurnRateFreeY);
                default:
                    return anchored ? new Vector2(GyroConfig.SwitchesAnchorOffsetX, GyroConfig.SwitchesAnchorOffsetY) : new Vector2(GyroConfig.SwitchesOffsetX, GyroConfig.SwitchesOffsetY);
            }
        }

        static void SetOffset(AnchoredWindow w, bool anchored, Vector2 v)
        {
            switch (w)
            {
                case AnchoredWindow.Spread:
                    if (anchored) { GyroConfig.SpreadOffsetX = v.x; GyroConfig.SpreadOffsetY = v.y; }
                    else { GyroConfig.SpreadFreeX = v.x; GyroConfig.SpreadFreeY = v.y; }
                    break;
                case AnchoredWindow.TurnRate:
                    if (anchored) { GyroConfig.TurnRateOffsetX = v.x; GyroConfig.TurnRateOffsetY = v.y; }
                    else { GyroConfig.TurnRateFreeX = v.x; GyroConfig.TurnRateFreeY = v.y; }
                    break;
                default:
                    if (anchored) { GyroConfig.SwitchesAnchorOffsetX = v.x; GyroConfig.SwitchesAnchorOffsetY = v.y; }
                    else { GyroConfig.SwitchesOffsetX = v.x; GyroConfig.SwitchesOffsetY = v.y; }
                    break;
            }
        }

        /// <summary>Save the window's anchoring and both of its offsets.</summary>
        static void SaveAnchoring(AnchoredWindow w)
        {
            switch (w)
            {
                case AnchoredWindow.Spread:
                    GyroConfig.Save("SpreadAnchored", GyroConfig.SpreadAnchored);
                    GyroConfig.Save("SpreadOffsetX", GyroConfig.SpreadOffsetX); GyroConfig.Save("SpreadOffsetY", GyroConfig.SpreadOffsetY);
                    GyroConfig.Save("SpreadFreeX", GyroConfig.SpreadFreeX); GyroConfig.Save("SpreadFreeY", GyroConfig.SpreadFreeY);
                    break;
                case AnchoredWindow.TurnRate:
                    GyroConfig.Save("TurnRateAnchored", GyroConfig.TurnRateAnchored);
                    GyroConfig.Save("TurnRateOffsetX", GyroConfig.TurnRateOffsetX); GyroConfig.Save("TurnRateOffsetY", GyroConfig.TurnRateOffsetY);
                    GyroConfig.Save("TurnRateFreeX", GyroConfig.TurnRateFreeX); GyroConfig.Save("TurnRateFreeY", GyroConfig.TurnRateFreeY);
                    break;
                default:
                    GyroConfig.Save("SwitchesAnchored", GyroConfig.SwitchesAnchored);
                    GyroConfig.Save("SwitchesAnchorOffsetX", GyroConfig.SwitchesAnchorOffsetX); GyroConfig.Save("SwitchesAnchorOffsetY", GyroConfig.SwitchesAnchorOffsetY);
                    GyroConfig.Save("SwitchesOffsetX", GyroConfig.SwitchesOffsetX); GyroConfig.Save("SwitchesOffsetY", GyroConfig.SwitchesOffsetY);
                    break;
            }
        }

        /// <summary>Where the window goes with a zero offset, anchored or free.</summary>
        bool BasePosition(AnchoredWindow w, bool anchored, out Vector2 pos)
        {
            pos = Vector2.zero;
            var panel = AnchoredPanel(w);
            if (panel == null) return false;
            if (!anchored)
            {
                if (w != AnchoredWindow.Switches) return true; // free: the offset is the position itself
                var screen = new Vector2(Screen.width * GyroConfig.SwitchesX, Screen.height * GyroConfig.SwitchesY);
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out pos);
            }
            if (w == AnchoredWindow.Switches) return RightOfDirections(panel, out pos);
            return BesideDialsBase(panel, ResizableOf(w), w == AnchoredWindow.Spread, out pos);
        }

        /// <summary>Position an anchorable window for this frame.</summary>
        void PlaceAnchorable(AnchoredWindow w)
        {
            var panel = AnchoredPanel(w);
            bool anchored = IsAnchored(w);
            Vector2 basePos;
            if (panel == null || !BasePosition(w, anchored, out basePos)) return;
            panel.anchoredPosition = basePos + GetOffset(w, anchored);
        }

        /// <summary>The anchor icon: switch modes without moving the window.</summary>
        void ToggleAnchor(AnchoredWindow w)
        {
            HideTip();
            var panel = AnchoredPanel(w);
            bool now = !IsAnchored(w);
            Vector2 basePos;
            if (panel != null && BasePosition(w, now, out basePos))
                SetOffset(w, now, panel.anchoredPosition - basePos); // stays put
            switch (w)
            {
                case AnchoredWindow.Spread: GyroConfig.SpreadAnchored = now; break;
                case AnchoredWindow.TurnRate: GyroConfig.TurnRateAnchored = now; break;
                default: GyroConfig.SwitchesAnchored = now; break;
            }
            SaveAnchoring(w);
            PaintAnchorIcon(w);
        }

        /// <summary>A drag on the window moves the offset of the mode in use.</summary>
        static void DragAnchorable(AnchoredWindow w, Vector2 d)
        {
            bool anchored = IsAnchored(w);
            SetOffset(w, anchored, GetOffset(w, anchored) + d);
        }

        /// <summary>Resizing a free window keeps its top-left corner (it is centred on its position).</summary>
        static void ShiftAnchorable(AnchoredWindow w, Vector2 by)
        {
            if (IsAnchored(w)) return; // anchored: placed by its anchored edges already
            SetOffset(w, false, GetOffset(w, false) + by);
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Right of the TDC dials (where they are, or would be), top or bottom level with them.</summary>
        bool BesideDialsBase(RectTransform panel, ResizableWindow w, bool top, out Vector2 pos)
        {
            pos = Vector2.zero;
            float dialsScale = EffectiveScale(ResizableWindow.Dials);
            Vector2 dialsCentre;
            float dialsW, dialsH;
            if (dialsPanel != null)
            {
                dialsCentre = dialsPanel.anchoredPosition;
                dialsW = dialsPanel.sizeDelta.x * dialsScale;
                dialsH = dialsPanel.sizeDelta.y * dialsScale;
            }
            else
            {
                Vector2 local;
                var screen = new Vector2(Screen.width * GyroConfig.DialsX, Screen.height * GyroConfig.DialsY);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local)) return false;
                dialsCentre = local + new Vector2(GyroConfig.DialsOffsetX, GyroConfig.DialsOffsetY);
                dialsW = (DialSize + 16f + DialPad * 2f) * dialsScale;
                dialsH = (DialSize + 80f) * 3f * dialsScale; // about three dials
            }
            Vector2 size = panel.sizeDelta * EffectiveScale(w);
            float y = top ? dialsCentre.y + dialsH * 0.5f - size.y * 0.5f
                          : dialsCentre.y - dialsH * 0.5f + size.y * 0.5f;
            pos = new Vector2(dialsCentre.x + dialsW * 0.5f + AnchorGap + size.x * 0.5f, y);
            return true;
        }

        /// <summary>Right of the direction selector (where it is placed, even while hidden), bottom level with it.</summary>
        bool RightOfDirections(RectTransform panel, out Vector2 pos)
        {
            pos = Vector2.zero;
            Vector2 local;
            var screen = new Vector2(Screen.width * GyroConfig.DirectionsX, Screen.height * GyroConfig.DirectionsY);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local)) return false;
            Vector2 dirCentre = local + new Vector2(GyroConfig.DirectionsOffsetX, GyroConfig.DirectionsOffsetY);
            float dirScale = EffectiveScale(ResizableWindow.Directions);
            Vector2 size = panel.sizeDelta * EffectiveScale(ResizableWindow.Switches);
            Vector2 dirSize = directionsPanel != null ? directionsPanel.sizeDelta * dirScale : new Vector2(size.x * 0.7f, size.y);
            pos = new Vector2(dirCentre.x + dirSize.x * 0.5f + AnchorGap + size.x * 0.5f,
                              dirCentre.y - dirSize.y * 0.5f + size.y * 0.5f);
            return true;
        }

        // ------------------------------------------------------------------ the icon

        /// <summary>The anchor icon at the top right of a window: dark = anchored, faint = free.</summary>
        void AddAnchorIcon(RectTransform panel, AnchoredWindow w)
        {
            var r = NewRect("Anchor", panel);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
            r.sizeDelta = new Vector2(AnchorIcon, AnchorIcon);
            r.anchoredPosition = new Vector2(-5f, -5f);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = AnchorSprite();
            img.raycastTarget = true;
            var btn = r.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() =>
            {
                ToggleAnchor(w);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            AddTooltip(r.gameObject, () => AnchorTip(w));
            anchorIcons[w] = img;
            PaintAnchorIcon(w);
        }

        void PaintAnchorIcon(AnchoredWindow w)
        {
            Image img;
            if (anchorIcons.TryGetValue(w, out img) && img != null) SetTintedColor(img, IsAnchored(w) ? BoxInk : new Color(BoxInk.r, BoxInk.g, BoxInk.b, 0.28f));
        }

        static string AnchorTip(AnchoredWindow w)
        {
            string to = w == AnchoredWindow.Switches ? Strings.AnchorToDirections : Strings.AnchorToDials;
            return IsAnchored(w) ? string.Format(Strings.TipAnchorOn, to) : string.Format(Strings.TipAnchorOff, to);
        }

        /// <summary>A ship's anchor: ring, shank with a stock, curved arms with flukes.</summary>
        static Sprite AnchorSprite()
        {
            if (anchorSprite != null) return anchorSprite;
            const int n = 56;
            float cx = n * 0.5f;
            anchorSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float dx = x - cx;
                // ring at the top
                float ry = y - 46f, rd = Mathf.Sqrt(dx * dx + ry * ry);
                if (rd <= 6f && rd >= 2.8f) return true;
                // shank
                if (Mathf.Abs(dx) <= 2.4f && y >= 10f && y <= 41f) return true;
                // stock across the top of the shank
                if (Mathf.Abs(y - 35f) <= 2.2f && Mathf.Abs(dx) <= 11f) return true;
                // arms: the lower half of a ring round the shank
                float ay = y - 26f, ad = Mathf.Sqrt(dx * dx + ay * ay);
                if (ay <= 0f && ad <= 19f && ad >= 15.2f) return true;
                // flukes: arrowheads pointing up at the arm tips
                for (int s = -1; s <= 1; s += 2)
                {
                    float fx = x - (cx + s * 17f), fy = y - 26f;
                    if (fy >= -2f && fy <= 7f && Mathf.Abs(fx) <= 5f * (1f - (fy + 2f) / 9f)) return true;
                }
                return false;
            });
            return anchorSprite;
        }
    }
}
