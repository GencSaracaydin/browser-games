using UnityEngine;

namespace LongShadow.AI.Encounters
{
    /// <summary>
    /// Beat 11 timing data (GDD §7.11, TECH_PLAN Appendix A B11): the B11.Exfil knots N0-N6 and segment times
    /// (8/8/10/10/8/6 s, 50 s), the left door-gun pivot in the cabin frame (HELI.DoorGunPivot), the rooftop-target windows
    /// and the two Clock Square trucks (B11.TruckSquare from t 16 at 4.5 m/s, 12 m apart). Used by
    /// <see cref="ExfilFlightPath"/>, the rooftop targets and the exfil-arc EditMode test.
    /// </summary>
    public static class ExfilSchedule
    {
        /// <summary>Spline vehicle id of the helicopter rail.</summary>
        public const string ExfilVehicleId = "B11.Exfil";
        /// <summary>Spline vehicle id of the Clock Square trucks.</summary>
        public const string TruckSplineId = "B11.TruckSquare";

        /// <summary>Exfil knots N0..N6 (Appendix A).</summary>
        public static readonly Vector3[] Knots =
        {
            new Vector3(350f, 0f, 406f), new Vector3(346f, 30f, 372f), new Vector3(300f, 35f, 330f), new Vector3(225f, 40f, 280f),
            new Vector3(186f, 35f, 215f), new Vector3(120f, 55f, 120f), new Vector3(40f, 80f, 40f),
        };

        /// <summary>Seconds per segment N0→N1 … N5→N6 (Appendix A).</summary>
        public static readonly float[] SegmentSeconds = { 8f, 8f, 10f, 10f, 8f, 6f };

        /// <summary>Total flight time (50 s).</summary>
        public const float TotalSeconds = 50f;

        /// <summary>Left door-gun pivot in the cabin frame (x right, y up, z forward; Appendix A HELI.DoorGunPivot).</summary>
        public static readonly Vector3 DoorGunPivotLocal = new Vector3(-1.05f, 1.10f, 0.30f);

        // Rooftop target windows in exfil seconds (GDD §7.11, Appendix A).
        public const float T1Start = 8f;
        public const float T1End = 16f;
        public const float T2Start = 15f;
        public const float T2End = 23f;
        /// <summary>T3 stands at t 28 (hidden before), raises 1.2 s and fires at t 30.</summary>
        public const float T3Start = 28f;
        public const float T3FireAt = 30f;
        public const float T3End = 30f;
        public const float T4Start = 26f;
        public const float T4End = 33f;

        /// <summary>Clock Square truck path start (east street).</summary>
        public static readonly Vector3 TruckPathStart = new Vector3(300f, 0f, 276f);
        /// <summary>Clock Square truck path end (they stop there).</summary>
        public static readonly Vector3 TruckPathEnd = new Vector3(246f, 0f, 276f);
        /// <summary>Truck start time (exfil seconds).</summary>
        public const float TruckStart = 16f;
        /// <summary>Truck speed (m/s).</summary>
        public const float TruckSpeed = 4.5f;
        /// <summary>Gap between the two trucks (m); the second follows behind the first.</summary>
        public const float TruckSpacing = 12f;
        /// <summary>Number of trucks.</summary>
        public const int TruckCount = 2;
        /// <summary>The trucks are in the gun arc during t 16-27 (GDD §7.11).</summary>
        public const float TruckWindowStart = 16f;
        /// <summary>End of the truck arc window.</summary>
        public const float TruckWindowEnd = 27f;
        /// <summary>Aim height above the truck path (cab/bed centre).</summary>
        public const float TruckAimHeight = 1.5f;

        /// <summary>
        /// Truck position at exfil time <paramref name="t"/>: truck 0 enters the path at t 16; truck 1 follows 12 m behind
        /// (coming up the street from 12 m further east); both stop at the end, 12 m apart.
        /// </summary>
        public static Vector3 TruckPosition(int index, float t)
        {
            var dir = (TruckPathEnd - TruckPathStart);
            var length = dir.magnitude;
            dir /= length;
            var s = TruckSpeed * Mathf.Max(0f, t - TruckStart) - TruckSpacing * index;
            s = Mathf.Min(s, length - TruckSpacing * index);
            return TruckPathStart + dir * s;
        }

        /// <summary>Exfil time at which knot <paramref name="index"/> is reached.</summary>
        public static float KnotTime(int index)
        {
            var t = 0f;
            for (var i = 0; i < index && i < SegmentSeconds.Length; i++) t += SegmentSeconds[i];
            return t;
        }
    }
}
