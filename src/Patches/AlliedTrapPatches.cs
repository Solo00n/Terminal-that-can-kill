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
}
