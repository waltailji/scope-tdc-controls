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
        // multiplied onto the off-white box colour
        static readonly Color TintWhite = Color.white;
        static readonly Color TintRed = new Color(1.00f, 0.55f, 0.50f, 1f);
        static readonly Color TintBlue = new Color(0.62f, 0.74f, 1.00f, 1f);
        const float LightingLookupInterval = 10f; // a scene search is costly: not often

        static readonly List<Image> boxFrames = new List<Image>();
        static PlayerShipInteriorLighting interiorLighting;
        static float interiorLookupAt;
        static Color lastTint = Color.white;

        /// <summary>The colour of the boat's interior light right now, as a multiplier (white = normal lights).</summary>
        static Color InteriorTint()
        {
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

        /// <summary>Recolour the off-white windows when the interior light changes.</summary>
        static void UpdateBoxTint()
        {
            Color tint = InteriorTint();
            tint.a = 1f;
            if (Mathf.Abs(tint.r - lastTint.r) < 0.002f && Mathf.Abs(tint.g - lastTint.g) < 0.002f && Mathf.Abs(tint.b - lastTint.b) < 0.002f)
                return;
            lastTint = tint;
            Color c = BoxColor * tint;
            c.a = BoxColor.a;
            for (int i = boxFrames.Count - 1; i >= 0; i--)
            {
                if (boxFrames[i] == null) { boxFrames.RemoveAt(i); continue; }
                boxFrames[i].color = c;
            }
        }
    }
}
