using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Door and spawn points of the ten Appendix A spawn closets, derived from the rooms the building catalogue and the
    /// structure generators cut (Docs/REQUESTS.md WP04 → WP10: door = the opening seen from the play space, 2+ spawn points
    /// deep inside; SC_B7_UP spawns at least 15 m from the office door (346, 0.3, 350)).
    /// </summary>
    public static class ClosetLayout
    {
        private static Dictionary<string, ClosetDef> s_all;

        /// <summary>Every closet by id.</summary>
        public static IReadOnlyDictionary<string, ClosetDef> All => s_all ?? (s_all = Build());

        /// <summary>The closet with an id, or null.</summary>
        public static ClosetDef Get(string id) => id != null && All.TryGetValue(id, out var c) ? c : null;

        private static ClosetDef C(string id, Vector3 door, float doorHeading, float spawnHeading, string notes, params Vector3[] spawns) =>
            new ClosetDef { Id = id, Door = door, DoorHeading = doorHeading, Spawns = spawns, SpawnHeading = spawnHeading, Notes = notes };

        private static Dictionary<string, ClosetDef> Build()
        {
            var list = new[]
            {
                C("SC_B3_BAL", new Vector3(189f, 4.2f, 222f), 270f, 270f, "E3a loggia (x 189, z 220.2-223.8 at +4.2) and the room behind it (BuildingCatalog BLD-03.E3a).",
                    new Vector3(192.6f, 4.2f, 220.8f), new Vector3(193.6f, 4.2f, 223.3f)),
                C("SC_B3_ROOF", new Vector3(175.2f, 7.8f, 214f), 90f, 90f, "Stair hut on the BLD-03.W2b roof terrace (+7.8), door east towards the street.",
                    new Vector3(173.6f, 7.8f, 213.5f), new Vector3(173.6f, 7.8f, 214.6f)),
                C("SC_B3_N", new Vector3(168.4f, 0f, 240f), 180f, 180f, "T6 courtyard door (x 167.4-169.4 in the z 240 face) and the room behind (Tinsmiths' Lane north end).",
                    new Vector3(166f, 0f, 244.5f), new Vector3(170.6f, 0f, 245f)),
                C("SC_B4_E", new Vector3(258.4f, 0f, 281f), 180f, 180f, "S3.EastSt.N1 courtyard door (x 257.6-259.2 in the z 281 face) onto the east street.",
                    new Vector3(256.8f, 0f, 285.6f), new Vector3(260f, 0f, 286.6f)),
                C("SC_B6_GATE", new Vector3(346f, 0f, 298f), 0f, 0f, "Gatehouse yard door (north face, x 345.4-346.6) and the guard room (BLD-09).",
                    new Vector3(342.6f, 0f, 292.6f), new Vector3(349.4f, 0f, 292.6f)),
                C("SC_B7_UP", new Vector3(340.6f, 4.8f, 352f), 270f, 270f,
                    "Upper floor over the office (x 346-356, z 330-350): the stair wave crosses the loft to the landing; spawns 16-19 m from the office door (WP04).",
                    new Vector3(353f, 4.8f, 333.6f), new Vector3(351f, 4.8f, 334.6f)),
                C("SC_B9_W", new Vector3(319.6f, 0.3f, 336.2f), 0f, 0f, "Head of the west stair (ground floor); spawns in the switch hall's north-west corner, out of sight of the basement.",
                    new Vector3(322.6f, 0.3f, 332f), new Vector3(325f, 0.3f, 332.6f)),
                C("SC_B9_E", new Vector3(352.3f, 0.3f, 337.8f), 270f, 270f, "Head of the east stair (ground floor, x 352); spawns in the archive east of it, clear of the shelves (x 354.6, z 340.5+).",
                    new Vector3(354.8f, 0.3f, 333.6f), new Vector3(354.8f, 0.3f, 335.6f)),
                C("SC_B9_H", new Vector3(332f, -4.2f, 334.3f), 0f, 0f, "The cable-trench hatch in the basement floor; spawns in the trench below (NavMesh link trench -> hatch).",
                    new Vector3(332f, -6f, 331.4f), new Vector3(332f, -6f, 329.6f)),
                C("SC_B10_W", new Vector3(300f, -1f, 388f), 90f, 90f, "Gap in the ruined pump shed west of the Pitch (S7.WestShed x 290-298, z 383-394).",
                    new Vector3(293.4f, -1f, 386.4f), new Vector3(293.4f, -1f, 390f)),
            };
            var d = new Dictionary<string, ClosetDef>();
            foreach (var c in list) d.Add(c.Id, c);
            return d;
        }
    }
}
