using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// TriggerVolume ids the mission scripts wait on (TECH_PLAN Appendix A, kind "T") and their Appendix A geometry as
    /// fallbacks (<see cref="TriggerShape"/>). Boxes without a "y" range span 1 m below to 4 m above the local floor, as
    /// in <c>markup_ids.csv</c>. Scripts never write trigger ids as literals; the tests check every id against the CSV.
    /// </summary>
    public static class MissionTriggers
    {
        public const string B1Scree = "B1.T.Scree";
        public const string B1Boulder = "B1.T.Boulder";
        public const string B1WadiMid = "B1.T.WadiMid";
        public const string B1GroveEdge = "B1.T.GroveEdge";
        public const string B1GroveEnter = "B1.T.GroveEnter";
        public const string B2FiringPos = "B2.T.FiringPos";
        public const string B2GateFight = "B2.T.GateFight";
        public const string B2Exit = "B2.T.Exit";
        public const string B3Z185 = "B3.T.Z185";
        public const string B3Z200 = "B3.T.Z200";
        public const string B3Crossing = "B3.T.Crossing";
        public const string B3Exit = "B3.T.Exit";
        public const string B4SideDoor = "B4.T.SideDoor";
        public const string B4Lobby = "B4.T.Lobby";
        public const string B4Auditorium = "B4.T.Auditorium";
        public const string B4Marquee = "B4.T.Marquee";
        public const string B4Ladder = "B4.T.Ladder";
        public const string B5R4 = "B5.T.R4";
        public const string B5Stair = "B5.T.Stair";
        public const string B6Alley = "B6.T.Alley";
        public const string B6StandClear = "B6.T.StandClear";
        public const string B6Kick = "B6.T.Kick";
        public const string B7OfficeDoor = "B7.T.OfficeDoor";
        public const string B7Landing = "B7.T.Landing";
        public const string B8Enter = "B8.T.Enter";
        public const string B8BasementFoot = "B8.T.BasementFoot";
        public const string B9Tunnel = "B9.T.Tunnel";
        public const string B10Postern = "B10.T.Postern";
        public const string B10LZ = "B10.T.LZ";

        private static readonly Dictionary<string, TriggerShape> s_shapes = new Dictionary<string, TriggerShape>
        {
            // ---- Beat 1 (Appendix A B1)
            { B1Scree, TriggerShape.Capsule(new Vector3(82f, 13f, 62f), new Vector3(90f, 8f, 72f), 2f) },
            { B1Boulder, TriggerShape.Sphere(98f, 4f, 82f, 2.5f) },
            { B1WadiMid, TriggerShape.Sphere(88f, 10f, 70f, 6f) },
            { B1GroveEdge, TriggerShape.Box(100f, 112f, -1f, 4f, 82f, 88f) },
            { B1GroveEnter, TriggerShape.Box(100f, 120f, -1f, 4f, 88f, 104f) },
            // ---- Beat 2
            { B2FiringPos, TriggerShape.Sphere(136f, 0f, 112f, 3f) },
            { B2GateFight, TriggerShape.Box(100f, 200f, -1f, 4f, 136f, 137f) },
            { B2Exit, TriggerShape.Box(176f, 191f, -1f, 4f, 168f, 170f) },
            // ---- Beat 3
            { B3Z185, TriggerShape.Box(174f, 197f, -1f, 8f, 185f, 187f) },
            { B3Z200, TriggerShape.Box(174f, 197f, -1f, 8f, 200f, 202f) },
            { B3Crossing, TriggerShape.Box(174f, 197f, -1f, 8f, 216f, 218f) },
            { B3Exit, TriggerShape.Sphere(185f, 0f, 252f, 8f) },
            // ---- Beat 4
            { B4SideDoor, TriggerShape.Box(186f, 189f, -1f, 4f, 305f, 309f) },
            { B4Lobby, TriggerShape.Box(186f, 198f, 0f, 4f, 302f, 312f) },
            { B4Auditorium, TriggerShape.Box(186f, 231f, 0f, 9f, 312f, 330f) },
            { B4Marquee, TriggerShape.Box(200f, 226f, 4.5f, 7f, 299f, 301f) },
            { B4Ladder, TriggerShape.Sphere(236f, 0f, 301f, 10f) },
            // ---- Beat 5
            { B5R4, TriggerShape.Box(277f, 292f, 11f, 15f, 312f, 332f) },
            { B5Stair, TriggerShape.Sphere(291f, 12f, 330f, 1.5f) },
            // ---- Beat 6
            { B6Alley, TriggerShape.Box(293f, 299f, -1f, 4f, 300f, 340f) },
            { B6StandClear, TriggerShape.Sphere(296f, 0f, 310f, 2.5f) },
            { B6Kick, TriggerShape.Sphere(338f, 0.3f, 328.5f, 1.8f) },
            // ---- Beat 7
            { B7OfficeDoor, TriggerShape.Sphere(346f, 0.3f, 350f, 6f) },
            { B7Landing, TriggerShape.Box(330f, 340f, 4.8f, 8f, 350f, 362f) },
            // ---- Beat 8
            { B8Enter, TriggerShape.Box(318f, 330f, 4.8f, 8f, 350f, 360f) },
            { B8BasementFoot, TriggerShape.Sphere(320.5f, -4.2f, 343f, 1.5f) },
            // ---- Beat 9
            { B9Tunnel, TriggerShape.Box(342f, 345f, -4.2f, 0f, 361f, 364f) },
            // ---- Beat 10
            { B10Postern, TriggerShape.Box(343f, 349f, -2f, 3f, 383f, 387f) },
            { B10LZ, TriggerShape.Box(326f, 352f, -2f, 3f, 386f, 398f) },
        };

        /// <summary>Every trigger id with a fallback shape.</summary>
        public static IEnumerable<string> Ids => s_shapes.Keys;

        /// <summary>The Appendix A fallback shape of a trigger id.</summary>
        public static bool TryGetShape(string id, out TriggerShape shape)
        {
            if (id != null && s_shapes.TryGetValue(id, out shape)) return true;
            shape = default;
            return false;
        }
    }
}
