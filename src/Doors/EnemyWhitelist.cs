using System;
using System.Collections.Generic;
using System.Linq;

namespace LethalDoors.Doors
{
    /// <summary>
    /// Decides which monsters may be crushed by a door. We use an EXCLUSION list
    /// (configurable) instead of a whitelist so the mod stays compatible with modded
    /// enemies by default — anything not explicitly excluded and physically able to
    /// stand in the doorway can be crushed.
    /// </summary>
    internal static class EnemyWhitelist
    {
        private static string[] _excluded = Array.Empty<string>();
        private static string _cachedRaw;

        private static void RefreshCache()
        {
            var raw = Plugin.Config.ExcludedEnemies.Value ?? string.Empty;
            if (raw == _cachedRaw) return;
            _cachedRaw = raw;
            _excluded = raw.Split(',')
                .Select(s => s.Trim().ToLowerInvariant())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        /// <summary>
        /// True if this enemy is allowed to be killed by a closing door.
        /// An enemy is excluded when its internal name contains any excluded token
        /// (case-insensitive substring match — forgiving of naming variants).
        /// </summary>
        public static bool CanBeCrushed(EnemyAI enemy)
        {
            if (enemy == null || GameCompat.IsEnemyDead(enemy)) return false;

            RefreshCache();
            string name = GameCompat.EnemyName(enemy).ToLowerInvariant();

            foreach (var token in _excluded)
                if (name.Contains(token))
                    return false;

            return true;
        }
    }
}
