using GameNetcodeStuff;
using HarmonyLib;
using LethalDoors.Traps;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// An allied turret must not see players at all.
    ///
    /// Turret.CheckForPlayersInLineOfSight is the single choke point for BOTH behaviours:
    ///   • Detection/LOS use it to pick targetPlayerWithRotation.
    ///   • The Berserk and Firing branches use `CheckForPlayersInLineOfSight(3f) == localPlayer`
    ///     as the condition for DamagePlayer/KillPlayer.
    /// Returning null from it therefore makes the turret unable to target OR hurt players,
    /// without touching any of the firing visuals.
    /// </summary>
    [HarmonyPatch(typeof(Turret), nameof(Turret.CheckForPlayersInLineOfSight))]
    internal static class Turret_IgnorePlayers_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Turret __instance, ref PlayerControllerB __result)
        {
            if (__result == null) return;
            if (AlliedTraps.IsAllied(__instance)) __result = null;
        }
    }

    /// <summary>
    /// Point an allied turret at its monster instead of at a player body.
    ///
    /// Vanilla Update calls SetTargetToPlayerBody() to derive targetTransform from
    /// targetPlayerWithRotation, then TurnTowardsTargetIfHasLOS() aims at targetTransform and
    /// sets hasLineOfSight — which is what lets Charging advance to Firing. By swapping
    /// targetTransform for our aim marker, the turret runs its completely normal firing
    /// sequence (warning beep, tracking, muzzle flash, firing SFX) against a monster.
    /// </summary>
    [HarmonyPatch(typeof(Turret), "SetTargetToPlayerBody")]
    internal static class Turret_AimAtMonster_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Turret __instance)
        {
            if (!AlliedTraps.TryGetAimTarget(__instance, out var aim)) return true;
            __instance.targetingDeadPlayer = false;
            __instance.targetTransform = aim;
            return false; // skip the vanilla player-body lookup
        }
    }

    /// <summary>
    /// An allied mine ignores players. Vanilla OnTriggerEnter only ever reacts to the LOCAL
    /// player (and to owned props/ragdolls), so suppressing it entirely while allied means the
    /// mine simply cannot be set off by the team. Monsters are handled by AlliedTraps.Tick,
    /// because vanilla mines have no enemy trigger at all.
    /// </summary>
    [HarmonyPatch(typeof(Landmine), "OnTriggerEnter")]
    internal static class Landmine_IgnorePlayers_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(Landmine __instance)
        {
            return !AlliedTraps.IsAllied(__instance); // false = skip vanilla trigger
        }
    }

    // =====================================================================================
    // Making the hijack known to EVERY client.
    //
    // The terminal only runs CallFunctionFromTerminal on the client that typed the code, so
    // registering the hijack there alone would leave the trap hostile for everyone else.
    // Instead the hijack fires a vanilla RPC that the game already syncs, and each client
    // re-derives "is this trap hijackable?" from the deterministic roll. Same answer
    // everywhere, still no custom networking.
    // =====================================================================================

    /// <summary>
    /// Turret hijack arrives as the vanilla "turret powered down" broadcast.
    ///
    /// The shutdown is only a carrier: it is the one turret RPC any client may raise that
    /// reaches everyone, it costs no custom networking, and it sounds exactly like the start of
    /// a hack. Every client then re-derives the same deterministic roll and, if the trap is
    /// hijackable, powers it straight back up on your side. Berserk is deliberately not used —
    /// that would make a hijacked turret spin out instead of behaving normally.
    /// </summary>
    [HarmonyPatch(typeof(Turret), "ToggleTurretEnabledLocalClient")]
    internal static class Turret_AlliedSync_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Turret __instance, bool enabled)
        {
            if (enabled) return;                       // only a shutdown can be a hijack

            bool already = AlliedTraps.IsAllied(__instance);
            if (!already && !AlliedTraps.RollAllied(__instance)) return;

            AlliedTraps.MakeAllied(__instance);
            GameCompat.BringTurretOnline(__instance);   // the shutdown was just the handshake
            if (!already) GameCompat.PlayHackCue(__instance);
        }
    }

    /// <summary>Mine hijack arrives as the vanilla "mine deactivated" broadcast.</summary>
    [HarmonyPatch(typeof(Landmine), nameof(Landmine.ToggleMineEnabledLocalClient))]
    internal static class Landmine_AlliedSync_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Landmine __instance, bool enabled)
        {
            if (enabled) return;                                  // only a shutdown can be a hijack
            if (AlliedTraps.IsAllied(__instance)) return;
            if (!AlliedTraps.RollAllied(__instance)) return;

            AlliedTraps.MakeAllied(__instance);
            GameCompat.PlayHackCue(__instance);
        }
    }
}
