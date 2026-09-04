using HarmonyLib;
using LethalDoors.Doors;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Attaches the per-session state tracker (post-landing grace period) once a round
    /// begins. Door hooks are handled by their own patches, so nothing else is needed here.
    /// </summary>
    [HarmonyPatch(typeof(StartOfRound))]
    internal static class StartOfRoundPatch
    {
        [HarmonyPatch(nameof(StartOfRound.Start))]
        [HarmonyPostfix]
        private static void OnStart(StartOfRound __instance)
        {
            if (LethalDoorsSession.Instance == null)
                __instance.gameObject.AddComponent<LethalDoorsSession>();

            // Reset per-object state carried over from a previous level.
            Doors.TerminalDoorCrush.Clear();
            TurretHeadFlipPatch.ClearStates();
            Traps.AlliedTraps.Clear();
            Landmine_AlliedSync_Patch.Clear();
        }
    }
}
