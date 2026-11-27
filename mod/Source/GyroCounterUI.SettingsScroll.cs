using UnityEngine;
using UnityEngine.EventSystems;

namespace ScopeTdcControls
{
    /// <summary>
    /// The settings drop-down keeps on screen: it is cut to the room left below the control
    /// panel (down to the bottom of the screen) and its rows scroll with the mouse wheel or by
    /// dragging the thin scroll bar on its right. Plain drags on the rows don't scroll, so the
    /// check boxes click as before.
    /// </summary>
    public partial class GyroCounterUI
    {
        sealed class SettingsScroller : MonoBehaviour, IScrollHandler
        {
            public RectTransform Content, Thumb;
            public float FullH;           // all rows
            public float MinH;            // never cut shorter than this
            public float Step;            // one wheel notch
            const float ScreenMargin = 8f;  // pixels kept free at the bottom of the screen
            const float BarW = 3f;

            float offset;                 // how far the rows are scrolled up
            RectTransform rect;
            readonly Vector3[] corners = new Vector3[4];

            float ViewH { get { return rect.sizeDelta.y; } }
            float MaxOffset { get { return Mathf.Max(0f, FullH - ViewH); } }

            void Awake() { rect = (RectTransform)transform; }

            public void OnScroll(PointerEventData e)
            {
                if (MaxOffset <= 0f) return;
                offset = Mathf.Clamp(offset - e.scrollDelta.y * Step, 0f, MaxOffset);
                Apply();
            }

            /// <summary>Dragging the thumb by a screen delta.</summary>
            public void DragThumb(float screenDy)
            {
                float scale = rect.lossyScale.y;
                if (scale <= 0f || MaxOffset <= 0f) return;
                float track = ViewH - ThumbH();
                if (track <= 0f) return;
                offset = Mathf.Clamp(offset - screenDy / scale * (MaxOffset / track), 0f, MaxOffset);
                Apply();
            }

            float ThumbH() { return Mathf.Max(16f, ViewH * ViewH / Mathf.Max(FullH, 1f)); }

            void LateUpdate()
            {
                if (rect == null || Content == null) return;
                // the room below the panel's top edge, in this panel's units (overlay canvas: corners are screen pixels)
                rect.GetWorldCorners(corners);
                float top = corners[1].y;
                float scale = rect.lossyScale.y;
                float room = scale > 0f ? (top - ScreenMargin) / scale : FullH;
                float h = Mathf.Min(FullH, Mathf.Max(MinH, room));
                if (Mathf.Abs(h - rect.sizeDelta.y) > 0.5f) rect.sizeDelta = new Vector2(rect.sizeDelta.x, h);
                Apply();
            }

            void Apply()
            {
                offset = Mathf.Clamp(offset, 0f, MaxOffset);
                Content.anchoredPosition = new Vector2(0f, offset);
                if (Thumb == null) return;
                bool show = MaxOffset > 0.5f;
                if (Thumb.gameObject.activeSelf != show) Thumb.gameObject.SetActive(show);
                if (!show) return;
                float th = ThumbH();
                float track = ViewH - th;
                Thumb.sizeDelta = new Vector2(BarW, th);
                Thumb.anchoredPosition = new Vector2(-1f, -(track * offset / MaxOffset));
            }
        }

        /// <summary>Drag the scroll bar's thumb to scroll the settings.</summary>
        sealed class SettingsThumbDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
        {
            public SettingsScroller Scroller;
            public void OnBeginDrag(PointerEventData e) { }
            public void OnDrag(PointerEventData e) { if (Scroller != null) Scroller.DragThumb(e.delta.y); }
            public void OnEndDrag(PointerEventData e) { }
            public void OnScroll(PointerEventData e) { if (Scroller != null) Scroller.OnScroll(e); }
        }
    }
}
