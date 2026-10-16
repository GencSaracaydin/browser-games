using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The dense housing of Zaroud that no playable space touches (GDD §6.2 Map A ':' areas, §6.6 BLD-15): row houses in
    /// city blocks separated by 3–4 m lanes, 2–3 storeys, generated deterministically for <c>S9_TownFar</c> (always
    /// shown, baked). It keeps clear of every catalogued building, the streets and squares, the Relay Station, the Sunset
    /// Lane sight line (GDD §6.5 A5: nothing built in its line) and the Beat 11 east street, and it guarantees the three
    /// rooftop-target houses of GDD §7.11 at their roof heights: T2 (285, 9, 268), T3 (224, 10, 226, behind a parapet),
    /// T4 (236, 9, 238).
    /// </summary>
    public static class DenseHousing
    {
        /// <summary>Districts filled with housing (Map A).</summary>
        public static readonly AreaRect[] Districts =
        {
            new AreaRect(120f, 158f, 158f, 300f), // west of Tinsmiths' Lane and the side alley
            new AreaRect(120f, 300f, 232f, 362f), // north-west, north of the cinema
            new AreaRect(199f, 158f, 300f, 252f), // east of the market, south of the square
            new AreaRect(262f, 252f, 300f, 296f), // east of the square, south of the roofs
            new AreaRect(232f, 346f, 300f, 362f), // north of the roofs
        };

        /// <summary>Areas kept free (playable spaces, catalogued blocks, the station).</summary>
        public static readonly AreaRect[] Exclusions =
        {
            new AreaRect(156f, 155f, 199.5f, 255f),   // market, Tinsmiths' Lane, frontage
            new AreaRect(146f, 154f, 219f, 172f),     // Weighbridge frontage
            new AreaRect(160f, 244f, 278f, 337f),     // square, cinema, alley, yard, east street near part
            new AreaRect(229f, 294f, 302f, 348f),     // Laundry Roofs and their housing
            new AreaRect(250f, 270f, 305f, 283f),     // east street to x 300 (B11 trucks)
            new AreaRect(296f, 280f, 420f, 440f),     // Relay Station, Pitch
        };

        /// <summary>Rooftop-target houses of GDD §7.11 (id, footprint, storeys, parapet).</summary>
        private static BuildingSpec[] Targets()
        {
            return new[]
            {
                Make("BLD-15.T2", new AreaRect(279f, 260f, 292f, 270f), new[] { 3.4f, 3.0f, 2.6f }, 0.5f, 2),
                Make("BLD-15.T3", new AreaRect(218f, 219f, 230f, 232f), new[] { 3.6f, 3.4f, 3.0f }, 1.3f, 4),
                Make("BLD-15.T4", new AreaRect(230.5f, 232.5f, 242f, 244f), new[] { 3.4f, 3.0f, 2.6f }, 0.6f, 1),
            };
        }

        private static BuildingSpec Make(string id, AreaRect r, float[] storeys, float parapet, int tint)
        {
            return new BuildingSpec
            {
                Id = id, Bld = "BLD-15", Section = SectionId.TownFar, Block = null, Footprint = r, Storeys = storeys, Parapet = parapet,
                Style = BuildingStyle.Vernacular, Tint = tint, FarOnly = true, RoofClutter = true, Seed = (int)(DetRandom.Hash(id) & 0x7FFFFFFF),
                Facades = new[] { FacadeKind.Residential, FacadeKind.Residential, FacadeKind.Residential, FacadeKind.Residential },
                Notes = "Beat 11 rooftop-target house (GDD §7.11).",
            };
        }

        private static List<BuildingSpec> s_all;

        /// <summary>Every far-only house.</summary>
        public static IReadOnlyList<BuildingSpec> All => s_all ?? (s_all = Generate());

        /// <summary>True when a footprint may hold far-only housing.</summary>
        public static bool IsFree(AreaRect r, IList<BuildingSpec> taken)
        {
            foreach (var e in Exclusions)
                if (r.Overlaps(e)) return false;
            if (CrossesSunsetLane(r)) return false;
            if (taken != null)
                foreach (var b in taken)
                {
                    // Rotated (Sunset Lane) houses are checked with a margin; axis-aligned neighbours may share an edge.
                    var fp = Mathf.Approximately(b.Yaw, 0f) ? b.Footprint : b.Footprint.Expanded(1.5f);
                    if (fp.Overlaps(r, 0.05f)) return false;
                }
            return true;
        }

        /// <summary>True if the rectangle comes within 6 m of the Sunset Lane axis (x 60–181), keeping its sun line clear.</summary>
        public static bool CrossesSunsetLane(AreaRect r)
        {
            var a = SunGeometry.LaneMouth;
            var b = SunGeometry.AlongBearing(a, WorldConst.SunAzimuth, 125f);
            var corners = new[] { new Vector3(r.X0, 0f, r.Z0), new Vector3(r.X1, 0f, r.Z0), new Vector3(r.X1, 0f, r.Z1), new Vector3(r.X0, 0f, r.Z1), r.Center };
            foreach (var c in corners)
            {
                var p = MathX.ClosestPointOnSegment(a, b, c, out _);
                if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(c.x, c.z)) < 6f) return true;
            }
            // Edges crossing the axis.
            for (var t = 0f; t <= 1f; t += 0.02f)
            {
                var p = Vector3.Lerp(a, b, t);
                if (r.Expanded(5.5f).Contains(p)) return true;
            }
            return false;
        }

        private static List<BuildingSpec> Generate()
        {
            var list = new List<BuildingSpec>(Targets());
            var taken = new List<BuildingSpec>(BuildingCatalog.All);
            taken.AddRange(list);
            var rng = new DetRandom(19841021UL);
            var index = 0;
            foreach (var d in Districts)
            {
                // Lanes split the district into blocks.
                var xs = Splits(d.X0, d.X1, rng, 26f, 40f);
                var zs = Splits(d.Z0, d.Z1, rng, 22f, 34f);
                for (var i = 0; i < xs.Count - 1; i++)
                for (var j = 0; j < zs.Count - 1; j++)
                {
                    var bx0 = xs[i] + (i == 0 ? 0f : 1.8f);
                    var bx1 = xs[i + 1] - (i == xs.Count - 2 ? 0f : 1.8f);
                    var bz0 = zs[j] + (j == 0 ? 0f : 1.8f);
                    var bz1 = zs[j + 1] - (j == zs.Count - 2 ? 0f : 1.8f);
                    FillBlock(list, taken, new AreaRect(bx0, bz0, bx1, bz1), rng, ref index);
                }
            }
            BuildingCatalog.ResolvePartyHeights(list);
            return list;
        }

        private static List<float> Splits(float a, float b, DetRandom rng, float min, float max)
        {
            var r = new List<float> { a };
            var x = a;
            while (b - x > max)
            {
                x += rng.Range(min, max);
                if (b - x < min * 0.6f) break;
                r.Add(x);
            }
            r.Add(b);
            return r;
        }

        private static void FillBlock(List<BuildingSpec> list, List<BuildingSpec> taken, AreaRect block, DetRandom rng, ref int index)
        {
            // Two rows of houses back to back along the block's long axis, a courtyard strip between them on deep blocks.
            var alongX = block.Width >= block.Depth;
            var depth = alongX ? block.Depth : block.Width;
            var rowDepth = depth > 22f ? (depth - rng.Range(3f, 6f)) * 0.5f : depth * 0.5f;
            for (var row = 0; row < 2; row++)
            {
                var start = alongX ? block.X0 : block.Z0;
                var end = alongX ? block.X1 : block.Z1;
                var pos = start;
                while (end - pos > 4.5f)
                {
                    var w = Mathf.Min(rng.Range(6f, 11.5f), end - pos);
                    if (end - pos - w < 4.5f) w = end - pos;
                    var d0 = row == 0 ? (alongX ? block.Z0 : block.X0) : (alongX ? block.Z1 : block.X1) - rowDepth;
                    var d1 = d0 + rowDepth;
                    var r = alongX ? new AreaRect(pos, d0, pos + w, d1) : new AreaRect(d0, pos, d1, pos + w);
                    pos += w;
                    if (!IsFree(r, taken)) continue;
                    var storeys = rng.Chance(0.3f) ? new[] { 3.6f, 3.2f, 3.0f } : rng.Chance(0.15f) ? new[] { 3.6f } : new[] { 3.6f, rng.Range(2.9f, 3.3f) };
                    var spec = new BuildingSpec
                    {
                        Id = "BLD-15.F" + index++, Bld = "BLD-15", Section = SectionId.TownFar, Block = null, Footprint = r, Storeys = storeys,
                        Parapet = rng.Range(0.3f, 0.9f), Style = rng.Chance(0.15f) ? BuildingStyle.Colonial : BuildingStyle.Vernacular, Tint = rng.Range(0, 6),
                        FarOnly = true, RoofClutter = true, Seed = (int)(rng.NextUInt() & 0x7FFFFFFF), Wear = 0.6f,
                    };
                    // Facades on the outer (lane) side of the block, party walls elsewhere (resolved against neighbours).
                    spec.Facades = new[] { FacadeKind.Party, FacadeKind.Party, FacadeKind.Party, FacadeKind.Party };
                    if (alongX) spec.Facades[row == 0 ? 0 : 2] = FacadeKind.Residential;
                    else spec.Facades[row == 0 ? 3 : 1] = FacadeKind.Residential;
                    if (Mathf.Abs(r.X0 - block.X0) < 0.1f) spec.Facades[3] = FacadeKind.Residential;
                    if (Mathf.Abs(r.X1 - block.X1) < 0.1f) spec.Facades[1] = FacadeKind.Residential;
                    if (Mathf.Abs(r.Z0 - block.Z0) < 0.1f) spec.Facades[0] = FacadeKind.Residential;
                    if (Mathf.Abs(r.Z1 - block.Z1) < 0.1f) spec.Facades[2] = FacadeKind.Residential;
                    if (depth > 22f) spec.Facades[alongX ? (row == 0 ? 2 : 0) : (row == 0 ? 1 : 3)] = FacadeKind.Service;
                    list.Add(spec);
                    taken.Add(spec);
                }
            }
        }
    }
}
