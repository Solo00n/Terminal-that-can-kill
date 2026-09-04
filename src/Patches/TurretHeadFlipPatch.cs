using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Turns a turret's head 180° when it comes out of berserk, making it rest facing the
    /// opposite way until the next berserk.
    ///
    ///   • enter berserk (mode → 3): remember the head's current yaw.
    ///   • exit berserk  (3 → anything): smoothly turn the head to remembered + 180° and
    ///     hold it there as the new resting facing (it still turns to fire at a detected player).
    ///   • while berserk (mode == 3): don't touch the rotation.
    ///
    /// Patched as a POSTFIX on Turret.Update because the turret sets turretRod.rotation at the
    /// end of its own Update every frame — running after it lets our value win.
    ///
    /// TurretMode values (verified in V81): 0 Detection, 1 Charging, 2 Firing, 3 Berserk.
    /// </summary>
    [HarmonyPatch(typeof(Turret), "Update")]
    internal static class TurretHeadFlipPatch
    {
        private const int Berserk = 3;
        private const int Detection = 0;

        private sealed class State
        {
            public Transform Head;
            public int PrevMode = -1;
            public float RememberedYaw;
            public bool HasResting;
            public float RestingYaw;
            public bool Animating;
            public float AnimTime;
            public float AnimDuration;
            public float AnimFromYaw;
            public float AnimToYaw;
        }

        private static readonly Dictionary<Turret, State> _states = new Dictionary<Turret, State>();
        private static readonly List<Turret> _dead = new List<Turret>();

        /// <summary>Wipe per-turret state on a new round (turrets are recreated each level).</summary>
        public static void ClearStates() => _states.Clear();

        [HarmonyPostfix]
        private static void Postfix(Turret __instance)
        {
            // This runs for EVERY turret on EVERY frame, so the idle case has to be almost free.
            // Until some turret has actually gone berserk there is nothing to remember and
            // nothing to hold, and an empty dictionary answers that with one field read — no
            // config lookup, no hashing of a Unity object, no exception frame.
            int mode = (int)__instance.turretMode;
            if (mode != Berserk && _states.Count == 0) return;

            if (!Plugin.Config.TurretFlipOnBerserkExit.Value) return;
            // An allied turret aims itself at monsters; don't fight it over turretRod.
            if (Traps.AlliedTraps.IsAllied(__instance)) return;

            try { Process(__instance, mode); }
            catch (Exception e) { Plugin.Log.LogError($"Turret head flip error: {e}"); }
        }

        private static void Process(Turret turret, int mode)
        {
            if (!_states.TryGetValue(turret, out var s))
            {
                // Only a turret that is going berserk right now is worth a state entry.
                if (mode != Berserk) return;

                Prune(); // rare enough to sweep destroyed turrets here rather than on a timer
                s = new State { Head = FindHead(turret), PrevMode = Detection };
                _states[turret] = s;
            }
            if (s.Head == null) { _states.Remove(turret); return; }

            // --- transitions ---
            if (s.PrevMode != Berserk && mode == Berserk)
            {
                // Entering berserk: remember where the head was resting.
                s.RememberedYaw = s.Head.localEulerAngles.y;
                s.Animating = false;
            }
            else if (s.PrevMode == Berserk && mode != Berserk)
            {
                // Leaving berserk: flip 180° from the remembered angle.
                s.RestingYaw = Normalize(s.RememberedYaw + 180f);
                s.HasResting = true;

                float dur = Plugin.Config.TurretFlipSmoothDuration.Value;
                if (dur > 0.01f)
                {
                    s.Animating = true;
                    s.AnimTime = 0f;
                    s.AnimDuration = dur;
                    s.AnimFromYaw = s.Head.localEulerAngles.y;
                    s.AnimToYaw = s.RestingYaw;
                }
                else
                {
                    s.Animating = false;
                    SetYaw(s.Head, s.RestingYaw);
                }
            }
            s.PrevMode = mode;

            // Point 3: never touch rotation while berserk.
            if (mode == Berserk) return;

            // Smooth turn to the flipped resting angle.
            if (s.Animating)
            {
                s.AnimTime += Time.deltaTime;
                float t = Mathf.Clamp01(s.AnimTime / s.AnimDuration);
                SetYaw(s.Head, Mathf.LerpAngle(s.AnimFromYaw, s.AnimToYaw, t));
                if (t >= 1f) s.Animating = false;
                return;
            }

            // Hold the flipped resting facing while idle (Detection). We let the turret aim
            // freely while Charging/Firing so it can still shoot players it detects.
            if (s.HasResting && mode == Detection)
                SetYaw(s.Head, s.RestingYaw);
        }

        private static void SetYaw(Transform head, float yaw)
        {
            Vector3 e = head.localEulerAngles;
            // Writing localEulerAngles dirties the transform hierarchy, so skip the write when
            // the head is already where we want it.
            if (Mathf.Abs(Mathf.DeltaAngle(e.y, yaw)) < 0.01f) return;
            head.localEulerAngles = new Vector3(e.x, yaw, e.z);
        }

        /// <summary>
        /// Drop entries for turrets that no longer exist. Their Update stopped running, so they
        /// would otherwise sit in the dictionary for the rest of the round and slow down the
        /// lookup every other turret does each frame — which adds up on levels where traps are
        /// spawned and destroyed in waves.
        /// </summary>
        private static void Prune()
        {
            if (_states.Count == 0) return;

            _dead.Clear();
            foreach (var kv in _states)
                if (kv.Key == null) _dead.Add(kv.Key);

            for (int i = 0; i < _dead.Count; i++) _states.Remove(_dead[i]);
            _dead.Clear();
        }

        private static float Normalize(float angle) => Mathf.Repeat(angle, 360f);

        /// <summary>
        /// Locate the head transform. turretRod is the one the game actually rotates, so it's
        /// the correct target; the name/child fallbacks cover any layout differences.
        /// </summary>
        private static Transform FindHead(Turret turret)
        {
            if (turret.turretRod != null) return turret.turretRod;

            Transform root = turret.transform.root != null ? turret.transform.root : turret.transform;
            var byName = DeepFind(root, "TurretHead") ?? DeepFind(root, "Gun");
            if (byName != null) return byName;

            Transform p = turret.transform.parent;
            if (p != null && p.childCount > 1) return p.GetChild(1);

            return turret.transform;
        }

        private static Transform DeepFind(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
