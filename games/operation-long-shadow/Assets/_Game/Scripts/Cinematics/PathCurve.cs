using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Cinematics
{
    /// <summary>
    /// Centripetal Catmull-Rom curve through knots with an arc-length table, so <see cref="Sample"/> moves at constant
    /// speed in t (helicopter approach/departure, squad crouch-run, crane paths of CINEMATIC_DIRECTION §2). Pure C#;
    /// the insertion uses it so the cold open does not depend on WP10's spline objects being loaded.
    /// </summary>
    public sealed class PathCurve
    {
        private const int SamplesPerSegment = 48;

        private readonly Vector3[] _knots;
        private readonly float[] _arc;   // cumulative length at each sample
        private readonly int _sampleCount;

        /// <summary>Total arc length in metres.</summary>
        public float Length { get; }

        /// <summary>Knot count.</summary>
        public int KnotCount => _knots.Length;

        /// <summary>Builds the curve. Needs at least two knots.</summary>
        public PathCurve(IReadOnlyList<Vector3> knots)
        {
            var n = knots?.Count ?? 0;
            if (n < 2)
            {
                _knots = new[] { n == 1 ? knots[0] : Vector3.zero, n == 1 ? knots[0] : Vector3.zero };
            }
            else
            {
                _knots = new Vector3[n];
                for (var i = 0; i < n; i++) _knots[i] = knots[i];
            }
            var segments = _knots.Length - 1;
            _sampleCount = segments * SamplesPerSegment + 1;
            _arc = new float[_sampleCount];
            var prev = Evaluate(0f);
            for (var s = 1; s < _sampleCount; s++)
            {
                var p = Evaluate((float)s / (_sampleCount - 1));
                _arc[s] = _arc[s - 1] + Vector3.Distance(prev, p);
                prev = p;
            }
            Length = _arc[_sampleCount - 1];
        }

        /// <summary>Knot by index.</summary>
        public Vector3 Knot(int i) => _knots[Mathf.Clamp(i, 0, _knots.Length - 1)];

        /// <summary>Point at arc-length fraction <paramref name="t01"/> (constant speed).</summary>
        public Vector3 Sample(float t01) => Evaluate(ArcToParam(Mathf.Clamp01(t01)));

        /// <summary>Unit tangent at arc-length fraction <paramref name="t01"/>.</summary>
        public Vector3 Tangent(float t01)
        {
            const float h = 0.002f;
            var a = Sample(Mathf.Clamp01(t01 - h));
            var b = Sample(Mathf.Clamp01(t01 + h));
            var d = b - a;
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// Signed horizontal curvature (1/m) at <paramref name="t01"/>; positive turns right (bank into the turn).
        /// </summary>
        public float TurnRate(float t01)
        {
            const float h = 0.01f;
            var a = Tangent(Mathf.Clamp01(t01 - h));
            var b = Tangent(Mathf.Clamp01(t01 + h));
            var ds = Mathf.Max(0.001f, Length * 2f * h);
            var angle = Vector3.SignedAngle(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z), Vector3.up) * Mathf.Deg2Rad;
            return angle / ds;
        }

        /// <summary>Converts an arc-length fraction to the curve parameter 0..1.</summary>
        private float ArcToParam(float t01)
        {
            if (Length <= 0f) return t01;
            var target = t01 * Length;
            int lo = 0, hi = _sampleCount - 1;
            while (hi - lo > 1)
            {
                var mid = (lo + hi) >> 1;
                if (_arc[mid] < target) lo = mid;
                else hi = mid;
            }
            var span = _arc[hi] - _arc[lo];
            var f = span > 1e-6f ? (target - _arc[lo]) / span : 0f;
            return (lo + f) / (_sampleCount - 1);
        }

        /// <summary>Curve point at parameter u (0..1 over all segments).</summary>
        private Vector3 Evaluate(float u)
        {
            var segments = _knots.Length - 1;
            var x = Mathf.Clamp01(u) * segments;
            var i = Mathf.Min((int)x, segments - 1);
            var t = x - i;
            var p1 = _knots[i];
            var p2 = _knots[i + 1];
            var p0 = i > 0 ? _knots[i - 1] : p1 + (p1 - p2);
            var p3 = i + 2 < _knots.Length ? _knots[i + 2] : p2 + (p2 - p1);
            return CentripetalCatmullRom(p0, p1, p2, p3, t);
        }

        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t0 = 0f;
            var t1 = Knot(t0, p0, p1);
            var t2 = Knot(t1, p1, p2);
            var t3 = Knot(t2, p2, p3);
            var tt = Mathf.Lerp(t1, t2, t);
            var a1 = Blend(p0, p1, t0, t1, tt);
            var a2 = Blend(p1, p2, t1, t2, tt);
            var a3 = Blend(p2, p3, t2, t3, tt);
            var b1 = Blend(a1, a2, t0, t2, tt);
            var b2 = Blend(a2, a3, t1, t3, tt);
            return Blend(b1, b2, t1, t2, tt);
        }

        private static float Knot(float ti, Vector3 a, Vector3 b)
        {
            var d = Mathf.Sqrt(Vector3.Distance(a, b));
            return ti + Mathf.Max(d, 1e-4f);
        }

        private static Vector3 Blend(Vector3 a, Vector3 b, float ta, float tb, float t)
        {
            var span = tb - ta;
            if (Mathf.Abs(span) < 1e-6f) return a;
            return a * ((tb - t) / span) + b * ((t - ta) / span);
        }
    }
}
