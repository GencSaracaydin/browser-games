using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// One spline route of TECH_PLAN Appendix A (kind SPLINE): knots, timing and the vehicle that rides it. Timing is either
    /// per segment (<see cref="SegmentSeconds"/>, constant speed inside each segment: the B11 exfil 8/8/10/10/8/6 s) or a
    /// constant <see cref="Speed"/> in m/s (B11 trucks 4.5 m/s).
    /// </summary>
    public sealed class VehicleRouteDef
    {
        /// <summary>Appendix A id ("B10.Truck1").</summary>
        public string Id;
        /// <summary>Section scene the route and its vehicle live in.</summary>
        public SectionId Section;
        /// <summary>What rides it.</summary>
        public VehicleKind Kind;
        /// <summary>Knots in world metres (first = start).</summary>
        public Vector3[] Knots;
        /// <summary>Seconds per segment (length Knots - 1), or null to use <see cref="Speed"/>.</summary>
        public float[] SegmentSeconds;
        /// <summary>Constant speed in m/s when <see cref="SegmentSeconds"/> is null.</summary>
        public float Speed;
        /// <summary>Seconds over which the vehicle decelerates into the last knot (0 = none).</summary>
        public float ArrivalEase;
        /// <summary>Id of another route whose vehicle root this route drives (shared Magpie); null = own vehicle.</summary>
        public string SharedRootOf;
        /// <summary>Followers riding the same spline this many metres behind the lead (B11 trucks: 12).</summary>
        public float[] FollowerSpacing;
        /// <summary>Heading in compass degrees to hold at the end (helicopter wheels-down 270°), or NaN to follow the tangent.</summary>
        public float FinalHeading = float.NaN;
        /// <summary>
        /// Offset from the spline point to the vehicle root in the vehicle's local frame. The exfil knots are the cabin
        /// floor (GDD §8.0: cabin floor Y = 0 at N0 (350, 0, 406)); the Magpie root is at the skids, 1 m lower.
        /// </summary>
        public Vector3 RootOffsetLocal;
        /// <summary>Notes from the appendix.</summary>
        public string Notes;

        /// <summary>Total authored duration in seconds (segment times, or length / speed).</summary>
        public float Duration
        {
            get
            {
                if (SegmentSeconds != null)
                {
                    var t = 0f;
                    for (var i = 0; i < SegmentSeconds.Length; i++) t += SegmentSeconds[i];
                    return t;
                }
                return Speed > 0f ? MathX.PolylineLength(Knots) / Speed : 0f;
            }
        }

        /// <summary>Time at which knot <paramref name="index"/> is reached (segment timing only; else by polyline length).</summary>
        public float KnotTime(int index)
        {
            var t = 0f;
            if (SegmentSeconds != null)
            {
                for (var i = 0; i < index && i < SegmentSeconds.Length; i++) t += SegmentSeconds[i];
                return t;
            }
            if (Speed <= 0f) return 0f;
            for (var i = 1; i <= index && i < Knots.Length; i++) t += Vector3.Distance(Knots[i - 1], Knots[i]) / Speed;
            return t;
        }
    }
}
