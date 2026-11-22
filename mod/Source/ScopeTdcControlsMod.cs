using System;
using HarmonyLib;
using UBOAT.Game;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Camera.ExternalControllers;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.UI.Compass;
using UBOAT.Game.UI.Periscope;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeTdcControls
{
    /// <summary>
    /// Entry point. Shows the TDC gyro angle (Schusswinkel) as a rolling
    /// mechanical counter next to the periscope bearing tape, in the style of
    /// the SH3 GWX / OneAlex gyro readouts, plus a gyro-set lamp and
    /// follow-up / gyro-receiver controls.
    /// </summary>
    [NonSerializedInGameState]
    public class ScopeTdcControlsMod : IUserMod
    {
        public const string Tag = "[ScopeTDC]";
        public const string Version = "1.3"; // keep in sync with Manifest.json

        public void OnLoaded()
        {
            try
            {
                Debug.Log(Tag + " v" + Version + " loading...");
                GyroConfig.Load();
                var harmony = new Harmony("scope-tdc-controls");
                harmony.PatchAll(typeof(ScopeTdcControlsMod).Assembly);
                GyroCounterUI.HookMenus(harmony); // hide the overlay while the game's menus are open
                Debug.Log(Tag + " loaded OK. Config: " + GyroConfig.FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + " failed to load.");
                Debug.LogException(e);
            }
        }
    }

    [HarmonyPatch(typeof(PeriscopeUI), "OnEnable")]
    static class PeriscopeUI_OnEnable_Patch
    {
        static void Postfix(PeriscopeUI __instance)
        {
            try { GyroCounterUI.Attach(__instance); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [HarmonyPatch(typeof(PeriscopeUI), "OnDisable")]
    static class PeriscopeUI_OnDisable_Patch
    {
        static void Postfix(PeriscopeUI __instance)
        {
            try { GyroCounterUI.Detach(__instance); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>
    /// Quitting from the in-game menu: the game fades to black for a few seconds before
    /// Application.quitting fires, so hide our overlay as soon as the quit is confirmed.
    /// </summary>
    [HarmonyPatch(typeof(UBOAT.Game.UI.EscapeMenu), "OnQuitConfirm")]
    static class EscapeMenu_OnQuitConfirm_Patch
    {
        static void Prefix() { GyroCounterUI.OnGameQuitting(); }
    }

    [HarmonyPatch(typeof(UBOAT.Game.UI.EscapeMenu), "Quit")]
    static class EscapeMenu_Quit_Patch
    {
        static void Prefix() { GyroCounterUI.OnGameQuitting(); }
    }

    /// <summary>Quit to the menu (or the save-then-quit path) goes through here too: hide before the screen goes dark.</summary>
    [HarmonyPatch(typeof(UBOAT.Game.Core.GameState), "ExitToMenu")]
    static class GameState_ExitToMenu_Patch
    {
        static void Prefix() { GyroCounterUI.OnGameQuitting(); }
    }

    /// <summary>Tracks the on-screen bearing tape so the counter can sit next to it.</summary>
    [HarmonyPatch(typeof(CompassUI), "LateUpdate")]
    static class CompassUI_LateUpdate_Patch
    {
        static void Postfix(RawImage ___compassImage)
        {
            GyroCounterUI.ReportCompass(___compassImage);
        }
    }

    /// <summary>
    /// Records the player's follow-up choice whenever it goes through the
    /// periscope's own handler (vanilla toggle or this mod's buttons).
    /// </summary>
    [HarmonyPatch(typeof(PeriscopeUI), "OnBearingFollowUpChanged")]
    static class PeriscopeUI_OnBearingFollowUpChanged_Patch
    {
        static void Postfix(bool value)
        {
            FollowUpOverride.UserSet(value);
        }
    }

    /// <summary>
    /// Vanilla forces follow-up ON every frame while a target is locked, which
    /// would make "Nicht folgen" impossible. Skip that while the player has
    /// chosen "Nicht folgen"; locking a new target hands control back to vanilla.
    /// </summary>
    [HarmonyPatch(typeof(PeriscopeController), "ValidateBearingFollowUp")]
    static class PeriscopeController_ValidateBearingFollowUp_Patch
    {
        static bool Prefix(PeriscopeController __instance)
        {
            try { return FollowUpOverride.AllowVanillaForce(__instance.LockTarget); }
            catch { return true; }
        }
    }

    static class FollowUpOverride
    {
        static bool userOff;
        static Entity lastLock;

        public static void UserSet(bool followUp)
        {
            userOff = !followUp;
        }

        public static bool AllowVanillaForce(Entity lockTarget)
        {
            if (!ReferenceEquals(lockTarget, lastLock))
            {
                lastLock = lockTarget;
                if (lockTarget != null)
                {
                    userOff = false;             // new lock = vanilla auto-follow again
                    TransmitterOverride.Clear(); // ... and vanilla's bearing source again
                }
            }
            return !userOff;
        }
    }

    /// <summary>
    /// Bearing transmitter (which scope feeds its bearing to the TDC): None,
    /// Attack periscope, UZO, Observation periscope. The game sets it from
    /// several places (the scope you look through, the locked target, the
    /// torpedo panel). A pick on this mod's panel wins over all of them until
    /// a new target is locked. Turning the physical knob on the fire-control
    /// box counts as a new manual pick.
    /// </summary>
    public static class TransmitterOverride
    {
        static BearingTransmitter? manual;
        static bool ownWrite;
        public static bool KnobWrite;

        public static void Clear() { manual = null; }

        public static void SetManual(TorpedoFireControlBoxes boxes, BearingTransmitter value)
        {
            if (boxes == null) return;
            manual = value;
            ownWrite = true;
            try { boxes.BearingTransmitter = value; }
            finally { ownWrite = false; }
        }

        public static void Filter(ref BearingTransmitter value)
        {
            if (ownWrite) return;
            if (KnobWrite) { manual = value; return; } // the player turned the real knob
            if (manual.HasValue) value = manual.Value;
        }
    }

    [HarmonyPatch(typeof(TorpedoFireControlBoxes), "BearingTransmitter", MethodType.Setter)]
    static class TorpedoFireControlBoxes_SetBearingTransmitter_Patch
    {
        static void Prefix(ref BearingTransmitter value)
        {
            TransmitterOverride.Filter(ref value);
        }
    }

    [HarmonyPatch(typeof(TorpedoFireControlBoxesBearingTransmitterKnob), "Value", MethodType.Setter)]
    static class BearingTransmitterKnob_SetValue_Patch
    {
        static void Prefix() { TransmitterOverride.KnobWrite = true; }
        static void Finalizer() { TransmitterOverride.KnobWrite = false; }
    }
}
