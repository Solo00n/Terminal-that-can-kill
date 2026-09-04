using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Lets terminal codes reach traps the vanilla lookup cannot see.
    ///
    /// Terminal.CallFunctionInAccessibleTerminalObject finds its targets with
    /// FindObjectsOfType&lt;TerminalAccessibleObject&gt;() — without includeInactive — so a code
    /// whose component is disabled, or whose GameObject is inactive, is silently unreachable.
    /// BrutalCompanyMinusExtraReborn disables the terminal object on its grabbable turret exactly
    /// like that, which is why such a turret ignored its code completely: it never berserked, never
    /// switched off, and never reached this mod at all.
    ///
    /// This is additive on purpose: vanilla runs first and we only pick up what it missed, so the
    /// normal path is untouched and other terminal mods are unaffected.
    /// </summary>
    [HarmonyPatch(typeof(Terminal), "CallFunctionInAccessibleTerminalObject")]
    internal static class TerminalReachPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Terminal __instance, string word)
        {
            if (string.IsNullOrEmpty(word)) return;

            try
            {
                var all = Object.FindObjectsOfType<TerminalAccessibleObject>(true);
                if (all == null || all.Length == 0) return;

                // Scanning the whole scene is expensive, and this used to do it twice on every
                // single code typed. An object that is active AND enabled is in the vanilla
                // lookup whichever way Unity filters, so unless a MATCHING object is hidden there
                // is nothing here for us and the second scan is pure waste.
                bool anyHidden = false;
                for (int i = 0; i < all.Length; i++)
                {
                    var tao = all[i];
                    if (tao != null && !tao.isActiveAndEnabled && tao.objectCode == word)
                    {
                        anyHidden = true;
                        break;
                    }
                }
                if (!anyHidden) return;

                // Something is hidden: now it is worth asking exactly what vanilla saw, compared
                // by identity rather than by guessing Unity's active/enabled rules.
                var handled = new HashSet<TerminalAccessibleObject>(
                    Object.FindObjectsOfType<TerminalAccessibleObject>());

                for (int i = 0; i < all.Length; i++)
                {
                    var tao = all[i];
                    if (tao == null || tao.objectCode != word) continue;
                    if (handled.Contains(tao)) continue; // vanilla already called it

                    Plugin.Log.LogInfo(
                        $"Terminal code '{word}' reached a hidden trap object on '{tao.gameObject.name}' " +
                        "(vanilla could not see it).");

                    __instance.broadcastedCodeThisFrame = true;
                    tao.CallFunctionFromTerminal();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Terminal reach patch failed: {e}");
            }
        }
    }
}
