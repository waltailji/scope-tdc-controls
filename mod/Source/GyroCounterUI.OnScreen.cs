using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Keeps the windows on screen. A saved position can leave a window off screen (a smaller
    /// resolution or UI scale than when it was placed, a different window size, an anchor the
    /// scope screen puts elsewhere), and then it can't be dragged back.
    ///  - A few seconds after entering the scope (once everything has found its place) and
    ///    shortly after the screen size changes: every window shown that isn't fully on screen
    ///    is moved just far enough to be, and the new position saved.
    ///  - Every few seconds in between: a window shown entirely off screen is brought back the
    ///    same way (one that pops up later, e.g. the spread window).
    /// A window moves as it would if dragged there, so its anchoring is kept. Windows being
    /// dragged are left alone.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float OnScreenMargin = 2f;        // canvas units kept clear of the screen edge
        const float OffScreenScanInterval = 4f;

        float onScreenCheckAt = -1f;
        float offScreenScanAt;
        int lastScreenW, lastScreenH;
        readonly List<DragRelay> relayScratch = new List<DragRelay>();
        readonly List<Graphic> graphicScratch = new List<Graphic>();
        readonly Dictionary<DragRelay, int> movesSoFar = new Dictionary<DragRelay, int>(); // a window that won't move isn't pushed forever

        void ScheduleOnScreenCheck(float delay)
        {
            movesSoFar.Clear();
            float at = Time.unscaledTime + delay;
            if (onScreenCheckAt < 0f || at > onScreenCheckAt) onScreenCheckAt = at;
        }

        void KeepOnScreen()
        {
            if (canvas == null || !canvas.enabled || canvasRect == null) return;
            if (Screen.width != lastScreenW || Screen.height != lastScreenH)
            {
                bool first = lastScreenW == 0;
                lastScreenW = Screen.width;
                lastScreenH = Screen.height;
                if (!first) ScheduleOnScreenCheck(1.5f); // the resolution or window size changed
            }
            float now = Time.unscaledTime;
            if (onScreenCheckAt >= 0f && now >= onScreenCheckAt)
            {
                onScreenCheckAt = -1f;
                offScreenScanAt = now + OffScreenScanInterval;
                // one window a frame: others anchored to it move with it, so measure again first
                if (BringOnScreen(false)) onScreenCheckAt = now;
            }
            else if (now >= offScreenScanAt)
            {
                offScreenScanAt = now + OffScreenScanInterval;
                if (BringOnScreen(true)) offScreenScanAt = now;
            }
        }

        /// <summary>
        /// onlyLost: only windows entirely off screen; otherwise any not fully on it. Moves the
        /// first one found and returns true (the rest wait for the next frame's layout).
        /// </summary>
        bool BringOnScreen(bool onlyLost)
        {
            Rect screen = canvasRect.rect;
            screen.xMin += OnScreenMargin; screen.yMin += OnScreenMargin;
            screen.xMax -= OnScreenMargin; screen.yMax -= OnScreenMargin;

            relayScratch.Clear();
            GetComponentsInChildren(false, relayScratch);
            foreach (var relay in relayScratch)
            {
                if (relay == null || relay.Dragging || !relay.isActiveAndEnabled) continue;
                // rows hanging under the gyro counter move with it: only the top one of a stack counts
                var up = relay.transform.parent;
                if (up != null && up.GetComponentInParent<DragRelay>() != null) continue;

                Rect r;
                if (!ShownRect((RectTransform)relay.transform, out r)) continue;
                bool lost = r.xMax < screen.xMin || r.xMin > screen.xMax || r.yMax < screen.yMin || r.yMin > screen.yMax;
                if (onlyLost && !lost) continue;

                Vector2 shift = Vector2.zero;
                // wider / taller than the screen: keep its left / top edge on screen
                if (r.width > screen.width) shift.x = screen.xMin - r.xMin;
                else if (r.xMin < screen.xMin) shift.x = screen.xMin - r.xMin;
                else if (r.xMax > screen.xMax) shift.x = screen.xMax - r.xMax;
                if (r.height > screen.height) shift.y = screen.yMax - r.yMax;
                else if (r.yMax > screen.yMax) shift.y = screen.yMax - r.yMax;
                else if (r.yMin < screen.yMin) shift.y = screen.yMin - r.yMin;
                if (shift.sqrMagnitude < 0.25f) continue;
                int n;
                movesSoFar.TryGetValue(relay, out n);
                if (n >= 3) continue;
                movesSoFar[relay] = n + 1;

                relay.Move(shift);
                relay.SaveMove();
                Debug.Log(ScopeTdcControlsMod.Tag + " window '" + relay.name + "' was " + (lost ? "off screen" : "partly off screen")
                          + ": moved by (" + Mathf.RoundToInt(shift.x) + ", " + Mathf.RoundToInt(shift.y) + ")");
                return true;
            }
            return false;
        }

        /// <summary>
        /// The area a window covers on the canvas: everything drawn in it that is shown (the rows
        /// hanging under the gyro counter included; the settings drop-down left out).
        /// </summary>
        bool ShownRect(RectTransform root, out Rect r)
        {
            r = new Rect();
            bool any = false;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            graphicScratch.Clear();
            root.GetComponentsInChildren(false, graphicScratch);
            foreach (var g in graphicScratch)
            {
                if (g == null || !g.enabled) continue;
                if (settingsPanel != null && g.transform.IsChildOf(settingsPanel)) continue;
                if (tipPanel != null && g.transform.IsChildOf(tipPanel)) continue;
                var rt = g.rectTransform;
                if (rt.rect.width <= 0f || rt.rect.height <= 0f) continue;
                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = canvasRect.InverseTransformPoint(corners[i]);
                    if (float.IsNaN(p.x) || float.IsNaN(p.y)) continue;
                    xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                    yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
                    any = true;
                }
            }
            if (!any) return false;
            r = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }
    }
}
