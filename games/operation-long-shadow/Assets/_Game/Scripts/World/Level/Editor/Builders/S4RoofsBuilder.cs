using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S4_Roofs (GDD §6.5 A9–A10, §7.5, §8.5): the Laundry Roofs R1 (+9) – R4 (+12) with the R1→R2 plank (z 318), the
    /// R2 tank stand (top +9) and plank to R3 (z 324), the R3→R4 steel stair (z 320), R4's pipe railing (west, south) and
    /// east parapet, the external switchback stair from (291, 12, 330) down to the west-wall alley (295, 0, 334), ladder L on
    /// R1's south face, water tanks as the Beat 5 cover (B5.WT1–4), the R3 MG nest (sandbag ring, empty tripod), laundry
    /// lines, antennas and the pigeon loft; the alley x 293–299, z 300–340 with its end clips.
    /// </summary>
    public sealed class S4RoofsBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.Roofs;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            TownKit.Ground(ctx, new AreaRect(292f, 296f, 300f, 346f), 0f, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Buildings(ctx);
            TownKit.Lamps(ctx);
            Links(ctx);
            R4(ctx);
            Switchback(ctx);
            RoofDressing(ctx);
            // Ladder L on R1's south face (B4.Ladder (236, 0, 301); Gate.LadderL clips its foot until E4 is dead).
            TownKit.Ladder(ctx, "L", new Vector3(236f, 0f, 302.85f), new Vector3(236f, 9.6f, 302.85f), Vector3.back, true);
            // Alley ends: hard clips (the alley is a dead end both ways; OOB volumes are not needed at ground level).
            TownKit.Clip(ctx, "AlleySouth", new Vector3(296f, 2f, 299.6f), new Vector3(8f, 4f, 0.4f));
            TownKit.Clip(ctx, "AlleyNorth", new Vector3(296f, 2f, 340.6f), new Vector3(8f, 4f, 0.4f));
        }

        private static void Links(SectionBuildContext ctx)
        {
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(255f, 8f, 320f), WorldConst.LightmapScaleHouse);
            // R1 → R2 plank (B5.Plank12, z 318, 1 m down).
            KitStructures.Plank(mb, new Vector3(245.7f, 9.06f, 318f), new Vector3(248.3f, 8.06f, 318f), 0.6f, KitSlots.Wood);
            // R2 tank stand (B5.TankStand (260, 8, 324), top +9): a steel platform with four legs and a step.
            mb.Slot(KitSlots.SteelFrame);
            mb.AddBox(new Vector3(259.7f, 8.94f, 324f), new Vector3(2.4f, 0.12f, 2.2f));
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
                mb.AddBox(new Vector3(259.7f + sx * 1.1f, 8.45f, 324f + sz * 1.0f), new Vector3(0.08f, 0.9f, 0.08f));
            mb.AddBox(new Vector3(258.2f, 8.46f, 324f), new Vector3(0.5f, 0.06f, 0.9f));
            mb.AddCollider(new Vector3(259.7f, 8.5f, 324f), new Vector3(2.4f, 1.0f, 2.2f), SurfaceType.Metal);
            mb.AddCollider(new Vector3(258.2f, 8.23f, 324f), new Vector3(0.5f, 0.46f, 0.9f), SurfaceType.Metal);
            // Stand → R3 plank (B5.Plank23, z 324, +9 to +10).
            KitStructures.Plank(mb, new Vector3(260.9f, 9.06f, 324f), new Vector3(263.3f, 10.06f, 324f), 0.6f, KitSlots.Wood);
            // R3 → R4 short steel stair (B5.Stair34, x 275–277, z 320, 2 m up).
            KitStructures.Stairs(mb, new Vector3(274.9f, 10f, 320f), Vector3.right, 1.0f, 2.0f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal);
            KitStructures.StairRail(mb, new Vector3(274.9f, 10f, 320.55f), new Vector3(278f, 12f, 320.55f), 0.95f, KitSlots.Iron);
        }

        private static void R4(SectionBuildContext ctx)
        {
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(285f, 12f, 320f), WorldConst.LightmapScaleHouse);
            const float y = 12f;
            // Pipe railing on the west and south edges (GDD A9), gap at z 319.3–320.7 for the stair from R3.
            KitStructures.PipeRailing(mb, new[] { new Vector3(277.15f, y, 335.85f), new Vector3(277.15f, y, 320.8f) }, 1.0f, KitSlots.Iron);
            KitStructures.PipeRailing(mb, new[] { new Vector3(277.15f, y, 319.2f), new Vector3(277.15f, y, 303.65f), new Vector3(291.85f, y, 303.65f) }, 1.0f, KitSlots.Iron);
            mb.AddCollider(new Vector3(277.15f, y + 0.5f, 328.3f), new Vector3(0.08f, 1f, 15.1f), SurfaceType.Metal);
            mb.AddCollider(new Vector3(277.15f, y + 0.5f, 311.4f), new Vector3(0.08f, 1f, 15.6f), SurfaceType.Metal);
            mb.AddCollider(new Vector3(284.5f, y + 0.5f, 303.65f), new Vector3(14.7f, 1f, 0.08f), SurfaceType.Metal);
            // East parapet (0.9 m solid) with the switchback stair gap at z 329.2–331.2.
            TownKit.Wall(ctx, new Vector3(291.85f, y, 303.5f), new Vector3(291.85f, y, 329.2f), 0.9f, 0.3f, KitSlots.Plaster(3), SurfaceType.Plaster);
            TownKit.Wall(ctx, new Vector3(291.85f, y, 331.2f), new Vector3(291.85f, y, 336f), 0.9f, 0.3f, KitSlots.Plaster(3), SurfaceType.Plaster);
            // North edge: a low parapet (the roof meets the north housing).
            TownKit.Wall(ctx, new Vector3(277f, y, 335.85f), new Vector3(292f, y, 335.85f), 0.45f, 0.3f, KitSlots.Plaster(3), SurfaceType.Plaster);
        }

        private static void Switchback(SectionBuildContext ctx)
        {
            // External steel switchback down R4's east face into the alley: four 3 m flights, landings at +9, +6, +3.
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(293.5f, 6f, 327f), WorldConst.LightmapScaleHouse);
            const float xa = 292.85f, xb = 293.95f, w = 1.05f;
            KitStructures.Stairs(mb, new Vector3(xa, 9f, 324.7f), Vector3.forward, w, 3f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal);
            KitStructures.Stairs(mb, new Vector3(xb, 6f, 329.5f), Vector3.back, w, 3f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal);
            KitStructures.Stairs(mb, new Vector3(xa, 3f, 324.7f), Vector3.forward, w, 3f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal);
            KitStructures.Stairs(mb, new Vector3(xb, 0f, 329.5f), Vector3.back, w, 3f, false, KitSlots.SteelFrame, KitSlots.SteelFrame, SurfaceType.Metal);
            void Landing(float y, float z0, float z1)
            {
                mb.Slot(KitSlots.SteelPlate);
                mb.AddBox(new Vector3(293.4f, y - 0.05f, (z0 + z1) * 0.5f), new Vector3(2.2f, 0.1f, z1 - z0));
                mb.AddCollider(new Vector3(293.4f, y - 0.05f, (z0 + z1) * 0.5f), new Vector3(2.2f, 0.1f, z1 - z0), SurfaceType.Metal);
                mb.Slot(KitSlots.SteelFrame);
                mb.AddBox(new Vector3(294.45f, y * 0.5f, z0 + 0.05f), new Vector3(0.1f, y, 0.1f));
                mb.AddBox(new Vector3(294.45f, y * 0.5f, z1 - 0.05f), new Vector3(0.1f, y, 0.1f));
            }
            Landing(12f, 329.4f, 331.2f);
            Landing(9f, 323.4f, 324.75f);
            Landing(6f, 329.45f, 330.8f);
            Landing(3f, 323.4f, 324.75f);
            // Outer railing along x 294.5 for the full height.
            for (var y = 3f; y <= 12f; y += 3f)
            {
                var z0 = Mathf.Approximately(y % 6f, 0f) ? 329.45f : 323.4f;
                KitStructures.PipeRailing(mb, new[] { new Vector3(294.5f, y, z0), new Vector3(294.5f, y, z0 + 1.6f) }, 1.0f, KitSlots.Iron, false);
            }
            KitStructures.StairRail(mb, new Vector3(294.5f, 0f, 329.5f), new Vector3(294.5f, 3f, 324.7f), 0.95f, KitSlots.Iron);
            KitStructures.StairRail(mb, new Vector3(294.5f, 6f, 329.5f), new Vector3(294.5f, 9f, 324.7f), 0.95f, KitSlots.Iron);
        }

        private static void RoofDressing(SectionBuildContext ctx)
        {
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(260f, 9f, 325f), WorldConst.LightmapScaleHouse);
            // Water tanks as the Beat 5 cover: each tank 1.1 m from its cover node towards the marksman (bearing 62°).
            var tanks = new[] { "B5.WT1", "B5.WT2", "B5.WT3", "B5.WT4" };
            for (var i = 0; i < tanks.Length; i++)
            {
                var row = ctx.Markup.TryGet(tanks[i], out var r) ? r.Position : Vector3.zero;
                if (row == Vector3.zero) continue;
                var p = row + MathX.DirectionFromBearing(MarkupDefaults.WaterTankCoverFacing(tanks[i])) * 1.15f;
                KitRoofProps.WaterTank(mb, p, 0.5f, 20 + i, 62f);
            }
            // The R3 MG nest: sandbag ring around an empty tripod (E5-03 stands in it), open to the east.
            KitRoofProps.SandbagRing(mb, new Vector3(272f, 10f, 329f), 1.35f, 0.8f, 80f, 31);
            TownKit.Prop(ctx, "MGTripod", new Vector3(272f, 10f, 329f), 260f);
            TownKit.Prop(ctx, "AmmoBox", new Vector3(272.6f, 10f, 328.4f), 30f);
            // Antennas, laundry lines and the pigeon loft (WP06 pigeons).
            KitRoofProps.TvAntenna(mb, new Vector3(234f, 9f, 333f), 3.2f, 41);
            KitRoofProps.TvAntenna(mb, new Vector3(258.5f, 8f, 306f), 2.8f, 42);
            KitRoofProps.TvAntenna(mb, new Vector3(273.6f, 10f, 305.4f), 3.4f, 43);
            KitRoofProps.TvAntenna(mb, new Vector3(289.6f, 12f, 306f), 3.0f, 44);
            KitRoofProps.LaundryLine(mb, new Vector3(234f, 9f, 308f), new Vector3(244.6f, 9f, 310f), 51, true);
            KitRoofProps.LaundryLine(mb, new Vector3(249f, 8f, 309f), new Vector3(259.6f, 8f, 312f), 52, true);
            KitRoofProps.LaundryLine(mb, new Vector3(264f, 10f, 332f), new Vector3(274f, 10f, 333.6f), 53, true);
            KitRoofProps.LaundryLine(mb, new Vector3(279f, 12f, 333.8f), new Vector3(286f, 12f, 334.4f), 54, true);
            KitRoofProps.PigeonLoft(mb, new Vector3(251f, 8f, 333.2f), 180f);
            KitRoofProps.StairHut(mb, new Vector3(240f, 9f, 333.6f), 180f, KitSlots.Plaster(2), 61);
            KitRoofProps.StairHut(mb, new Vector3(268.6f, 10f, 306f), 0f, KitSlots.Plaster(5), 62);
            // Cables between the roofs.
            TownKit.Cable(ctx, new Vector3(246f, 11.4f, 330f), new Vector3(248f, 10.6f, 330.6f), 0.15f);
            TownKit.Cable(ctx, new Vector3(261f, 10.8f, 310f), new Vector3(263f, 12.2f, 310.4f), 0.15f);
            TownKit.Cable(ctx, new Vector3(275f, 12.4f, 312f), new Vector3(277f, 14.4f, 312.3f), 0.15f);
        }
    }
}
