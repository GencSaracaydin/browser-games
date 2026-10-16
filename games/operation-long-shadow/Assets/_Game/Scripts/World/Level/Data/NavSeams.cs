using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The section seams of TECH_PLAN §7.15 with their links (owner = the section that loads later, GDD §6.7):
    /// S1↔S2 Weighbridge arch, S2↔S3 market arch and side alley, S3↔S4 ladder L, S4↔S5 breach hole, S5↔S6 Hall double
    /// door, S6↔S5 tunnel ladder, S5↔S7 postern and ramp, plus the in-section traversal links (wadi boulder, roof planks,
    /// tank stand, steel stair, R4 switchback, basement hatch) that the section builders place next to their geometry.
    /// </summary>
    public static class NavSeams
    {
        private static List<NavSeam> s_all;

        /// <summary>Every seam link.</summary>
        public static IReadOnlyList<NavSeam> All => s_all ?? (s_all = Build());

        /// <summary>Seam links owned by a section.</summary>
        public static IEnumerable<NavSeam> OwnedBy(SectionId section)
        {
            foreach (var s in All)
                if (s.Owner == section) yield return s;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static List<NavSeam> Build()
        {
            return new List<NavSeam>
            {
                new NavSeam
                {
                    Name = "S1-S2.Weighbridge", Owner = SectionId.Market, Other = SectionId.Approach,
                    Start = V(183f, 0f, 166.5f), End = V(183f, 0f, 171.5f), Width = 5f,
                    Notes = "Through the Weighbridge arch passage (x 180-186 in the x 176-191 block, z 160-170); S2 loads at Beat 1 start, after S1.",
                },
                new NavSeam
                {
                    Name = "S2-S3.MarketArch", Owner = SectionId.Square, Other = SectionId.Market,
                    Start = V(185f, 0f, 248.5f), End = V(185f, 0f, 254.5f), Width = 5f,
                    Notes = "Market arch (z 248-254); S3 loads at Beat 2 start, after S2.",
                },
                new NavSeam
                {
                    Name = "S2-S3.SideAlley", Owner = SectionId.Square, Other = SectionId.Market,
                    Start = V(177f, 0f, 243.5f), End = V(177f, 0f, 248.5f), Width = 4f,
                    Notes = "Side alley mouth (the alley north of z 246 is S3's, DECISIONS).",
                },
                new NavSeam
                {
                    Name = "S3-S4.LadderL", Owner = SectionId.Roofs, Other = SectionId.Square,
                    Start = V(236f, 0f, 301f), End = V(236f, 9f, 304.2f), Width = 0f,
                    Notes = "Ladder L (scripted climb); S4 loads on D019, after S3.",
                },
                new NavSeam
                {
                    Name = "S4-S5.Breach", Owner = SectionId.StationExt, Other = SectionId.Roofs,
                    Start = V(297.5f, 0f, 322f), End = V(302.8f, 0f, 322f), Width = 4.5f, StartsInactive = true,
                    Notes = "Breach hole x 300, z 319-325; activated by BreachWall on the blast (B6.Blast).",
                },
                new NavSeam
                {
                    Name = "S5-S6.HallDoor", Owner = SectionId.StationInt, Other = SectionId.StationExt,
                    Start = V(338f, 0f, 327.8f), End = V(338f, 0.3f, 332.2f), Width = 2.4f,
                    Notes = "Relay Hall double door (338, 0.3, 330); S6 loads on D034, after S5.",
                },
                new NavSeam
                {
                    Name = "S6-S5.TunnelLadder", Owner = SectionId.StationInt, Other = SectionId.StationExt,
                    Start = V(344.3f, -4.2f, 380.4f), End = V(344.3f, 0f, 380.2f), Width = 0f,
                    Notes = "Ladder from the cable tunnel up to the yard behind the postern (scripted climb).",
                },
                new NavSeam
                {
                    Name = "S5-S7.Postern", Owner = SectionId.Pitch, Other = SectionId.StationExt,
                    Start = V(346f, 0f, 380.8f), End = V(346f, 0f, 384.6f), Width = 1.8f,
                    Notes = "Through the postern (346, 0, 382) onto the ramp; S7 loads at MID-A, after S5.",
                },
            };
        }
    }
}
