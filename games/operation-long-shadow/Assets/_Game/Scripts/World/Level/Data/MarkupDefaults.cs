using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Facing, stance and tags Appendix A leaves implicit, chosen from the GDD beat text: where each squad node set looks
    /// (towards the threat of its fight, GDD §7), which node set the squad returns to on a checkpoint reload, perch tags and
    /// facings (the encounter tables' facings), and cover-node facings.
    /// </summary>
    public static class MarkupDefaults
    {
        private static readonly Dictionary<string, NodeSetPose> s_sets = new Dictionary<string, NodeSetPose>
        {
            // Beat 1: overlooking Zaroud (GDD §7.1: Hollis prone with field glasses, heading 45°).
            { "B1.Squad.Start", Pose(45f, 60f, 30f, true) },
            // Beat 2: behind the irrigation wall facing the gate team; then at the guard hut towards the arch.
            { "B2.NodeSet.A", Pose(15f, 20f, 25f, true) },
            { "B2.NodeSet.B", Pose(10f, 0f, 350f, true) },
            // Beat 3: leapfrogging north up the market.
            { "B3.NodeSet.M1", Pose(0f, 350f, 5f, true) },
            { "B3.NodeSet.M2", Pose(0f, 350f, 5f, true) },
            { "B3.NodeSet.M3", Pose(0f, 350f, 5f, true) },
            { "B3.NodeSet.M4", Pose(10f, 0f, 20f, false) },
            // Beat 4: pinned at the arch towards the technical (bearing 52°), then the square fight to the east.
            { "B4.NodeSet.Q1", Pose(52f, 48f, 55f, true) },
            { "B4.NodeSet.Q2", Pose(70f, 80f, 60f, true) },
            // Beat 5: single file east across the roofs; MID-A crouched facing east at the R4 parapet (GDD §7.5, §8.5).
            { "B5.NodeSet.R1", Pose(75f, 80f, 70f, true) },
            { "B5.NodeSet.R2", Pose(75f, 80f, 70f, true) },
            { "B5.NodeSet.R3", Pose(75f, 80f, 70f, true) },
            { "B5.NodeSet.MIDA", Pose(90f, 90f, 90f, true) },
            // Beat 6: rubble at the breach, cable drums, the door stack (facing the Hall door).
            { "B6.NodeSet.Y1", Pose(80f, 95f, 90f, true) },
            { "B6.NodeSet.Y2", Pose(60f, 90f, 75f, true) },
            { "B6.NodeSet.Y3", Pose(0f, 0f, 0f, false) },
            // Beat 8: Hollis guards the door facing east with his back to the screen; the others watch the screen.
            { "B8.NodeSet.Reel", Pose(90f, 0f, 0f, false) },
            // Beat 9: Hollis on the west stair, Sparrow at the safe, Tinker on the east stair.
            { "B9.NodeSet.Defend", Pose(257f, 90f, 141f, true) },
            // Beat 10: skirmish line behind the bleacher facing north and east.
            { "B10.NodeSet.P1", Pose(30f, 45f, 60f, true) },
        };

        private static NodeSetPose Pose(float h, float s, float t, bool crouch) => new NodeSetPose { Hollis = h, Sparrow = s, Tinker = t, Crouch = crouch };

        /// <summary>Pose of a node set (defaults: facing north, standing).</summary>
        public static NodeSetPose NodeSet(string id) => id != null && s_sets.TryGetValue(id, out var p) ? p : Pose(0f, 0f, 0f, false);

        /// <summary>Doorway node sets (GDD §15 exception).</summary>
        public static bool AllowDoorway(string id) => id == "B8.NodeSet.Reel";

        private static readonly Dictionary<string, string> s_checkpointSets = new Dictionary<string, string>
        {
            { "CP1", "B1.Squad.Start" }, { "CP3", "B3.NodeSet.M1" }, { "CP3b", "B3.NodeSet.M3" }, { "CP4", "B4.NodeSet.Q1" },
            { "CP5", "B5.NodeSet.R1" }, { "CP6", "B6.NodeSet.Y1" }, { "CP7", "B6.NodeSet.Y3" }, { "CP8", "B8.NodeSet.Reel" },
            { "CP9", "B9.NodeSet.Defend" }, { "CP10", "B10.NodeSet.P1" },
        };

        /// <summary>Node set the squad goes to when a checkpoint reloads (empty = the squad decides).</summary>
        public static string CheckpointNodeSet(string cpId) => cpId != null && s_checkpointSets.TryGetValue(cpId, out var s) ? s : string.Empty;

        private static readonly Dictionary<string, float> s_checkpointHeadings = new Dictionary<string, float>
        {
            { "CP1", 45f }, { "CP2", 60f }, { "CP3", 0f }, { "CP3b", 0f }, { "CP4", 40f }, { "CP5", 80f }, { "CP6", 180f },
            { "CP7", 0f }, { "CP8", 270f }, { "CP9", 90f }, { "CP10", 10f },
        };

        /// <summary>Player yaw at a checkpoint (facing the way on).</summary>
        public static float CheckpointHeading(string cpId) => cpId != null && s_checkpointHeadings.TryGetValue(cpId, out var h) ? h : 0f;

        /// <summary>Perch tag and facing of a perch id (GDD §7 encounter tables).</summary>
        public static PerchTag PerchTagOf(string id, out float facing)
        {
            switch (id)
            {
                case "B2.Perch.TruckBed": facing = 200f; return PerchTag.Vehicle;
                case "B3.Perch.Balcony": facing = 225f; return PerchTag.Balcony;
                case "B3.Perch.WestRoof": facing = 135f; return PerchTag.Rooftop;
                case "B5.Perch.Mast": facing = 250f; return PerchTag.Rooftop;
                case "B6.Perch.Gatehouse": facing = 300f; return PerchTag.Rooftop;
                default: facing = 0f; return PerchTag.Rooftop;
            }
        }

        /// <summary>Facing of the Beat 5 water-tank cover nodes (towards the marksman on the mast, about 60°).</summary>
        public static float WaterTankCoverFacing(string id) => 62f;
    }
}
