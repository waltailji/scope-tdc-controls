using System.Collections.Generic;
using UBOAT.Game.Scene.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// The off-white windows (TDC dials, firing selector, direction selector, launch lever)
    /// take on the colour of the boat's interior lighting, like the real boxes would:
    /// off-white under the normal white lights, reddish under the red (alarm) lights,
    /// bluish under the blue (silent running) lights. The tint follows the game's own
    /// blend between its light sets (PlayerShipInteriorLighting.Presets), so it fades
    /// when the lights change instead of snapping.
    /// </summary>
    public partial class GyroCounterUI
    {
        // multiplied onto everything in the off-white windows (box, text, scales, pointers...)
        static readonly Color TintWhite = Color.white;
        static readonly Color TintRed = new Color(0.74f, 0.30f, 0.26f, 1f);
        static readonly Color TintBlue = new Color(0.36f, 0.46f, 0.78f, 1f);
        const float ContentScanInterval = 1f; // look for newly built parts this often
        const float LightingLookupInterval = 10f; // a scene search is costly: not often

        static readonly List<Image> boxFrames = new List<Image>();
        static PlayerShipInteriorLighting interiorLighting;
        static float interiorLookupAt;
        static Color lastTint = Color.white;

        /// <summary>The colour of the boat's interior light right now, as a multiplier (white = normal lights).</summary>
        static Color InteriorTint()
        {
            if (!GyroConfig.TintPanels) return TintWhite; // setting off: always as under the normal lights
            try
            {
                if (interiorLighting == null && Time.unscaledTime >= interiorLookupAt)
                {
                    interiorLookupAt = Time.unscaledTime + LightingLookupInterval;
                    interiorLighting = Get<PlayerShipInteriorLighting>() ?? Object.FindObjectOfType<PlayerShipInteriorLighting>();
                }
                if (interiorLighting == null) return TintWhite;

                // the game's blend weights for its light sets: Normal, Red, Blue
                var w = interiorLighting.Presets;
                float wn = 0f, wr = 0f, wb = 0f;
                if (w != null && w.Length >= 3)
                {
                    wn = Mathf.Max(0f, w[(int)PlayerShipInteriorLighting.LightSet.Normal]);
                    wr = Mathf.Max(0f, w[(int)PlayerShipInteriorLighting.LightSet.Red]);
                    wb = Mathf.Max(0f, w[(int)PlayerShipInteriorLighting.LightSet.Blue]);
                }
                float sum = wn + wr + wb;
                if (sum < 0.001f)
                {
                    // no weights to go by: use the set that is in charge
                    switch (interiorLighting.DominantPreset)
                    {
                        case PlayerShipInteriorLighting.LightSet.Red: return TintRed;
                        case PlayerShipInteriorLighting.LightSet.Blue: return TintBlue;
                        default: return TintWhite;
                    }
                }
                return (TintWhite * wn + TintRed * wr + TintBlue * wb) / sum;
            }
            catch { return TintWhite; }
        }

        // every part of the off-white windows with its own (untinted) colour
        readonly Dictionary<Graphic, Color> tintBase = new Dictionary<Graphic, Color>();
        readonly List<Graphic> tintScratch = new List<Graphic>();
        float contentScanAt;

        /// <summary>
        /// Recolour the off-white windows when the interior light changes: the box itself and
        /// everything on it (text, scales, pointers, knobs, plates...), all multiplied by the
        /// light colour. Lamps stay as they are (they are lights). Newly built parts (a window
        /// shown later, the torpedo window rebuilt) are picked up within a second.
        /// </summary>
        void UpdateBoxTint()
        {
            Color tint = InteriorTint();
            tint.a = 1f;
            bool changed = !(Mathf.Abs(tint.r - lastTint.r) < 0.002f && Mathf.Abs(tint.g - lastTint.g) < 0.002f && Mathf.Abs(tint.b - lastTint.b) < 0.002f);
            bool scan = Time.unscaledTime >= contentScanAt;
            if (!changed && !scan) return;
            if (scan) { contentScanAt = Time.unscaledTime + ContentScanInterval; ScanTintContent(tint); }
            if (!changed) return;
            lastTint = tint;

            Color c = BoxColor * tint;
            c.a = BoxColor.a;
            for (int i = boxFrames.Count - 1; i >= 0; i--)
            {
                if (boxFrames[i] == null) { boxFrames.RemoveAt(i); continue; }
                boxFrames[i].color = c;
            }
            foreach (var kv in tintBase)
                if (kv.Key != null) kv.Key.color = Tinted(kv.Value, tint);
        }

        /// <summary>
        /// A colour under the interior light: multiplied by the light colour, plus a faint glow
        /// of it on dark parts (black knobs, faces and handles would otherwise stay black under
        /// red or blue light). Under the normal white light the glow is zero.
        /// </summary>
        static Color Tinted(Color c, Color tint)
        {
            float coloured = 1f - Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)); // 0 = white light
            float dark = 1f - (0.30f * c.r + 0.59f * c.g + 0.11f * c.b);         // 1 = black part
            float glow = DarkGlow * coloured * dark;
            return new Color(c.r * tint.r + tint.r * glow, c.g * tint.g + tint.g * glow, c.b * tint.b + tint.b * glow, c.a);
        }

        const float DarkGlow = 0.32f; // how much of the light a black part picks up

        /// <summary>Note the colour of parts not seen before (and tint them now); forget destroyed ones.</summary>
        void ScanTintContent(Color tint)
        {
            var dead = new List<Graphic>();
            foreach (var g in tintBase.Keys) if (g == null) dead.Add(g);
            foreach (var g in dead) tintBase.Remove(g);

            RectTransform[] panels = { dialsPanel, switchesPanel, leverPanel, directionsPanel, torpedoPanel, spreadPanel, turnPanel, tubesPanel };
            foreach (var p in panels)
            {
                if (p == null) continue;
                tintScratch.Clear();
                p.GetComponentsInChildren(true, tintScratch);
                foreach (var g in tintScratch)
                {
                    if (g == null || tintBase.ContainsKey(g) || boxFrames.Contains(g as Image) || InLamp(g.transform, p)) continue;
                    tintBase[g] = g.color;           // built untinted
                    g.color = Tinted(g.color, lastTint); // the light as it is now (a change follows below)
                }
            }
        }

        /// <summary>Lamps are lights, tube circles show status colours: not tinted.</summary>
        static bool InLamp(Transform t, Transform stop)
        {
            for (; t != null && t != stop; t = t.parent)
                if (t.name == "Lamp" || t.name == "TubeDot") return true; // lights, and the tube status circles
            return false;
        }

        /// <summary>Change a part's own colour where it is changed after building (it keeps the light tint).</summary>
        void SetTintedColor(Graphic g, Color c)
        {
            if (g == null) return;
            if (tintBase.ContainsKey(g)) tintBase[g] = c;
            g.color = tintBase.ContainsKey(g) ? Tinted(c, lastTint) : c;
        }
    }
}
