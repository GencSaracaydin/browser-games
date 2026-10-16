using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Math helpers shared by every system: angles and bearings (compass, markers, callouts), FOV conversions
    /// (settings use horizontal FOV, cameras use vertical), frame-rate independent damping, polylines (critical path,
    /// flank routes), decibels and the FNV-1a hash used for objective flavour groups (GDD §7.12). Allocation-free.
    /// </summary>
    public static class MathX
    {
        /// <summary>Wraps an angle to (-180, 180].</summary>
        public static float WrapAngle180(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>Wraps an angle to [0, 360).</summary>
        public static float WrapAngle360(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees >= 360f ? 0f : degrees;
        }

        /// <summary>Compass bearing in degrees [0, 360) of a direction on the XZ plane: 0 = +Z (north), 90 = +X (east).</summary>
        public static float BearingOf(Vector3 direction)
        {
            if (direction.x == 0f && direction.z == 0f) return 0f;
            return WrapAngle360(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
        }

        /// <summary>Compass bearing from one point to another (see <see cref="BearingOf"/>).</summary>
        public static float Bearing(Vector3 from, Vector3 to) => BearingOf(to - from);

        /// <summary>Direction on the XZ plane for a compass bearing (inverse of <see cref="BearingOf"/>).</summary>
        public static Vector3 DirectionFromBearing(float bearingDegrees)
        {
            var r = bearingDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>Signed angle in degrees from <paramref name="from"/> to <paramref name="to"/> around +Y (positive = clockwise seen from above).</summary>
        public static float SignedAngleXZ(Vector3 from, Vector3 to) => WrapAngle180(BearingOf(to) - BearingOf(from));

        /// <summary>Vertical FOV (degrees) for a horizontal FOV at an aspect ratio (width / height).</summary>
        public static float HorizontalToVerticalFov(float horizontalDegrees, float aspect)
        {
            if (aspect <= 0f) return horizontalDegrees;
            var h = horizontalDegrees * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / aspect) * Mathf.Rad2Deg;
        }

        /// <summary>Horizontal FOV (degrees) for a vertical FOV at an aspect ratio (width / height).</summary>
        public static float VerticalToHorizontalFov(float verticalDegrees, float aspect)
        {
            var v = verticalDegrees * Mathf.Deg2Rad;
            return 2f * Mathf.Atan(Mathf.Tan(v * 0.5f) * aspect) * Mathf.Rad2Deg;
        }

        /// <summary>Frame-rate independent exponential approach: <c>lerp(a, b, 1 - exp(-lambda dt))</c>.</summary>
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));

        /// <summary>Vector3 version of <see cref="Damp(float,float,float,float)"/>.</summary>
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));

        /// <summary>Angle version of <see cref="Damp(float,float,float,float)"/> (shortest way round).</summary>
        public static float DampAngle(float a, float b, float lambda, float dt) => Mathf.LerpAngle(a, b, 1f - Mathf.Exp(-lambda * dt));

        /// <summary>Quaternion version of <see cref="Damp(float,float,float,float)"/>.</summary>
        public static Quaternion Damp(Quaternion a, Quaternion b, float lambda, float dt) => Quaternion.Slerp(a, b, 1f - Mathf.Exp(-lambda * dt));

        /// <summary>Maps a value from one range to another, clamped to the output range.</summary>
        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMin, inMax)) return outMin;
            return Mathf.Lerp(outMin, outMax, Mathf.InverseLerp(inMin, inMax, value));
        }

        /// <summary>Maps a value from one range to another without clamping.</summary>
        public static float RemapUnclamped(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMin, inMax)) return outMin;
            return outMin + (value - inMin) / (inMax - inMin) * (outMax - outMin);
        }

        /// <summary>Linear gain to decibels (gain 0 gives -80 dB).</summary>
        public static float LinearToDb(float linear) => linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);

        /// <summary>Decibels to linear gain.</summary>
        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>Closest point on segment ab to p; <paramref name="t"/> is 0..1 along the segment.</summary>
        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p, out float t)
        {
            var ab = b - a;
            var len2 = ab.sqrMagnitude;
            t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return a + ab * t;
        }

        /// <summary>Total length of a polyline.</summary>
        public static float PolylineLength(IReadOnlyList<Vector3> points)
        {
            if (points == null) return 0f;
            var len = 0f;
            for (var i = 1; i < points.Count; i++) len += Vector3.Distance(points[i - 1], points[i]);
            return len;
        }

        /// <summary>
        /// Projects a point onto a polyline. Returns the closest point; <paramref name="distanceAlong"/> is the arc length
        /// from the first knot to it, <paramref name="totalLength"/> the polyline length.
        /// </summary>
        public static Vector3 ProjectOnPolyline(IReadOnlyList<Vector3> points, Vector3 p, out float distanceAlong, out float totalLength)
        {
            distanceAlong = 0f;
            totalLength = 0f;
            if (points == null || points.Count == 0) return p;
            if (points.Count == 1) return points[0];
            var best = points[0];
            var bestD2 = float.MaxValue;
            var run = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var seg = Vector3.Distance(a, b);
                var c = ClosestPointOnSegment(a, b, p, out var t);
                var d2 = (c - p).sqrMagnitude;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = c;
                    distanceAlong = run + seg * t;
                }
                run += seg;
            }
            totalLength = run;
            return best;
        }

        /// <summary>Point at an arc length along a polyline (clamped to its ends).</summary>
        public static Vector3 PointAlongPolyline(IReadOnlyList<Vector3> points, float distance)
        {
            if (points == null || points.Count == 0) return Vector3.zero;
            if (distance <= 0f || points.Count == 1) return points[0];
            for (var i = 1; i < points.Count; i++)
            {
                var seg = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= seg) return seg > 1e-6f ? Vector3.Lerp(points[i - 1], points[i], distance / seg) : points[i];
                distance -= seg;
            }
            return points[points.Count - 1];
        }

        /// <summary>FNV-1a 32-bit hash of the UTF-8 bytes of a string (allocation-free).</summary>
        public static uint Fnv1a32(string text)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var h = offset;
            if (string.IsNullOrEmpty(text)) return h;
            for (var i = 0; i < text.Length; i++)
            {
                int cp = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                if (cp < 0x80)
                {
                    h = (h ^ (uint)cp) * prime;
                }
                else if (cp < 0x800)
                {
                    h = (h ^ (uint)(0xC0 | (cp >> 6))) * prime;
                    h = (h ^ (uint)(0x80 | (cp & 0x3F))) * prime;
                }
                else if (cp < 0x10000)
                {
                    h = (h ^ (uint)(0xE0 | (cp >> 12))) * prime;
                    h = (h ^ (uint)(0x80 | ((cp >> 6) & 0x3F))) * prime;
                    h = (h ^ (uint)(0x80 | (cp & 0x3F))) * prime;
                }
                else
                {
                    h = (h ^ (uint)(0xF0 | (cp >> 18))) * prime;
                    h = (h ^ (uint)(0x80 | ((cp >> 12) & 0x3F))) * prime;
                    h = (h ^ (uint)(0x80 | ((cp >> 6) & 0x3F))) * prime;
                    h = (h ^ (uint)(0x80 | (cp & 0x3F))) * prime;
                }
            }
            return h;
        }

        /// <summary>Objective flavour group 0..99999 = FNV-1a 32 of the objective id mod 100000 (GDD §7.12). Format with "D5".</summary>
        public static int FlavourGroup(string objectiveId) => (int)(Fnv1a32(objectiveId) % 100000u);

        /// <summary>True if a point lies inside a cone (apex, unit axis, half-angle in degrees, range in metres).</summary>
        public static bool InCone(Vector3 apex, Vector3 axis, float halfAngleDegrees, float range, Vector3 point)
        {
            var d = point - apex;
            var dist2 = d.sqrMagnitude;
            if (dist2 > range * range) return false;
            if (dist2 < 1e-8f) return true;
            return Vector3.Angle(axis, d) <= halfAngleDegrees;
        }
    }
}
