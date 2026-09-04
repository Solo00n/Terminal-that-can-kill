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

        /// <summary>
        /// Vanilla "deactivate mine". Goes through ToggleMineServerRpc, so it reaches every
        /// client, and it flips the flag the game itself checks before detonating on a player.
        /// Detonate() does NOT check that flag, so we can still set the mine off on monsters.
        /// </summary>
        public static void BroadcastHijack(Landmine mine)
        {
            if (mine == null || Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(MineHijackBlink(mine));
        }

        /// <summary>
        /// Mines have no RPC carrying a value we could tag, so the hijack is signalled as a
        /// deliberate off-then-on BLINK. A power system only ever sets a steady state, so a
        /// blink is unmistakably ours — this is what stops a blackout being mistaken for a hack.
        /// </summary>
        private static System.Collections.IEnumerator MineHijackBlink(Landmine mine)
        {
            try { mine.ToggleMine(false); } catch { yield break; }
            yield return null;
            if (mine != null) { try { mine.ToggleMine(true); } catch { /* ignore */ } }
        }

        // ================================================================== ALLIED TURRET
        // An allied turret runs the NORMAL vanilla pipeline (Detection -> Charging -> Firing):
        // that is what produces the warning beep, the firing SFX and the bullet particles, and
        // the server syncs the mode change itself. All we do is hand the turret a target and let
        // it get on with it. Berserk is deliberately NOT used — that is the malfunction spin.

        /// <summary>Turret is in its Firing state (mode 2), i.e. actually shooting.</summary>
        public static bool IsFiring(Turret turret)
        {
            try { return turret != null && (int)turret.turretMode == 2; }
            catch { return false; }
        }

        /// <summary>
        /// Give the turret a target so its own state machine engages. targetPlayerWithRotation
        /// only has to be non-null for Update to enter the targeting branch; what it actually
        /// aims at is targetTransform, which our SetTargetToPlayerBody patch points at the
        /// monster.
        /// </summary>
        public static void EngageTurret(Turret turret)
        {
            if (turret == null) return;
            try
            {
                // Deliberately does NOT touch turretActive. Whether a trap has power belongs to
                // the game (and to power mods such as DefendFacility, which re-applies trap power
                // several times a second). Forcing it on here fought that and made the turret
                // flicker on and off forever. We only ever change WHO the trap targets.
                if (turret.targetPlayerWithRotation == null)
                    turret.targetPlayerWithRotation = LocalPlayer;
            }
            catch { /* ignore */ }
        }

        /// <summary>Drop the target so the turret goes back to its normal scanning sweep.</summary>
        public static void DisengageTurret(Turret turret)
        {
            if (turret == null) return;
            try { turret.targetPlayerWithRotation = null; }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Sentinel passed as "playerWhoTriggered" to mark a berserk broadcast as a HIJACK
        /// rather than a real berserk. Vanilla only ever sends a real player id (0-3) there, and
        /// nothing else in the game or in other mods sends this value, so it is an unambiguous
        /// private message that still rides a vanilla, already-synced RPC.
        ///
        /// This replaced using the turret power toggle as the carrier: the facility power system
        /// (PowerSwitchable) toggles turrets too, so re-enabling on every shutdown made our patch
        /// fight the breaker and flip the turret on/off forever.
        /// </summary>
        public const int HijackSignal = 424242;

        /// <summary>Broadcast "this turret has been hijacked" to every client.</summary>
        public static void BroadcastHijack(Turret turret)
        {
            if (turret == null) return;
            try { turret.EnterBerserkModeServerRpc(HijackSignal); }
            catch (Exception e) { Plugin.Log.LogError($"BroadcastHijack failed: {e}"); }
        }

        /// <summary>Cancel the berserk that the carrier RPC would otherwise cause.</summary>
        public static void CancelBerserk(Turret turret)
        {
            if (turret == null) return;
            try
            {
                if (IsBerserk(turret)) turret.SwitchTurretMode(0); // straight back to scanning
            }
            catch { /* ignore */ }
        }

        // ================================================================== HACK CUE
        /// <summary>
        /// Audible "successfully hacked" confirmation, built from the trap's own clips: the
        /// vanilla shutdown sound has just played, so a beat later we play the power-up clip.
        /// The result reads as powered down -> came back online on your side.
        /// </summary>
        public static void PlayHackCue(Turret turret)
        {
            if (turret == null || !Plugin.Config.AlliedHackSound.Value) return;
            if (Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(HackCue(turret.mainAudio, turret.turretActivate));
        }

        public static void PlayHackCue(Landmine mine)
        {
            if (mine == null || !Plugin.Config.AlliedHackSound.Value) return;
            if (Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(HackCue(mine.mineAudio, mine.minePress));
        }

        private static System.Collections.IEnumerator HackCue(AudioSource source, AudioClip clip)
        {
            yield return new WaitForSeconds(0.35f);
            if (source == null || clip == null) yield break;

            float pitch = source.pitch;
            try
            {
                source.pitch = 1.25f;                 // higher = "handshake accepted"
                source.PlayOneShot(clip, 0.9f);
                WalkieTalkie.TransmitOneShotAudio(source, clip);
            }
            catch { /* ignore */ }

            yield return new WaitForSeconds(0.12f);
            if (source != null) source.pitch = pitch;
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
