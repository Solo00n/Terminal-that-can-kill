using System;
using System.Collections;
using HarmonyLib;
using LethalDoors.Traps;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// DefendFacility's mini turret belongs to the crew — they bought it and carried it in — so
    /// treating it as facility hardware that has to be hacked never made sense. It is on our side
    /// from the moment it exists and stays there for good: no terminal code, no roll, no expiry.
    ///
    /// It is identified by the MiniTurretTag component DefendFacility puts on the same GameObject
    /// as the Turret. Bound by reflection, so none of this exists without that mod.
    /// </summary>
    internal static class MiniTurretCompat
    {
        private const string PluginGuid = Plugin.DefendFacilityGuid;
        private const string AssemblyName = "DefendFacility";
        private const string TagTypeName = "MiniTurretTag";

        private static Type _tagType;

        /// <summary>True once DefendFacility's mini turret marker has been located.</summary>
        public static bool Active => _tagType != null;

        public static void Init()
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(PluginGuid)) return;

                _tagType = FindType(AssemblyName, TagTypeName);
                Plugin.Log.LogInfo(Active
                    ? "DefendFacility detected — its mini turret is always allied and never needs hacking."
                    : "DefendFacility detected but its mini turret marker was not found; mini turrets behave like ordinary turrets.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"DefendFacility compatibility setup failed: {e.Message}");
            }
        }

        public static bool IsMiniTurret(Turret turret)
        {
            if (!Active || turret == null) return false;
            try { return turret.GetComponent(_tagType) != null; }
            catch { return false; }
        }

        /// <summary>
        /// Put a mini turret on our side. Safe to call more than once — a turret that is already
        /// allied is left alone, so this never resets a hijack or re-runs the tint.
        /// </summary>
        public static void TryRegister(Turret turret)
        {
            if (!Active) return;
            if (!Plugin.Config.EnableAlliedTraps.Value) return;
            if (!Plugin.Config.MiniTurretAlwaysAllied.Value) return;
            if (turret == null || AlliedTraps.IsAllied(turret)) return;
            if (!IsMiniTurret(turret)) return;

            AlliedTraps.MakeAlliedPermanently(turret);
            Plugin.Log.LogInfo("Mini turret is on the crew's side — allied from the start, no code needed.");

            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(TintNextFrame(turret));
        }

        /// <summary>Catch mini turrets that already exist — carried over, or dropped earlier.</summary>
        public static void RegisterExisting()
        {
            if (!Active) return;

            try
            {
                var turrets = UnityEngine.Object.FindObjectsOfType<Turret>(true);
                for (int i = 0; i < turrets.Length; i++) TryRegister(turrets[i]);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Mini turret sweep failed: {e.Message}");
            }
        }

        private static IEnumerator TintNextFrame(Turret turret)
        {
            // DefendFacility paints its own skin from MiniTurretTag.Start, and Unity does not
            // define the order of Start between two components on one object. Waiting a frame puts
            // our green on last whichever way round the two mods happened to wake up.
            yield return null;
            if (turret != null) TrapVisuals.ApplyAllied(turret);
        }

        private static Type FindType(string assemblyName, string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != assemblyName) continue;
                try
                {
                    var t = asm.GetType(typeName, false);
                    if (t != null) return t;
                    foreach (var candidate in asm.GetTypes())
                        if (candidate.Name == typeName) return candidate;
                }
                catch { /* ignore a partially loadable assembly */ }
            }
            return null;
        }
    }

    /// <summary>
    /// Every client runs Start for the turret it owns a copy of, so each one reaches the same
    /// conclusion on its own — the mini turret needs no sync to be allied everywhere.
    /// </summary>
    [HarmonyPatch(typeof(Turret), "Start")]
    internal static class Turret_MiniTurretAllied_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Turret __instance) => MiniTurretCompat.TryRegister(__instance);
    }
}
