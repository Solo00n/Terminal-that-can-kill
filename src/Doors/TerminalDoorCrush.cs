using System.Collections.Generic;
using UnityEngine;

namespace LethalDoors.Doors
{
    /// <summary>
    /// Frame-driven crush for big facility (terminal-code) doors. Replaces the old
    /// coroutine approach, which silently cancelled itself whenever a door was reopened
    /// within its close-animation window — so rapid toggling meant the crush never ran.
    ///
    /// The SetDoorOpen postfix reports a real open→closed transition via <see cref="OnClosed"/>;
    /// <see cref="Tick"/> (called every frame from <see cref="LethalDoorsSession"/>) then runs
    /// the crush once the close animation has had time to finish, and keeps DOT going while shut.
    /// </summary>
    internal static class TerminalDoorCrush
    {
        private sealed class Tracked
        {
            public TerminalAccessibleObject Door;
            public float CloseTime;
            public bool Crushed;
            public float DotAccumulator;
        }

        private static readonly List<Tracked> _tracked = new List<Tracked>();

        /// <summary>Called when a big door genuinely transitions from open to closed.</summary>
        public static void OnClosed(TerminalAccessibleObject door)
        {
            if (door == null) return;

            for (int i = 0; i < _tracked.Count; i++)
            {
                if (_tracked[i].Door == door)
                {
                    _tracked[i].CloseTime = Time.time;
                    _tracked[i].Crushed = false;
                    _tracked[i].DotAccumulator = 0f;
                    return;
                }
            }

            _tracked.Add(new Tracked { Door = door, CloseTime = Time.time });
            Plugin.Log.LogInfo($"Terminal door '{door.gameObject.name}' closing — tracking for crush.");
        }

        /// <summary>Per-frame processing of all currently-closing terminal doors.</summary>
        public static void Tick()
        {
            if (_tracked.Count == 0) return;

            float closeSeconds = Plugin.Config.TerminalDoorCloseSeconds.Value;

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var t = _tracked[i];

                // Drop doors that were destroyed or reopened.
                if (t.Door == null || t.Door.isDoorOpen)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                if (Time.time - t.CloseTime < closeSeconds) continue; // still animating shut

                DoorZone zone = BuildTerminalZone(t.Door);

                if (Plugin.Config.DamageMode.Value == DamageMode.DamageOverTime)
                {
                    DoorCrushManager.ExecuteDamageTick(DoorKind.Terminal, zone, ref t.DotAccumulator);
                }
                else if (!t.Crushed)
                {
                    t.Crushed = true;
                    DoorCrushManager.ExecuteCrush(DoorKind.Terminal, zone);
                }
            }
        }

        public static void Clear() => _tracked.Clear();

        private static DoorZone BuildTerminalZone(TerminalAccessibleObject door)
        {
            var c = Plugin.Config;
            // The TerminalAccessibleObject sits on the door itself, so its transform IS the
            // door. Centre the slab at the door origin (raised toward mid-height) and take the
            // "through" axis from the door's forward. The diagnostic log prints the player's
            // local offset (width/height/through) so the geometry can be tuned.
            Vector3 center = door.transform.position + Vector3.up * (c.DoorwayHeight.Value * 0.25f);
            Vector3 through = door.transform.forward;
            return DoorZone.FromThroughAxis(center, through,
                c.DoorwayWidth.Value, c.DoorwayHeight.Value, c.DoorwayThickness.Value);
        }
    }
}
