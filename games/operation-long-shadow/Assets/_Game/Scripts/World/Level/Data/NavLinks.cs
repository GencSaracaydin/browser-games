using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Off-mesh links inside a section where the geometry is too narrow or too steep for the 0.35 m agent (TECH_PLAN
    /// §7.15): the Beat 5 planks (0.6 m wide, GDD §7.5), the tank stand, the R3→R4 steel stair, the marquee crate-stack
    /// drop (GDD §7.4), the cable-trench hatch (SC_B9_H, WP04) and the Relay Hall spiral stair (SC_B7_UP's stair wave).
    /// WP04's LinkTraversal picks the animation from the rise (> 2.6 ladder, > 1.4 climb, &lt; −1 drop, else vault).
    /// Same type as the section seams, with <see cref="NavSeam.Other"/> = <see cref="NavSeam.Owner"/>.
    /// </summary>
    public static class NavLinks
    {
        private static List<NavSeam> s_all;

        /// <summary>Every in-section link.</summary>
        public static IReadOnlyList<NavSeam> All => s_all ?? (s_all = Build());

        /// <summary>Links of a section.</summary>
        public static IEnumerable<NavSeam> Of(SectionId section)
        {
            foreach (var l in All)
                if (l.Owner == section) yield return l;
        }

        private static NavSeam L(string name, SectionId s, Vector3 a, Vector3 b, float width, string notes) =>
            new NavSeam { Name = name, Owner = s, Other = s, Start = a, End = b, Width = width, Notes = notes };

        private static List<NavSeam> Build()
        {
            return new List<NavSeam>
            {
                L("S3.MarqueeToCrates", SectionId.Square, new Vector3(226.6f, 4.5f, 300.2f), new Vector3(227f, 2.2f, 298.4f), 0.8f, "Marquee walkway down onto the crate stack (B4.MarqueeDrop, top +2.2)."),
                L("S3.CratesToStreet", SectionId.Square, new Vector3(227f, 2.2f, 298f), new Vector3(227f, 0f, 296.4f), 0.8f, "Crate stack down to the square."),
                L("S4.Plank12", SectionId.Roofs, new Vector3(245.4f, 9f, 318f), new Vector3(248.6f, 8f, 318f), 0f, "B5.Plank12 (x 246-248, z 318): R1 +9 to R2 +8."),
                L("S4.TankStand", SectionId.Roofs, new Vector3(256.8f, 8f, 324f), new Vector3(259f, 9f, 324f), 0f, "R2 deck up onto the tank stand (B5.TankStand, top +9)."),
                L("S4.Plank23", SectionId.Roofs, new Vector3(260.6f, 9f, 324f), new Vector3(263.6f, 10f, 324f), 0f, "B5.Plank23 (x 261-263, z 324): tank stand +9 to R3 +10."),
                L("S4.Stair34", SectionId.Roofs, new Vector3(274.4f, 10f, 320f), new Vector3(277.8f, 12f, 320f), 0.8f, "B5.Stair34 steel stair (x 275-277): R3 +10 to R4 +12."),
                L("S6.TrenchHatch", SectionId.StationInt, new Vector3(332f, -6f, 332.6f), new Vector3(332f, -4.2f, 335.2f), 0f, "Cable trench up through the hatch (SC_B9_H, WP04)."),
                L("S6.SpiralStair", SectionId.StationInt, new Vector3(334.5f, 0.3f, 356.3f), new Vector3(334.1f, 4.8f, 356.3f), 0f, "Spiral stair (334.5, 0.3, 358) ground floor to the landing (+4.8)."),
            };
        }
    }
}
