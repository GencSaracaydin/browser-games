using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using Unity.AI.Navigation;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S5_StationExt (GDD §6.3 Map B, §6.5 A11, §6.6 BLD-09–13, §7.6): the Relay Station compound — 4 m concrete walls with
    /// razor wire (x 300–392, z 290–382), the breach section (x 300, z 319–325, intact ↔ blown via <see cref="BreachWall"/>
    /// on B6.BreachWall with the S4-S5 NavMesh link), the gatehouse (BLD-09) in the south wall, the generator shed (BLD-11),
    /// the Relay Hall's Art Deco shell (BLD-10 exterior; interiors are S6), four flood towers under B6.F1–F4 (WP10 Look binds
    /// the heads), the mast fence, yard cover (cable drums at the Y2 slots, sandbags, a parked truck), the postern in the
    /// north wall and the tunnel shaft hatch behind it, the road outside the south wall.
    /// </summary>
    public sealed class S5StationExtBuilder : SectionBuilderBase
    {
        /// <summary>Tunnel shaft opening in the yard (RelayHallGeometry.ShaftX, z behind the postern).</summary>
        public static readonly AreaRect ShaftHole = new AreaRect(343.6f, 379.6f, 345.0f, 381.2f);

        /// <inheritdoc/>
        public override SectionId Section => SectionId.StationExt;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            Ground(ctx);
            TownKit.Buildings(ctx);
            TownKit.Lamps(ctx);
            Walls(ctx);
            var shell = new MeshBuilder();
            RelayHallGeometry.BuildShell(shell, ctx.Anchors);
            ctx.Mesh("RelayHall_Shell", ctx.RootAt(new Vector3(336f, 0f, 346f)), Vector3.zero, Quaternion.identity, shell, WorldConst.LightmapScaleHero * 0.5f);
            Floods(ctx);
            Yard(ctx);
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            if (!ctx.Markers.TryGetValue("B6.BreachWall", out var marker))
            {
                ctx.Warn("B6.BreachWall marker missing");
                return;
            }
            var t = marker.transform;
            var intactMb = new MeshBuilder();
            Landmarks.FreeWall(intactMb, new Vector3(300f, 0f, 319f), new Vector3(300f, 0f, 325f), 4f, 0.35f, KitSlots.Concrete, KitSlots.Concrete, SurfaceType.Concrete, false);
            var intact = ctx.Mesh("Breach_Intact", t, Vector3.zero, Quaternion.identity, intactMb, 0f, false);
            var blownMb = new MeshBuilder();
            Blown(blownMb);
            var blown = ctx.Mesh("Breach_Blown", t, Vector3.zero, Quaternion.identity, blownMb, 0f, false);
            LevelObjects.ClearStatic(intact);
            LevelObjects.ClearStatic(blown);
            blown.SetActive(false);
            marker.AddComponent<BreachWall>().Configure(new[] { intact }, new[] { blown }, new Behaviour[0]);
            SoftComponents.AddDestructible(marker, "B6.BreachWall", SoftDestructibleKind.Generic, 1000f, 0, 0f, t, true, new[] { intact }, new[] { blown }, null, null);
        }

        /// <inheritdoc/>
        public override void AfterNav(SectionBuildContext ctx, Dictionary<string, NavMeshLink> links)
        {
            if (!ctx.Markers.TryGetValue("B6.BreachWall", out var marker)) return;
            var bw = marker.GetComponent<BreachWall>();
            if (bw == null || !links.TryGetValue("S4-S5.Breach", out var link)) return;
            var intact = marker.transform.Find("Breach_Intact");
            var blown = marker.transform.Find("Breach_Blown");
            bw.Configure(new[] { intact != null ? intact.gameObject : null }, new[] { blown != null ? blown.gameObject : null }, new Behaviour[] { link });
            ctx.Info("BreachWall wired to the S4-S5.Breach link.");
        }

        private static void Ground(SectionBuildContext ctx)
        {
            // Yard (concrete apron near the Hall, packed dirt elsewhere) with a hole for the tunnel shaft behind the postern.
            var yard = new AreaRect(300f, 290f, 392f, 382f);
            foreach (var r in Interiors.SubtractRect(Rect.MinMaxRect(yard.X0, yard.Z0, yard.X1, yard.Z1), Rect.MinMaxRect(ShaftHole.X0, ShaftHole.Z0, ShaftHole.X1, ShaftHole.Z1)))
                TownKit.Ground(ctx, new AreaRect(r.xMin, r.yMin, r.xMax, r.yMax), 0f, KitSlots.Dirt, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(310f, 322f, 362f, 330f), 0.02f, KitSlots.ConcreteFloor, SurfaceType.Concrete, false);
            // Road outside the south wall (z 282–290) and the west strip by the breach (the alley is S4's).
            TownKit.Ground(ctx, new AreaRect(298f, 282f, 396f, 290f), 0f, KitSlots.Road, SurfaceType.Gravel);
            TownKit.Ground(ctx, new AreaRect(392f, 290f, 396f, 386f), 0f, KitSlots.Dirt, SurfaceType.Gravel);
            // Shaft curb and an open hatch lid.
            var mb = ctx.Batch(TownKit.BDressing, ShaftHole.Center);
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(new Vector3(ShaftHole.Center.x, 0.06f, ShaftHole.Z0 - 0.1f), new Vector3(ShaftHole.Width + 0.4f, 0.12f, 0.2f));
            mb.AddBox(new Vector3(ShaftHole.Center.x, 0.06f, ShaftHole.Z1 + 0.1f), new Vector3(ShaftHole.Width + 0.4f, 0.12f, 0.2f));
            mb.AddBox(new Vector3(ShaftHole.X0 - 0.1f, 0.06f, ShaftHole.Center.z), new Vector3(0.2f, 0.12f, ShaftHole.Depth));
            mb.AddBox(new Vector3(ShaftHole.X1 + 0.1f, 0.06f, ShaftHole.Center.z), new Vector3(0.2f, 0.12f, ShaftHole.Depth));
            mb.Slot(KitSlots.SteelPlate);
            mb.Push(new Vector3(ShaftHole.X1 + 0.2f, 0.12f, ShaftHole.Center.z), Quaternion.Euler(0f, 0f, 75f));
            mb.AddBox(new Vector3(-ShaftHole.Width * 0.5f, 0.02f, 0f), new Vector3(ShaftHole.Width, 0.04f, ShaftHole.Depth));
            mb.Pop();
        }

        private static void Walls(SectionBuildContext ctx)
        {
            void Run(Vector3 a, Vector3 b, bool outsideRight) => Landmarks.CompoundWall(ctx.Batch(TownKit.BWalls, (a + b) * 0.5f, WorldConst.LightmapScaleHouse), a, b, outsideRight, true);
            // South wall (z 290) either side of the gatehouse.
            Run(new Vector3(300f, 0f, 290f), new Vector3(340f, 0f, 290f), true);
            Run(new Vector3(352f, 0f, 290f), new Vector3(392f, 0f, 290f), true);
            // West wall (x 300) with the breach section z 319–325 (built on B6.BreachWall).
            Run(new Vector3(300f, 0f, 290f), new Vector3(300f, 0f, 319f), false);
            Run(new Vector3(300f, 0f, 325f), new Vector3(300f, 0f, 382f), false);
            // North wall (z 382) with the postern x 345.2–346.8.
            Run(new Vector3(300f, 0f, 382f), new Vector3(345.2f, 0f, 382f), false);
            Run(new Vector3(346.8f, 0f, 382f), new Vector3(392f, 0f, 382f), false);
            // Postern frame (lintel over the opening).
            var mb = ctx.Batch(TownKit.BWalls, new Vector3(346f, 0f, 382f));
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(new Vector3(346f, 3.2f, 382f), new Vector3(1.8f, 1.6f, 0.4f));
            mb.AddCollider(new Vector3(346f, 3.2f, 382f), new Vector3(1.8f, 1.6f, 0.4f), SurfaceType.Concrete);
            // East wall (x 392).
            Run(new Vector3(392f, 0f, 290f), new Vector3(392f, 0f, 382f), true);
            // Mast fence x 372–384, z 356–368 (the mast itself is in S9_TownFar, visible from every section).
            var fence = ctx.Batch(TownKit.BDressing, new Vector3(378f, 0f, 362f));
            KitIndustrial.ChainLinkFence(fence, new Vector3(372f, 0f, 356f), new Vector3(384f, 0f, 356f), 2.4f, KitSlots.SheetMetal);
            KitIndustrial.ChainLinkFence(fence, new Vector3(384f, 0f, 356f), new Vector3(384f, 0f, 368f), 2.4f, KitSlots.SheetMetal);
            KitIndustrial.ChainLinkFence(fence, new Vector3(384f, 0f, 368f), new Vector3(372f, 0f, 368f), 2.4f, KitSlots.SheetMetal);
            KitIndustrial.ChainLinkFence(fence, new Vector3(372f, 0f, 368f), new Vector3(372f, 0f, 356f), 2.4f, KitSlots.SheetMetal);
        }

        private static void Blown(MeshBuilder mb)
        {
            // Jagged stubs at both ends, a lintel remnant above the hole and a low rubble heap on both sides.
            mb.Slot(KitSlots.Concrete);
            mb.AddBox(new Vector3(300f, 1.6f, 319.35f), new Vector3(0.35f, 3.2f, 0.7f));
            mb.AddBox(new Vector3(300f, 1.9f, 324.6f), new Vector3(0.35f, 3.8f, 0.8f));
            mb.AddBox(new Vector3(300f, 3.55f, 322f), new Vector3(0.35f, 0.9f, 4.6f));
            mb.AddCollider(new Vector3(300f, 1.6f, 319.35f), new Vector3(0.35f, 3.2f, 0.7f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(300f, 1.9f, 324.6f), new Vector3(0.35f, 3.8f, 0.8f), SurfaceType.Concrete);
            mb.AddCollider(new Vector3(300f, 3.55f, 322f), new Vector3(0.35f, 0.9f, 4.6f), SurfaceType.Concrete);
            var rng = new DetRandom("S5.Rubble");
            for (var i = 0; i < 26; i++)
            {
                var p = new Vector3(300f + rng.Jitter(2.6f), 0f, 322f + rng.Jitter(2.4f));
                var s = new Vector3(rng.Range(0.2f, 0.7f), rng.Range(0.1f, 0.35f), rng.Range(0.2f, 0.6f));
                mb.Push(p + Vector3.up * s.y * 0.5f, Quaternion.Euler(rng.Jitter(20f), rng.Range(0f, 360f), rng.Jitter(20f)));
                mb.AddChamferBox(Vector3.zero, s, 0.04f, true, false);
                mb.Pop();
            }
            mb.Slot(KitSlots.Iron);
            for (var i = 0; i < 5; i++) mb.AddTube(new Vector3(300f, 3.1f, 320f + i * 0.9f), new Vector3(300f + rng.Jitter(0.4f), 2.4f + rng.Jitter(0.3f), 320.2f + i * 0.9f), 0.008f, 4);
        }

        private static void Floods(SectionBuildContext ctx)
        {
            var aim = new Vector3(345f, 0f, 325f);
            foreach (var id in new[] { "B6.F1", "B6.F2", "B6.F3", "B6.F4" })
            {
                if (!ctx.Markup.TryGet(id, out var row)) continue;
                var foot = new Vector3(row.Position.x, 0f, row.Position.z);
                KitIndustrial.FloodTower(ctx.Batch(TownKit.BDressing, foot, WorldConst.LightmapScaleHouse), foot, row.Position.y - 0.4f, MathX.Bearing(foot, aim));
            }
        }

        private static void Yard(SectionBuildContext ctx)
        {
            // Cable drums at the Y2 slots (cover towards the Hall, GDD §7.6) and sandbags for the E6 positions.
            TownKit.Prop(ctx, "CableDrum", new Vector3(331.1f, 0f, 315.4f), 30f);
            TownKit.Prop(ctx, "CableDrum", new Vector3(335.2f, 0f, 321.6f), 80f);
            TownKit.Prop(ctx, "CableDrum", new Vector3(327.1f, 0f, 311.4f), 10f);
            TownKit.SandbagWall(ctx, new Vector3(314.9f, 0f, 304.3f), new Vector3(315.3f, 0f, 306.3f), 4);
            TownKit.SandbagWall(ctx, new Vector3(349f, 0f, 311.1f), new Vector3(351f, 0f, 310.9f), 4);
            TownKit.SandbagWall(ctx, new Vector3(361f, 0f, 321f), new Vector3(361f, 0f, 323f), 4);
            // A parked truck, barrels and crates along the east side; the generators and drums in the shed.
            TownKit.Prop(ctx, "CargoTruckTarpOff", new Vector3(372f, 0f, 330f), 0f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(388.6f, 0f, 300.6f), 0f);
            TownKit.Prop(ctx, "Barrel03", new Vector3(389.4f, 0f, 301.2f), 0f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(388.8f, 0f, 301.9f), 0f);
            TownKit.Prop(ctx, "MilitaryCrate", new Vector3(357.6f, 0f, 338.4f), 90f);
            TownKit.Prop(ctx, "MilitaryCrate", new Vector3(357.6f, 0.5f, 338.4f), 92f);
            TownKit.Prop(ctx, "Generator", new Vector3(369.4f, 0f, 304.4f), 0f);
            TownKit.Prop(ctx, "Generator", new Vector3(374.4f, 0f, 304.2f), 0f);
            TownKit.Prop(ctx, "CableDrum", new Vector3(376.4f, 0f, 307.6f), 90f);
            TownKit.Prop(ctx, "PropaneTank", new Vector3(367.2f, 0f, 308.6f), 0f);
            // PA horn 3 on the gatehouse roof (the marker PA.Horn3 is in S9).
            TownKit.Prop(ctx, "PAHorn", new Vector3(346f, 5.6f, 294f), 0f);
            TownKit.Prop(ctx, "SecurityLight", new Vector3(346f, 3.4f, 298.2f), 0f);
        }
    }
}
