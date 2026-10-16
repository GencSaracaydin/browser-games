using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Every spline of TECH_PLAN Appendix A and CINEMATIC_DIRECTION §2 with its timing (GDD §7.10, §7.11): the Beat 10
    /// trucks, Magpie's approach and wave-off, the Beat 11 exfil rail, the Clock Square trucks and the insertion splines.
    /// Timing choices the docs leave open are logged in Docs/DECISIONS.md ([WP10_LEVEL]).
    /// </summary>
    public static class VehicleRoutes
    {
        /// <summary>Beat 10 truck 1 on the Customs Road (t 8 arrival; WP04 boards riders 4 s before).</summary>
        public const string Truck1 = "B10.Truck1";
        /// <summary>Beat 10 truck 2 on the salt-pan track (t 40 arrival, RPG gunner on the bed).</summary>
        public const string Truck2 = "B10.Truck2";
        /// <summary>Magpie's final approach (root = Magpie; the RPG's target, WP04 RpgBehaviour).</summary>
        public const string HeliApproach = "B10.HeliApproach";
        /// <summary>Magpie's wave-off loop (+20 s), rejoining the approach at (600, 60, 420).</summary>
        public const string HeliWaveOff = "B10.HeliWaveOff";
        /// <summary>The Beat 11 exfil rail N0-N6 (Elapsed in exfil seconds).</summary>
        public const string Exfil = "B11.Exfil";
        /// <summary>The two Clock Square trucks of Beat 11 (12 m apart, 4.5 m/s, from t 16).</summary>
        public const string TruckSquare = "B11.TruckSquare";
        /// <summary>Insertion approach (CINEMATIC_DIRECTION §2).</summary>
        public const string InsertionApproach = "INS.Heli.Approach";
        /// <summary>Insertion departure.</summary>
        public const string InsertionDepart = "INS.Heli.Depart";
        /// <summary>Squad crouch-run route up the reverse slope.</summary>
        public const string InsertionSquadPath = "INS.Squad.Path";

        /// <summary>Destructible id of the first Clock Square truck (WP07 ObjectiveTable B11Truck1).</summary>
        public const string SquareTruck1Id = "B11.Truck1";
        /// <summary>Destructible id of the second Clock Square truck (WP07 ObjectiveTable B11Truck2).</summary>
        public const string SquareTruck2Id = "B11.Truck2";

        /// <summary>Height of Magpie's cabin floor above its skids (GDD §8.0 cabin frame; the pad is at -1, N0 at 0).</summary>
        public const float HeliCabinFloorHeight = 1.0f;

        /// <summary>Knot index on <see cref="HeliApproach"/> from which the last 15 s of the approach start ((600, 60, 420)).</summary>
        public const int ApproachFinalKnot = 1;

        private static List<VehicleRouteDef> s_all;

        /// <summary>Every route.</summary>
        public static IReadOnlyList<VehicleRouteDef> All => s_all ?? (s_all = Build());

        /// <summary>A route by id, or null.</summary>
        public static VehicleRouteDef Get(string id)
        {
            foreach (var r in All)
                if (r.Id == id) return r;
            return null;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static List<VehicleRouteDef> Build()
        {
            return new List<VehicleRouteDef>
            {
                new VehicleRouteDef
                {
                    Id = Truck1, Section = SectionId.Pitch, Kind = VehicleKind.CargoTruck,
                    Knots = new[] { V(430, -1, 395), V(392, -1, 396) },
                    // WP04: authored travel about 4 s (riders board 4 s before the GDD t 8 arrival); 38 m with an eased stop.
                    SegmentSeconds = new[] { 4.2f }, ArrivalEase = 2.0f,
                    Notes = "GDD §7.10 t 8: truck 1 arrives on the Customs Road, 3 Legion dismount at the tailgate (394, -1, 396).",
                },
                new VehicleRouteDef
                {
                    Id = Truck2, Section = SectionId.Pitch, Kind = VehicleKind.CargoTruck,
                    Knots = new[] { V(362, -1, 470), V(362, -1, 424) },
                    SegmentSeconds = new[] { 4.6f }, ArrivalEase = 2.0f,
                    Notes = "GDD §7.10 t 40: truck 2 on the salt-pan track with 2 Legion and the RPG gunner on the bed (seat 3).",
                },
                new VehicleRouteDef
                {
                    Id = HeliApproach, Section = SectionId.Heli, Kind = VehicleKind.Helicopter,
                    Knots = new[] { V(900, 120, 450), V(600, 60, 420), V(420, 20, 408), V(350, -1, 406) },
                    // The last 15 s run from (600, 60, 420) to wheels down (GDD §7.10 t 60 → t 75); 9 + 6 s with a 5 s flare.
                    SegmentSeconds = new[] { 9f, 9f, 6f }, ArrivalEase = 5f, FinalHeading = 270f,
                    Notes = "Root = Magpie. Play from knot 1 at t 60 (SetProgress to KnotProgress(1)) to land at t 75 heading 270°.",
                },
                new VehicleRouteDef
                {
                    Id = HeliWaveOff, Section = SectionId.Heli, Kind = VehicleKind.Helicopter,
                    Knots = new[] { V(420, 20, 408), V(400, 45, 470), V(520, 80, 520), V(640, 70, 440), V(600, 60, 420) },
                    SegmentSeconds = new[] { 4.5f, 5.5f, 5.5f, 4.5f }, SharedRootOf = HeliApproach,
                    Notes = "20 s loop that rejoins the approach at (600, 60, 420); then the approach replays its last 15 s.",
                },
                new VehicleRouteDef
                {
                    Id = Exfil, Section = SectionId.Heli, Kind = VehicleKind.Helicopter,
                    Knots = new[] { V(350, 0, 406), V(346, 30, 372), V(300, 35, 330), V(225, 40, 280), V(186, 35, 215), V(120, 55, 120), V(40, 80, 40) },
                    SegmentSeconds = new[] { 8f, 8f, 10f, 10f, 8f, 6f }, SharedRootOf = HeliApproach,
                    RootOffsetLocal = new Vector3(0f, -HeliCabinFloorHeight, 0f),
                    Notes = "Auto (Catmull-Rom) tangents, cabin yaw = tangent; Elapsed = exfil seconds (rooftop target windows).",
                },
                new VehicleRouteDef
                {
                    Id = TruckSquare, Section = SectionId.Heli, Kind = VehicleKind.CargoTruck,
                    Knots = new[] { V(300, 0, 276), V(246, 0, 276) },
                    Speed = 4.5f, FollowerSpacing = new[] { 12f }, ArrivalEase = 1.5f,
                    Notes = "Two trucks 12 m apart from exfil t 16 at 4.5 m/s, then stop (GDD §7.11); destructibles B11.Truck1/2.",
                },
                new VehicleRouteDef
                {
                    Id = InsertionApproach, Section = SectionId.Approach, Kind = VehicleKind.Helicopter,
                    // CINEMATIC_DIRECTION §2. The apron dunes are at Y 10-14, so the first four knot heights are read as
                    // heights above the ground (else the rail would run underground); INS.LZ0 is absolute.
                    Knots = new[] { V(-420, 34, -150), V(-260, 18, -95), V(-120, 9, -50), V(-60, 4.5f, -33), V(-46, 12.5f, -28) },
                    SegmentSeconds = new[] { 6f, 6f, 5f, 4f }, ArrivalEase = 4f, FinalHeading = 45f,
                    Notes = "Knots 0-3 y = metres above the terrain (see DECISIONS); skid contact at INS.LZ0.",
                },
                new VehicleRouteDef
                {
                    Id = InsertionDepart, Section = SectionId.Approach, Kind = VehicleKind.Helicopter,
                    Knots = new[] { V(-46, 12.5f, -28), V(-20, 18, -60), V(60, 40, -140), V(200, 60, -260) },
                    SegmentSeconds = new[] { 4f, 6f, 8f }, SharedRootOf = InsertionApproach,
                    Notes = "Banks away SSE, staying clear of the town (CINEMATIC_DIRECTION §2 I-10).",
                },
                new VehicleRouteDef
                {
                    Id = InsertionSquadPath, Section = SectionId.Approach, Kind = VehicleKind.PathOnly,
                    Knots = new[] { V(-46, 12.5f, -28), V(-10, 15, 0), V(22, 20, 18), V(36, 22, 28) },
                    Speed = 3.2f,
                    Notes = "Crouch-run route up the reverse slope; each member ends on its B1.Squad.Start slot.",
                },
            };
        }

        /// <summary>True when the knot heights of a route (except the last) are above the ground, not absolute.</summary>
        public static bool KnotHeightsAboveGround(string id) => id == InsertionApproach;
    }
}
