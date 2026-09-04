using System.Collections;
using UnityEngine;

namespace LethalDoors.Traps
{
    /// <summary>
    /// Turns a terminal "disable" command into a detonate (mine) or rampage (turret),
    /// with a configurable chance of a critical malfunction.
    ///
    /// The random roll happens exactly once, on the client that typed the code, and
    /// every resulting effect is fired through a vanilla, already-synced game method
    /// (Landmine.ExplodeMineServerRpc / Turret.Hit), so all clients see the same result
    /// without any custom networking.
    /// </summary>
    internal static class RemoteControlManager
    {
        // ------------------------------------------------------------------ MINE
        public static void HandleMine(Landmine mine)
        {
            if (mine == null) return;

            // Hijack takes priority: this mine flips to our side instead of blowing up now.
            if (AlliedTraps.RollAllied(mine))
            {
                // Off-then-on blink: reaches every client and cannot be confused with a power
                // system switching mines off (see Landmine_AlliedSync_Patch).
                GameCompat.BroadcastHijack(mine);
                AlliedTraps.MakeAllied(mine);
                Plugin.Log.LogInfo("Remote mine HIJACKED — it now ignores players and waits for monsters.");
                return;
            }

            float chance = Plugin.Config.MineErrorChance.Value;
            float roll = Random.value;
            bool error = roll < chance;

            if (!error && Plugin.Config.MineWarnBeforeDetonate.Value)
            {
                // Normal command with a short warning beep before the blast.
                Plugin.Instance.StartCoroutine(WarnThenDetonate(mine));
                Plugin.Log.LogInfo($"Remote mine: armed, warning beep (roll {roll:F3} >= {chance:F3}).");
                return;
            }

            // Instant detonation (this is also the "error = no delay" behaviour).
            GameCompat.DetonateMine(mine);

            if (error)
            {
                Plugin.Log.LogWarning($"Remote mine CRITICAL ERROR (roll {roll:F3} < {chance:F3}) — chain detonation!");
                ChainDetonateNearby(mine.transform.position);
            }
            else
            {
                Plugin.Log.LogInfo($"Remote mine: detonated (roll {roll:F3} >= {chance:F3}, no error).");
            }
        }

        private static IEnumerator WarnThenDetonate(Landmine mine)
        {
            // A brief telegraph so a non-error mine feels different from an error one.
            yield return new WaitForSeconds(0.85f);
            GameCompat.DetonateMine(mine);
        }

        /// <summary>
        /// Amplify an error blast by detonating neighbouring mines. Each neighbour is
        /// triggered through its own ServerRpc, so the whole chain is network-synced.
        /// This is how <c>MineErrorDamageMultiplier</c> manifests as extra damage.
        /// </summary>
        private static void ChainDetonateNearby(Vector3 origin)
        {
            float radius = Plugin.Config.MineErrorChainRadius.Value * Plugin.Config.MineErrorDamageMultiplier.Value;
            float sqr = radius * radius;

            var mines = Object.FindObjectsOfType<Landmine>();
            foreach (var m in mines)
            {
                if (m == null) continue;
                if ((m.transform.position - origin).sqrMagnitude > sqr) continue;
                // Small stagger so the explosions cascade visually rather than all at once.
                Plugin.Instance.StartCoroutine(DelayedDetonate(m, Random.Range(0.02f, 0.25f)));
            }
        }

        private static IEnumerator DelayedDetonate(Landmine mine, float delay)
        {
            yield return new WaitForSeconds(delay);
            GameCompat.DetonateMine(mine);
        }

        // ------------------------------------------------------------------ TURRET

        // Vanilla berserk length: 1.3s spin-up + 9s firing (see Turret.Update, case Berserk).
        private const float VanillaBerserkSeconds = 10.3f;
        private const float RampageTickSeconds = 0.5f;

        /// <summary>
        /// Handle a turret code. Returns true when we took over (the vanilla "disable" must be
        /// suppressed), false when vanilla should run normally.
        /// </summary>
        public static bool HandleTurret(Turret turret)
        {
            if (turret == null) return false;

            // Hijack takes priority over the berserk/disable outcome.
            if (AlliedTraps.RollAllied(turret))
            {
                // Tagged broadcast: reaches every client, cannot be confused with anyone else's
                // event (including the facility power system), and the berserk it would cause is
                // cancelled on arrival so the turret never spins out.
                GameCompat.BroadcastHijack(turret);
                AlliedTraps.MakeAllied(turret);
                Plugin.Log.LogInfo("Remote turret HIJACKED — it now ignores players and hunts monsters.");
                return true;
            }

            float chance = Plugin.Config.TurretErrorChance.Value;
            float roll = Random.value;
            bool error = roll < chance;

            // With TurretAlwaysBerserk = false the roll decides berserk-vs-disable outright,
            // which makes the configured chance directly observable.
            if (!error && !Plugin.Config.TurretAlwaysBerserk.Value)
            {
                Plugin.Log.LogInfo($"Remote turret: roll {roll:F3} >= {chance:F3} -> vanilla disable.");
                return false;
            }

            GameCompat.BerserkTurret(turret);

            if (error)
            {
                float extra = Plugin.Config.TurretRampageDuration.Value;
                Plugin.Log.LogWarning(
                    $"Remote turret CRITICAL ERROR (roll {roll:F3} < {chance:F3}) — rampage extended by {extra:0.#}s.");
                Plugin.Instance.StartCoroutine(Rampage(turret, extra));
            }
            else
            {
                Plugin.Log.LogInfo($"Remote turret: berserk (roll {roll:F3} >= {chance:F3}, no error).");
            }
            return true;
        }

        /// <summary>
        /// Hold the rampage open for <paramref name="extraSeconds"/> beyond vanilla.
        ///
        /// Important: we do NOT re-issue the berserk trigger on a timer. Re-sending
        /// EnterBerserkModeServerRpc while the turret is already berserk replays the entry on
        /// remote clients, which is what made a single command look like the turret "entering
        /// berserk" five times. We only re-trigger if it genuinely dropped out of berserk;
        /// otherwise we just keep the firing countdown from expiring.
        /// </summary>
        private static IEnumerator Rampage(Turret turret, float extraSeconds)
        {
            float end = Time.time + VanillaBerserkSeconds + extraSeconds;
            var wait = new WaitForSeconds(RampageTickSeconds);

            while (Time.time < end)
            {
                if (turret == null) yield break;

                if (!GameCompat.IsBerserk(turret))
                    GameCompat.BerserkTurret(turret);                 // settled early -> relight
                else
                    GameCompat.SustainBerserk(turret, RampageTickSeconds * 3f);

                yield return wait;
            }
        }
    }
}
