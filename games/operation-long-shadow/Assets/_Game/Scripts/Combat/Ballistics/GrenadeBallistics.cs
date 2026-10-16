using UnityEngine;

namespace LongShadow.Combat.Ballistics
{
    /// <summary>
    /// Grenade throw maths. The player's throw speed is chosen so a 45° throw from eye height lands at the GDD range
    /// (§13.2: 18 m); the view pitch plus a small upward bias sets the angle, so a level look lands about 13 m out and
    /// looking up reaches the full range. AI throws solve an arc through a target point.
    /// </summary>
    public static class GrenadeBallistics
    {
        /// <summary>Gravity magnitude used by the solvers (matches the project's default physics).</summary>
        public static float Gravity => Mathf.Abs(Physics.gravity.y) > 0.1f ? Mathf.Abs(Physics.gravity.y) : 9.81f;

        /// <summary>Horizontal distance travelled on flat ground from <paramref name="height"/> above it.</summary>
        public static float Range(float speed, float angleDegrees, float height, float g)
        {
            var a = angleDegrees * Mathf.Deg2Rad;
            var vx = speed * Mathf.Cos(a);
            var vy = speed * Mathf.Sin(a);
            var disc = vy * vy + 2f * g * Mathf.Max(0f, height);
            var t = (vy + Mathf.Sqrt(disc)) / g;
            return vx * t;
        }

        /// <summary>Launch speed whose 45° throw from <paramref name="height"/> lands at <paramref name="range"/> (bisection).</summary>
        public static float SpeedForMaxRange(float range, float height, float g)
        {
            float lo = 1f, hi = 60f;
            for (var i = 0; i < 40; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Range(mid, 45f, height, g) < range) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Throw velocity along the view with an upward bias (pitch clamped to [-30°, 60°] after the bias).</summary>
        public static Vector3 ThrowVelocity(Quaternion view, float speed, float pitchBiasDegrees)
        {
            var fwd = view * Vector3.forward;
            var flat = new Vector3(fwd.x, 0f, fwd.z);
            if (flat.sqrMagnitude < 1e-6f) flat = view * Vector3.up; // looking straight up/down
            flat.Normalize();
            var pitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg + pitchBiasDegrees;
            pitch = Mathf.Clamp(pitch, -30f, 60f) * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * speed;
        }

        /// <summary>Initial velocity that reaches <paramref name="to"/> from <paramref name="from"/> in <paramref name="flightTime"/>.</summary>
        public static Vector3 SolveArc(Vector3 from, Vector3 to, float flightTime, float g)
        {
            var t = Mathf.Max(0.05f, flightTime);
            var d = to - from;
            return new Vector3(d.x / t, (d.y + 0.5f * g * t * t) / t, d.z / t);
        }

        /// <summary>A natural AI flight time for a throw distance (0.6-1.6 s).</summary>
        public static float AiFlightTime(float distance) => Mathf.Clamp(distance / 12f, 0.6f, 1.6f);
    }
}
