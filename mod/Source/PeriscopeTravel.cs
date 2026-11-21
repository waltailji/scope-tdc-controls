using System;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.UI.Periscope;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// "Raise fully" / "Lower fully": keeps the periscope screen's own raise or
    /// lower input held until the periscope stops moving.
    ///
    /// The stock raise/lower buttons set PeriscopeUI's private 'elevator' value
    /// to +1 / -1 while held, and every frame the screen adds
    /// elevator x speed x time to the periscope's height (clamped by the game).
    /// This sets the same value every frame, so the periscope moves at its
    /// normal speed, with the game's own limits and sound. It stops when the
    /// height stops changing, when the player presses a stock raise/lower
    /// button (which writes the value itself), or when the button is clicked again.
    /// </summary>
    public static class PeriscopeTravel
    {
        static AccessTools.FieldRef<PeriscopeUI, float> elevatorRef;
        static bool resolved;

        static int direction;          // +1 raising, -1 lowering, 0 idle
        static PeriscopeUI ui;
        static float startTime, lastHeight, lastWritten;
        static int stalledFrames;

        public static int Direction { get { return direction; } }

        static bool Resolve()
        {
            if (!resolved)
            {
                resolved = true;
                try { elevatorRef = AccessTools.FieldRefAccess<PeriscopeUI, float>("elevator"); }
                catch (Exception e) { Debug.LogWarning(ScopeTdcControlsMod.Tag + " raise/lower fully unavailable: " + e.Message); }
            }
            return elevatorRef != null;
        }

        static Periscope Scope()
        {
            var ctrl = ScopeAim.CurrentController();
            return ctrl != null ? ctrl.Periscope : null;
        }

        /// <summary>Click on a button: start in that direction, or stop if already going that way.</summary>
        public static void Toggle(PeriscopeUI screen, int dir)
        {
            if (direction == dir) { Stop(); return; }
            if (screen == null || !Resolve() || Scope() == null) return;
            ui = screen;
            direction = dir;
            startTime = Time.unscaledTime;
            lastHeight = float.NaN;
            stalledFrames = 0;
            lastWritten = dir;
            elevatorRef(ui) = dir;
        }

        public static void Stop()
        {
            if (direction != 0 && ui != null && elevatorRef != null)
            {
                // release our hold, but don't undo a stock button the player is pressing now
                if (elevatorRef(ui) == lastWritten) elevatorRef(ui) = 0f;
            }
            direction = 0;
            ui = null;
        }

        /// <summary>Every frame while the scope view is open.</summary>
        public static void Tick()
        {
            if (direction == 0) return;
            var scope = Scope();
            if (ui == null || !ui.isActiveAndEnabled || scope == null) { Stop(); return; }

            // the player pressed or released a stock raise/lower button: hand control back
            if (Time.unscaledTime - startTime > 0.1f && elevatorRef(ui) != lastWritten) { direction = 0; ui = null; return; }

            elevatorRef(ui) = direction;
            lastWritten = direction;

            float h = scope.Height;
            if (!float.IsNaN(lastHeight) && Mathf.Abs(h - lastHeight) < 1e-5f) stalledFrames++;
            else stalledFrames = 0;
            lastHeight = h;

            // fully up / down (the game clamps the height), after a moment to get going
            if (stalledFrames >= 6 && Time.unscaledTime - startTime > 0.25f) Stop();
        }
    }
}
