using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Resizable windows: a small grip in the bottom-right corner of the TDC dials,
    /// firing selector, direction selector and fire lever windows. Dragging it scales
    /// the whole window (it is laid out at a fixed size), keeping its top-left corner
    /// where it is. Each window's size is saved; "Reset window sizes" on the panel
    /// sets them all back to 1.
    /// </summary>
    public partial class GyroCounterUI
    {
        enum ResizableWindow { Dials, Switches, Directions, Lever }

        const float GripSize = 12f;

        static float WindowScale(ResizableWindow w)
        {
            switch (w)
            {
                case ResizableWindow.Dials: return GyroConfig.DialsScale;
                case ResizableWindow.Switches: return GyroConfig.SwitchesScale;
                case ResizableWindow.Directions: return GyroConfig.DirectionsScale;
                default: return GyroConfig.LeverScale;
            }
        }

        RectTransform WindowPanel(ResizableWindow w)
        {
            switch (w)
            {
                case ResizableWindow.Dials: return dialsPanel;
                case ResizableWindow.Switches: return switchesPanel;
                case ResizableWindow.Directions: return directionsPanel;
                default: return leverPanel;
            }
        }

        /// <summary>The window's total scale: the global Scale setting times its own size.</summary>
        static float EffectiveScale(ResizableWindow w) { return GyroConfig.Scale * WindowScale(w); }

        /// <summary>
        /// Set a window's size, moving its saved offset so the top-left corner stays put
        /// (the window is centred on its position, so growing it would otherwise spread it
        /// out in all directions).
        /// </summary>
        void SetWindowScale(ResizableWindow w, float scale, bool save)
        {
            scale = Mathf.Clamp(scale, GyroConfig.WindowScaleMin, GyroConfig.WindowScaleMax);
            float old = WindowScale(w);
            var panel = WindowPanel(w);
            if (panel != null && Mathf.Abs(scale - old) > 0.0001f)
            {
                Vector2 size = panel.sizeDelta * GyroConfig.Scale;
                // the dial window is laid out for all its dials; its top is fixed at the full height
                if (w == ResizableWindow.Dials) size.y = dialsFullH * GyroConfig.Scale;
                Vector2 grow = size * (scale - old);
                ShiftWindow(w, new Vector2(grow.x * 0.5f, -grow.y * 0.5f));
            }
            switch (w)
            {
                case ResizableWindow.Dials: GyroConfig.DialsScale = scale; break;
                case ResizableWindow.Switches: GyroConfig.SwitchesScale = scale; break;
                case ResizableWindow.Directions: GyroConfig.DirectionsScale = scale; break;
                default: GyroConfig.LeverScale = scale; break;
            }
            if (panel != null) panel.localScale = Vector3.one * EffectiveScale(w);
            if (save) SaveWindowGeometry(w);
        }

        static void ShiftWindow(ResizableWindow w, Vector2 by)
        {
            switch (w)
            {
                case ResizableWindow.Dials: GyroConfig.DialsOffsetX += by.x; GyroConfig.DialsOffsetY += by.y; break;
                case ResizableWindow.Switches: GyroConfig.SwitchesOffsetX += by.x; GyroConfig.SwitchesOffsetY += by.y; break;
                case ResizableWindow.Directions: GyroConfig.DirectionsOffsetX += by.x; GyroConfig.DirectionsOffsetY += by.y; break;
                default: GyroConfig.LeverOffsetX += by.x; GyroConfig.LeverOffsetY += by.y; break;
            }
        }

        static void SaveWindowGeometry(ResizableWindow w)
        {
            switch (w)
            {
                case ResizableWindow.Dials:
                    GyroConfig.SavePrecise("DialsScale", GyroConfig.DialsScale);
                    GyroConfig.Save("DialsOffsetX", GyroConfig.DialsOffsetX); GyroConfig.Save("DialsOffsetY", GyroConfig.DialsOffsetY);
                    break;
                case ResizableWindow.Switches:
                    GyroConfig.SavePrecise("SwitchesScale", GyroConfig.SwitchesScale);
                    GyroConfig.Save("SwitchesOffsetX", GyroConfig.SwitchesOffsetX); GyroConfig.Save("SwitchesOffsetY", GyroConfig.SwitchesOffsetY);
                    break;
                case ResizableWindow.Directions:
                    GyroConfig.SavePrecise("DirectionsScale", GyroConfig.DirectionsScale);
                    GyroConfig.Save("DirectionsOffsetX", GyroConfig.DirectionsOffsetX); GyroConfig.Save("DirectionsOffsetY", GyroConfig.DirectionsOffsetY);
                    break;
                default:
                    GyroConfig.SavePrecise("LeverScale", GyroConfig.LeverScale);
                    GyroConfig.Save("LeverOffsetX", GyroConfig.LeverOffsetX); GyroConfig.Save("LeverOffsetY", GyroConfig.LeverOffsetY);
                    break;
            }
        }

        /// <summary>The panel button: every window back to its normal size (top-left corners stay).</summary>
        void ResetWindowSizes()
        {
            HideTip();
            foreach (ResizableWindow w in new[] { ResizableWindow.Dials, ResizableWindow.Switches, ResizableWindow.Directions, ResizableWindow.Lever })
                if (Mathf.Abs(WindowScale(w) - 1f) > 0.0001f) SetWindowScale(w, 1f, true);
        }

        /// <summary>Add the corner grip to a window; also applies its saved size.</summary>
        void AddResizeGrip(RectTransform panel, ResizableWindow w)
        {
            panel.localScale = Vector3.one * EffectiveScale(w);
            var grip = NewRect("ResizeGrip", panel);
            grip.anchorMin = grip.anchorMax = grip.pivot = new Vector2(1f, 0f);
            grip.sizeDelta = new Vector2(GripSize, GripSize);
            grip.anchoredPosition = new Vector2(-2f, 2f);
            var img = grip.gameObject.AddComponent<Image>();
            img.sprite = GripSprite();
            img.color = BoxEdge;
            img.raycastTarget = true;
            var relay = grip.gameObject.AddComponent<ResizeRelay>();
            relay.Owner = this;
            relay.Window = w;
            AddTooltip(grip.gameObject, () => Strings.TipResize);
        }

        static Sprite gripSprite;

        /// <summary>Three short diagonal strokes in the bottom-right corner (the usual resize grip).</summary>
        static Sprite GripSprite()
        {
            if (gripSprite != null) return gripSprite;
            const int n = 48;
            gripSprite = MakeShapeSprite(n, n, (x, y) =>
            {
                float t = (n - x) + y;          // distance from the bottom-right corner along the diagonal
                if (x < 2f || y > n - 2f) return false;
                for (int k = 1; k <= 3; k++)
                {
                    float c = k * 14f;
                    if (t >= c - 3.5f && t <= c + 3.5f && t <= n + 2f) return true;
                }
                return false;
            });
            return gripSprite;
        }

        /// <summary>Drag the grip: the window grows or shrinks with the pointer, its top-left corner fixed.</summary>
        sealed class ResizeRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public GyroCounterUI Owner;
            public ResizableWindow Window;
            Vector2 baseSize;     // window size at scale 1 (canvas units)
            float startScale;
            Vector2 pulled;       // pointer movement since the drag began (canvas units)

            public void OnBeginDrag(PointerEventData e)
            {
                Owner.HideTip();
                var panel = Owner.WindowPanel(Window);
                if (panel == null) { e.pointerDrag = null; return; }
                baseSize = panel.sizeDelta * GyroConfig.Scale;
                startScale = WindowScale(Window);
                pulled = Vector2.zero;
            }

            public void OnDrag(PointerEventData e)
            {
                if (baseSize.x <= 0f || baseSize.y <= 0f) return;
                float k = Owner.canvas != null && Owner.canvas.scaleFactor > 0f ? Owner.canvas.scaleFactor : 1f;
                pulled += e.delta / k;
                // right and down make it bigger; average the two directions
                float sx = (baseSize.x * startScale + pulled.x) / baseSize.x;
                float sy = (baseSize.y * startScale - pulled.y) / baseSize.y;
                Owner.SetWindowScale(Window, (sx + sy) * 0.5f, false);
            }

            public void OnEndDrag(PointerEventData e)
            {
                SaveWindowGeometry(Window);
            }
        }
    }
}
