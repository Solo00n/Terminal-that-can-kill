using System;
using GameNetcodeStuff; // PlayerControllerB lives here (unlike EnemyAI/Landmine which are global)
using Unity.Netcode;
using UnityEngine;

namespace LethalDoors
{
    /// <summary>
    /// Thin wrapper over the parts of the game we call into. Signatures below were
    /// verified by compiling against the V81 stub assembly (LethalCompany.GameLibs.Steam
    /// 81.0.5-ngd.0). A handful of *behavioural* assumptions remain (documented inline);
    /// everything is wrapped defensively so a surprise never crashes the whole mod.
    /// </summary>
    internal static class GameCompat
    {
        // ------------------------------------------------------------------ Networking helpers
        public static bool IsHost =>
            NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

        public static PlayerControllerB LocalPlayer =>
            GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;

        public static int LocalPlayerId
        {
            get
            {
                var p = LocalPlayer;
                return p != null ? (int)p.playerClientId : -1;
            }
        }

        public static bool IsPlayerAlive(PlayerControllerB p) =>
            p != null && p.isPlayerControlled && !p.isPlayerDead;

        // ================================================================== PLAYER DEATH
        /// <summary>
        /// Kill the local player, mirroring the Barber (ClaySurgeonAI) death exactly:
        /// the body is launched up (Vector3.up * 14) with cause "Snipping" and ragdoll
        /// index 7. Runs on the owning client, so KillPlayer syncs the death itself.
        /// The ragdoll index stays configurable via <paramref name="deathAnimation"/>.
        /// </summary>
        public static void KillLocalPlayer(PlayerControllerB player, int deathAnimation)
        {
            try
            {
                player.KillPlayer(Vector3.up * 14f, true, CauseOfDeath.Snipping, deathAnimation, default);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"KillLocalPlayer failed: {e}");
            }
        }

        // ================================================================== ENEMY DEATH
        /// <summary>Kill an enemy (host only — host owns enemy AI). Syncs to clients.</summary>
        public static void KillEnemy(EnemyAI enemy)
        {
            try
            {
                enemy.KillEnemyOnOwnerClient(true);
            }
            catch (Exception)
            {
                try { enemy.KillEnemyServerRpc(true); }
                catch (Exception e) { Plugin.Log.LogError($"KillEnemy failed: {e}"); }
            }
        }

        public static bool IsEnemyDead(EnemyAI enemy)
        {
            try { return enemy == null || enemy.isEnemyDead; }
            catch { return enemy == null; }
        }

        public static string EnemyName(EnemyAI enemy)
        {
            try
            {
                if (enemy != null && enemy.enemyType != null && !string.IsNullOrEmpty(enemy.enemyType.enemyName))
                    return enemy.enemyType.enemyName;
            }
            catch { /* ignore */ }
            return enemy != null ? enemy.GetType().Name : "unknown";
        }

        // ================================================================== MINE
        /// <summary>Fully networked mine explosion (ExplodeMineServerRpc broadcasts).</summary>
        public static void DetonateMine(Landmine mine)
        {
            if (mine == null) return;
            try
            {
                if (mine.hasExploded) return; // don't double-fire
                mine.ExplodeMineServerRpc();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"DetonateMine failed: {e}");
            }
        }

        // ================================================================== TURRET
        // TurretMode value for "Berserk" (spins wildly & fires at everything). Verified
        // against V81: SwitchTurretMode(3) is what Turret.Hit() uses to go berserk.
        private const int TurretModeBerserk = 3;

        /// <summary>
        /// Send a turret berserk, mirroring exactly what the game does when a turret is
        /// hit: SwitchTurretMode(3) locally (because EnterBerserkModeClientRpc deliberately
        /// SKIPS the triggering client), then EnterBerserkModeServerRpc to berserk it for
        /// everyone else. Missing the local call was why the caller's turret never reacted.
        ///
        /// SwitchTurretMode is private in-game; the assembly publicizer makes it callable.
        /// </summary>
        public static void BerserkTurret(Turret turret)
        {
            if (turret == null) return;
            try
            {
                turret.turretActive = true;         // ensure it isn't in a disabled state
                turret.SwitchTurretMode(TurretModeBerserk); // local, immediate (the caller)
                turret.EnterBerserkModeServerRpc(LocalPlayerId); // sync to all other clients
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BerserkTurret failed: {e}");
            }
        }

        /// <summary>Host-only: keep a rampaging turret's berserk timer topped up so it lasts.</summary>
        public static void SustainBerserk(Turret turret, float minTimer)
        {
            if (turret == null || !IsHost) return;
            try { turret.berserkTimer = Mathf.Max(turret.berserkTimer, minTimer); }
            catch { /* ignore */ }
        }

        // NOTE: no custom crush sound needed — the game already plays StartOfRound.playerCrushDeath
        // for CauseOfDeath.Crushing when the dead body spawns.

        // ================================================================== GEOMETRY
        /// <summary>Best guess at a doorway centre: a child collider's bounds, else the transform.</summary>
        public static Vector3 DoorCenter(Component door)
        {
            if (door == null) return Vector3.zero;
            try
            {
                var col = door.GetComponentInChildren<Collider>();
                if (col != null) return col.bounds.center;
            }
            catch { /* ignore */ }
            return door.transform.position;
        }
    }
}
