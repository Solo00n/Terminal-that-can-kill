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

        /// <summary>True while the turret is in the berserk state (mode 3).</summary>
        public static bool IsBerserk(Turret turret)
        {
            try { return turret != null && (int)turret.turretMode == TurretModeBerserk; }
            catch { return false; }
        }

        /// <summary>
        /// Host-only: extend an ongoing rampage.
        ///
        /// berserkTimer has TWO meanings in vanilla: while <c>enteringBerserkMode</c> it is the
        /// 1.3s spin-up countdown (after which the game sets it to 9s and starts firing), and
        /// afterwards it is the firing countdown. Writing to it during the spin-up stretches
        /// that wind-up instead of prolonging the rampage — the turret just whines and spins
        /// without shooting. So only ever top up the FIRING phase.
        /// </summary>
        public static void SustainBerserk(Turret turret, float minTimer)
        {
            if (turret == null || !IsHost) return;
            try
            {
                if (turret.enteringBerserkMode) return;      // never touch the spin-up countdown
                if (!IsBerserk(turret)) return;              // only while actually berserk
                turret.berserkTimer = Mathf.Max(turret.berserkTimer, minTimer);
            }
            catch { /* ignore */ }
        }

        /// <summary>Vanilla "disable turret" (what the terminal code normally does).</summary>
        public static void DisableTurret(Turret turret)
        {
            if (turret == null) return;
            try { turret.ToggleTurretEnabled(false); }
            catch (Exception e) { Plugin.Log.LogError($"DisableTurret failed: {e}"); }
        }

        // ================================================================== ALLIED TURRET
        /// <summary>
        /// Hold an allied turret in its firing state. Berserk is the only firing state that
        /// sustains itself without a player target, and the CheckForPlayersInLineOfSight patch
        /// stops it hurting players, so it becomes a pure anti-monster gun.
        /// </summary>
        public static void KeepTurretFiring(Turret turret)
        {
            if (turret == null) return;
            try
            {
                turret.turretActive = true;
                if (!IsBerserk(turret)) turret.SwitchTurretMode(TurretModeBerserk);
                SustainBerserk(turret, 2f); // host-only, firing phase only
            }
            catch { /* ignore */ }
        }

        /// <summary>Drop an allied turret back to normal scanning.</summary>
        public static void StopTurretFiring(Turret turret)
        {
            if (turret == null || !IsHost) return;
            try
            {
                turret.SwitchTurretMode(0);      // Detection
                turret.SetToModeClientRpc(0);    // and tell everyone else
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Point an allied turret at a world position. Mirrors what vanilla does when it has a
        /// target (compass LookAt, then RotateTowards), and runs from our Update postfix so it
        /// wins over the turret's own scanning rotation for that frame.
        /// </summary>
        public static void AimTurretAt(Turret turret, Vector3 worldPoint)
        {
            if (turret == null || turret.turretRod == null) return;
            try
            {
                Vector3 dir = worldPoint - turret.turretRod.position;
                if (dir.sqrMagnitude < 0.0001f) return;

                Quaternion desired = Quaternion.LookRotation(dir);
                turret.turretRod.rotation = Quaternion.RotateTowards(
                    turret.turretRod.rotation, desired, 140f * Time.deltaTime);
            }
            catch { /* ignore */ }
        }

        /// <summary>Host-side damage to an enemy (host owns enemy AI, so this syncs).</summary>
        public static void HurtEnemy(EnemyAI enemy, int force)
        {
            if (enemy == null || !IsHost) return;
            try
            {
                if (enemy.enemyType != null && !enemy.enemyType.canDie) return; // unkillable enemies
                enemy.HitEnemyOnLocalClient(force, Vector3.zero, null, true, -1);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HurtEnemy failed: {e}");
            }
        }

        /// <summary>Simple LOS check against world/room geometry.</summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            try
            {
                int mask = StartOfRound.Instance != null
                    ? StartOfRound.Instance.collidersAndRoomMask
                    : ~0;
                return !Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore);
            }
            catch { return true; }
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
