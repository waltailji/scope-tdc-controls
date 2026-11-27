using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
            try { GyroCounterUI.OnFireCommand(__instance); } // firing circuit lamp on the firing selector
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
    /// The fire sequence's wait before the first launch: 1.5 s in the base game, 0.25 s with
    /// enhanced torpedo tube handling (the doors are already open and the tubes flooded, so
    /// only the firing impulse is left). The 4 s between salvo tubes is unchanged. Rewrites the
    /// one "1.5" that feeds a WaitForSeconds; if a game update moves it, the patch does nothing
    /// and says so in the log.
    /// </summary>
    [HarmonyPatch]
    static class LaunchSequence_FireDelay_Patch
    {
        public const float BaseDelay = 1.5f, EnhancedDelay = 0.25f;

        static MethodBase TargetMethod()
        {
            var iterator = typeof(PlayerShip).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(t => t.Name.Contains("LaunchAllTorpedoesInternal"));
            return iterator != null ? AccessTools.Method(iterator, "MoveNext") : null;
        }

        static bool Prepare() { return TargetMethod() != null; }

        public static float FireDelay() { return TubeDoors.Enabled ? EnhancedDelay : BaseDelay; }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var wait = AccessTools.Constructor(typeof(WaitForSeconds), new[] { typeof(float) });
            var delay = AccessTools.Method(typeof(LaunchSequence_FireDelay_Patch), "FireDelay");
            var hits = new List<int>();
            for (int i = 0; i + 1 < list.Count; i++)
                if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float && (float)list[i].operand == BaseDelay
                    && list[i + 1].opcode == OpCodes.Newobj && Equals(list[i + 1].operand, wait))
                    hits.Add(i);
            if (hits.Count != 1)
            {
                Debug.LogWarning(ScopeTdcControlsMod.Tag + " fire sequence: found " + hits.Count + " 1.5 s waits (expected 1); left as is");
                return list;
            }
            int at = hits[0];
            var call = new CodeInstruction(OpCodes.Call, delay);
            call.labels.AddRange(list[at].labels);
            call.blocks.AddRange(list[at].blocks);
            list[at] = call;
            Debug.Log(ScopeTdcControlsMod.Tag + " fire sequence: first-launch wait follows enhanced tube handling");
            return list;
        }
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
