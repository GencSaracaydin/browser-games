using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Placements for the Appendix A ids the appendix does not locate (WP00-A handoff §6: <c>OOB.Block.*</c>,
    /// <c>B2.AlertCover</c>, <c>B5.Ridge</c>, <c>B7.Fill1/2</c>, <c>B9.R1..R4</c>, <c>B11.FireProp</c>, <c>CAM.Look.*</c>) and the
    /// extra ids WP10 Level places for other packages (sodium lamp heads <c>LAMP.nn</c> for WP10 Look's ripple, the mast's
    /// aviation lights <c>MAST.AvLight1..5</c>). Each placement is justified in its notes.
    /// </summary>
    public static class DerivedMarkers
    {
        /// <summary>The station mast foot (GDD §6.3 Map B 'M', §7.5 marksman platform at +25).</summary>
        public static readonly Vector3 MastBase = new Vector3(378f, 0f, 362f);
        /// <summary>Mast height (GDD §6.6 BLD-12).</summary>
        public const float MastHeight = 90f;
        /// <summary>Maintenance platform height (GDD §6.6 BLD-12).</summary>
        public const float MastPlatformHeight = 25f;
        /// <summary>Vertical spacing of the mast's aviation lights (top at <see cref="MastHeight"/>).</summary>
        public const float AvLightSpacing = 18f;
        /// <summary>The generator the sodium ripple starts from (GDD §8.5).</summary>
        public static readonly Vector3 RippleOrigin = new Vector3(372f, 0f, 305f);

        private static List<DerivedMarker> s_all;

        /// <summary>Every derived marker.</summary>
        public static IReadOnlyList<DerivedMarker> All => s_all ?? (s_all = Build());

        /// <summary>Derived markers of one section.</summary>
        public static IEnumerable<DerivedMarker> For(SectionId section)
        {
            foreach (var m in All)
                if (m.Section == section) yield return m;
        }

        /// <summary>The first derived marker with an id, or null.</summary>
        public static DerivedMarker Get(string id)
        {
            foreach (var m in All)
                if (m.Id == id) return m;
            return null;
        }

        private static DerivedMarker M(string id, SectionId s, string comp, float x, float y, float z, float heading = 0f, string notes = null) =>
            new DerivedMarker { Id = id, Section = s, Component = comp, Position = new Vector3(x, y, z), Heading = heading, Notes = notes };

        private static DerivedMarker Box(string id, SectionId s, string comp, float x, float y, float z, float sx, float sy, float sz, string notes) =>
            new DerivedMarker { Id = id, Section = s, Component = comp, Position = new Vector3(x, y, z), BoxSize = new Vector3(sx, sy, sz), Notes = notes };

        private static DerivedMarker Cover(string group, SectionId s, float x, float y, float z, float heading, bool standing, string notes = null) =>
            new DerivedMarker { Id = string.Empty, Group = group, Section = s, Component = "CoverNode", Position = new Vector3(x, y, z), Heading = heading, Standing = standing, Notes = notes };

        private static DerivedMarker Look(string id, SectionId s, float x, float y, float z, float heading, float pitch, string notes) =>
            new DerivedMarker { Id = id, Section = s, Component = "SceneMarker", Position = new Vector3(x, y, z), Heading = heading, Pitch = pitch, Notes = notes };

        private static List<DerivedMarker> Build()
        {
            var list = new List<DerivedMarker>();
            const string oob = "OutOfBoundsVolume";
            // ---- OOB volumes in front of the backtrack blockers (GDD §6.7: "each blocker sits behind an OOB volume"),
            // armed only while the blocker is closed (GateLinkedVolume), 4 m deep on the side the player stands on.
            list.Add(Box("OOB.Block.MarketSouth", SectionId.Market, oob, 185f, 1.5f, 167.35f, 8f, 5f, 4f, "Block.MarketSouth z 165, player side north (Beat 4+)."));
            list.Add(Box("OOB.Block.MarketArch", SectionId.Square, oob, 185f, 1.5f, 252.35f, 8f, 5f, 4f, "Block.MarketArch z 250, player side north (square)."));
            list.Add(Box("OOB.Block.SideAlley", SectionId.Square, oob, 177f, 1.5f, 248.35f, 6f, 5f, 4f, "Block.SideAlley z 246, player side north (alley)."));
            list.Add(Box("OOB.Block.R4Stair", SectionId.Roofs, oob, 293.4f, 9.8f, 330.6f, 2.6f, 4.4f, 4.2f, "Upper flight of the R4 switchback below the clipped stair top (Beat 6+)."));
            list.Add(Box("OOB.Block.Breach", SectionId.StationExt, oob, 302.35f, 1.5f, 322f, 4f, 5f, 6f, "Block.Breach x 300, player side east (yard, Beat 7+)."));
            list.Add(Box("OOB.Block.Postern", SectionId.StationExt, oob, 346f, 1.0f, 382.4f, 3.6f, 3f, 0.3f,
                "A 0.3 m strip on the closed postern: CP10 (346, 0, 383) is 1 m from the wall, a 4 m volume would start the countdown at the checkpoint."));

            // ---- B2.AlertCover: grove-facing cover along z 140 for the alerted gate team (GDD §7.2), behind the sandbag
            // and barrier-block cover S1 builds there.
            var alert = new[] { 162f, 168.5f, 175f, 181f, 187f, 193f };
            for (var i = 0; i < alert.Length; i++)
                list.Add(Cover("B2.AlertCover", SectionId.Approach, alert[i], 0f, 140.6f + (i % 2) * 0.8f, 200f - i * 4f, false, "Sandbags/blocks at z 139.5"));

            // ---- MID-A ridge silhouette reference: 2.5 km from the R4 parapet along az 258° at the crest height that hides
            // the disc at sun 0° (GDD §8.5, §9.3; ≈ 24.6 m).
            var ridge = SunGeometry.AlongBearing(new Vector3(291f, 0f, 328f), WorldConst.SunAzimuth, WorldConst.RidgeSilhouetteDistance);
            list.Add(M("B5.Ridge", SectionId.TownFar, "SceneMarker", ridge.x, SunGeometry.MidARidgeCrest(), ridge.z, 78f, "Crest point of the western ridge silhouette on the R4 sun line."));

            // ---- Realtime fills over teletype rows 1 and 3 (GDD §7.7 Light; rows at z 339 and 352, Map C1).
            list.Add(M("B7.Fill1", SectionId.StationInt, "SceneMarker", 332f, 3.9f, 339.2f, 0f, "Unshadowed fill over teletype row 1."));
            list.Add(M("B7.Fill2", SectionId.StationInt, "SceneMarker", 332f, 3.9f, 352.6f, 0f, "Unshadowed fill over teletype row 3."));

            // ---- Transmitter racks R1-R4 along z 356-358 (GDD §6.4 Map C3: racks x 322-329 and 333-340, tape deck at 331-332).
            list.Add(M("B9.R1", SectionId.StationInt, "SceneMarker", 323.75f, -4.2f, 357.3f, 180f, "Rack group west (Map C3 x 322-329), half A."));
            list.Add(M("B9.R2", SectionId.StationInt, "SceneMarker", 327.25f, -4.2f, 357.3f, 180f, "Rack group west, half B."));
            list.Add(M("B9.R3", SectionId.StationInt, "SceneMarker", 334.75f, -4.2f, 357.3f, 180f, "Rack group east (Map C3 x 333-340), half A."));
            list.Add(M("B9.R4", SectionId.StationInt, "SceneMarker", 338.25f, -4.2f, 357.3f, 180f, "Rack group east, half B (the 4th rack cuts D066)."));

            // ---- MID-B projection target (Docs/REQUESTS.md WP09 -> WP10_LEVEL): screen centre, forward to the projector,
            // scale x/y = the screen's width/height (RelayHallGeometry: 6.8 x 2.8 m on the z 359.7 wall).
            list.Add(new DerivedMarker
            {
                Id = "B8.Screen", Section = SectionId.StationInt, Component = "SceneMarker", Position = new Vector3(324f, 6.9f, 359.68f), Heading = 180f,
                BoxSize = new Vector3(6.8f, 2.8f, 1f), Notes = "Screening-room screen (localScale = width, height).",
            });

            // ---- The burning-technical copy in S8 (GDD §7.11): same spot as B4.FireProp.
            list.Add(M("B11.FireProp", SectionId.Heli, "SceneMarker", 232f, 1.0f, 286f, 234f, "Copy of the static fire prop; S3 is unloaded in Beat 11."));

            // ---- Beauty shots (Appendix A CAM.Look.*, >= 2 per section) for WP10 Look's LookCapture.
            list.Add(Look("CAM.Look.S1.01", SectionId.Approach, 42f, 23.6f, 30.5f, 45f, 3f, "Ridge vista, Beat 1 golden hour, long shadows NE."));
            list.Add(Look("CAM.Look.S1.02", SectionId.Approach, 124f, 1.7f, 104f, 72f, -4f, "Grove canopy shafts towards the sun side."));
            list.Add(Look("CAM.Look.S1.03", SectionId.Approach, 166f, 1.7f, 128f, 22f, 0f, "Weighbridge arch and truck from the grove."));
            list.Add(Look("CAM.Look.S2.01", SectionId.Market, 185f, 1.7f, 174f, 0f, -2f, "Long Market north: blue street shade, gold upper storeys."));
            list.Add(Look("CAM.Look.S2.02", SectionId.Market, 187.5f, 1.6f, 230f, 300f, 2f, "Into the Sunset Lane band (GDD §7.3)."));
            list.Add(Look("CAM.Look.S3.01", SectionId.Square, 186f, 1.7f, 251f, 52f, -6f, "Clock Square from the arch: tower top lit, technical."));
            list.Add(Look("CAM.Look.S3.02", SectionId.Square, 206f, 6.2f, 315.5f, 0f, 8f, "Auditorium from the balcony: dust shafts, EXIT sign."));
            list.Add(Look("CAM.Look.S3.03", SectionId.Square, 216f, 6.2f, 300f, 140f, 12f, "Marquee walkway over the technical."));
            list.Add(Look("CAM.Look.S4.01", SectionId.Roofs, 289.5f, 13.65f, 328f, 258f, -1f, "MID-A: sun disc over the western ridge from R4."));
            list.Add(Look("CAM.Look.S4.02", SectionId.Roofs, 284f, 13.65f, 322f, 80f, 10f, "MID-A: long shadows across R4 towards the mast."));
            list.Add(Look("CAM.Look.S5.01", SectionId.StationExt, 303f, 1.7f, 322f, 70f, -4f, "Yard through the breach: floods and the Hall."));
            list.Add(Look("CAM.Look.S5.02", SectionId.StationExt, 352f, 1.7f, 334f, 42f, -30f, "Mast and aviation lights at afterglow."));
            list.Add(Look("CAM.Look.S6.01", SectionId.StationInt, 321f, 2.0f, 333f, 40f, 4f, "Switch hall: teletype rows under the clock wall."));
            list.Add(Look("CAM.Look.S6.02", SectionId.StationInt, 332f, -2.5f, 346f, 0f, 2f, "Basement transmitter racks glowing orange."));
            list.Add(Look("CAM.Look.S6.03", SectionId.StationInt, 329f, 6.5f, 351f, 300f, 6f, "Screening room: projector beam on the screen."));
            list.Add(Look("CAM.Look.S7.01", SectionId.Pitch, 340f, 0.7f, 391f, 20f, -3f, "The Pitch at blue hour from the bleacher."));
            list.Add(Look("CAM.Look.S7.02", SectionId.Pitch, 362f, 0.7f, 414f, 232f, 0f, "LZ and green smoke against the town glow."));
            list.Add(Look("CAM.Look.S8.01", SectionId.Heli, 300f, 40f, 330f, 228f, 18f, "Exfil over the roofs towards Clock Square."));
            list.Add(Look("CAM.Look.S8.02", SectionId.Heli, 186f, 37f, 215f, 200f, 25f, "Exfil down the Long Market's lamps."));
            list.Add(Look("CAM.Look.S9.01", SectionId.TownFar, 291f, 13.65f, 328f, 258f, -0.5f, "Ridge silhouette and the far hills (GDD §9.3)."));
            list.Add(Look("CAM.Look.S9.02", SectionId.TownFar, 420f, 4f, 440f, 228f, -2f, "Town from the salt pan."));

            // ---- Mast aviation lights, top to bottom 0.3 s apart (GDD §8.5 row 4): WP10 Look's AviationLights finds them.
            // Heights 90/72/54/36/18 m match the lenses on Look's PF_LookRig (Docs/REQUESTS.md WP10_LOOK -> WP10_LEVEL (9)).
            for (var i = 0; i < 5; i++)
            {
                var y = MastHeight - i * AvLightSpacing;
                list.Add(M("MAST.AvLight" + (i + 1), SectionId.TownFar, "SceneMarker", MastBase.x, y, MastBase.z, 0f, "Red aviation light " + (i + 1) + " (top = 1)."));
            }

            // ---- Sodium lamp heads (GDD §8.5: 38 heads rippling from the generator at 15 m/s).
            foreach (var lamp in LampLayout.All)
                list.Add(M(lamp.Id, lamp.Section, "SceneMarker", lamp.Head.x, lamp.Head.y, lamp.Head.z, lamp.Facing, lamp.Mount.ToString()));
            return list;
        }
    }
}
