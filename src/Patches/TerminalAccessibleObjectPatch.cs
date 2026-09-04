using System;
using System.Collections.Generic;
using HarmonyLib;
using LethalDoors.Doors;
using LethalDoors.Traps;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Remembers what each TerminalAccessibleObject actually belongs to.
    ///
    /// Working it out means walking the hierarchy up and down looking for a Landmine and then a
    /// Turret — six searches — and the answer never changes for a given object. That was being
    /// redone on every terminal code AND on every door toggle, and SetDoorOpen runs on every
    /// client each time anyone opens or shuts a door.
    /// </summary>
    internal static class TaoTrapLookup
    {
        private sealed class Entry
        {
            public Landmine Mine;
            public Turret Turret;
        }

        private static readonly Dictionary<TerminalAccessibleObject, Entry> _cache =
            new Dictionary<TerminalAccessibleObject, Entry>();
        private static readonly List<TerminalAccessibleObject> _dead =
            new List<TerminalAccessibleObject>();

        /// <summary>Wipe on a new level — every object in here belongs to the old scene.</summary>
        public static void Clear() { _cache.Clear(); _dead.Clear(); }

        public static void Resolve(TerminalAccessibleObject tao, out Landmine mine, out Turret turret)
        {
            mine = null;
            turret = null;
            if (tao == null) return;

            if (!_cache.TryGetValue(tao, out var e))
            {
                Prune(); // only on a first sight, which is rare

                e = new Entry();
                e.Mine = tao.GetComponent<Landmine>()
                    ?? tao.GetComponentInParent<Landmine>()
                    ?? tao.GetComponentInChildren<Landmine>();

                e.Turret = e.Mine != null ? null
                    : tao.GetComponent<Turret>()
                      ?? tao.GetComponentInParent<Turret>()
                      ?? tao.GetComponentInChildren<Turret>();

                _cache[tao] = e;
            }

            mine = e.Mine;
            turret = e.Turret;
        }

        /// <summary>True when this code belongs to a trap rather than to a real door.</summary>
        public static bool IsTrap(TerminalAccessibleObject tao)
        {
            Resolve(tao, out var mine, out var turret);
            return mine != null || turret != null;
        }

        private static void Prune()
        {
            if (_cache.Count == 0) return;

            _dead.Clear();
            foreach (var kv in _cache)
                if (kv.Key == null) _dead.Add(kv.Key);

            for (int i = 0; i < _dead.Count; i++) _cache.Remove(_dead[i]);
            _dead.Clear();
        }
    }

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
                TaoTrapLookup.Resolve(__instance, out var mine, out var turret);

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
            if (TaoTrapLookup.IsTrap(__instance)) return; // a trap, not a door
            if (!Plugin.Config.Enabled.Value) return;
            if (Plugin.Config.AffectedDoors.Value == AffectedDoors.ShipDoor) return; // ship-only mode

            // Only on a real transition from open to closed.
            if (!(__state && !open)) return;

            TerminalDoorCrush.OnClosed(__instance);
        }
    }
}
