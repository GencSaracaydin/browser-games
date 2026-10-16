using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Zone boxes per section: ZoneTags covering every walkable space (nested ones with higher priority, WP06) and the four
    /// interior EyeAdaptationZones WP10 Look asked for (cinema auditorium +1.2 EV, Relay Hall +0.8, screening room +1.0,
    /// basement +1.0; GDD §9.2a G10). The open desert needs none (ZoneTag.Resolve falls back to OpenDesert).
    /// </summary>
    public static class ZoneVolumes
    {
        private static List<ZoneVolumeDef> s_all;

        /// <summary>Every zone box.</summary>
        public static IReadOnlyList<ZoneVolumeDef> All => s_all ?? (s_all = Build());

        /// <summary>Zone boxes of a section.</summary>
        public static IEnumerable<ZoneVolumeDef> For(SectionId section)
        {
            foreach (var z in All)
                if (z.Section == section) yield return z;
        }

        private static ZoneVolumeDef Z(string name, SectionId s, float x0, float z0, float x1, float z1, float y0, float y1, ZoneType zone, int priority) =>
            new ZoneVolumeDef { Name = name, Section = s, Rect = new AreaRect(x0, z0, x1, z1), Y0 = y0, Y1 = y1, Zone = zone, Priority = priority };

        private static ZoneVolumeDef Eye(string name, SectionId s, float x0, float z0, float x1, float z1, float y0, float y1, float ev, string profile) =>
            new ZoneVolumeDef { Name = name, Section = s, Rect = new AreaRect(x0, z0, x1, z1), Y0 = y0, Y1 = y1, EyeAdaptation = true, Ev = ev, Profile = profile };

        private static List<ZoneVolumeDef> Build()
        {
            const SectionId a = SectionId.Approach, m = SectionId.Market, q = SectionId.Square, r = SectionId.Roofs,
                ext = SectionId.StationExt, inn = SectionId.StationInt, p = SectionId.Pitch;
            return new List<ZoneVolumeDef>
            {
                // S1: the Weighbridge plaza and the guard hut.
                Z("Zone_WeighbridgePlaza", a, 140f, 128f, 220f, 170f, -2f, 14f, ZoneType.Street, 1),
                Z("Zone_GuardHut", a, 170f, 150.5f, 174f, 153.5f, -0.5f, 2.8f, ZoneType.Interior, 3),
                Z("Zone_PumpHouse", a, 144.5f, 128f, 155.5f, 136f, -0.5f, 3.5f, ZoneType.Interior, 3),
                // S2: the Long Market, Tinsmiths' Lane, Sunset Lane; the loggia room and the T6 room; the W2b roof terrace.
                Z("Zone_LongMarket", m, 164f, 166f, 199f, 254f, -2f, 16f, ZoneType.Street, 1),
                Z("Zone_SunsetLane", m, 118f, 214f, 182f, 244f, -2f, 14f, ZoneType.Street, 1),
                Z("Zone_LoggiaRoom", m, 189.4f, 219.6f, 194.6f, 224.4f, 4.1f, 7.2f, ZoneType.Interior, 3),
                Z("Zone_T6Room", m, 164.5f, 240.45f, 172.55f, 246.4f, -0.2f, 3.3f, ZoneType.Interior, 3),
                Z("Zone_W2bTerrace", m, 171f, 209f, 181f, 218f, 7.6f, 12f, ZoneType.Roof, 2),
                // S3: Clock Square, the side alley and east street, the cinema (lobby, concession, auditorium), the marquee.
                Z("Zone_ClockSquare", q, 172f, 244f, 262f, 301f, -2f, 18f, ZoneType.Square, 1),
                Z("Zone_SideAlley", q, 163f, 246f, 182f, 300f, -2f, 12f, ZoneType.Street, 2),
                Z("Zone_EastStreet", q, 252f, 272f, 290f, 281f, -2f, 12f, ZoneType.Street, 2),
                Z("Zone_CinemaLobby", q, 186.4f, 301.4f, 231f, 312f, -0.5f, 11f, ZoneType.Interior, 3),
                Z("Zone_CinemaAuditorium", q, 186.4f, 312f, 230.6f, 329.6f, -0.5f, 11.2f, ZoneType.Interior, 3),
                Z("Zone_Marquee", q, 200f, 298.4f, 226f, 301.4f, 4.4f, 7.5f, ZoneType.Roof, 4),
                Z("Zone_N1Room", q, 255.2f, 281.45f, 261.5f, 287.5f, -0.2f, 3.6f, ZoneType.Interior, 3),
                Eye("Eye_Cinema", q, 186.4f, 301.4f, 230.6f, 329.6f, -0.5f, 11.2f, 1.2f, "VP_Interior_Cinema"),
                // S4: the Laundry Roofs and the west-wall alley.
                Z("Zone_LaundryRoofs", r, 230f, 298f, 301f, 346f, 6f, 22f, ZoneType.Roof, 1),
                Z("Zone_WallAlley", r, 292f, 298f, 300f, 342f, -2f, 6f, ZoneType.Street, 2),
                // S5: the relay yard, gatehouse, generator shed.
                Z("Zone_RelayYard", ext, 298f, 282f, 396f, 386f, -2f, 20f, ZoneType.Square, 1),
                Z("Zone_Gatehouse", ext, 340.6f, 290.6f, 351.4f, 297.4f, -0.2f, 3.6f, ZoneType.Interior, 3),
                Z("Zone_GeneratorShed", ext, 366f, 300f, 378f, 310f, -0.2f, 4.5f, ZoneType.Interior, 3),
                // S6: the Relay Hall, screening room, basement and tunnel.
                Z("Zone_RelayHall", inn, 316.4f, 330.4f, 355.6f, 361.6f, 0f, 8.6f, ZoneType.Interior, 3),
                Z("Zone_ScreeningRoom", inn, 316.4f, 350f, 330f, 361.6f, 4.7f, 8.6f, ZoneType.Interior, 4),
                Z("Zone_Basement", inn, 318f, 328f, 346f, 360f, -6.6f, -0.4f, ZoneType.Basement, 4),
                Z("Zone_CableTunnel", inn, 342.4f, 359.6f, 346.2f, 382f, -4.6f, 0f, ZoneType.Basement, 4),
                Eye("Eye_RelayHall", inn, 316.4f, 330.4f, 355.6f, 350f, 0f, 8.6f, 0.8f, "VP_Interior_Hall"),
                Eye("Eye_HallNorth", inn, 330f, 350f, 355.6f, 361.6f, 0f, 8.6f, 0.8f, "VP_Interior_Hall"),
                Eye("Eye_HallNorthWest", inn, 316.4f, 350f, 330f, 361.6f, 0f, 4.7f, 0.8f, "VP_Interior_Hall"),
                Eye("Eye_Screening", inn, 316.4f, 350f, 330f, 361.6f, 4.7f, 8.6f, 1.0f, "VP_Interior_Screening"),
                Eye("Eye_Basement", inn, 318f, 328f, 346f, 360f, -6.6f, -0.4f, 1.0f, "VP_Interior_Basement"),
                Eye("Eye_Tunnel", inn, 342.4f, 359.6f, 346.2f, 382f, -4.6f, -0.2f, 1.0f, "VP_Interior_Basement"),
                // S7: the ruined west shed.
                Z("Zone_WestShed", p, 290f, 383f, 298f, 394f, -1.2f, 3f, ZoneType.Interior, 3),
            };
        }
    }
}
