using UnityEngine;

namespace LongShadow.Player.Movement
{
    /// <summary>
    /// Feet path of a vault, climb, air mantle or ladder step-off: phase 1 rises to just above the crest while closing
    /// on the face (ease-out, like pulling up), phase 2 moves over the top to the end point (ease-in when dropping on the
    /// far side of a thin wall). Continuous, starts exactly at <see cref="Start"/>, ends exactly at <see cref="End"/>, and
    /// never dips below the lower of the two.
    /// </summary>
    public struct TraversalPath
    {
        /// <summary>Feet at the start.</summary>
        public Vector3 Start;
        /// <summary>Feet at the crest (capsule front at the face, height <see cref="PeakY"/>).</summary>
        public Vector3 Approach;
        /// <summary>Feet at the end.</summary>
        public Vector3 End;
        /// <summary>Feet height at the crest.</summary>
        public float PeakY;
        /// <summary>Duration in seconds.</summary>
        public float Duration;
        /// <summary>Fraction of the duration spent rising (0..1).</summary>
        public float RiseFraction;

        /// <summary>Builds a path from the probe result.</summary>
        public static TraversalPath Create(Vector3 start, Vector3 edge, Vector3 end, Vector3 forward, float radius,
                                           float duration, float riseFraction, float clearance = 0.05f)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var peak = Mathf.Max(Mathf.Max(edge.y, end.y) + clearance, start.y);
            var approach = edge - forward * (radius + 0.02f);
            if (Vector3.Dot(approach - start, forward) < 0f) approach = start; // already at the face
            approach.y = peak;
            return new TraversalPath
            {
                Start = start,
                Approach = approach,
                End = end,
                PeakY = peak,
                Duration = Mathf.Max(0.01f, duration),
                RiseFraction = Mathf.Clamp(riseFraction, 0.05f, 0.95f),
            };
        }

        /// <summary>Feet position at normalised time <paramref name="t01"/>.</summary>
        public Vector3 Evaluate(float t01)
        {
            var t = Mathf.Clamp01(t01);
            if (t <= RiseFraction)
            {
                var u = t / RiseFraction;
                var y = Mathf.Lerp(Start.y, PeakY, EaseOutCubic(u));
                var xz = Vector3.Lerp(Start, Approach, SmoothStep(u));
                return new Vector3(xz.x, y, xz.z);
            }
            else
            {
                var u = (t - RiseFraction) / (1f - RiseFraction);
                var xz = Vector3.Lerp(Approach, End, SmoothStep(u));
                var y = Mathf.Lerp(PeakY, End.y, End.y < PeakY - 0.25f ? EaseInQuad(u) : SmoothStep(u));
                return new Vector3(xz.x, y, xz.z);
            }
        }

        /// <summary>Horizontal length of the path (for exit-speed and camera effects).</summary>
        public float HorizontalLength
        {
            get
            {
                var a = Approach - Start;
                var b = End - Approach;
                a.y = 0f;
                b.y = 0f;
                return a.magnitude + b.magnitude;
            }
        }

        private static float SmoothStep(float x) => x * x * (3f - 2f * x);
        private static float EaseOutCubic(float x)
        {
            var i = 1f - x;
            return 1f - i * i * i;
        }
        private static float EaseInQuad(float x) => x * x;
    }
}
