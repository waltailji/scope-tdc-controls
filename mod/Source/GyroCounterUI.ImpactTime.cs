using System;
using HarmonyLib;
using TMPro;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Time to impact (Laufzeit): three drums at the bottom of the TDC dials window, the
    /// torpedo's run in seconds from launch to the point the TDC aims it at.
    ///
    /// The game doesn't work this out; it is done here in the TDC's own model, from its
    /// entries (target bearing, distance, AOB and speed, the torpedo speed it is set for,
    /// and the gyro angle it computed):
    ///  - The torpedo leaves the tube, runs 9.5 m straight and turns onto its gyro angle on a
    ///    95 m radius (behind the tube offset from the periscope). The TDC's parallax model
    ///    (TorpedoFiringSolution.CalculateIdealTorpedoStartPoint) folds that into a virtual
    ///    start point: the point the torpedo's final straight track would start from if the
    ///    whole run (straight bit, turn and track) were straight. So a large gyro angle adds
    ///    the turn and moves the start point.
    ///  - From that point, the aim point is where a torpedo at the TDC's torpedo speed meets
    ///    the target running its course (from AOB) at its speed; the run is the distance to it.
    ///  - The time is that run at the selected torpedoes' own speed (the slowest of them, for
    ///    a salvo; the Torpedo Room mod's per-torpedo speed when present), or the TDC's.
    /// No tube selected, or no meeting point (a target faster than the torpedo running away):
    /// the drums keep the last time. 000 until a first time has been worked out.
    /// </summary>
    public partial class GyroCounterUI
    {
        const float KnotsToMs = 0.514444f;
        const float ImpactTimeMax = 999f;
        const string ImpactFontName = "ROBOTO-BLACK"; // a heavy sans from the game's own fonts, for the drums

        static readonly Color ImpactBezel = new Color(0.24f, 0.24f, 0.23f, 1f);
        CounterRow impactRow;
        static float impactSeconds;          // the last time worked out (kept while no tube is selected)

        static AccessTools.FieldRef<TorpedoCalculator, float> tdcLauncherDistance, tdcParallaxMultiplier;
        static Func<float, float, float, Vector2> idealStartPoint;
        static bool impactResolved, impactWarned;

        static void ResolveImpactMembers()
        {
            if (impactResolved) return;
            impactResolved = true;
            try { tdcLauncherDistance = AccessTools.FieldRefAccess<TorpedoCalculator, float>("distanceToLauncher"); } catch { }
            try { tdcParallaxMultiplier = AccessTools.FieldRefAccess<TorpedoCalculator, float>("parallaxMultiplier"); } catch { }
            try
            {
                var m = AccessTools.Method(typeof(TorpedoFiringSolution), "CalculateIdealTorpedoStartPoint",
                                           new[] { typeof(float), typeof(float), typeof(float) });
                if (m != null && m.IsStatic && m.ReturnType == typeof(Vector2))
                    idealStartPoint = (Func<float, float, float, Vector2>)Delegate.CreateDelegate(typeof(Func<float, float, float, Vector2>), m);
            }
            catch { }
            if ((idealStartPoint == null || tdcLauncherDistance == null || tdcParallaxMultiplier == null) && !impactWarned)
            {
                impactWarned = true;
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " time to impact: the TDC's start point model was not found; the torpedo's turn is left out");
            }
        }

        /// <summary>The font for the time-to-impact drums: one of the game's loaded fonts, else the panel's.</summary>
        static TMP_FontAsset ImpactFont(TMP_FontAsset fallback)
        {
            try
            {
                string want = GyroConfig.ImpactTimeFont;
                if (string.IsNullOrEmpty(want)) return fallback;
                foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (f == null || f.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) < 0
                        || f.name.IndexOf("Padding", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    // a font asset can be in memory without its glyph atlas (an unloaded bundle) or its material
                    var atlas = f.atlasTexture;
                    bool usable = atlas != null && atlas.width > 1 && f.material != null && f.HasCharacters("0123456789");
                    LogImpactFont(f.name + (usable ? "" : " (no atlas or digits, skipped)"));
                    if (usable) return f;
                }
                LogImpactFont("panel font ('" + want + "' not loaded)");
            }
            catch { }
            return fallback;
        }

        static string loggedImpactFont;
        static void LogImpactFont(string what)
        {
            if (loggedImpactFont == what) return;
            loggedImpactFont = what;
            Debug.Log(ScopeTdcControlsMod.Tag + " time to impact font: " + what);
        }

        static Material impactMaterial;
        static TMP_FontAsset impactMaterialFont;
        static float impactMaterialWeight;

        /// <summary>
        /// A plain copy of the font's material: the game's own material for a font can be set up
        /// for other text (a dark face, an outline), which on black drums would leave them blank.
        /// </summary>
        static Material PlainMaterial(TMP_FontAsset f, TMP_FontAsset panelFont)
        {
            float weight = Mathf.Clamp(GyroConfig.ImpactTimeWeight, -1f, 1f);
            if (impactMaterial != null && impactMaterialFont == f && impactMaterialWeight == weight) return impactMaterial;
            var m = new Material(f.material);
            m.name = f.name + " (time to impact)";
            // Roboto Black's game material uses the warped shader that bends the map's range circle
            // labels round their circle: flat text on it ends up out of sight. Use the plain shader
            // the panel text uses (the font's atlas and settings stay).
            Shader plain = panelFont != null && panelFont.material != null ? panelFont.material.shader : null;
            if (plain == null || plain.name.IndexOf("Warp", StringComparison.OrdinalIgnoreCase) >= 0)
                plain = Shader.Find("TextMeshPro/Distance Field");
            if (plain != null && m.shader != plain) m.shader = plain;
            LogImpactFont(f.name + " with shader " + m.shader.name);
            if (m.HasProperty(ShaderUtilities.ID_FaceColor)) m.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            if (m.HasProperty(ShaderUtilities.ID_FaceDilate)) m.SetFloat(ShaderUtilities.ID_FaceDilate, weight); // + bolder, - thinner
            if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor)) m.SetColor(ShaderUtilities.ID_UnderlayColor, Color.clear);
            m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            m.DisableKeyword(ShaderUtilities.Keyword_Glow);
            m.DisableKeyword(ShaderUtilities.Keyword_Outline);
            impactMaterial = m;
            impactMaterialFont = f;
            impactMaterialWeight = weight;
            return m;
        }

        /// <summary>Title and drums at the bottom of the TDC dials window; returns the new y.</summary>
        float BuildImpactTime(TMP_FontAsset font, float x, float y, float inner)
        {
            var title = ControlTitle(dialsPanel, font, Strings.DialImpactTime, x, y, inner);
            AddTooltip(title.gameObject, () => Strings.TipDialImpactTime);
            title.raycastTarget = true;
            y -= TitleH + TitleGap;

            var drumFont = ImpactFont(font);
            const float housePad = 4f, unitW = 10f;
            float drumsW = DigitW * 3f + DigitGap * 2f;
            float houseW = housePad * 2f + drumsW + 2f + unitW;
            float houseH = DigitH + housePad * 2f;

            var row = new CounterRow();
            row.Signed = true;   // plain 000-999, no wrap at 360 (and no sign slot)
            row.Panel = NewRect("ImpactTime", dialsPanel);
            PlaceV(row.Panel, x + (inner - houseW) * 0.5f, y, houseW, houseH);
            var bg = row.Panel.gameObject.AddComponent<Image>();
            bg.color = ImpactBezel; // dark grey bezel, so the black drums stand out from it
            RoundValueBox(bg);
            bg.raycastTarget = true;
            AddTooltip(row.Panel.gameObject, () => Strings.TipDialImpactTime);

            float dx = housePad;
            for (int i = 0; i < 3; i++)
            {
                MakeDrum(row, row.Panel, drumFont, dx, false);
                dx += DigitW + DigitGap;
            }
            foreach (var drum in row.Digits)
                foreach (var d in drum) d.fontSize = DigitH * 0.7f; // a little smaller than the gyro counter's
            if (drumFont != null)
            {
                try
                {
                    var mat = PlainMaterial(drumFont, font);
                    foreach (var drum in row.Digits)
                        foreach (var d in drum) d.fontSharedMaterial = mat;
                }
                catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " time to impact font material: " + e.Message); }
            }
            var unit = NewText("Unit", row.Panel, font, "s", DigitH * 0.6f, DigitLight);
            unit.alignment = TextAlignmentOptions.BottomLeft;
            PlaceH(unit.rectTransform, dx + 1f, unitW, DigitH);

            impactRow = row;
            row.Displayed = float.NaN;
            Render(row, Mathf.Round(impactSeconds));
            y -= houseH + 12f;
            return y;
        }

        void UpdateImpactTime(TorpedoCalculator tdc)
        {
            if (impactRow == null) return;
            float t;
            var ship = Ship();
            if (ship != null && AnyTubeSelected(ship) && TryImpactTime(tdc, ship, out t))
                impactSeconds = Mathf.Clamp(t, 0f, ImpactTimeMax);

            float target = Mathf.Round(impactSeconds);
            if (float.IsNaN(impactRow.Displayed) || GyroConfig.RollSpeed <= 0f) impactRow.Displayed = target;
            else
            {
                // rolls like the gyro counter, quicker over big jumps so it settles within a second or so
                float rate = Mathf.Max(GyroConfig.RollSpeed * 0.5f, Mathf.Abs(target - impactRow.Displayed) * 3f);
                impactRow.Displayed = Mathf.MoveTowards(impactRow.Displayed, target, rate * Time.unscaledDeltaTime);
            }
            Render(impactRow, impactRow.Displayed);
        }

        static bool AnyTubeSelected(PlayerShip ship)
        {
            var tubes = ship.TorpedoLaunchers;
            if (tubes == null) return false;
            foreach (var t in tubes) if (t != null && t.IsMarkedForLaunch) return true;
            return false;
        }

        /// <summary>The torpedo's run time in seconds to where the TDC aims it; false if there is none.</summary>
        static bool TryImpactTime(TorpedoCalculator tdc, PlayerShip ship, out float seconds)
        {
            seconds = 0f;
            if (tdc == null) return false;
            float tdcSpeed = tdc.TorpedoSpeed * KnotsToMs;   // what the gyro angle was worked out for
            float dist = tdc.DistanceToTarget;
            if (!(tdcSpeed > 0.5f) || !(dist > 0f) || float.IsInfinity(dist)) return false;

            // own boat frame, the periscope at the origin: x ahead, y to starboard; a bearing b is (cos b, sin b)
            float b = tdc.TargetBearing * Mathf.Deg2Rad;
            var target = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * dist;
            // target course relative to own bow: AOB to starboard (we see its starboard side) = it heads to our right
            float h = (tdc.TargetBearing + 180f - tdc.AngleOnBow) * Mathf.Deg2Rad;
            var vel = new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * Mathf.Max(0f, tdc.TargetSpeed * KnotsToMs);

            Vector2 start = Vector2.zero;
            ResolveImpactMembers();
            if (idealStartPoint != null && tdcLauncherDistance != null && tdcParallaxMultiplier != null)
            {
                try { start = idealStartPoint(tdc.GyroAngle, tdcParallaxMultiplier(tdc), tdcLauncherDistance(tdc)); }
                catch { start = Vector2.zero; }
                if (float.IsNaN(start.x) || float.IsNaN(start.y) || start.sqrMagnitude > dist * dist) start = Vector2.zero;
            }

            // meeting time from the start point: |d + v t| = s t
            var d = target - start;
            float a = Vector2.Dot(vel, vel) - tdcSpeed * tdcSpeed;
            float bq = 2f * Vector2.Dot(d, vel);
            float c = Vector2.Dot(d, d);
            float meet;
            if (Mathf.Abs(a) < 1e-4f)
            {
                if (bq >= 0f) return false;
                meet = -c / bq;
            }
            else
            {
                float disc = bq * bq - 4f * a * c;
                if (disc < 0f) return false;
                float sq = Mathf.Sqrt(disc);
                float t1 = (-bq - sq) / (2f * a), t2 = (-bq + sq) / (2f * a);
                meet = float.PositiveInfinity;
                if (t1 > 0f) meet = t1;
                if (t2 > 0f && t2 < meet) meet = t2;
                if (float.IsInfinity(meet)) return false;
            }
            float run = meet * tdcSpeed;   // metres, from launch (the start point counts the straight bit and the turn)

            float speed = SelectedTorpedoSpeed(ship);
            if (!(speed > 0.5f)) speed = tdcSpeed;
            seconds = run / speed;
            return !float.IsNaN(seconds) && !float.IsInfinity(seconds);
        }

        /// <summary>
        /// The slowest of the selected torpedoes' set speeds in m/s (the Torpedo Room mod's own
        /// speed per torpedo when present, else the speed setting), 0 if none is known.
        /// </summary>
        static float SelectedTorpedoSpeed(PlayerShip ship)
        {
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return 0f;
            float setSpeed = ship.TorpedoAttackSettings != null ? ship.TorpedoAttackSettings.TorpedoSpeed : 0f;
            float slowest = 0f;
            foreach (var t in tubes)
            {
                if (t == null || !t.IsMarkedForLaunch) continue;
                var st = t.Torpedo;
                if (st == null) continue;
                try
                {
                    float speed;
                    if (!TorpedoRoomLink.TryGetSpeed(st, out speed)) speed = setSpeed;
                    speed = Mathf.Clamp(speed, (float)st.Speed3, (float)st.Speed1);
                    if (speed > 0f && (slowest <= 0f || speed < slowest)) slowest = speed;
                }
                catch { }
            }
            return slowest;
        }
    }
}
