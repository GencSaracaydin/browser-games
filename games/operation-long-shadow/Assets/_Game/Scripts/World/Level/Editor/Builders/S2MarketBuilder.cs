using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S2_Market (GDD §6.5 A5, §7.3): the Long Market street x 181–189 from the Weighbridge (z 170) to the market arch
    /// (z 252), its west (≤ 7 m) and east (9–11 m) rows, the café, Tinsmiths' Lane x 166–171, the cross-passages, Sunset Lane
    /// at azimuth 258° (nothing built in its line), stalls placed as cover in front of the node-set slots, the eight
    /// awnings (B3.AW01–08), six fruit crates (B3.Fruit1–6), the mantle crates (B3.Mantle), overhead cables and laundry,
    /// the SC_B3_ROOF stair hut on the W2b terrace, lamps.
    /// </summary>
    public sealed class S2MarketBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.Market;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            GroundAndStreets(ctx);
            TownKit.Buildings(ctx);
            TownKit.Lamps(ctx);
            Stalls(ctx);
            Overhead(ctx);
            RoofHut(ctx);
            Dressing(ctx);
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            // East-side balcony awnings (z 214–230 at +4.5): fixed on the x 189 facade, projecting 2 m over the street.
            for (var i = 1; i <= 5; i++)
            {
                var id = "B3.AW0" + i;
                if (!ctx.Markers.TryGetValue(id, out var m)) continue;
                var z = m.transform.position.z;
                TownKit.Awning(ctx, id, new Vector3(189f, 5.6f, z + 1.5f), new Vector3(189f, 5.6f, z - 1.5f), new Vector3(-2.1f, 0f, 0f), 0.7f, i);
            }
            // Street-spanning awnings (z 230–240): cloth from facade to facade at +4.5 to +4.9.
            for (var i = 6; i <= 8; i++)
            {
                var id = "B3.AW0" + i;
                if (!ctx.Markers.TryGetValue(id, out var m)) continue;
                var z = m.transform.position.z;
                TownKit.Awning(ctx, id, new Vector3(181.05f, 4.9f, z - 1.4f), new Vector3(188.95f, 4.9f, z - 1.4f), new Vector3(0f, 0f, 2.8f), 0.4f, i);
                // North of z 233.2 the west side is the Sunset Lane passage: the cloth is tied to timber poles there.
                if (z + 1.4f > 233.2f)
                {
                    var mb = ctx.Batch(TownKit.BDressing, new Vector3(181.3f, 0f, z));
                    mb.Slot(KitSlots.WoodDark);
                    foreach (var dz in new[] { -1.4f, 1.4f })
                        if (z + dz > 233.2f)
                        {
                            mb.AddCylinder(new Vector3(181.3f, 0.03f, z + dz), 0.06f, 4.95f, 6, true, false);
                            mb.AddCollider(new Vector3(181.3f, 2.5f, z + dz), new Vector3(0.12f, 4.95f, 0.12f), SurfaceType.Wood);
                        }
                }
            }
            // Fruit crates: oranges and pomegranates alternate (GDD §7.3 "oranges and pomegranates").
            for (var i = 1; i <= 6; i++) TownKit.FruitCrate(ctx, "B3.Fruit" + i, i % 2 == 0, i % 2 == 0 ? 90f : 270f);
        }

        private static void GroundAndStreets(SectionBuildContext ctx)
        {
            TownKit.Ground(ctx, new AreaRect(164f, 170f, 199f, 254f), 0f, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(118f, 214f, 164f, 244f), 0f, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(181f, 170f, 189f, 249.6f), 0.03f, KitSlots.Paving, SurfaceType.Stone, false);
            TownKit.Ground(ctx, new AreaRect(166f, 195f, 171f, 240f), 0.01f, KitSlots.Paving, SurfaceType.Stone, false);
            TownKit.Ground(ctx, new AreaRect(171f, 195f, 181f, 200f), 0.03f, KitSlots.Paving, SurfaceType.Stone, false);
            TownKit.Ground(ctx, new AreaRect(171f, 233.2f, 181f, 241f), 0.03f, KitSlots.Paving, SurfaceType.Stone, false);
            // Sunset Lane: a 5 m strip along azimuth 258° from x 171 to the town edge (120, 225) (GDD A5).
            var mouth = SunGeometry.LaneMouth;
            var end = SunGeometry.LaneEnd;
            var dir = (end - mouth).normalized;
            var start = mouth + dir * ((171f - mouth.x) / dir.x);
            var side = Vector3.Cross(Vector3.up, dir) * (SunGeometry.LaneWidth * 0.5f);
            var mb = ctx.Batch(TownKit.BGround, start);
            mb.Slot(KitSlots.Paving);
            var y = Vector3.up * 0.02f;
            // Winding: corners ordered so the face points up (clockwise seen from above).
            var a = start - side + y;
            var b = start + side + y;
            var c = end + side + y;
            var d = end - side + y;
            if (Vector3.Cross(b - a, c - a).y < 0f) mb.AddQuad(a, d, c, b);
            else mb.AddQuad(a, b, c, d);
        }

        private static void Stalls(SectionBuildContext ctx)
        {
            // West stalls just north of the west node-set slots (cover towards the enemies to the north), east likewise.
            float[] west = { 181.7f, 187.7f, 202.7f, 207.7f, 225.7f, 233.7f };
            float[] east = { 185.7f, 194.3f, 205.7f, 220.6f, 229.7f };
            foreach (var z in west) TownKit.Prop(ctx, "MarketStall", new Vector3(181.95f, 0.03f, z), 90f);
            foreach (var z in east) TownKit.Prop(ctx, "MarketStall", new Vector3(188.05f, 0.03f, z), 270f);
            // B3.Mantle (184, 0, 226): a waist-high crate stack to vault.
            TownKit.Prop(ctx, "WoodenCrateLong", new Vector3(184.4f, 0.03f, 226.6f), 90f);
            TownKit.Prop(ctx, "WoodenCrate", new Vector3(184.4f, 0.4f, 226.6f), 85f);
            TownKit.Prop(ctx, "WickerBasket", new Vector3(183.2f, 0.03f, 226.2f), 20f);
            // Baskets and crates under the stalls.
            var rng = new DetRandom("S2.Stalls");
            foreach (var z in west)
            {
                TownKit.Prop(ctx, rng.Chance(0.5f) ? "WickerBasket" : "WoodenCrate", new Vector3(181.5f, 0.03f, z + rng.Jitter(0.8f)), rng.Range(0f, 360f));
            }
        }

        private static void Overhead(SectionBuildContext ctx)
        {
            var rng = new DetRandom("S2.Overhead");
            for (var z = 176f; z < 250f; z += rng.Range(9f, 13f))
                TownKit.Cable(ctx, new Vector3(181.05f, rng.Range(6.2f, 6.8f), z), new Vector3(188.95f, rng.Range(6.8f, 8.6f), z + rng.Jitter(2f)), rng.Range(0.25f, 0.6f));
            var dressing = ctx.Batch(TownKit.BDressing, new Vector3(185f, 0f, 200f));
            KitRoofProps.LaundryLine(dressing, new Vector3(181.05f, 6.0f, 199.2f), new Vector3(188.95f, 6.4f, 199.8f), 11, false);
            KitRoofProps.LaundryLine(ctx.Batch(TownKit.BDressing, new Vector3(185f, 0f, 243f)), new Vector3(181.05f, 6.1f, 243.4f), new Vector3(188.95f, 6.5f, 242.6f), 12, false);
            KitRoofProps.LaundryLine(ctx.Batch(TownKit.BDressing, new Vector3(168f, 0f, 220f)), new Vector3(166.05f, 4.4f, 218f), new Vector3(170.95f, 4.6f, 218.6f), 13, false);
        }

        private static void RoofHut(SectionBuildContext ctx)
        {
            // SC_B3_ROOF: a hollow stair hut on the W2b terrace (+7.8), door east at x 175.2 (ClosetLayout), so spawns stand inside.
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(174f, 7.8f, 214f), WorldConst.LightmapScaleHouse);
            const float y = 7.8f;
            const float x0 = 173.0f, x1 = 175.2f, z0 = 212.9f, z1 = 215.1f, h = 2.3f, t = 0.18f;
            mb.Slot(KitSlots.Plaster(4));
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, y + h * 0.5f, z0 + t * 0.5f), new Vector3(x1 - x0, h, t));
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, y + h * 0.5f, z1 - t * 0.5f), new Vector3(x1 - x0, h, t));
            mb.AddBox(new Vector3(x0 + t * 0.5f, y + h * 0.5f, 214f), new Vector3(t, h, z1 - z0));
            // East wall with a 0.95 m door gap at z 214.
            mb.AddBox(new Vector3(x1 - t * 0.5f, y + h * 0.5f, (z0 + 213.5f) * 0.5f), new Vector3(t, h, 213.5f - z0));
            mb.AddBox(new Vector3(x1 - t * 0.5f, y + h * 0.5f, (214.5f + z1) * 0.5f), new Vector3(t, h, z1 - 214.5f));
            mb.AddBox(new Vector3(x1 - t * 0.5f, y + 2.1f, 214f), new Vector3(t, 0.4f, 1f));
            mb.Slot(KitSlots.PlasterRough);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, y + h + 0.05f, 214f), new Vector3(x1 - x0 + 0.15f, 0.1f, z1 - z0 + 0.15f));
            mb.Slot(KitSlots.InteriorCard);
            mb.AddBox(new Vector3((x0 + x1) * 0.5f - 0.3f, y + 0.01f, 214f), new Vector3(1.0f, 0.02f, 1.6f), 4);
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f, y + h * 0.5f, z0 + t * 0.5f), new Vector3(x1 - x0, h, t), SurfaceType.Plaster);
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f, y + h * 0.5f, z1 - t * 0.5f), new Vector3(x1 - x0, h, t), SurfaceType.Plaster);
            mb.AddCollider(new Vector3(x0 + t * 0.5f, y + h * 0.5f, 214f), new Vector3(t, h, z1 - z0), SurfaceType.Plaster);
            mb.AddCollider(new Vector3(x1 - t * 0.5f, y + h * 0.5f, (z0 + 213.5f) * 0.5f), new Vector3(t, h, 213.5f - z0), SurfaceType.Plaster);
            mb.AddCollider(new Vector3(x1 - t * 0.5f, y + h * 0.5f, (214.5f + z1) * 0.5f), new Vector3(t, h, z1 - 214.5f), SurfaceType.Plaster);
            mb.AddCollider(new Vector3((x0 + x1) * 0.5f, y + h + 0.05f, 214f), new Vector3(x1 - x0, 0.1f, z1 - z0), SurfaceType.Plaster);
            // Roof clutter by the hut: a water tank and an antenna.
            KitRoofProps.WaterTank(mb, new Vector3(172.4f, y, 210.6f), 0.6f, 7);
            KitRoofProps.TvAntenna(mb, new Vector3(176.8f, y, 216.6f), 2.6f, 8);
        }

        private static void Dressing(SectionBuildContext ctx)
        {
            // Café arcade (BLD-04, x 178–181 under the upper floor): tables, chairs, a half-eaten meal, the cassette player.
            TownKit.Prop(ctx, "PaintedTable", new Vector3(179.4f, 0f, 178.2f), 0f);
            TownKit.Prop(ctx, "MealPlate", new Vector3(179.3f, 0.76f, 178.1f), 30f);
            TownKit.Prop(ctx, "Kettle", new Vector3(179.7f, 0.76f, 178.5f), 200f);
            TownKit.Prop(ctx, "WoodenChair", new Vector3(178.6f, 0f, 178.3f), 95f);
            TownKit.Prop(ctx, "WoodenChair", new Vector3(180.2f, 0f, 177.4f), 300f);
            TownKit.Prop(ctx, "PaintedTable", new Vector3(179.4f, 0f, 182.4f), 8f);
            TownKit.Prop(ctx, "CassettePlayer", new Vector3(179.2f, 0.76f, 182.5f), 160f);
            TownKit.Prop(ctx, "WoodenChair", new Vector3(178.6f, 0f, 182.6f), 80f);
            // Tinsmiths' Lane: crates and drums outside the workshops.
            TownKit.Prop(ctx, "WoodenCrate", new Vector3(170.3f, 0f, 203.4f), 90f);
            TownKit.Prop(ctx, "WoodenCrate", new Vector3(170.4f, 0f, 215.2f), 75f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(170.4f, 0f, 226.4f), 0f);
            TownKit.Prop(ctx, "Barrel03", new Vector3(166.7f, 0f, 211.6f), 0f);
            TownKit.Prop(ctx, "Trashbag", new Vector3(166.8f, 0f, 233.2f), 40f);
            // Sunset Lane: keep the line clear; a few low items against its walls only.
            TownKit.Prop(ctx, "WickerBasket", new Vector3(150f, 0f, 228.6f), 10f);
            TownKit.Prop(ctx, "Jerrycan", new Vector3(140.2f, 0f, 227.2f), 70f);
            // The market south end by the arch: barrels and a utility box.
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(181.6f, 0.03f, 172.2f), 0f);
            TownKit.Prop(ctx, "UtilityBox", new Vector3(188.7f, 1.2f, 174.4f), 270f);
        }
    }
}
