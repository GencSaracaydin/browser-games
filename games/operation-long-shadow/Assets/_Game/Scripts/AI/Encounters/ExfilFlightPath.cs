using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// A time-parametrised model of the B11.Exfil flight (Appendix A: knots N0-N6, segment times 8/8/10/10/8/6 s,
    /// auto (Catmull-Rom) tangents, cabin yaw = tangent). Two interpretations are provided because the World
    /// SplineVehicle (WP10) owns the real one: <see cref="SampleCatmullRom"/> (uniform Catmull-Rom, time-linear in each
    /// segment) and <see cref="SampleSpline"/> over a Unity <see cref="Spline"/> with AutoSmooth knots, parametric or
    /// constant speed per segment. The rooftop targets use the live vehicle when it exists and this model otherwise;
    /// the exfil-arc test checks the GDD guarantee against all of them.
    /// </summary>
    public static class ExfilFlightPath
    {
        /// <summary>Segment index and local fraction (0..1) for an exfil time.</summary>
        public static int Segment(float t, IReadOnlyList<float> segmentSeconds, out float u)
        {
            var n = segmentSeconds.Count;
            if (t <= 0f)
            {
                u = 0f;
                return 0;
            }
            for (var i = 0; i < n; i++)
            {
                var d = segmentSeconds[i];
                if (t <= d || i == n - 1)
                {
                    u = d > 0f ? Mathf.Clamp01(t / d) : 1f;
                    return i;
                }
                t -= d;
            }
            u = 1f;
            return n - 1;
        }

        /// <summary>Uniform Catmull-Rom through the knots (phantom end knots reflected), time-linear per segment.</summary>
        public static void SampleCatmullRom(float t, out Vector3 position, out Vector3 tangent) =>
            SampleCatmullRom(ExfilSchedule.Knots, ExfilSchedule.SegmentSeconds, t, out position, out tangent);

        /// <summary>Uniform Catmull-Rom over arbitrary knots.</summary>
        public static void SampleCatmullRom(IReadOnlyList<Vector3> knots, IReadOnlyList<float> segmentSeconds, float t,
                                            out Vector3 position, out Vector3 tangent)
        {
            var i = Segment(t, segmentSeconds, out var u);
            var p0 = Knot(knots, i - 1);
            var p1 = Knot(knots, i);
            var p2 = Knot(knots, i + 1);
            var p3 = Knot(knots, i + 2);
            var u2 = u * u;
            var u3 = u2 * u;
            position = 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
            tangent = 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * u + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * u2);
            if (tangent.sqrMagnitude < 1e-8f) tangent = p2 - p1;
        }

        private static Vector3 Knot(IReadOnlyList<Vector3> k, int i)
        {
            var n = k.Count;
            if (i < 0) return 2f * k[0] - k[1];
            if (i >= n) return 2f * k[n - 1] - k[n - 2];
            return k[i];
        }

        /// <summary>A Unity Spline through the exfil knots with AutoSmooth tangents (what a SplineContainer made from Appendix A gives).</summary>
        public static Spline BuildSpline() => BuildSpline(ExfilSchedule.Knots);

        /// <summary>A Unity Spline through knots with AutoSmooth tangents.</summary>
        public static Spline BuildSpline(IReadOnlyList<Vector3> knots)
        {
            var pts = new List<float3>(knots.Count);
            for (var i = 0; i < knots.Count; i++) pts.Add(knots[i]);
            return new Spline(pts, TangentMode.AutoSmooth, false);
        }

        /// <summary>
        /// Samples a Unity Spline by exfil time: curve i covers segment i; <paramref name="constantSpeed"/> moves at constant
        /// speed inside each curve, otherwise the curve parameter is time-linear.
        /// </summary>
        public static void SampleSpline(Spline spline, IReadOnlyList<float> segmentSeconds, float t, bool constantSpeed,
                                        out Vector3 position, out Vector3 tangent)
        {
            var i = Segment(t, segmentSeconds, out var u);
            i = Mathf.Clamp(i, 0, spline.Count - 2);
            var curve = spline.GetCurve(i);
            if (constantSpeed)
            {
                var length = CurveUtility.CalculateLength(curve, 60);
                u = CurveUtility.GetDistanceToInterpolation(curve, u * length);
            }
            position = CurveUtility.EvaluatePosition(curve, u);
            tangent = CurveUtility.EvaluateTangent(curve, u);
            if (tangent.sqrMagnitude < 1e-8f) tangent = (Vector3)(curve.P3 - curve.P0);
        }

        /// <summary>Cabin yaw in degrees (compass) for a tangent ("cabin yaw = tangent").</summary>
        public static float YawOf(Vector3 tangent) => LongShadow.Core.MathX.BearingOf(tangent);
    }
}
