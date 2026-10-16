using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Terrain;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S1_Approach (GDD §6.5 A1–A4, §7.1–7.2): the approach Terrain (shoulder, wadi, grove, plaza, insertion apron), basalt
    /// outcrops and wadi rocks, the mantle boulder (98, 4, 82), the Pump House Six grove (about 24 olive and pomegranate
    /// trees clear of the route, the mudbrick irrigation wall x 140–170 with gaps at 148 and 162), Pump House Six and its
    /// diesel pump, the Weighbridge (sandstone arch x 176–191 with the passage x 180–186, 12 m scale plate z 145–157,
    /// barrier boom (182, 0, 144), the truck at (190, 0, 150) under E2-06's perch, the guard hut with the cassette radio) and
    /// the alert-cover sandbags along z 140.
    /// </summary>
    public sealed class S1ApproachBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.Approach;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            TerrainBuilder.Build(ctx.Group(SectionBuildContext.GGround));
            TownKit.Buildings(ctx);
            Weighbridge(ctx);
            Grove(ctx);
            Rocks(ctx);
        }

        private static void Weighbridge(SectionBuildContext ctx)
        {
            var arch = new MeshBuilder();
            Landmarks.WeighbridgeArch(arch, 176f, 191f, 160f, 170f, 180f, 186f);
            ctx.Mesh("WeighbridgeArch", ctx.RootAt(new Vector3(183f, 0f, 165f)), Vector3.zero, Quaternion.identity, arch, WorldConst.LightmapScaleHero * 0.5f);

            var street = ctx.Batch(TownKit.BStreet, new Vector3(183f, 0f, 151f));
            KitIndustrial.ScalePlate(street, new Vector3(183f, 0f, 151f), 12f, 3.6f);
            KitIndustrial.BarrierBoom(street, new Vector3(181.6f, 0f, 144f), 90f, 6.2f, false, KitSlots.PaintRed, KitSlots.PaintWhite);
            // The track from the scale plate into the arch (packed gravel, a hand's breadth above the terrain).
            TownKit.Ground(ctx, new AreaRect(179.5f, 140f, 186.5f, 160f), 0.03f, KitSlots.Gravel, SurfaceType.Gravel, false);

            // The truck: E2-06 stands on its bed at (190, 2.2, 150) facing 200, so the truck centre sits 1.6 m ahead.
            var fwd = MathX.DirectionFromBearing(200f);
            TownKit.Prop(ctx, "CargoTruck", new Vector3(190f, 0f, 150f) + fwd * 1.6f, 200f);
            // Barrier blocks by the gate (E2-04 cover) and sandbags along z 140 (B2.AlertCover).
            TownKit.Prop(ctx, "RoadBarrier", new Vector3(186.4f, 0f, 145.6f), 90f);
            TownKit.Prop(ctx, "RoadBarrier", new Vector3(178.8f, 0f, 145.2f), 92f);
            foreach (var d in DerivedMarkers.For(SectionId.Approach))
            {
                if (d.Group != "B2.AlertCover") continue;
                var p = d.Position;
                TownKit.SandbagWall(ctx, new Vector3(p.x - 0.9f, 0f, p.z - 1.0f), new Vector3(p.x + 0.9f, 0f, p.z - 1.0f), 3);
            }
            // Guard hut dressing: table, cassette radio (WP06 AMB_CASSETTE_TUNE), chair; jerrycans and a tyre by the arch.
            TownKit.Prop(ctx, "WoodenTable", new Vector3(171.6f, 0f, 152.4f), 90f);
            TownKit.Prop(ctx, "CassettePlayer", new Vector3(171.5f, 0.8f, 152.4f), 120f);
            TownKit.Prop(ctx, "WoodenChair", new Vector3(172.9f, 0f, 151.6f), 250f);
            TownKit.Prop(ctx, "Jerrycan", new Vector3(176.6f, 0f, 158.6f), 15f);
            TownKit.Prop(ctx, "Jerrycan", new Vector3(177.1f, 0f, 158.4f), 40f);
            TownKit.Prop(ctx, "OldTyre", new Vector3(192.4f, 0f, 157.8f), 70f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(193.2f, 0f, 158.2f), 0f);
            // Pump house: the diesel pump, the sentries' bench, an oil drum.
            TownKit.Prop(ctx, "DieselPump", new Vector3(150f, 0f, 126.4f), 180f);
            TownKit.Prop(ctx, "WoodenBench", new Vector3(145.2f, 0f, 126.8f), 180f);
            TownKit.Prop(ctx, "Barrel03", new Vector3(156.4f, 0f, 129f), 0f);
        }

        private static void Grove(SectionBuildContext ctx)
        {
            // Irrigation wall x 140–170 at z 109.5, 1.2 m high, gaps (1.6 m) at x 148 and 162 (GDD A3).
            var segments = new[] { new Vector2(140f, 147.2f), new Vector2(148.8f, 161.2f), new Vector2(162.8f, 170f) };
            foreach (var s in segments) TownKit.Wall(ctx, new Vector3(s.x, 0f, 109.5f), new Vector3(s.y, 0f, 109.5f), 1.2f, 0.55f, KitSlots.Mudbrick, SurfaceType.Plaster);

            var rng = new DetRandom("S1.Grove");
            var placed = new List<Vector3>();
            var route = CriticalPathTable.Knots(2);
            var keep = new List<Vector3> { new Vector3(146f, 0f, 125f), new Vector3(154f, 0f, 126f), new Vector3(152f, 0f, 108f), new Vector3(158f, 0f, 108f),
                new Vector3(164f, 0f, 108f), new Vector3(136f, 0f, 112f), new Vector3(139f, 0f, 113f), new Vector3(130f, 0f, 108f) };
            var attempts = 0;
            while (placed.Count < 24 && attempts++ < 4000)
            {
                var p = new Vector3(rng.Range(102f, 170f), 0f, rng.Range(92f, 150f));
                if (p.z > 106.5f && p.z < 112.5f && p.x > 137f && p.x < 173f) continue; // irrigation wall
                if (p.x > 140f && p.x < 160f && p.z > 124f && p.z < 140f) continue; // pump house
                if (p.x > 164f && p.z > 128f) continue; // plaza edge
                if (NearPolyline(p, route, 3.2f)) continue;
                var clear = true;
                foreach (var k in keep)
                    if (Flat(p, k) < 3f) clear = false;
                foreach (var q in placed)
                    if (Flat(p, q) < 6.2f) clear = false;
                if (!clear) continue;
                placed.Add(p);
            }
            var olives = new[] { "OliveTreeA", "OliveTreeB", "OliveTreeC" };
            var poms = new[] { "PomegranateTreeA", "PomegranateTreeB", "PomegranateTreeC" };
            for (var i = 0; i < placed.Count; i++)
            {
                var p = placed[i];
                var key = i % 3 == 2 ? poms[i % poms.Length] : olives[i % olives.Length];
                p.y = ApproachHeightField.Height(p.x, p.z);
                TownKit.Prop(ctx, key, p, rng.Range(0f, 360f), rng.Range(0.85f, 1.15f));
            }
            ctx.Info("Grove: " + placed.Count + " trees.");
            // Dry grass and fallen branches along the grove edges (no colliders).
            for (var i = 0; i < 26; i++)
            {
                var x = rng.Range(100f, 176f);
                var z = rng.Chance(0.5f) ? rng.Range(88f, 94f) : rng.Range(146f, 150f);
                LevelProps.Place("DryGrass", ctx.RootAt(new Vector3(x, 0f, z)), new Vector3(x, ApproachHeightField.Height(x, z), z), rng.Range(0f, 360f), rng.Range(0.8f, 1.4f), null, true, false);
            }
            for (var i = 0; i < 6; i++)
            {
                var p = placed.Count > 0 ? placed[rng.Range(0, placed.Count)] + new Vector3(rng.Jitter(2.5f), 0f, rng.Jitter(2.5f)) : new Vector3(130f, 0f, 120f);
                LevelProps.Place(i % 2 == 0 ? "DryBranchesA" : "DryBranchesB", ctx.RootAt(p), new Vector3(p.x, ApproachHeightField.Height(p.x, p.z), p.z), rng.Range(0f, 360f), 1f, null, true, false);
            }
        }

        private static void Rocks(SectionBuildContext ctx)
        {
            // The mantle boulder in the wadi (B1.T.Boulder): its top about 1.9 m above the wadi floor.
            TownKit.Prop(ctx, "Boulder", new Vector3(98.6f, ApproachHeightField.Height(98.6f, 82.4f) - 0.2f, 82.4f), 35f);
            // Basalt outcrops on the shoulder (cover around the spawn), kept off the spawn pad and the footpath.
            var outcrops = new[]
            {
                new Vector4(30f, 0f, 44f, 120f), new Vector4(55f, 0f, 20f, 40f), new Vector4(22f, 0f, 14f, 200f), new Vector4(64f, 0f, 30f, 300f),
                new Vector4(12f, 0f, 40f, 80f), new Vector4(48f, 0f, 52f, 10f),
            };
            foreach (var o in outcrops)
                TownKit.Prop(ctx, "RockFace", new Vector3(o.x, ApproachHeightField.Height(o.x, o.z) - 0.6f, o.z), o.w, 0.8f);
            // Wadi bank rocks (3-6 m off the floor line), scree pebbles in the chute (decor).
            var rng = new DetRandom("S1.Rocks");
            var wadi = ApproachHeightField.Wadi;
            for (var i = 0; i < wadi.Length - 1; i++)
            for (var k = 0; k < 2; k++)
            {
                var a = wadi[i];
                var b = wadi[i + 1];
                var t = rng.Value01();
                var p = Vector3.Lerp(a, b, t);
                var side = Vector3.Cross(Vector3.up, (b - a).normalized) * ((k == 0 ? 1f : -1f) * rng.Range(5.5f, 8f));
                p += side;
                p.y = ApproachHeightField.Height(p.x, p.z) - 0.3f;
                TownKit.Prop(ctx, "Rock07", p, rng.Range(0f, 360f), rng.Range(6f, 12f));
            }
            for (var i = 0; i < 18; i++)
            {
                var p = Vector3.Lerp(new Vector3(82f, 0f, 62f), new Vector3(90f, 0f, 72f), rng.Value01()) + new Vector3(rng.Jitter(1.8f), 0f, rng.Jitter(1.8f));
                p.y = ApproachHeightField.Height(p.x, p.z);
                LevelProps.Place("Rock09", ctx.RootAt(p), p, rng.Range(0f, 360f), rng.Range(2f, 4f), null, true, false);
            }
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static bool NearPolyline(Vector3 p, Vector3[] pts, float d)
        {
            if (pts == null) return false;
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var c = MathX.ClosestPointOnSegment(new Vector3(pts[i].x, 0f, pts[i].z), new Vector3(pts[i + 1].x, 0f, pts[i + 1].z), new Vector3(p.x, 0f, p.z), out _);
                if (Flat(c, p) < d) return true;
            }
            return false;
        }
    }
}
