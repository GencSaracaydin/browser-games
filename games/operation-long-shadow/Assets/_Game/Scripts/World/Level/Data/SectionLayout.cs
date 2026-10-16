using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Ground extents of each streaming section (GDD §6.7) as rectangles in world metres, used for NavMesh bake bounds,
    /// section lookup of markers and validation. A rectangle list may overlap a neighbour: the NavMesh of each section
    /// extends <see cref="WorldConst.SeamOverlap"/> past its seams (TECH_PLAN §7.15).
    /// <para>Decision ([WP10_LEVEL] DECISIONS.md): the side alley north of z 246 and the service yard live in
    /// <c>S3_Square</c>, not <c>S2_Market</c>, so that <c>Block.SideAlley</c> (Beat 5 start, when S2 unloads) closes onto
    /// geometry that is still loaded.</para>
    /// </summary>
    public static class SectionLayout
    {
        private static readonly Dictionary<SectionId, AreaRect[]> s_rects = new Dictionary<SectionId, AreaRect[]>
        {
            // Ridge, wadi, grove, Weighbridge (+ the insertion apron SW of the tile, CINEMATIC_DIRECTION §2).
            { SectionId.Approach, new[] { new AreaRect(-220f, -160f, 200f, 170f) } },
            // Long Market, Tinsmiths' Lane, Sunset Lane, the alley mouth up to z 246.
            {
                SectionId.Market, new[]
                {
                    new AreaRect(164f, 166f, 199f, 254f), new AreaRect(118f, 214f, 182f, 244f), new AreaRect(172f, 240f, 182f, 248f),
                }
            },
            // Clock Square, the cinema, side alley north of z 246, service yard.
            { SectionId.Square, new[] { new AreaRect(172f, 244f, 262f, 334f) } },
            // Laundry Roofs, west-wall alley.
            { SectionId.Roofs, new[] { new AreaRect(230f, 298f, 301f, 346f) } },
            // Yard, walls, gatehouse, Relay Hall shell.
            { SectionId.StationExt, new[] { new AreaRect(298f, 282f, 396f, 386f) } },
            // Hall interiors, screening room, basement, cable tunnel (below the yard).
            { SectionId.StationInt, new[] { new AreaRect(314f, 326f, 358f, 384f) } },
            // The Pitch, roads, salt-pan near field.
            { SectionId.Pitch, new[] { new AreaRect(290f, 380f, 450f, 490f) } },
            // Rides on splines over the whole tile.
            { SectionId.Heli, new[] { new AreaRect(-10f, -10f, 950f, 560f) } },
            // The whole tile and the backdrop.
            { SectionId.TownFar, new[] { new AreaRect(-3000f, -3000f, 3000f, 3000f) } },
        };

        /// <summary>Rectangles of a section (empty for the booth).</summary>
        public static IReadOnlyList<AreaRect> Rects(SectionId id) =>
            s_rects.TryGetValue(id, out var r) ? r : (IReadOnlyList<AreaRect>)new AreaRect[0];

        /// <summary>Union of a section's rectangles.</summary>
        public static AreaRect Bounds(SectionId id)
        {
            var rects = Rects(id);
            if (rects.Count == 0) return new AreaRect(0f, 0f, 0f, 0f);
            var u = rects[0];
            for (var i = 1; i < rects.Count; i++) u = u.Union(rects[i]);
            return u;
        }

        /// <summary>Vertical range of a section's walkable geometry (NavMesh bake bounds).</summary>
        public static Vector2 HeightRange(SectionId id)
        {
            switch (id)
            {
                case SectionId.Approach: return new Vector2(-12f, 40f);
                case SectionId.StationInt: return new Vector2(-8f, 10f);
                case SectionId.Pitch: return new Vector2(-4f, 12f);
                case SectionId.Roofs: return new Vector2(-2f, 18f);
                default: return new Vector2(-3f, 26f);
            }
        }

        /// <summary>True if the XZ position lies in one of the section's rectangles.</summary>
        public static bool Contains(SectionId id, Vector3 p)
        {
            var rects = Rects(id);
            for (var i = 0; i < rects.Count; i++)
                if (rects[i].Contains(p)) return true;
            return false;
        }

        /// <summary>
        /// Section whose ground holds a world point, by priority (interiors below the yard, then the playable sections,
        /// then the backdrop). Booth and heli-cabin ids use local frames and never come here.
        /// </summary>
        public static SectionId SectionOfPoint(Vector3 p)
        {
            if (Contains(SectionId.StationInt, p) && p.y < -0.5f) return SectionId.StationInt;
            if (InsideRelayHall(p)) return SectionId.StationInt;
            if (Contains(SectionId.Approach, p) && p.z < 168f) return SectionId.Approach;
            if (Contains(SectionId.Pitch, p) && p.z > 382f) return SectionId.Pitch;
            if (Contains(SectionId.Roofs, p) && (p.y > 5f || p.x > 292f && p.x < 300f)) return SectionId.Roofs;
            if (Contains(SectionId.StationExt, p)) return SectionId.StationExt;
            if (Contains(SectionId.Square, p) && p.z >= 246f) return SectionId.Square;
            if (Contains(SectionId.Market, p)) return SectionId.Market;
            if (Contains(SectionId.Square, p)) return SectionId.Square;
            if (Contains(SectionId.Approach, p)) return SectionId.Approach;
            return SectionId.TownFar;
        }

        /// <summary>Relay Hall footprint (GDD §6.5 A12: x 316–356, z 330–362), interiors of S6.</summary>
        public static readonly AreaRect RelayHall = new AreaRect(316f, 330f, 356f, 362f);

        private static bool InsideRelayHall(Vector3 p) => RelayHall.Expanded(-0.2f).Contains(p) && p.y < 12f;
    }
}
