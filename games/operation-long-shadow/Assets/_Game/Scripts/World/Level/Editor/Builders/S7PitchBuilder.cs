using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Vehicles;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S7_Pitch (GDD §6.3 Map B, §6.5 A16, §7.10): the ramp down from the postern, the football pitch x 312–382, z 392–418
    /// at −1 with salt crust, rusting goalposts, the bleacher x 328–348, z 392–397, the groundsman's shed (BLD-14), the ruined
    /// pump shed west of the pitch (SC_B10_W), the Customs Road, the salt pan beyond the OOB lines, the LZ pad and the two
    /// Beat 10 trucks on their splines (B10.Truck1/2).
    /// </summary>
    public sealed class S7PitchBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.Pitch;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            const float y = WorldConst.PitchY;
            // Ground: the pitch area at -1, the ramp from the postern (y 0 at z 382.2 to -1 at z 391), salt pan around.
            TownKit.Ground(ctx, new AreaRect(290f, 391f, 388f, 424f), y, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(290f, 382.2f, 342f, 391f), y, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(350f, 382.2f, 388f, 391f), y, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(312f, 392f, 382f, 418f), y + 0.01f, KitSlots.MudCracked, SurfaceType.SaltCrust, false);
            TownKit.Ground(ctx, new AreaRect(394f, 380f, 402f, 490f), y + 0.02f, KitSlots.Road, SurfaceType.Gravel, false);
            TownKit.Ground(ctx, new AreaRect(388f, 380f, 450f, 490f), WorldConst.SaltPanY + 0.3f, KitSlots.SaltCrust, SurfaceType.SaltCrust);
            TownKit.Ground(ctx, new AreaRect(290f, 424f, 388f, 490f), WorldConst.SaltPanY + 0.3f, KitSlots.SaltCrust, SurfaceType.SaltCrust);
            Ramp(ctx);
            TownKit.Buildings(ctx);
            TownKit.Lamps(ctx);

            var mb = ctx.Batch(TownKit.BDressing, new Vector3(347f, y, 405f), WorldConst.LightmapScaleHouse);
            KitIndustrial.Bleacher(mb, new AreaRect(328f, 392f, 348f, 397f), y);
            KitIndustrial.Goalpost(mb, new Vector3(314f, y, 405f), 90f);
            KitIndustrial.Goalpost(mb, new Vector3(380f, y, 405f), 270f);
            WestShed(ctx);
            // LZ pad: a ring of whitewashed stones around (350, -1, 406).
            var rng = new DetRandom("S7.Pad");
            for (var i = 0; i < 16; i++)
            {
                var p = new Vector3(350f, y, 406f) + MathX.DirectionFromBearing(i * 22.5f) * 6f;
                LevelProps.Place("Rock07", ctx.RootAt(p), p, rng.Range(0f, 360f), rng.Range(1.6f, 2.2f), "PadStone" + i, true, false);
            }
            // Dressing: tyres, a rusted car, barrels by the shed, dry grass on the margins.
            TownKit.Prop(ctx, "CoveredCar", new Vector3(300.6f, y, 410.4f), 75f);
            TownKit.Prop(ctx, "OldTyre", new Vector3(361.6f, y, 397.4f), 0f);
            TownKit.Prop(ctx, "OldTyre", new Vector3(362.2f, y, 398.2f), 30f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(371f, y, 393f), 0f);
            TownKit.Prop(ctx, "Barrel03", new Vector3(371.6f, y, 393.8f), 0f);
            for (var i = 0; i < 20; i++)
            {
                var p = new Vector3(rng.Range(292f, 386f), y, rng.Chance(0.5f) ? rng.Range(383f, 390f) : rng.Range(419f, 423f));
                LevelProps.Place("DryGrass", ctx.RootAt(p), p, rng.Range(0f, 360f), rng.Range(0.8f, 1.3f), null, true, false);
            }
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            var t1 = VehicleBuilder.Truck(ctx, "Truck_B10_1", "CargoTruck", out var s1);
            VehicleBuilder.Attach(ctx, VehicleRoutes.Truck1, t1.transform, s1, false, true, false);
            var t2 = VehicleBuilder.Truck(ctx, "Truck_B10_2", "CargoTruckTarpOff", out var s2);
            VehicleBuilder.Attach(ctx, VehicleRoutes.Truck2, t2.transform, s2, false, true, false);
        }

        private static void Ramp(SectionBuildContext ctx)
        {
            // From the postern (z 382.2, y 0) down to the pitch (z 391, y -1): 6.5° concrete ramp x 342–350.
            var mb = ctx.Batch(TownKit.BGround, new Vector3(346f, -0.5f, 386.6f));
            mb.Slot(KitSlots.ConcreteFloor);
            var a = new Vector3(342f, 0f, 382.2f);
            var b = new Vector3(342f, WorldConst.PitchY, 391f);
            var c = new Vector3(350f, WorldConst.PitchY, 391f);
            var d = new Vector3(350f, 0f, 382.2f);
            mb.AddQuad(a, b, c, d);
            var mid = (a + c) * 0.5f;
            var len = Vector3.Distance(a, b);
            mb.Push(mid, Quaternion.LookRotation((b - a).normalized, Vector3.up));
            mb.AddCollider(new Vector3(0f, -0.5f, 0f), new Vector3(8f, 1f, len), SurfaceType.Concrete);
            mb.Pop();
        }

        private static void WestShed(SectionBuildContext ctx)
        {
            // Ruined pump shed x 290–298, z 383–394: block walls 2.6 m, roof half gone, a gap in the east wall z 386–390
            // (SC_B10_W's door is outside it at (300, -1, 388)).
            const float y = WorldConst.PitchY;
            TownKit.Wall(ctx, new Vector3(290f, y, 383f), new Vector3(298f, y, 383f), 2.6f, 0.3f, KitSlots.Concrete, SurfaceType.Concrete);
            TownKit.Wall(ctx, new Vector3(290f, y, 394f), new Vector3(298f, y, 394f), 2.2f, 0.3f, KitSlots.Concrete, SurfaceType.Concrete);
            TownKit.Wall(ctx, new Vector3(290f, y, 383f), new Vector3(290f, y, 394f), 2.6f, 0.3f, KitSlots.Concrete, SurfaceType.Concrete);
            TownKit.Wall(ctx, new Vector3(298f, y, 383f), new Vector3(298f, y, 385.8f), 2.6f, 0.3f, KitSlots.Concrete, SurfaceType.Concrete);
            TownKit.Wall(ctx, new Vector3(298f, y, 390.4f), new Vector3(298f, y, 394f), 1.8f, 0.3f, KitSlots.Concrete, SurfaceType.Concrete);
            var mb = ctx.Batch(TownKit.BDressing, new Vector3(294f, y, 388f));
            mb.Slot(KitSlots.CorrugatedWorn);
            mb.AddQuadDoubleSided(new Vector3(290f, y + 2.7f, 383f), new Vector3(290f, y + 2.7f, 388.5f), new Vector3(298f, y + 2.5f, 388.5f), new Vector3(298f, y + 2.5f, 383f));
            mb.AddCollider(new Vector3(294f, y + 2.65f, 385.75f), new Vector3(8f, 0.1f, 5.5f), SurfaceType.Metal);
            TownKit.Prop(ctx, "DieselPump", new Vector3(292.2f, y, 391.6f), 90f);
            TownKit.Prop(ctx, "Jerrycan", new Vector3(296.6f, y, 392.8f), 15f);
        }
    }
}
