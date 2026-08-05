using UnityEngine;

namespace LethalDoors.Doors
{
    /// <summary>
    /// An oriented, thin slab that matches a doorway opening — the "line inside the door".
    /// Local axes: X = doorway width, Y = height, Z = through-door depth (the thin one).
    /// A point is inside only when it is actually standing in the opening, not merely near it.
    /// </summary>
    public readonly struct DoorZone
    {
        public readonly Vector3 Center;
        public readonly Quaternion Rot;
        public readonly Vector3 HalfExtents; // (width/2, height/2, thickness/2)

        public DoorZone(Vector3 center, Quaternion rot, Vector3 halfExtents)
        {
            Center = center;
            Rot = rot;
            HalfExtents = halfExtents;
        }

        /// <summary>Position in the slab's local space (z = through-door depth).</summary>
        public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(Rot) * (world - Center);

        public bool Contains(Vector3 world)
        {
            Vector3 l = ToLocal(world);
            return Mathf.Abs(l.x) <= HalfExtents.x
                && Mathf.Abs(l.y) <= HalfExtents.y
                && Mathf.Abs(l.z) <= HalfExtents.z;
        }

        /// <summary>
        /// Build a slab from a centre and a "through the door" direction (perpendicular to
        /// the opening). The thin axis is aligned to that direction. Falls back to world
        /// forward if the direction is degenerate.
        /// </summary>
        public static DoorZone FromThroughAxis(Vector3 center, Vector3 throughAxis,
            float width, float height, float thickness)
        {
            Vector3 fwd = throughAxis;
            fwd.y = 0f; // keep the doorway upright
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            var rot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            return new DoorZone(center, rot, new Vector3(width * 0.5f, height * 0.5f, thickness * 0.5f));
        }
    }
}
