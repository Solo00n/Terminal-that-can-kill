using System;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalDoors.Traps;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Compatibility with Fair AI, which replaces the turret AI wholesale: it prefixes
    /// Turret.Update, CheckForPlayersInLineOfSight, SetTargetToPlayerBody and
    /// TurnTowardsTargetIfHasLOS, keeps its own target type (player OR monster) on a FAIR_AI
    /// component, and damages the player from its own raycast in ApplyDamageToLocalPlayer.
    ///
    /// That last point is why a hijacked turret still shot the crew: our block sits on the
    /// vanilla line-of-sight check, and Fair AI never consults it for damage. Worse, Fair AI
    /// reads turret.targetPlayerWithRotation to decide what it is shooting — and this mod writes
    /// the local player there to drive the vanilla firing sequence, effectively handing Fair AI a
    /// player as the target.
    ///
    /// Fair AI also hurts monsters itself (Plugin.AttackTargets), so when it is present we hand
    /// the turret over to it completely and only carve out the hijack: an allied turret may not
    /// take a player as its target and may not damage players. Everything is soft-bound by
    /// reflection, so the mod runs unchanged when Fair AI is absent.
    /// </summary>
    internal static class FairAiCompat
    {
        private const string PluginGuid = "GoldenKitten.FairAI";

        /// <summary>True when Fair AI owns the turret AI; our own aiming must then stand down.</summary>
        public static bool Active { get; private set; }

        private static PropertyInfo _isPlayerProp;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(PluginGuid)) return;

                Type patchType = FindType("TurretAIPatch");
                if (patchType == null)
                {
                    Plugin.Log.LogWarning("Fair AI is loaded but its turret patch class was not found — " +
                                          "hijacked turrets may still target players.");
                    return;
                }

                // Each hook stands on its own: Fair AI may rename or drop one in a future
                // version, and losing one must not cost us the others.
                bool damage = Hook(harmony, patchType, "ApplyDamageToLocalPlayer", nameof(BlockAlliedPlayerDamage));

                // Fair AI does its own player sweep, so our CheckForPlayersInLineOfSight block
                // never sees it. Cutting the sweep off at the source makes FindBestTarget fall
                // through to "no targets", which is a state Fair AI already handles.
                bool sweep = Hook(harmony, patchType, "SweepForPlayer", nameof(BlockAlliedPlayerSweep));

                Hook(harmony, patchType, "SetCurrentTarget", nameof(BlockAlliedPlayerTarget));

                // Standing down is only safe once Fair AI can actually be told to spare the crew.
                Active = damage && sweep;
                Plugin.Log.LogInfo(Active
                    ? "Fair AI detected — it drives the turrets, and hijacked ones are carved out of its player targeting and damage."
                    : "Fair AI detected but its turret hooks did not match; falling back to our own allied turret handling.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Fair AI compatibility patch failed: {e.Message}");
            }
        }

        private static bool Hook(Harmony harmony, Type patchType, string method, string prefix)
        {
            try
            {
                var target = AccessTools.Method(patchType, method);
                if (target == null)
                {
                    Plugin.Log.LogWarning($"Fair AI: {method} not found, that hook is skipped.");
                    return false;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(FairAiCompat), prefix));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Fair AI: could not patch {method} — {e.Message}");
                return false;
            }
        }

        /// <summary>Fair AI damages the player from its own raycast — never for one of ours.</summary>
        private static bool BlockAlliedPlayerDamage(Turret turret)
        {
            return !AlliedTraps.IsAllied(turret);
        }

        /// <summary>An allied turret finds no players when Fair AI sweeps for them.</summary>
        private static bool BlockAlliedPlayerSweep(Turret turret, ref PlayerControllerB __result)
        {
            if (!AlliedTraps.IsAllied(turret)) return true;
            __result = null;
            return false;
        }

        /// <summary>
        /// Belt and braces for any other path into Fair AI's target. The target is a private
        /// struct, so it is inspected through __args by reflection.
        /// An allied turret simply never accepts a player as its target; monster targets and
        /// clearing the target both pass through untouched.
        /// </summary>
        private static bool BlockAlliedPlayerTarget(Turret turret, object[] __args)
        {
            try
            {
                if (!AlliedTraps.IsAllied(turret)) return true;
                if (__args == null || __args.Length < 3) return true;

                object target = __args[2];
                if (target == null) return true;

                if (_isPlayerProp == null || _isPlayerProp.DeclaringType != target.GetType())
                    _isPlayerProp = target.GetType().GetProperty("IsPlayer");

                if (_isPlayerProp != null && _isPlayerProp.GetValue(target) is bool isPlayer && isPlayer)
                    return false; // refuse the player target
            }
            catch { /* never break Fair AI on our account */ }
            return true;
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "FairAI") continue;
                try
                {
                    var t = asm.GetType(name, false);
                    if (t != null) return t;
                    foreach (var candidate in asm.GetTypes())
                        if (candidate.Name == name) return candidate;
                }
                catch { /* ignore a partially loadable assembly */ }
            }
            return null;
        }
    }
}
