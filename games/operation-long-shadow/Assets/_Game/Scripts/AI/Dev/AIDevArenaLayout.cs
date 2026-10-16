using UnityEngine;

namespace LongShadow.AI.Dev
{
    /// <summary>
    /// Geometry and marker ids of the generated AI dev arena (TECH_PLAN §7.6 AIDevArena): an 80 × 80 m yard with the
    /// player side in the south and the enemy side in the north. Shared by the editor builder (places the markup and
    /// blocks) and <see cref="AIDevArenaDirector"/> (builds the dev encounters from the same ids).
    /// </summary>
    public static class AIDevArenaLayout
    {
        /// <summary>Scene path.</summary>
        public const string ScenePath = "Assets/_Game/Scenes/Dev/AIDevArena.unity";
        /// <summary>Half size of the ground (m).</summary>
        public const float HalfSize = 40f;

        /// <summary>Player start.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -26f);
        /// <summary>Dummy patrol points (when no PF_Player is used).</summary>
        public static readonly Vector3[] PlayerPatrol =
        {
            new Vector3(0f, 0f, -26f), new Vector3(-6f, 0f, -18f), new Vector3(-12f, 0f, -12f), new Vector3(-4f, 0f, -8f),
            new Vector3(6f, 0f, -12f), new Vector3(10f, 0f, -20f),
        };

        /// <summary>Squad node set near the player start.</summary>
        public const string NodeSetA = "DEV.NodeSet.A";
        /// <summary>Squad node set after the push.</summary>
        public const string NodeSetB = "DEV.NodeSet.B";
        /// <summary>Slots of <see cref="NodeSetA"/> (HOL, SPA, TIN).</summary>
        public static readonly Vector3[] NodeSetASlots = { new Vector3(-5f, 0f, -21f), new Vector3(5f, 0f, -22f), new Vector3(0f, 0f, -25f) };
        /// <summary>Slots of <see cref="NodeSetB"/> (HOL, SPA, TIN).</summary>
        public static readonly Vector3[] NodeSetBSlots = { new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f), new Vector3(0f, 0f, -10f) };

        /// <summary>North closet (Legion wave B).</summary>
        public const string ClosetNorth = "DEV.SC_N";
        /// <summary>East closet (Greycoat pair).</summary>
        public const string ClosetEast = "DEV.SC_E";
        /// <summary>North closet position and door (the room's opening, hidden behind a screen wall).</summary>
        public static readonly Vector3 ClosetNorthPos = new Vector3(-31f, 0f, 33f), ClosetNorthDoor = new Vector3(-28f, 0f, 33f);
        /// <summary>East closet position and door.</summary>
        public static readonly Vector3 ClosetEastPos = new Vector3(34f, 0f, 22f), ClosetEastDoor = new Vector3(34f, 0f, 19f);

        /// <summary>West flank route (wave B).</summary>
        public const string RouteWest = "DEV.Route.W";
        /// <summary>East flank route (Greycoat flanker).</summary>
        public const string RouteEast = "DEV.Route.E";
        /// <summary>West route points.</summary>
        public static readonly Vector3[] RouteWestPoints = { new Vector3(-31f, 0f, 33f), new Vector3(-22f, 0f, 30f), new Vector3(-33f, 0f, 12f), new Vector3(-28f, 0f, -4f), new Vector3(-18f, 0f, -12f) };
        /// <summary>East route points.</summary>
        public static readonly Vector3[] RouteEastPoints = { new Vector3(34f, 0f, 22f), new Vector3(31f, 0f, 12f), new Vector3(35f, 0f, 2f), new Vector3(28f, 0f, -9f), new Vector3(18f, 0f, -14f) };

        /// <summary>Greycoat suppressor position.</summary>
        public const string SuppressPos = "DEV.SuppressPos";
        /// <summary>Suppressor point.</summary>
        public static readonly Vector3 SuppressPoint = new Vector3(20f, 0f, 11f);

        /// <summary>Gunner perch on the tower.</summary>
        public const string Perch = "DEV.Perch";
        /// <summary>Perch point (top of a 2.5 m platform).</summary>
        public static readonly Vector3 PerchPoint = new Vector3(13f, 2.5f, 19f);

        /// <summary>Push trigger across the yard.</summary>
        public const string PushTrigger = "DEV.T.Push";
        /// <summary>Push trigger centre and size.</summary>
        public static readonly Vector3 PushCentre = new Vector3(0f, 2f, -4f), PushSize = new Vector3(70f, 4f, 2f);

        /// <summary>Flood test cone (owner id starts with "B6.F" so it gives the ×1.25 flood bonus).</summary>
        public const string FloodOwner = "B6.FDEV";
        /// <summary>Flood lamp position and aim point.</summary>
        public static readonly Vector3 FloodLamp = new Vector3(-22f, 8f, -34f), FloodAim = new Vector3(-6f, 0f, -18f);

        /// <summary>Wave A rifle positions (facing south).</summary>
        public static readonly Vector3[] WaveA = { new Vector3(-9f, 0f, 15f), new Vector3(-2f, 0f, 17f), new Vector3(5f, 0f, 14f), new Vector3(9f, 0f, 22f) };
        /// <summary>Beat 2-style sentries (optional).</summary>
        public static readonly Vector3[] Sentries = { new Vector3(-16f, 0f, 2f), new Vector3(-12f, 0f, 3f) };

        /// <summary>
        /// Cover blocks as (centre x, height, centre z, width along x); every block is 0.4 m deep. Height ≥ 1.6 m is full
        /// cover. Enemy-row blocks get cover nodes on their north side facing south, player-row blocks on their south side
        /// facing north, middle-row blocks on both sides.
        /// </summary>
        public static readonly Vector4[] Blocks =
        {
            // enemy line (north)
            new Vector4(-9f, 1.0f, 13.6f, 4f), new Vector4(-2f, 1.0f, 15.6f, 3f), new Vector4(5f, 2.0f, 12.6f, 2.5f), new Vector4(9f, 1.0f, 20.6f, 3f),
            new Vector4(-16f, 2.0f, 18f, 2.5f), new Vector4(18f, 1.0f, 16f, 4f), new Vector4(0f, 2.0f, 24f, 3f), new Vector4(-24f, 1.0f, 8f, 3f),
            // middle row (both sides)
            new Vector4(-12f, 1.0f, 2f, 3f), new Vector4(0f, 2.0f, 4f, 2.5f), new Vector4(12f, 1.0f, 2f, 3f), new Vector4(24f, 2.0f, 0f, 2.5f),
            // player side (south)
            new Vector4(-6f, 1.0f, -17f, 3f), new Vector4(6f, 1.0f, -18f, 3f), new Vector4(-14f, 2.0f, -12f, 2.5f), new Vector4(14f, 1.0f, -10f, 3f),
        };

        /// <summary>Index of the first middle-row block in <see cref="Blocks"/>.</summary>
        public const int MiddleRowStart = 8;
        /// <summary>Index of the first player-side block in <see cref="Blocks"/>.</summary>
        public const int PlayerRowStart = 12;
    }
}
