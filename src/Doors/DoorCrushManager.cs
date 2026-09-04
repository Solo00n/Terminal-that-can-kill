using UnityEngine;

namespace LethalDoors.Doors
{
    public enum DoorKind { Ship, Terminal }

    /// <summary>
    /// Executes a "door fully closed" event against a <see cref="DoorZone"/> — a thin slab
    /// in the doorway opening.
    ///
    /// Multiplayer model (no custom NetworkObject required):
    ///   • Players  : every client evaluates the zone against its OWN local player and kills
    ///                only that one. KillPlayer syncs the death. => one authoritative kill.
    ///   • Monsters : only the host iterates enemies and kills them (host owns enemy AI).
    /// </summary>
    internal static class DoorCrushManager
    {
        public static void ExecuteCrush(DoorKind kind, DoorZone zone)
        {
            var cfg = Plugin.Config;
            if (!cfg.Enabled.Value) return;
            if (!AppliesTo(kind)) return;
            if (LethalDoorsSession.Instance != null && LethalDoorsSession.Instance.InSafePeriod) return;

            // ---- Players (local only) --------------------------------------
            if (cfg.KillPlayers.Value)
            {
                var local = GameCompat.LocalPlayer;
                if (GameCompat.IsPlayerAlive(local))
                {
                    if (zone.Contains(local.transform.position))
                    {
                        GameCompat.KillLocalPlayer(local, cfg.PlayerDeathAnimationId.Value);
                        Plugin.Log.LogInfo($"{kind} door crushed local player.");
                    }
                    else if (Plugin.Verbose)
                    {
                        // Tuning aid for the zone size — off unless VerboseLogging is on, since it
                        // fires on every door close for every player and formats three floats and
                        // a Vector3 to build a line nobody normally reads.
                        Vector3 l = zone.ToLocal(local.transform.position);
                        Plugin.Log.LogInfo(
                            $"{kind} door closed: local player not in doorway. " +
                            $"Offset from zone (width={l.x:F2}, height={l.y:F2}, through={l.z:F2}); " +
                            $"half-extents={zone.HalfExtents}.");
                    }
                }
            }

            // ---- Monsters (host only) --------------------------------------
            if (cfg.KillMonsters.Value && GameCompat.IsHost && RoundManager.Instance != null)
            {
                var list = RoundManager.Instance.SpawnedEnemies;
                for (int i = 0; i < list.Count; i++)
                {
                    var enemy = list[i];
                    if (enemy == null) continue;
                    if (!EnemyWhitelist.CanBeCrushed(enemy)) continue;
                    if (!zone.Contains(enemy.transform.position)) continue;

                    GameCompat.KillEnemy(enemy);
                    Plugin.Log.LogInfo($"{kind} door crushed enemy '{GameCompat.EnemyName(enemy)}'.");
                }
            }
        }

        /// <summary>
        /// Damage-over-time tick for the local player while trapped in a shut doorway.
        /// Monsters are always instant-killed (handled by ExecuteCrush).
        /// </summary>
        public static void ExecuteDamageTick(DoorKind kind, DoorZone zone, ref float accumulator)
        {
            var cfg = Plugin.Config;
            if (!cfg.Enabled.Value || !cfg.KillPlayers.Value) return;
            if (!AppliesTo(kind)) return;
            if (LethalDoorsSession.Instance != null && LethalDoorsSession.Instance.InSafePeriod) return;

            var local = GameCompat.LocalPlayer;
            if (!GameCompat.IsPlayerAlive(local)) return;
            if (!zone.Contains(local.transform.position)) { accumulator = 0f; return; }

            accumulator += cfg.DamageAmount.Value * Time.deltaTime;
            if (accumulator < 1f) return;

            int dmg = Mathf.FloorToInt(accumulator);
            accumulator -= dmg;
            try
            {
                local.DamagePlayer(dmg, true, true, CauseOfDeath.Snipping, cfg.PlayerDeathAnimationId.Value, false, default);
            }
            catch
            {
                try { local.DamagePlayer(dmg); } catch { /* ignore */ }
            }
        }

        private static bool AppliesTo(DoorKind kind)
        {
            switch (Plugin.Config.AffectedDoors.Value)
            {
                case AffectedDoors.ShipDoor: return kind == DoorKind.Ship;
                case AffectedDoors.TerminalDoors: return kind == DoorKind.Terminal;
                default: return true; // Both
            }
        }
    }
}
