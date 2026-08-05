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

            bool error = Random.value < Plugin.Config.MineErrorChance.Value;

            if (!error && Plugin.Config.MineWarnBeforeDetonate.Value)
            {
                // Normal command with a short warning beep before the blast.
                Plugin.Instance.StartCoroutine(WarnThenDetonate(mine));
                Plugin.Log.LogInfo("Remote mine: armed (warning beep).");
                return;
            }

            // Instant detonation (this is also the "error = no delay" behaviour).
            GameCompat.DetonateMine(mine);

            if (error)
            {
                Plugin.Log.LogWarning("Remote mine CRITICAL ERROR — chain detonation!");
                ChainDetonateNearby(mine.transform.position);
            }
            else
            {
                Plugin.Log.LogInfo("Remote mine: detonated.");
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
        public static void HandleTurret(Turret turret)
        {
            if (turret == null) return;

            bool error = Random.value < Plugin.Config.TurretErrorChance.Value;

            // Normal: turret goes berserk (spins & fires at everything, players included).
            GameCompat.BerserkTurret(turret);

            if (error)
            {
                Plugin.Log.LogWarning("Remote turret CRITICAL ERROR — sustained rampage!");
                Plugin.Instance.StartCoroutine(Rampage(turret, Plugin.Config.TurretRampageDuration.Value));
            }
            else
            {
                Plugin.Log.LogInfo("Remote turret: berserk.");
            }
        }

        /// <summary>
        /// Keep the turret raging for the configured duration. On the host we top up the
        /// berserk timer directly (smoothest); on any client we also re-issue the berserk
        /// trigger through the vanilla ServerRpc so it re-ignites if it settles early.
        /// Vanilla berserk lasts ~10s on its own, so this mainly matters for longer values.
        /// </summary>
        private static IEnumerator Rampage(Turret turret, float duration)
        {
            float end = Time.time + duration;
            while (Time.time < end)
            {
                if (turret == null) yield break;
                GameCompat.SustainBerserk(turret, 3f);
                GameCompat.BerserkTurret(turret);
                yield return new WaitForSeconds(1.5f);
            }
        }
    }
}
