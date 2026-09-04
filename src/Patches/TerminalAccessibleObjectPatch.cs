using System;
using HarmonyLib;
using LethalDoors.Doors;
using LethalDoors.Traps;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Remote trap control. <c>CallFunctionFromTerminal()</c> runs on the client that
    /// typed the code (it then fires ServerRpcs to sync), and is shared by mines,
    /// turrets and big doors.
    ///
    ///   • Big door -> leave it to vanilla (crush is handled by the door patch below).
    ///   • Mine      -> detonate instead of disable.
    ///   • Turret    -> berserk instead of disable.
    ///
    /// Returning false suppresses the vanilla disable.
    /// </summary>
    [HarmonyPatch(typeof(TerminalAccessibleObject), nameof(TerminalAccessibleObject.CallFunctionFromTerminal))]
    internal static class TerminalAccessibleObject_CallFunction_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(TerminalAccessibleObject __instance)
        {
            try
            {
                // Identify what this code actually belongs to FIRST. isBigDoor cannot be trusted
                // as a filter: vanilla declares it `= true` by default, so a modded trap that adds
                // a TerminalAccessibleObject at runtime without clearing the flag (DefendFacility's
                // mini turret, for one) would be mistaken for a door and silently ignored.
                var mine = __instance.GetComponent<Landmine>()
                    ?? __instance.GetComponentInParent<Landmine>()
                    ?? __instance.GetComponentInChildren<Landmine>();

                var turret = mine != null ? null
                    : __instance.GetComponent<Turret>()
                      ?? __instance.GetComponentInParent<Turret>()
                      ?? __instance.GetComponentInChildren<Turret>();

                if (mine == null && turret == null)
                    return true; // a real door (or something we do not handle) -> vanilla

                if (!Plugin.Config.EnableRemoteControl.Value)
                    return true; // remote control off -> vanilla disable

                if (mine != null)
                {
                    RemoteControlManager.HandleMine(mine);
                    return false;
                }

                // Returns false when it wants vanilla to run (TurretAlwaysBerserk = false and the
                // error roll failed), so the turret is simply disabled as usual.
                return !RemoteControlManager.HandleTurret(turret);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"CallFunctionFromTerminal prefix error: {e}");
            }

            return true; // unknown object -> vanilla behaviour
        }
    }

    /// <summary>
    /// Big powered facility door close hook. <c>SetDoorOpen(bool open)</c> is the single
    /// method that actually applies a big door's open/closed state on EVERY client — called
    /// locally by the toggler AND via SetDoorOpenClientRpc on everyone else — and it drives
    /// the door's AnimatedObjectTrigger. (The vanilla terminal-code path goes through
    /// SetDoorToggleLocalClient, never SetDoorLocalClient, which is why the earlier hook
    /// never fired.) The actual crush is frame-driven by <see cref="TerminalDoorCrush"/>.
    /// </summary>
    [HarmonyPatch(typeof(TerminalAccessibleObject), nameof(TerminalAccessibleObject.SetDoorOpen))]
    internal static class TerminalAccessibleObject_SetDoor_Patch
    {
        // Capture the door's open-state BEFORE the call so we only react to a genuine
        // open -> closed transition (SetDoorOpen is a no-op when the state doesn't change).
        [HarmonyPrefix]
        private static void Prefix(TerminalAccessibleObject __instance, out bool __state)
        {
            __state = __instance.isDoorOpen;
        }

        [HarmonyPostfix]
        private static void Postfix(TerminalAccessibleObject __instance, bool open, bool __state)
        {
            if (!__instance.isBigDoor) return;
            if (__instance.GetComponentInParent<Turret>() != null ||
                __instance.GetComponentInParent<Landmine>() != null) return; // a trap, not a door
            if (!Plugin.Config.Enabled.Value) return;
            if (Plugin.Config.AffectedDoors.Value == AffectedDoors.ShipDoor) return; // ship-only mode

            // Only on a real transition from open to closed.
            if (!(__state && !open)) return;

            TerminalDoorCrush.OnClosed(__instance);
        }
    }
}
