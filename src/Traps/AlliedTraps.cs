using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalDoors.Traps
{
    /// <summary>
    /// Hijacked traps: a trap can flip to the players side and start reacting to monsters
    /// instead of to players.
    ///
    /// NETWORKING: the allied roll is DETERMINISTIC, not random per client. It is derived from
    /// the synced level seed plus the trap NetworkObjectId, so every client independently
    /// computes the same answer for the same trap — no custom RPC needed, and re-entering the
    /// code cannot re-roll the result. This mirrors how the game itself seeds per-level
    /// randomness (System.Random(randomMapSeed + ...)).
    /// </summary>
    internal static class AlliedTraps
    {
        private sealed class TurretState
        {
            public float Until;
            public float NextHit;
            public float NextScan;
            public EnemyAI Target;
            public bool Firing;
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

        public static void Clear()
        {
            _turrets.Clear();
            _mines.Clear();
            TrapVisuals.Clear();
        }

        // ------------------------------------------------------------------ roll
        /// <summary>
        /// Deterministic per-trap allied roll: identical on every client for a given trap and
        /// level, so all clients agree without any networking.
        /// </summary>
        public static bool RollAllied(NetworkBehaviour trap)
        {
            if (trap == null) return false;
            if (!Plugin.Config.EnableAlliedTraps.Value) return false;

            float chance = Plugin.Config.AlliedChance.Value;
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;

            var sor = StartOfRound.Instance;
            int levelPart = sor != null ? sor.randomMapSeed + sor.currentLevelID : 0;
            int idPart = (int)(trap.NetworkObjectId & 0x7FFFFFFF);

            int seed;
            unchecked { seed = levelPart * 397 + idPart; }

            return new System.Random(seed).NextDouble() < chance;
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
                if (now >= s.Until)
                {
                    Plugin.Log.LogInfo("Allied turret: hijack expired, back to normal.");
                    TrapVisuals.Restore(turret);
                    GameCompat.StopTurretFiring(turret);
                    _turrets.Remove(turret);
                    continue;
                }

                if (now >= s.NextScan)
                {
                    s.NextScan = now + ScanInterval;
                    s.Target = FindEnemyForTurret(turret);
                }

                bool hasTarget = s.Target != null && !GameCompat.IsEnemyDead(s.Target);

                if (!hasTarget)
                {
                    // Idle: leave the turret in its normal scanning state so the (now green)
                    // laser sweeps as usual. It cannot acquire players — the
                    // CheckForPlayersInLineOfSight patch hides them from it.
                    if (s.Firing) { s.Firing = false; GameCompat.StopTurretFiring(turret); }
                    continue;
                }

                // Target acquired: berserk is the only firing state that needs no player target,
                // and with players hidden from the turret it can only hurt monsters.
                if (!s.Firing) s.Firing = true;
                GameCompat.KeepTurretFiring(turret);
                GameCompat.AimTurretAt(turret, s.Target.transform.position + Vector3.up * 0.6f);

                // Damage is host-authoritative (the host owns enemy AI).
                if (GameCompat.IsHost && now >= s.NextHit)
                {
                    s.NextHit = now + Plugin.Config.AlliedTurretHitInterval.Value;
                    GameCompat.HurtEnemy(s.Target, Plugin.Config.AlliedTurretDamage.Value);
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

                Vector3 aimAt = enemy.transform.position + Vector3.up * 0.6f;
                float sqr = (aimAt - eye.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (!GameCompat.HasLineOfSight(eye.position, aimAt)) continue;

                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }
    }
}
