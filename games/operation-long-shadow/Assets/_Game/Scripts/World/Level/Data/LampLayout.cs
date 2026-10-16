using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// The 38 sodium lamp heads of GDD §8.5 ("38 emissive lamp heads plus at most 6 realtime unshadowed lamps near the
    /// player") along the market, the lanes, Clock Square, the east street, the roofs alley, the road south of the
    /// compound, the yard and the Customs Road. WP10 Look's ripple strikes them by <see cref="LampDef.RippleDistance"/>
    /// at 15 m/s from the generator (372, 0, 305). Lamps keep clear of the Sunset Lane band and of the awning lines.
    /// </summary>
    public static class LampLayout
    {
        /// <summary>Head height of wall brackets above the mount floor.</summary>
        public const float BracketHeadHeight = 6.0f;
        /// <summary>Head height of posts above their foot.</summary>
        public const float PostHeadHeight = 6.6f;
        /// <summary>Bracket/arm outreach from the wall or post axis.</summary>
        public const float Outreach = 1.1f;

        private static List<LampDef> s_all;

        /// <summary>Every lamp, ids LAMP.01..LAMP.38.</summary>
        public static IReadOnlyList<LampDef> All => s_all ?? (s_all = Build());

        /// <summary>Lamps of one section.</summary>
        public static IEnumerable<LampDef> For(SectionId section)
        {
            foreach (var l in All)
                if (l.Section == section) yield return l;
        }

        private struct Raw
        {
            public LampMount Mount;
            public float X, Y, Z, Facing;
            public SectionId Section;
        }

        private static Raw W(float x, float y, float z, float facing, SectionId s) => new Raw { Mount = LampMount.WallBracket, X = x, Y = y, Z = z, Facing = facing, Section = s };
        private static Raw P(float x, float y, float z, float facing, SectionId s) => new Raw { Mount = LampMount.Post, X = x, Y = y, Z = z, Facing = facing, Section = s };

        private static List<LampDef> Build()
        {
            var raw = new[]
            {
                // Long Market: alternating facade brackets (west facade x 181, east facade x 189), arms over the street.
                W(181f, 0f, 176f, 90f, SectionId.Market), W(189f, 0f, 190f, 270f, SectionId.Market), W(181f, 0f, 204f, 90f, SectionId.Market),
                W(189f, 0f, 220f, 270f, SectionId.Market), W(181f, 0f, 231f, 90f, SectionId.Market), W(189f, 0f, 246f, 270f, SectionId.Market),
                // Tinsmiths' Lane.
                W(171f, 0f, 206f, 270f, SectionId.Market), W(166f, 0f, 228f, 90f, SectionId.Market),
                // Side alley (east wall, x 180).
                W(180f, 0f, 268f, 270f, SectionId.Square), W(180f, 0f, 292f, 270f, SectionId.Square),
                // Clock Square: four corner posts and two at the tower base.
                P(203f, 0f, 259f, 45f, SectionId.Square), P(240f, 0f, 262f, 315f, SectionId.Square), P(201f, 0f, 289f, 135f, SectionId.Square),
                P(244f, 0f, 292f, 225f, SectionId.Square), P(214.5f, 0f, 269.5f, 225f, SectionId.Square), P(225.5f, 0f, 280.5f, 45f, SectionId.Square),
                // East street (z 272-281).
                W(258f, 0f, 272f, 0f, SectionId.Square), W(270f, 0f, 281f, 180f, SectionId.Square),
                // Roofs alley (R4 house wall x 292, station wall x 300).
                W(292f, 0f, 316f, 90f, SectionId.Roofs), W(300f, 0f, 336f, 270f, SectionId.Roofs),
                // Road along the south compound wall (z 286).
                P(310f, 0f, 285f, 0f, SectionId.StationExt), P(330f, 0f, 285f, 0f, SectionId.StationExt),
                P(362f, 0f, 285f, 0f, SectionId.StationExt), P(382f, 0f, 285f, 0f, SectionId.StationExt),
                // Relay Hall south facade (z 330).
                W(322f, 0.3f, 330f, 180f, SectionId.StationExt), W(330f, 0.3f, 330f, 180f, SectionId.StationExt),
                W(346f, 0.3f, 330f, 180f, SectionId.StationExt), W(353f, 0.3f, 330f, 180f, SectionId.StationExt),
                // Yard posts north of the Hall and on the east side.
                P(326f, 0f, 372f, 90f, SectionId.StationExt), P(362f, 0f, 374f, 270f, SectionId.StationExt),
                P(382f, 0f, 322f, 270f, SectionId.StationExt), P(382f, 0f, 344f, 270f, SectionId.StationExt),
                // Gatehouse yard side (z 298) and the generator shed (x 366).
                W(341f, 0f, 298f, 0f, SectionId.StationExt), W(351f, 0f, 298f, 0f, SectionId.StationExt), W(366f, 0f, 304f, 270f, SectionId.StationExt),
                // Customs Road east of the compound.
                P(398f, -1f, 330f, 270f, SectionId.Pitch), P(398f, -1f, 362f, 270f, SectionId.Pitch), P(398f, -1f, 392f, 270f, SectionId.Pitch),
            };
            var list = new List<LampDef>(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var r = raw[i];
                var dir = MathX.DirectionFromBearing(r.Facing);
                var baseP = new Vector3(r.X, r.Y, r.Z);
                var height = r.Mount == LampMount.Post ? PostHeadHeight : BracketHeadHeight;
                var head = baseP + dir * Outreach + Vector3.up * height;
                list.Add(new LampDef
                {
                    Id = "LAMP." + (i + 1).ToString("00"),
                    Section = r.Section,
                    Mount = r.Mount,
                    Base = baseP,
                    Head = head,
                    Facing = r.Facing,
                    RippleDistance = Vector3.Distance(new Vector3(head.x, 0f, head.z), DerivedMarkers.RippleOrigin),
                });
            }
            return list;
        }
    }
}
