using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalDoors.Traps
{
    /// <summary>
    /// Hijacked traps: a trap can flip to the players side and start reacting to monsters
    /// instead of to players.
    ///
    /// NETWORKING: the client that typed the code rolls, and then announces the result on a
    /// vanilla RPC carrying a marker only this mod ever sends. Everyone else obeys that marker
    /// and never rolls again, so clients cannot disagree — and no custom netcode is needed.
    /// </summary>
    internal static class AlliedTraps
    {
        private sealed class TurretState
        {
            public float Until;
            public float NextHit;
            public float NextScan;
            public EnemyAI Target;
            public int HitForce;
            public bool Engaged;
            public Transform AimHelper;
        }

        private sealed class MineState
        {
            public float Until;
            public float NextScan;
        }

        private static readonly Dictionary<Turret, TurretState> _turrets = new Dictionary<Turret, TurretState>();
        private static readonly Dictionary<Landmine, MineState> _mines = new Dictionary<Landmine, MineState>();

        private static readonly List<Turret> _scratchTurrets = new List<Turret>();
        private static readonly List<Landmine> _scratchMines = new List<Landmine>();

        // Enemy scans are the only recurring cost this feature adds, so they run a few times
        // a second rather than every frame.
        private const float ScanInterval = 0.25f;

        // Vanilla turrets damage on a 0.21s tick and take a target from full to dead in two of
        // them (50 damage against 100 player health). An allied turret matches that exactly, so
        // it is no weaker and no stronger than a normal one — the force is simply scaled to the
        // monster's health instead of a player's.
        private const float VanillaFireInterval = 0.21f;

        public static void Clear()
        {
            foreach (var kv in _turrets) DestroyHelper(kv.Value);
            _turrets.Clear();
            _mines.Clear();
            TrapVisuals.Clear();
        }

        // ------------------------------------------------------------------ roll
        /// <summary>
        /// Rolled fresh for each command, on the client that typed the code.
        ///
        /// This used to be seeded per trap so that every client computed the same answer without
        /// networking. That is no longer needed — the hijack is broadcast explicitly — and it had
        /// a nasty consequence: the result was fixed forever, so a trap that rolled badly could
        /// NEVER be hijacked no matter how often you tried, and at a 0.25 chance three traps in
        /// four were permanently un-hijackable. A fresh roll makes the configured chance behave
        /// the way you would expect.
        /// </summary>
        public static bool RollAllied(NetworkBehaviour trap)
        {
            if (trap == null) return false;
            if (!Plugin.Config.EnableAlliedTraps.Value) return false;

            float chance = Plugin.Config.AlliedChance.Value;
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;

            float roll = Random.value;
            bool allied = roll < chance;
            Plugin.Log.LogInfo($"Hijack roll {roll:F3} vs {chance:F3} -> {(allied ? "HIJACK" : "no")}.");
            return allied;
        }

        private static float ExpiryFromConfig()
        {
            float dur = Plugin.Config.AlliedDuration.Value;
            return dur <= 0f ? float.MaxValue : Time.time + dur; // 0 = rest of the round
        }

        // ------------------------------------------------------------------ registration
        public static void MakeAllied(Turret turret)
        {
            if (turret == null) return;
            if (!_turrets.TryGetValue(turret, out var s)) { s = new TurretState(); _turrets[turret] = s; }
            s.Until = ExpiryFromConfig();
            TrapVisuals.ApplyAllied(turret);
        }

        public static void MakeAllied(Landmine mine)
        {
            if (mine == null) return;
            if (!_mines.TryGetValue(mine, out var s)) { s = new MineState(); _mines[mine] = s; }
            s.Until = ExpiryFromConfig();
            TrapVisuals.ApplyAllied(mine);
        }

        /// <summary>Aim marker for an engaged allied turret (used by the SetTargetToPlayerBody patch).</summary>
        public static bool TryGetAimTarget(Turret turret, out Transform aim)
        {
            aim = null;
            if (turret == null || _turrets.Count == 0) return false;
            if (!_turrets.TryGetValue(turret, out var s)) return false;
            if (!s.Engaged || s.AimHelper == null || Time.time >= s.Until) return false;
            aim = s.AimHelper;
            return true;
        }

        private static void DestroyHelper(TurretState s)
        {
            if (s != null && s.AimHelper != null) Object.Destroy(s.AimHelper.gameObject);
            if (s != null) s.AimHelper = null;
        }

        public static bool IsAllied(Turret turret)
        {
            if (turret == null || _turrets.Count == 0) return false;
            return _turrets.TryGetValue(turret, out var s) && Time.time < s.Until;
        }

        public static bool IsAllied(Landmine mine)
        {
            if (mine == null || _mines.Count == 0) return false;
            return _mines.TryGetValue(mine, out var s) && Time.time < s.Until;
        }

        // ------------------------------------------------------------------ per-frame driving
        /// <summary>Called once per frame from the session component. Cheap when nothing is allied.</summary>
        public static void Tick()
        {
            if (_turrets.Count > 0) TickTurrets();
            if (_mines.Count > 0) TickMines();
        }

        private static void TickTurrets()
        {
            float now = Time.time;

            _scratchTurrets.Clear();
            foreach (var kv in _turrets) _scratchTurrets.Add(kv.Key);

            for (int i = 0; i < _scratchTurrets.Count; i++)
            {
                var turret = _scratchTurrets[i];
                if (turret == null) { _turrets.Remove(turret); continue; }

                var s = _turrets[turret];
                if (s.Target != null && GameCompat.IsEnemyDead(s.Target)) s.Target = null;
                if (now >= s.Until)
                {
                    Plugin.Log.LogInfo("Allied turret: hijack expired, back to normal.");
                    TrapVisuals.Restore(turret);
                    GameCompat.DisengageTurret(turret);
                    DestroyHelper(s);
                    _turrets.Remove(turret);
                    continue;
                }

                if (now >= s.NextScan)
                {
                    s.NextScan = now + ScanInterval;
                    var found = FindEnemyForTurret(turret);
                    if (found != s.Target)
                    {
                        s.Target = found;
                        // Two ticks to a kill, mirroring what a normal turret does to a player.
                        s.HitForce = found != null ? Mathf.Max(1, Mathf.CeilToInt(found.enemyHP / 2f)) : 1;
                    }
                }

                bool hasTarget = s.Target != null && !GameCompat.IsEnemyDead(s.Target);

                if (!hasTarget)
                {
                    // Idle: normal scanning sweep with the (now green) laser. It cannot acquire
                    // players — the CheckForPlayersInLineOfSight patch hides them from it.
                    if (s.Engaged) { s.Engaged = false; GameCompat.DisengageTurret(turret); }
                    continue;
                }

                // Keep the aim marker on the monster. The turret's own TurnTowardsTargetIfHasLOS
                // will track this transform, so aiming, LOS and the firing arc all stay vanilla.
                if (s.AimHelper == null)
                    s.AimHelper = new GameObject("TTCK_AlliedAimTarget").transform;
                s.AimHelper.position = s.Target.transform.position + Vector3.up * 0.7f;

                s.Engaged = true;
                GameCompat.EngageTurret(turret); // vanilla Detection -> Charging -> Firing takes over

                // Damage only while it is genuinely in its Firing state, so the hits line up with
                // the muzzle flash and sound. Host-authoritative (the host owns enemy AI).
                if (GameCompat.IsHost && GameCompat.IsFiring(turret) && now >= s.NextHit)
                {
                    s.NextHit = now + VanillaFireInterval;
                    GameCompat.HurtEnemy(s.Target, s.HitForce);
                }
            }
        }

        private static void TickMines()
        {
            if (!GameCompat.IsHost) return; // only the host decides that an allied mine went off

            float now = Time.time;
            float radius = Plugin.Config.AlliedMineTriggerRadius.Value;
            float sqr = radius * radius;

            _scratchMines.Clear();
            foreach (var kv in _mines) _scratchMines.Add(kv.Key);

            for (int i = 0; i < _scratchMines.Count; i++)
            {
                var mine = _scratchMines[i];
                if (mine == null) { _mines.Remove(mine); continue; }

                var s = _mines[mine];
                if (now >= s.Until) { TrapVisuals.Restore(mine); _mines.Remove(mine); continue; }
                if (now < s.NextScan) continue;
                s.NextScan = now + ScanInterval;

                var enemies = RoundManager.Instance != null ? RoundManager.Instance.SpawnedEnemies : null;
                if (enemies == null) continue;

                Vector3 pos = mine.transform.position;
                for (int e = 0; e < enemies.Count; e++)
                {
                    var enemy = enemies[e];
                    if (enemy == null || GameCompat.IsEnemyDead(enemy)) continue;
                    if ((enemy.transform.position - pos).sqrMagnitude > sqr) continue;

                    Plugin.Log.LogInfo($"Allied mine detonating on '{GameCompat.EnemyName(enemy)}'.");
                    TrapVisuals.Restore(mine);
                    GameCompat.DetonateMine(mine);
                    _mines.Remove(mine);
                    break;
                }
            }
        }

        private static EnemyAI FindEnemyForTurret(Turret turret)
        {
            var enemies = RoundManager.Instance != null ? RoundManager.Instance.SpawnedEnemies : null;
            if (enemies == null || enemies.Count == 0) return null;

            Transform eye = turret.centerPoint != null ? turret.centerPoint : turret.transform;
            float range = Plugin.Config.AlliedTurretRange.Value;
            float bestSqr = range * range;
            EnemyAI best = null;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || GameCompat.IsEnemyDead(enemy)) continue;

                Vector3 aimAt = enemy.transform.position + Vector3.up * 0.7f;
                float sqr = (aimAt - eye.position).sqrMagnitude;
                if (sqr > bestSqr) continue;

                // Respect the turret's own firing arc, exactly like TurnTowardsTargetIfHasLOS
                // does — otherwise we would hand it a target it can never face.
                if (turret.forwardFacingPos != null &&
                    Vector3.Angle(aimAt - eye.position, turret.forwardFacingPos.forward) > turret.rotationRange)
                    continue;

                if (!GameCompat.HasLineOfSight(eye.position, aimAt)) continue;

                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }
    }
}
