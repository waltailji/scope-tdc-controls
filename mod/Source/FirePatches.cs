using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace ScopeTdcControls
{
    /// <summary>
    /// "Single torpedo launch fix": a fire command fires only the tubes that were
    /// selected when it was given.
    ///
    /// The game's fire sequence (PlayerShip.LaunchAllTorpedoesInternal) waits
    /// 1.5 s after the order, then walks the tubes in launch order with 4 s
    /// between launches, and checks each tube's check box only when it gets
    /// there. So a tube checked a few seconds after pressing fire still went off.
    /// On a real boat the firing key was a momentary impulse to the tubes
    /// pre-selected on the fire-control box; anything selected afterwards
    /// needed a new fire command.
    ///
    /// The selection is recorded when the sequence starts. While the sequence
    /// runs, a tube only counts as checked if it was checked then (and still
    /// is, so unchecking one mid-sequence still holds it back). The check boxes
    /// on screen and the torpedo-room launch lever are not affected.
    /// </summary>
    public static class FireCommand
    {
        static readonly HashSet<ExtendedTorpedoLauncher> selectedAtFire = new HashSet<ExtendedTorpedoLauncher>();
        public static bool InSequence;
        static bool loggedHeldBack;

        public static void Record(PlayerShip ship)
        {
            selectedAtFire.Clear();
            var tubes = ship != null ? ship.TorpedoLaunchers : null;
            if (tubes == null) return;
            foreach (var t in tubes)
                if (t != null && t.IsMarkedForLaunch) selectedAtFire.Add(t);
        }

        public static bool WasSelectedAtFire(ExtendedTorpedoLauncher tube)
        {
            bool ok = selectedAtFire.Contains(tube);
            if (!ok && !loggedHeldBack)
            {
                loggedHeldBack = true;
                Debug.Log(ScopeTdcControlsMod.Tag + " tube '" + tube.name + "' was checked after the fire command; held for the next one.");
            }
            return ok;
        }
    }

    /// <summary>Fire pressed: remember which tubes are selected right now.</summary>
    [HarmonyPatch(typeof(PlayerShip), "LaunchAllTorpedoesInternal")]
    static class PlayerShip_LaunchAllTorpedoesInternal_Patch
    {
        static void Prefix(PlayerShip __instance)
        {
            try { FireCommand.Record(__instance); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Marks the frames in which the game's fire sequence is stepping.</summary>
    [HarmonyPatch]
    static class LaunchSequence_MoveNext_Patch
    {
        static MethodBase TargetMethod()
        {
            // the compiler-generated iterator class behind LaunchAllTorpedoesInternal
            var iterator = typeof(PlayerShip).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(t => t.Name.Contains("LaunchAllTorpedoesInternal"));
            return iterator != null ? AccessTools.Method(iterator, "MoveNext") : null;
        }

        static bool Prepare()
        {
            bool found = TargetMethod() != null;
            if (!found) Debug.LogWarning(ScopeTdcControlsMod.Tag + " fire sequence not found; realistic fire command unavailable.");
            return found;
        }

        static void Prefix() { FireCommand.InSequence = true; }
        static void Finalizer() { FireCommand.InSequence = false; }
    }

    /// <summary>
    /// Inside the fire sequence, a tube checked after the command doesn't count.
    /// (The getter is tiny, so the runtime may inline it into the sequence and
    /// skip this patch; the Launch patch below is the reliable guard.)
    /// </summary>
    [HarmonyPatch(typeof(ExtendedTorpedoLauncher), "IsMarkedForLaunch", MethodType.Getter)]
    static class ExtendedTorpedoLauncher_GetMarked_Patch
    {
        static void Postfix(ExtendedTorpedoLauncher __instance, ref bool __result)
        {
            if (!__result || !FireCommand.InSequence || !GyroConfig.SingleTorpedoLaunchFix) return;
            if (!FireCommand.WasSelectedAtFire(__instance)) __result = false;
        }
    }

    /// <summary>
    /// The actual launch the fire sequence performs for each tube. During the
    /// sequence, a tube that wasn't selected when fire was pressed doesn't
    /// launch. This method is far too large to be inlined, so it always applies.
    /// The torpedo-room levers use a different method and are not affected.
    /// </summary>
    [HarmonyPatch(typeof(TorpedoLauncher), "Launch", new Type[] {
        typeof(Entity), typeof(float), typeof(float), typeof(float), typeof(float),
        typeof(int), typeof(bool), typeof(bool), typeof(float), typeof(float) })]
    static class TorpedoLauncher_Launch_Patch
    {
        static bool Prefix(TorpedoLauncher __instance)
        {
            if (!FireCommand.InSequence || !GyroConfig.SingleTorpedoLaunchFix) return true;
            var tube = __instance as ExtendedTorpedoLauncher;
            if (tube == null) return true;
            return FireCommand.WasSelectedAtFire(tube);
        }
    }
}
