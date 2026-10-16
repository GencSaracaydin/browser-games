using UnityEngine;

namespace LongShadow.Cinematics.Sets
{
    /// <summary>
    /// The §8.2a INSERTION cold open as data (CINEMATIC_DIRECTION §2; markup_ids.csv INS.*): helicopter approach and
    /// departure knots, the landing point, the squad's crouch-run path to their Beat 1 crest slots, and the cabin seats
    /// of the cast (helicopter-local, +Z nose, +X right, cabin floor at <see cref="CabinFloor"/> above the model pivot).
    /// Knot heights below the terrain are lifted to a clearance above the ground at run time (the CSV mixes absolute and
    /// above-ground heights; DECISIONS.md [WP09]).
    /// </summary>
    public static class InsertionLayout
    {
        /// <summary>INS.LZ0: skid contact point of the landing, heading 45°.</summary>
        public static readonly Vector3 LandingZone = new Vector3(-46f, 12.5f, -28f);
        /// <summary>Landing heading in degrees.</summary>
        public const float LandingHeading = 45f;

        /// <summary>INS.Heli.Approach: low over the dunes out of the sun, nose-up flare over the last 25 m.</summary>
        public static readonly Vector3[] Approach =
        {
            new Vector3(-420f, 34f, -150f), new Vector3(-260f, 18f, -95f), new Vector3(-120f, 9f, -50f),
            new Vector3(-60f, 4.5f, -33f), LandingZone,
        };

        /// <summary>INS.Heli.Depart: banks away SSE, clear of the town.</summary>
        public static readonly Vector3[] Depart =
        {
            LandingZone, new Vector3(-20f, 18f, -60f), new Vector3(60f, 40f, -140f), new Vector3(200f, 60f, -260f),
        };

        /// <summary>INS.Squad.Path: crouch-run up the reverse slope (last knot = Hollis's crest slot).</summary>
        public static readonly Vector3[] SquadPath =
        {
            LandingZone, new Vector3(-10f, 15f, 0f), new Vector3(22f, 20f, 18f), new Vector3(36f, 22f, 28f),
        };

        /// <summary>B1.Squad.Start slots and the player's CP1 (TECH_PLAN Appendix A B1).</summary>
        public static readonly Vector3 CrestHollis = new Vector3(36f, 22f, 28f);
        public static readonly Vector3 CrestSparrow = new Vector3(46f, 22f, 26f);
        public static readonly Vector3 CrestTinker = new Vector3(39f, 22f, 24f);
        public static readonly Vector3 CrestCalder = new Vector3(42f, 22f, 30f);

        /// <summary>Clearance kept above the ground while flying (m).</summary>
        public const float FlightClearance = 7f;
        /// <summary>Distance of the landing flare (CINEMATIC_DIRECTION §2: last 25 m).</summary>
        public const float FlareMetres = 25f;

        /// <summary>Cabin floor above the exterior model's pivot (skid bottom); verified once in the editor.</summary>
        public static readonly Vector3 CabinFloor = new Vector3(0f, 0.78f, 0f);
        /// <summary>WP10_FAL rotor hubs (helicopter-local).</summary>
        public static readonly Vector3 MainRotorHub = new Vector3(0.004f, 4.537f, 2.121f);
        public static readonly Vector3 TailRotorHub = new Vector3(-0.519f, 3.992f, -5.592f);

        // Seats (cabin-local, on the floor plane), facing the open left door (−X) as in shots I-2..I-6.
        public static readonly Vector3 SeatTinker = new Vector3(-1.02f, 0.02f, 0.55f);
        public const float SeatTinkerYaw = 270f;
        public static readonly Vector3 SeatCalder = new Vector3(-0.30f, 0.02f, -0.30f);
        public const float SeatCalderYaw = 280f;
        public static readonly Vector3 SeatSparrow = new Vector3(-0.42f, 0.02f, 0.95f);
        public const float SeatSparrowYaw = 255f;
        public static readonly Vector3 SeatHollis = new Vector3(-0.15f, 0.02f, -1.05f);
        public const float SeatHollisYaw = 300f;

        /// <summary>Per-member lateral offsets (m, right of the path) on the crouch-run so they do not overlap.</summary>
        public const float OffsetHollis = 0f;
        public const float OffsetSparrow = 2.2f;
        public const float OffsetTinker = -2.0f;
        public const float OffsetCalder = 1.0f;
    }
}
