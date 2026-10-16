using UnityEngine;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// The left door gun's arc (GDD §13.3: yaw ±70° around the left-door normal, pitch +15° / −60°) for a cabin at a
    /// position and compass yaw (no roll or pitch: "cabin yaw = tangent").
    /// </summary>
    public static class DoorGunArc
    {
        /// <summary>Yaw half-arc around the door normal (degrees).</summary>
        public const float YawLimit = 70f;
        /// <summary>Highest pitch (degrees, up).</summary>
        public const float PitchUp = 15f;
        /// <summary>Lowest pitch (degrees, down; stored positive).</summary>
        public const float PitchDown = 60f;

        /// <summary>World position of a cabin-local point for a cabin at <paramref name="cabin"/> with compass yaw <paramref name="yawDegrees"/>.</summary>
        public static Vector3 CabinToWorld(Vector3 cabin, float yawDegrees, Vector3 local) => cabin + Yaw(yawDegrees, local);

        /// <summary>
        /// Rotates a vector about +Y by a compass yaw (same as <c>Quaternion.Euler(0, yaw, 0) * v</c>, written out so the
        /// arc maths stays pure managed code).
        /// </summary>
        public static Vector3 Yaw(float yawDegrees, Vector3 v)
        {
            var r = yawDegrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(r);
            var s = Mathf.Sin(r);
            return new Vector3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z);
        }

        /// <summary>
        /// Yaw (signed, relative to the left-door normal; positive towards the nose) and pitch (positive up) of a target
        /// from the door-gun pivot.
        /// </summary>
        public static void Angles(Vector3 cabin, float yawDegrees, Vector3 pivotLocal, Vector3 target, out float yaw, out float pitch)
        {
            var pivot = cabin + Yaw(yawDegrees, pivotLocal);
            var doorNormal = Yaw(yawDegrees, Vector3.left);
            var forward = Yaw(yawDegrees, Vector3.forward);
            var d = target - pivot;
            var planar = new Vector3(d.x, 0f, d.z);
            var horizontal = planar.magnitude;
            pitch = Mathf.Atan2(d.y, Mathf.Max(1e-4f, horizontal)) * Mathf.Rad2Deg;
            if (horizontal < 1e-4f)
            {
                yaw = 0f;
                return;
            }
            var unsigned = Vector3.Angle(doorNormal, planar);
            yaw = Vector3.Dot(planar, forward) >= 0f ? unsigned : -unsigned;
        }

        /// <summary>True if the target is inside the arc.</summary>
        public static bool Contains(Vector3 cabin, float yawDegrees, Vector3 pivotLocal, Vector3 target, out float yaw, out float pitch)
        {
            Angles(cabin, yawDegrees, pivotLocal, target, out yaw, out pitch);
            return Mathf.Abs(yaw) <= YawLimit && pitch <= PitchUp && pitch >= -PitchDown;
        }
    }
}
