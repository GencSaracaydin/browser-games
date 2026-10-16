using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S3_Square (GDD §6.5 A6–A8, §7.4): the market arch (passage x 182–188 at z 249.6–254), Clock Square x 182–252,
    /// z 252–300 with the civic clock tower (220, 0, 275; square, flat-topped, faces at 6:40), the dry fountain, the
    /// arcaded south and east ranges, the east street, the walled side alley and service yard, the burning technical
    /// (232, 0, 286, h 234) and Cinema Zenith (lobby, auditorium with seats and shrapnel holes, balcony, upper foyer, marquee
    /// walkway, crate stack) with its baked tungsten practicals.
    /// </summary>
    public sealed class S3SquareBuilder : SectionBuilderBase
    {
        /// <summary>Fountain centre: 1.6 m north of GDD (205, 0, 266) so Hollis's Q2 slot (205, 0, 265) crouches behind its rim.</summary>
        public static readonly Vector3 FountainCenter = new Vector3(205f, 0f, 267.6f);

        /// <inheritdoc/>
        public override SectionId Section => SectionId.Square;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            Ground(ctx);
            TownKit.Buildings(ctx);
            TownKit.Lamps(ctx);
            BuildLandmarks(ctx);
            Cinema(ctx);
            Dressing(ctx);
        }

        private static void Ground(SectionBuildContext ctx)
        {
            TownKit.GroundCollider(ctx, new AreaRect(172f, 244f, 262f, 334f), 0f, SurfaceType.Stone);
            TownKit.Ground(ctx, new AreaRect(182f, 254f, 252f, 301f), 0f, KitSlots.Paving, SurfaceType.Stone, false);
            // South of z 254 west of x 199 the market's ground (S2, or its S9 proxy once S2 unloads) shows; only the collider overlaps.
            TownKit.Ground(ctx, new AreaRect(172f, 254f, 182f, 301f), 0f, KitSlots.Dirt, SurfaceType.Gravel, false);
            TownKit.Ground(ctx, new AreaRect(199f, 244f, 262f, 254f), 0f, KitSlots.Dirt, SurfaceType.Gravel, false);
            TownKit.Ground(ctx, new AreaRect(252f, 254f, 262f, 301f), 0f, KitSlots.Road, SurfaceType.Gravel, false);
            TownKit.Ground(ctx, new AreaRect(172f, 301f, 186f, 334f), 0f, KitSlots.Dirt, SurfaceType.Gravel, false);
            TownKit.Ground(ctx, new AreaRect(231f, 301f, 262f, 334f), 0f, KitSlots.Dirt, SurfaceType.Gravel, false);
            // Kerbs around the square's paving.
            TownKit.Kerb(ctx, new Vector3(182f, 0f, 254f), new Vector3(202f, 0f, 254f));
            TownKit.Kerb(ctx, new Vector3(252f, 0f, 260f), new Vector3(252f, 0f, 300f));
        }

        private static void BuildLandmarks(SectionBuildContext ctx)
        {
            var arch = new MeshBuilder();
            Landmarks.MarketArch(arch, 180.2f, 197f, 249.6f, 254f, 182f, 188f, 2);
            ctx.Mesh("MarketArch", ctx.RootAt(new Vector3(185f, 0f, 252f)), Vector3.zero, Quaternion.identity, arch, WorldConst.LightmapScaleHero * 0.5f);

            var tower = new MeshBuilder();
            Landmarks.ClockTower(tower, new Vector3(220f, 0f, 275f), 6f, 22f);
            ctx.Mesh("ClockTower", ctx.RootAt(new Vector3(220f, 0f, 275f)), Vector3.zero, Quaternion.identity, tower, WorldConst.LightmapScaleHero * 0.5f);
            TownKit.Prop(ctx, "PAHorn", new Vector3(220f, 22.9f, 272.4f), 180f);
            TownKit.Prop(ctx, "PAHorn", new Vector3(222.6f, 22.9f, 275f), 90f);

            KitStreet.Fountain(ctx.Batch(TownKit.BStreet, FountainCenter), FountainCenter, 2.0f);

            // The side alley's east wall (x 180–180.6) between the market arch and the service yard: the square is walled
            // off from the alley (lamp brackets LAMP.09/10 hang on it).
            TownKit.Wall(ctx, new Vector3(180.3f, 0f, 254f), new Vector3(180.3f, 0f, 300f), 4.6f, 0.6f, KitSlots.Plaster(3), SurfaceType.Plaster);
            // Service-yard south wall from the alley wall to the cinema's west facade.
            TownKit.Wall(ctx, new Vector3(180.3f, 0f, 300f), new Vector3(186f, 0f, 300f), 3.2f, 0.45f, KitSlots.Plaster(3), SurfaceType.Plaster);
        }

        private static void Cinema(SectionBuildContext ctx)
        {
            var shell = new MeshBuilder();
            var interior = new MeshBuilder();
            CinemaGeometry.Build(shell, interior, ctx.Anchors, 1);
            var root = ctx.BlockRoot("S3.Cinema");
            ctx.Mesh("Cinema_Shell", root, Vector3.zero, Quaternion.identity, shell, WorldConst.LightmapScaleHero * 0.5f);
            ctx.Mesh("Cinema_Interior", root, Vector3.zero, Quaternion.identity, interior, WorldConst.LightmapScaleHero * 0.75f);
            // Baked tungsten practicals (WP10 Look (7)).
            foreach (var kv in ctx.Anchors.WithPrefix("Sconce.")) TownKit.BakedLight(ctx, "CinemaTungsten", "Sconce_" + kv.Key.Substring(7), kv.Value.position, 6f, 1.2f);
            if (ctx.Anchors.TryGet("Lobby.Light", out var lobby)) TownKit.BakedLight(ctx, "CinemaTungsten", "Lobby", lobby.position, 9f, 2.4f);
            if (ctx.Anchors.TryGet("Foyer.Light", out var foyer)) TownKit.BakedLight(ctx, "CinemaTungsten", "Foyer", foyer.position, 10f, 2.0f);
            if (ctx.Anchors.TryGet("ExitSign", out var exit)) TownKit.BakedLight(ctx, "CinemaTungsten", "ExitGlow", exit.position + exit.rotation * Vector3.forward * 0.3f, 4f, 0.6f, 6500f);
            // Lobby dressing: a broken chair, film cans, a poster stand.
            if (ctx.Anchors.TryGet("Lobby.Posters", out var posters)) TownKit.Prop(ctx, "WoodenChair", posters.position + Vector3.right * 1.2f, 140f);
            TownKit.Prop(ctx, "FilmCanClosed", new Vector3(196.6f, 0f, 309.6f), 30f);
            TownKit.Prop(ctx, "FilmCan", new Vector3(196.2f, 0f, 309.9f), 80f);
        }

        private static void Dressing(SectionBuildContext ctx)
        {
            // The technical (E4-01's DShK pickup) where it burns (B4.FireProp on its bed; Look's fire binds there).
            TownKit.Prop(ctx, "Technical", new Vector3(232f, 0f, 286f), 234f);
            // Square cover for the E4 positions (GDD §7.4): a covered car, crates, barrels, a fallen stall.
            TownKit.Prop(ctx, "CoveredCar", new Vector3(227.4f, 0f, 263.2f), 118f);
            TownKit.Prop(ctx, "MarketStall", new Vector3(241.2f, 0f, 269.4f), 200f);
            TownKit.Prop(ctx, "WoodenCrate", new Vector3(207.2f, 0f, 287.6f), 12f);
            TownKit.Prop(ctx, "WoodenCrateLong", new Vector3(205.6f, 0f, 288.2f), 95f);
            TownKit.Prop(ctx, "BarrelGreen", new Vector3(200.6f, 0f, 292.6f), 0f);
            TownKit.Prop(ctx, "Barrel03", new Vector3(201.4f, 0f, 293.3f), 0f);
            TownKit.Prop(ctx, "RoadBarrier", new Vector3(214f, 0f, 262.2f), 30f);
            TownKit.Prop(ctx, "Sandbag", new Vector3(242.6f, 0f, 261.4f), 0f);
            // Side alley and service yard: trash, crates, cables, a parked car by the side door.
            TownKit.Prop(ctx, "TrashCanRust", new Vector3(174.6f, 0f, 258f), 10f);
            TownKit.Prop(ctx, "Trashbag", new Vector3(174.8f, 0f, 259.2f), 40f);
            TownKit.Prop(ctx, "WoodenCrate", new Vector3(179.5f, 0f, 281.2f), 92f);
            TownKit.Prop(ctx, "OldTyre", new Vector3(174.6f, 0f, 289.4f), 20f);
            TownKit.Prop(ctx, "CoveredCar", new Vector3(177.8f, 0f, 311.6f), 2f);
            TownKit.Prop(ctx, "MilitaryCrate", new Vector3(183.4f, 0f, 302.2f), 80f);
            TownKit.Prop(ctx, "Jerrycan", new Vector3(184.6f, 0f, 302.4f), 15f);
            var rng = new DetRandom("S3.Cables");
            for (var z = 252f; z < 300f; z += rng.Range(10f, 15f))
                TownKit.Cable(ctx, new Vector3(174.05f, rng.Range(5.2f, 6f), z), new Vector3(180f, rng.Range(4.4f, 4.6f), z + rng.Jitter(1.5f)), rng.Range(0.2f, 0.4f));
        }
    }
}
