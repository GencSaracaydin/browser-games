using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// AI miss placement around the player (GDD §14.3: "misses land 0.5-3 m from the player, so they whiz and crack") and
    /// the closest approach of a bullet path to the player's head (near-miss crack/whizz within 3 m).
    /// </summary>
    public static class NearMissMath
    {
        /// <summary>
        /// A point offset from <paramref name="head"/> perpendicular to the shot direction, at a distance in
        /// [<paramref name="minR"/>, <paramref name="maxR"/>]. Downward offsets are flattened a little (more rounds kick up
        /// dirt at the feet and crack past the ears than fly overhead). u1, u2 uniform in [0, 1).
        /// </summary>
        public static Vector3 MissPoint(Vector3 head, Vector3 shotDirection, float minR, float maxR, float u1, float u2)
        {
            var dir = shotDirection.sqrMagnitude > 1e-8f ? shotDirection.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.forward, dir);
            side.Normalize();
            var up = Vector3.Cross(dir, side).normalized;
            var angle = u2 * 2f * Mathf.PI;
            var offset = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            if (offset.y < 0f) offset.y *= 0.75f;
            offset.Normalize();
            var r = Mathf.Lerp(minR, maxR, Mathf.Clamp01(u1));
            return head + offset * r;
        }

        /// <summary>
        /// Closest distance from <paramref name="point"/> to the segment <paramref name="a"/>→<paramref name="b"/>;
        /// <paramref name="closest"/> is the nearest point and <paramref name="t"/> its 0..1 position along the segment.
        /// </summary>
        public static float ClosestApproach(Vector3 a, Vector3 b, Vector3 point, out Vector3 closest, out float t)
        {
            var ab = b - a;
            var len2 = ab.sqrMagnitude;
            t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / len2) : 0f;
            closest = a + ab * t;
            return Vector3.Distance(closest, point);
        }

        /// <summary>
        /// True when a bullet path from <paramref name="a"/> to <paramref name="b"/> passes within <paramref name="radius"/>
        /// of <paramref name="head"/> on its way (not when it ends short of the head at a wall in front of the shooter).
        /// </summary>
        public static bool IsNearMiss(Vector3 a, Vector3 b, Vector3 head, float radius, out Vector3 closest, out float distance)
        {
            distance = ClosestApproach(a, b, head, out closest, out var t);
            return distance <= radius && t > 0.02f;
        }
    }
}
