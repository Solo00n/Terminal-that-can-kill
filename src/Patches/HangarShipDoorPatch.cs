using HarmonyLib;
using LethalDoors.Doors;
using UnityEngine;

namespace LethalDoors.Patches
{
    /// <summary>
    /// Ship hangar door crush.
    ///
    /// Detection mirrors the proven approach used by other working door mods, because
    /// the game never calls a "close" method we could hook directly:
    ///   • <c>HangarShipDoor.doorPower &lt; 1f</c> means the door is in its closed mode
    ///     (power drains while shut, recovers while open) — verified in V81's Update().
    ///   • The animator's current state named <c>"ShipDoorClose"</c> marks the door as
    ///     fully shut. A time fallback covers the case where that state name ever changes.
    ///
    /// Update runs on every client (scene MonoBehaviour), so each client evaluates the
    /// zone against its own local player — matching our multiplayer model.
    /// </summary>
    [HarmonyPatch(typeof(HangarShipDoor), "Update")]
    internal static class HangarShipDoorPatch
    {
        private const string ClosedStateName = "ShipDoorClose";

        // The ship is a fixed scene object at a constant world position in Lethal Company,
        // so the hangar doorway is always at (roughly) this world coordinate on every moon.
        // Hard-coded rather than exposed as config because it never changes.
        private static readonly Vector3 ShipDoorZoneCenter = new Vector3(-5.72f, 0.305f, -14.1f);

        private static float _closeTimer = -1f;
        private static bool _crushed;
        private static float _dotAccumulator;

        [HarmonyPostfix]
        private static void Postfix(HangarShipDoor __instance)
        {
            if (!Plugin.Config.Enabled.Value) return;
            if (Plugin.Config.AffectedDoors.Value == AffectedDoors.TerminalDoors) return;

            // Only lethal on the surface (matches the "after landing" intent + the safe period).
            bool landed = StartOfRound.Instance != null && StartOfRound.Instance.shipHasLanded;

            bool closingMode = landed && __instance.doorPower < 1f;
            if (!closingMode)
            {
                _closeTimer = -1f;
                _crushed = false;
                _dotAccumulator = 0f;
                return;
            }

            _closeTimer = _closeTimer < 0f ? 0f : _closeTimer + Time.deltaTime;

            // Fully shut once the animator reaches the closed state, or after the fallback time.
            bool animClosed = false;
            var anim = __instance.shipDoorsAnimator;
            if (anim != null)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                animClosed = st.IsName(ClosedStateName);
            }
            bool fullyClosed = animClosed || _closeTimer >= Plugin.Config.ShipDoorCloseSeconds.Value;
            if (!fullyClosed) return;

            DoorZone zone = BuildShipZone(__instance);

            if (Plugin.Config.DamageMode.Value == DamageMode.DamageOverTime)
            {
                DoorCrushManager.ExecuteDamageTick(DoorKind.Ship, zone, ref _dotAccumulator);
            }
            else if (!_crushed)
            {
                _crushed = true;
                DoorCrushManager.ExecuteCrush(DoorKind.Ship, zone);
            }
        }

        private static DoorZone BuildShipZone(HangarShipDoor door)
        {
            var c = Plugin.Config;
            Vector3 center = ShipDoorZoneCenter;

            // "Through the door" axis. outsideDoorPoint sits at the doorway and is oriented
            // along the door normal (enemies align to it when prying the door open), so its
            // forward is the reliable through-door direction. Fall back to the offset vector.
            Vector3 through = Vector3.forward;
            if (door.outsideDoorPoint != null)
            {
                through = door.outsideDoorPoint.forward;
                if (new Vector3(through.x, 0f, through.z).sqrMagnitude < 0.01f)
                    through = door.outsideDoorPoint.position - center;
            }

            return DoorZone.FromThroughAxis(center, through,
                c.DoorwayWidth.Value, c.DoorwayHeight.Value, c.DoorwayThickness.Value);
        }
    }
}
