using UnityEngine;

namespace LethalDoors.Doors
{
    /// <summary>
    /// Tracks the post-landing grace period. Attached to the ship once per session by
    /// <see cref="Patches.StartOfRoundPatch"/>. shipHasLanded is networked, so every
    /// client records the same landing moment.
    /// </summary>
    public sealed class LethalDoorsSession : MonoBehaviour
    {
        public static LethalDoorsSession Instance { get; private set; }

        private float _landedTime = -9999f;
        private bool _prevLanded;

        private void Awake() => Instance = this;

        private void Update()
        {
            var sor = StartOfRound.Instance;
            if (sor == null) return;

            bool landed = sor.shipHasLanded;
            if (landed && !_prevLanded)
                _landedTime = Time.time; // rising edge = touchdown
            _prevLanded = landed;

            // Frame-driven crush for facility (terminal) doors.
            TerminalDoorCrush.Tick();
        }

        /// <summary>True while doors must NOT kill (grace period right after landing).</summary>
        public bool InSafePeriod
        {
            get
            {
                float grace = Plugin.Config.SafeZoneSeconds.Value;
                if (grace <= 0f) return false;
                return Time.time - _landedTime < grace;
            }
        }
    }
}
