using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The blockers behind the fifteen Appendix A gates (GDD §7.13 progression gates, closed at start) and backtrack
    /// blockers (GDD §6.7, open until their beat). Every blocker is a PlayerClip box sized to the real opening (the marker
    /// stays on its Appendix A point); doors also get hinged leaves for <see cref="GateDoorAnimator"/>.
    /// </summary>
    public static class GateBlockers
    {
        private static Dictionary<string, GateBlockerDef> s_all;

        /// <summary>Every blocker by gate id.</summary>
        public static IReadOnlyDictionary<string, GateBlockerDef> All => s_all ?? (s_all = Build());

        /// <summary>The blocker of a gate id, or null.</summary>
        public static GateBlockerDef Get(string id) => id != null && All.TryGetValue(id, out var g) ? g : null;

        private static GateBlockerDef B(string id, float x, float y, float z, float sx, float sy, float sz, bool startsOpen, string notes) =>
            new GateBlockerDef { Id = id, Center = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz), StartsOpen = startsOpen, Notes = notes };

        private static GateBlockerDef Door(GateBlockerDef d, float width, float height, int kind, bool kick, params float[] hinge)
        {
            // hinge = { x, y, z, closedYaw, openYaw } per leaf.
            var n = hinge.Length / 5;
            d.Hinges = new Vector3[n];
            d.ClosedYaw = new float[n];
            d.OpenYaw = new float[n];
            for (var i = 0; i < n; i++)
            {
                d.Hinges[i] = new Vector3(hinge[i * 5], hinge[i * 5 + 1], hinge[i * 5 + 2]);
                d.ClosedYaw[i] = hinge[i * 5 + 3];
                d.OpenYaw[i] = hinge[i * 5 + 4];
            }
            d.LeafWidth = width;
            d.LeafHeight = height;
            d.LeafKind = kind;
            d.Kick = kick;
            return d;
        }

        private static Dictionary<string, GateBlockerDef> Build()
        {
            var list = new[]
            {
                Door(B("Gate.Weighbridge", 183f, 1.8f, 163f, 6.2f, 3.6f, 0.4f, false, "Chained iron gate in the arch passage x 180-186; swings north onto the Long Market (B2.GateOpen)."),
                    3f, 3.6f, 2, false, 180f, 0f, 163f, 0f, -92f, 186f, 0f, 163f, 180f, 92f),
                Door(B("Gate.CinemaSideDoor", 186.2f, 1.1f, 307f, 0.5f, 2.3f, 1.3f, false, "Cinema side door, west wall z 306.4-307.6; opens into the lobby."),
                    1.2f, 2.2f, 0, false, 186.2f, 0f, 306.4f, -90f, 90f),
                B("Gate.LadderL", 236f, 1.6f, 301.7f, 1.8f, 3.2f, 1.6f, false, "PlayerClip at the foot of ladder L until every E4 enemy is dead."),
                B("Gate.R4Stair", 291f, 13.2f, 330f, 2.4f, 2.4f, 2.4f, false, "PlayerClip at the R4 stair top until MID-A ends (B5.T.Stair)."),
                Door(B("Gate.HallDoor", 338f, 1.6f, 330f, 2.6f, 2.6f, 0.5f, false, "Relay Hall double door x 336.8-339.2 (south wall); kicked open inward."),
                    1.2f, 2.5f, 1, true, 336.8f, 0.3f, 330f, 0f, -100f, 339.2f, 0.3f, 330f, 180f, 100f),
                Door(B("Gate.ScreeningDoor", 330f, 5.9f, 353.5f, 0.5f, 2.2f, 1.3f, false, "Screening-room door in the x 330 partition, z 352.9-354.1; opens onto the landing."),
                    1.2f, 2.2f, 0, false, 330f, 4.8f, 352.9f, -90f, 90f),
                Door(B("Gate.WestStairDoor", 319.6f, 1.4f, 334.6f, 2.1f, 2.3f, 0.5f, false, "Ground-floor door of the west basement stair (z 334.6 wall, x 318.6-320.6); opens into the hall."),
                    2.0f, 2.2f, 1, false, 318.6f, 0.3f, 334.6f, 0f, 90f),
                Door(B("Gate.TunnelDoor", 344f, -3.1f, 359.8f, 1.3f, 2.2f, 0.5f, false, "Steel door in the basement north wall into the cable tunnel; opens north."),
                    1.2f, 2.1f, 1, false, 343.4f, -4.2f, 359.8f, 0f, -95f),
                B("Gate.HeliDoor", 350f, 0.2f, 404f, 0f, 0f, 0f, false, "Logical gate (boarding prompt); no blocker."),
                B("Block.MarketSouth", 183f, 2f, 165f, 6.4f, 4f, 0.3f, true, "Arch passage x 180-186 at z 165 (the CSV box x 181-189 would leave a gap on the west pier)."),
                B("Block.MarketArch", 185f, 2f, 250f, 8f, 4f, 0.3f, true, "Market arch x 181-189, z 250."),
                B("Block.SideAlley", 177f, 2f, 246f, 6f, 4f, 0.3f, true, "Side alley x 174-180, z 246."),
                B("Block.R4Stair", 291f, 13.2f, 330f, 2.4f, 2.4f, 2.4f, true, "R4 stair top (closes at Beat 6 start)."),
                B("Block.Breach", 300f, 2f, 322f, 0.3f, 4f, 6f, true, "Breach hole x 300, z 319-325."),
                Door(B("Block.Postern", 346f, 1.5f, 382f, 1.8f, 3f, 0.5f, true, "Postern in the north compound wall x 345.2-346.8; swings shut behind the player at Beat 10."),
                    1.6f, 2.4f, 1, false, 345.2f, 0f, 382f, 0f, -95f),
            };
            var d = new Dictionary<string, GateBlockerDef>();
            foreach (var g in list) d.Add(g.Id, g);
            return d;
        }
    }
}
