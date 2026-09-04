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
    // Instead the hijack rides a vanilla RPC the game already syncs, carrying a marker only we
    // ever send. The client that typed the code decides; everyone else simply obeys the marker
    // and never rolls again, so no one can disagree. Still no custom networking.
    // =====================================================================================

    /// <summary>
    /// Turret hijack arrives tagged on the vanilla berserk broadcast.
    ///
    /// The berserk RPC carries an int ("who triggered it"), and vanilla only ever puts a real
    /// player id in it. We put a sentinel there instead, which makes the message unmistakably
    /// ours: no other system sends it, so we never react to somebody else's event. The berserk
    /// the carrier would cause is cancelled immediately, so a hijacked turret does not spin out.
    ///
    /// The previous carrier (the turret power toggle) was wrong: the facility power system
    /// toggles turrets as well, so re-enabling on every shutdown fought the breaker and flipped
    /// the turret on and off forever.
    /// </summary>
    [HarmonyPatch(typeof(Turret), nameof(Turret.EnterBerserkModeClientRpc))]
    internal static class Turret_AlliedSync_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Turret __instance, int playerWhoTriggered)
        {
            if (playerWhoTriggered != GameCompat.HijackSignal) return; // a genuine berserk

            bool already = AlliedTraps.IsAllied(__instance);
            AlliedTraps.MakeAllied(__instance);
            GameCompat.CancelBerserk(__instance);
            if (!already) GameCompat.PlayHackCue(__instance);
        }
    }

    /// <summary>
    /// Mine hijack arrives as a deliberate off-then-on BLINK of the mine's power.
    ///
    /// A single shutdown is ambiguous — the facility power system (and power mods such as
    /// DefendFacility) switches mines off too, and treating that as a hack would silently turn a
    /// quarter of the map's mines friendly during every blackout. A blink is something only we
    /// ever produce.
    /// </summary>
    [HarmonyPatch(typeof(Landmine), nameof(Landmine.ToggleMineEnabledLocalClient))]
    internal static class Landmine_AlliedSync_Patch
    {
        private const float BlinkWindow = 0.6f;
        private static readonly System.Collections.Generic.Dictionary<Landmine, float> _offAt =
            new System.Collections.Generic.Dictionary<Landmine, float>();

        public static void Clear() => _offAt.Clear();

        [HarmonyPostfix]
        private static void Postfix(Landmine __instance, bool enabled)
        {
            if (__instance == null) return;

            if (!enabled) { _offAt[__instance] = UnityEngine.Time.time; return; }

            // Powering back on: only a hack if it came right after our own shutdown.
            if (!_offAt.TryGetValue(__instance, out float off)) return;
            _offAt.Remove(__instance);
            if (UnityEngine.Time.time - off > BlinkWindow) return;

            // The blink itself IS the decision — never re-roll here, or clients would disagree
            // with the player who actually issued the command.
            if (AlliedTraps.IsAllied(__instance)) return;

            AlliedTraps.MakeAllied(__instance);
            GameCompat.PlayHackCue(__instance);
        }
    }
}
