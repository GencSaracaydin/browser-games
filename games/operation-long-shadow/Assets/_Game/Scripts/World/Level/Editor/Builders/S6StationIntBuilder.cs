using LongShadow.Core;
using LongShadow.World.Level.Data;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Geometry;
using UnityEngine;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// S6_StationInt (GDD §6.4 Map C1–C3, §6.5 A12–A15, §7.7–7.9): the Relay Hall interiors — switch hall with teletype
    /// rows, clock plates and pendants, Harrou's office (ledgers on the desk), the archive, both basement stairs, the spiral
    /// stair, the screening room (projector, can rack, seats, the B8.Screen target), the basement (transmitter racks R1–R4
    /// as WP03 Destructibles behind the PlayerClip cage, tape deck, speaker, ON AIR lamp, safe, receiver desk, crates,
    /// cable trench and hatch) and the cable tunnel with its shaft ladder; baked tungsten practicals (basement and tunnel
    /// under "BasementTungsten") and the baked red "Emergency" lights (WP10 Look's SectionBaker contract).
    /// </summary>
    public sealed class S6StationIntBuilder : SectionBuilderBase
    {
        /// <inheritdoc/>
        public override SectionId Section => SectionId.StationInt;

        /// <inheritdoc/>
        public override void Geometry(SectionBuildContext ctx)
        {
            var mb = new MeshBuilder();
            RelayHallGeometry.BuildInterior(mb, ctx.Anchors);
            ctx.Mesh("RelayHall_Interior", ctx.Group(SectionBuildContext.GArchitecture), Vector3.zero, Quaternion.identity, mb, WorldConst.LightmapScaleHero * 0.75f);
            Lights(ctx);
            Props(ctx);
            if (ctx.Anchors.TryGet("Tunnel.LadderBottom", out var lb) && ctx.Anchors.TryGet("Tunnel.LadderTop", out var lt))
                TownKit.LadderVolume(ctx, "TunnelShaft", lb.position, lt.position, Vector3.back);
        }

        /// <inheritdoc/>
        public override void Wire(SectionBuildContext ctx)
        {
            // Transmitter racks: WP03 Destructible (kind Rack, 60 HP, invulnerable until WP07 releases it on D065) with a hit box.
            for (var i = 1; i <= 4; i++)
            {
                var id = "B9.R" + i;
                if (!ctx.Markers.TryGetValue(id, out var m)) continue;
                var col = m.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.1f, 0f);
                col.size = new Vector3(3.3f, 2.2f, 0.8f);
                m.AddComponent<SurfaceTag>().Surface = SurfaceType.Metal;
                SoftComponents.AddDestructible(m, id, SoftDestructibleKind.Rack, 60f, 0, 0.6f, m.transform, true, null, null, null, null);
            }
        }

        private static void Lights(SectionBuildContext ctx)
        {
            foreach (var kv in ctx.Anchors.WithPrefix("Pendant.")) TownKit.BakedLight(ctx, "HallTungsten", "Pendant_" + kv.Key.Substring(8), kv.Value.position, 9f, 1.6f);
            if (ctx.Anchors.TryGet("Office.Light", out var office)) TownKit.BakedLight(ctx, "HallTungsten", "Office", office.position, 7f, 1.4f);
            if (ctx.Anchors.TryGet("Landing.Light", out var landing)) TownKit.BakedLight(ctx, "HallTungsten", "Landing", landing.position, 7f, 1.2f);
            if (ctx.Anchors.TryGet("Screening.DeskLamp", out var desk)) TownKit.BakedLight(ctx, "HallTungsten", "ScreeningDesk", desk.position + Vector3.up * 0.3f, 4f, 0.7f);
            if (ctx.Anchors.TryGet("Basement.Light", out var basement))
            {
                TownKit.BakedLight(ctx, "BasementTungsten", "Basement_A", basement.position + new Vector3(-7f, 0f, 0f), 9f, 1.4f);
                TownKit.BakedLight(ctx, "BasementTungsten", "Basement_B", basement.position + new Vector3(7f, 0f, 0f), 9f, 1.4f);
                TownKit.BakedLight(ctx, "BasementTungsten", "Basement_C", basement.position + new Vector3(0f, 0f, 8f), 8f, 1.2f);
            }
            foreach (var kv in ctx.Anchors.WithPrefix("Tunnel.Lamp.")) TownKit.BakedLight(ctx, "BasementTungsten", "Tunnel_" + kv.Key.Substring(12), kv.Value.position, 5f, 0.8f);
            // Baked red emergency lights at B9.Emergency1/2 (Look bakes the EMERGENCY set with these and without BasementTungsten).
            if (ctx.Markup.TryGet("B9.Emergency1", out var e1)) Emergency(TownKit.BakedLight(ctx, "Emergency", "Emergency1", e1.Position, 10f, 2.2f));
            if (ctx.Markup.TryGet("B9.Emergency2", out var e2)) Emergency(TownKit.BakedLight(ctx, "Emergency", "Emergency2", e2.Position, 10f, 2.2f));
        }

        private static void Emergency(Light l)
        {
            l.useColorTemperature = false;
            l.color = new Color(1f, 0.12f, 0.06f);
        }

        private static void Props(SectionBuildContext ctx)
        {
            void At(string anchor, string key, float yawOffset = 0f, Vector3 offset = default)
            {
                if (!ctx.Anchors.TryGet(anchor, out var p)) return;
                LevelProps.Place(key, ctx.Group(SectionBuildContext.GProps), p.position + offset, p.rotation.eulerAngles.y + yawOffset, 1f, anchor);
            }
            foreach (var kv in ctx.Anchors.WithPrefix("Teletype.")) At(kv.Key, "Teletype");
            foreach (var kv in ctx.Anchors.WithPrefix("Archive.Shelf.")) At(kv.Key, "SteelShelves");
            foreach (var kv in ctx.Anchors.WithPrefix("Screening.Seat.")) At(kv.Key, "CinemaSeat");
            foreach (var kv in ctx.Anchors.WithPrefix("Crate.")) At(kv.Key, "MilitaryCrate");
            foreach (var kv in ctx.Anchors.WithPrefix("Fan.")) At(kv.Key, "CeilingFan");
            At("Office.Desk", "OfficeDesk");
            At("Office.Typewriter", "Typewriter");
            At("Office.Cabinet.0", "DrawerCabinet");
            At("Office.Cabinet.1", "DrawerCabinet");
            At("Screening.Projector", "Projector16mm");
            At("Screening.Table", "WoodenTable");
            At("Screening.DeskLamp", "DeskLamp");
            At("TapeDeck", "ReelToReel");
            At("Speaker", "WallSpeaker");
            At("OnAir", "OnAirLamp");
            At("Safe", "WallSafe");
            At("ReceiverDesk", "OfficeDesk");
            At("ReceiverDesk", "RadioSet", 0f, new Vector3(0f, 0.76f, 0f));
            if (ctx.Anchors.TryGet("Screening.CanRack", out var rack))
            {
                for (var i = 0; i < 6; i++)
                    LevelProps.Place(i % 2 == 0 ? "FilmCanClosed" : "FilmCan", ctx.Group(SectionBuildContext.GProps), rack.position + new Vector3(-0.9f + i * 0.36f, 0.9f + (i % 3) * 0.4f, 0f), i * 23f, 1f, "FilmCan" + i);
            }
            // Harrou's ledgers on the desk (B7.Ledgers, the INTEL 1/3 pickup is WP07's on the marker).
            if (ctx.Markup.TryGet("B7.Ledgers", out var ledgers)) LevelProps.Place("LedgerStack", ctx.Group(SectionBuildContext.GProps), ledgers.Position, 15f, 1f, "Ledgers");
            if (ctx.Markup.TryGet("B8.CanRack", out var can)) LevelProps.Place("FilmCanClosed", ctx.Group(SectionBuildContext.GProps), can.Position, 0f, 1f, "IntelCan");
            // Clipboards and notepads in the switch hall.
            LevelProps.Place("Clipboard", ctx.Group(SectionBuildContext.GProps), new Vector3(326.2f, 1.08f, 339f), 40f, 1f, null, true, false);
            LevelProps.Place("Notepads", ctx.Group(SectionBuildContext.GProps), new Vector3(338.4f, 1.08f, 352.4f), 300f, 1f, null, true, false);
        }
    }
}
