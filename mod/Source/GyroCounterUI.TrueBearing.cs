using TMPro;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// TRUE bearing counter: where the scope (periscope or UZO) is looking, as a compass bearing
    /// (000-359), in the same rolling-drum style as the gyro angle counter. Its own window, by
    /// default just above the TDC gyro angle counter and moving with it; dragged, it keeps its
    /// offset from there. Shown with its check box in the WINDOWS list.
    /// True bearing = the scope's bearing from the bow + the boat's heading, as the game's own
    /// course dial at the scope works it out (PeriscopeCourseUI.ComputeAbsoluteCourse).
    /// </summary>
    public partial class GyroCounterUI
    {
        const float TrueGap = 4f; // between this window and the TDC counter under it

        CounterRow trueRow;

        void BuildTrueBearing(TMP_FontAsset font, float labelW)
        {
            trueRow = BuildCounterRow(transform, font, "TrueBearingCounter", Strings.TrueLabel, labelW, RowKind.True);
            trueRow.Absolute = true;
            var p = trueRow.Panel;
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0.5f);
            p.pivot = new Vector2(0.5f, 0f); // placed by its bottom edge, above the TDC counter
            p.localScale = Vector3.one * GyroConfig.Scale;
            AddTooltip(p.gameObject, () => Strings.TipTrueBearing);
            AddDrag(p.gameObject, false).TrueBearing = true;
        }

        /// <summary>The scope's line of sight as a compass bearing, or NaN when there is no scope.</summary>
        static float ScopeTrueBearing()
        {
            try
            {
                var ctrl = ScopeAim.CurrentController();
                var scope = ctrl != null ? ctrl.Periscope : null;
                if (scope == null) return float.NaN;
                var boat = scope.ParentEntity;
                if (boat == null) return float.NaN;
                return Normalize(scope.LocalBearing + boat.transform.localEulerAngles.y);
            }
            catch { return float.NaN; }
        }

        // the stand-alone "True Bearing on Periscope" mod: while its own counter is on screen, ours stays hidden
        const string StandaloneTrueObject = "TrueBearingOnPeriscope";
        static Canvas standaloneTrue;
        static float standaloneLookupAt;
        static bool loggedStandalone;

        /// <summary>
        /// The True Bearing on Periscope mod is running and its counter showing (its canvas on). Its
        /// object is found by name (it lives in DontDestroyOnLoad), looked for every few seconds until
        /// found. Where it hides (binoculars), ours may still show, with "TRUE bearing on binoculars".
        /// </summary>
        static bool StandaloneTrueBearingShown()
        {
            if (standaloneTrue == null && Time.unscaledTime >= standaloneLookupAt)
            {
                standaloneLookupAt = Time.unscaledTime + 3f;
                var go = GameObject.Find(StandaloneTrueObject);
                standaloneTrue = go != null ? go.GetComponent<Canvas>() : null;
                if (standaloneTrue != null && !loggedStandalone)
                {
                    loggedStandalone = true;
                    Debug.Log(ScopeTdcControlsMod.Tag + " True Bearing on Periscope found: its counter replaces our TRUE counter while it shows");
                }
            }
            return standaloneTrue != null && standaloneTrue.enabled && standaloneTrue.gameObject.activeInHierarchy;
        }

        void UpdateTrueBearing()
        {
            if (trueRow == null) return;
            float b = ScopeTrueBearing();
            if (!float.IsNaN(b)) Drive(trueRow, b);
        }

        /// <summary>
        /// Centred over the TDC counter, its bottom just above the counter's top, plus the dragged
        /// offset. On the binoculars (where the TDC counter is hidden) it takes the counter's own
        /// place instead, under the bearing tape; dragging it there moves it on its own.
        /// </summary>
        void PositionTrueBearing(bool binoculars)
        {
            if (trueRow == null || counterPanel == null) return;
            float h = counterPanel.sizeDelta.y * GyroConfig.Scale;
            Vector2 offset = binoculars ? new Vector2(GyroConfig.TrueBinoOffsetX, GyroConfig.TrueBinoOffsetY)
                                        : new Vector2(GyroConfig.TrueOffsetX, GyroConfig.TrueOffsetY);
            float bottom = binoculars
                ? counterPanel.anchoredPosition.y - counterPanel.pivot.y * h                            // the counter's bottom edge
                : counterPanel.anchoredPosition.y + (1f - counterPanel.pivot.y) * h + TrueGap * GyroConfig.Scale; // just above its top
            trueRow.Panel.anchoredPosition = new Vector2(counterPanel.anchoredPosition.x, bottom) + offset;
        }
    }
}
