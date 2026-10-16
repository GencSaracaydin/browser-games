using System.Collections.Generic;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Markers deliberately placed away from their Appendix A coordinates because the geometry the appendix assumes is
    /// elsewhere (each in Docs/DECISIONS.md [WP10_LEVEL]). The markup placer uses these positions and the
    /// validator accepts them.
    /// </summary>
    public static class MarkupOverrides
    {
        private static readonly Dictionary<string, MarkupOverride> s_all = new Dictionary<string, MarkupOverride>
        {
            {
                "Gate.WestStairDoor", new MarkupOverride
                {
                    Id = "Gate.WestStairDoor", Position = new Vector3(319.6f, 0.3f, 334.6f),
                    Reason = "Appendix A (319, 0.3, 337) is above the west stair's first flight; the door is in the z 334.6 wall (Map C1).",
                }
            },
            {
                "B11.T1", new MarkupOverride
                {
                    Id = "B11.T1", Position = new Vector3(346f, 4f, 294f),
                    Reason = "The gatehouse roof is +4 (BLD-09, the same spot as B6.Perch.Gatehouse and E6-05); Appendix A gives +6.",
                }
            },
        };

        /// <summary>True when an id has an override.</summary>
        public static bool TryGet(string id, out MarkupOverride o) => s_all.TryGetValue(id ?? string.Empty, out o);

        /// <summary>Every override.</summary>
        public static IEnumerable<MarkupOverride> All => s_all.Values;
    }
}
