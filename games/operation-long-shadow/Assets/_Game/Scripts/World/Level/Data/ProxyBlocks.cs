using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The far-proxy blocks of the tile (TECH_PLAN D7). Section builders put every generated renderer of a block under a
    /// <c>SectionBlock</c> with the block id; the FarProxyBuilder writes the matching <c>FarProxyBlock</c> into
    /// <c>S9_TownFar</c>. Dense housing that no playable space touches exists only in S9 (always shown).
    /// </summary>
    public static class ProxyBlocks
    {
        private static List<ProxyBlockDef> s_all;

        /// <summary>Every block.</summary>
        public static IReadOnlyList<ProxyBlockDef> All => s_all ?? (s_all = Build());

        /// <summary>A block by id, or null.</summary>
        public static ProxyBlockDef Get(string id)
        {
            foreach (var b in All)
                if (b.Id == id) return b;
            return null;
        }

        /// <summary>Blocks of a section.</summary>
        public static IEnumerable<ProxyBlockDef> Of(SectionId section)
        {
            foreach (var b in All)
                if (b.Section == section) yield return b;
        }

        /// <summary>The block of a section containing a ground point (the closest one when none contains it).</summary>
        public static ProxyBlockDef Find(SectionId section, Vector3 p)
        {
            ProxyBlockDef best = null;
            var bestD = float.MaxValue;
            foreach (var b in All)
            {
                if (b.Section != section) continue;
                var d = b.Rect.DistanceTo(p);
                if (d < bestD)
                {
                    bestD = d;
                    best = b;
                }
            }
            return best;
        }

        private static ProxyBlockDef B(string id, SectionId s, float x0, float z0, float x1, float z1, float swap = WorldConst.FarProxySwapDistance) =>
            new ProxyBlockDef { Id = id, Section = s, Rect = new AreaRect(x0, z0, x1, z1), SwapDistance = swap };

        private static List<ProxyBlockDef> Build()
        {
            return new List<ProxyBlockDef>
            {
                // S1: the terrain is one Unity Terrain, so the whole approach is one block (real whenever S1 is loaded and the
                // player is on or near it; the S9 far mesh sits 0.5 m lower and shows only after S1 unloads).
                B("S1.Approach", SectionId.Approach, -220f, -160f, 200f, 170f),
                B("S2.South", SectionId.Market, 164f, 166f, 199f, 208f),
                B("S2.North", SectionId.Market, 164f, 208f, 199f, 254f),
                B("S2.SunsetLane", SectionId.Market, 118f, 214f, 164f, 244f),
                B("S3.Square", SectionId.Square, 182f, 244f, 262f, 300f),
                B("S3.Cinema", SectionId.Square, 184f, 298f, 233f, 334f),
                B("S3.Alley", SectionId.Square, 170f, 244f, 184f, 320f),
                B("S4.Roofs", SectionId.Roofs, 230f, 300f, 301f, 346f),
                B("S5.West", SectionId.StationExt, 298f, 282f, 347f, 386f),
                B("S5.East", SectionId.StationExt, 347f, 282f, 396f, 386f),
                B("S7.Pitch", SectionId.Pitch, 290f, 382f, 450f, 490f),
            };
        }
    }
}
