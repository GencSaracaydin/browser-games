using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.World.Level.Data
{
    /// <summary>
    /// Which section scene holds each Appendix A marker (GDD §6.7 load/unload table). The rules keep every marker loaded
    /// for as long as a beat can use it: a backtrack blocker that closes when its section unloads lives in the
    /// neighbouring section that stays loaded; markers that several beats share (PA horns, the mast perch, the ridge
    /// silhouette, the critical path) live in <c>S9_TownFar</c>, which is never unloaded.
    /// </summary>
    public static class MarkupSections
    {
        private static readonly Dictionary<string, SectionId> s_explicit = new Dictionary<string, SectionId>
        {
            // Global
            { "PA.Horn1", SectionId.TownFar }, { "PA.Horn2", SectionId.TownFar }, { "PA.Horn3", SectionId.TownFar },
            { "CAM.IntroRidge", SectionId.Approach },
            { "CRATE.Weighbridge", SectionId.Approach }, { "CRATE.MarketArch", SectionId.Market },
            { "CRATE.R4", SectionId.Roofs }, { "CRATE.Basement", SectionId.StationInt }, { "CRATE.LZ", SectionId.Pitch },
            // OOB
            { "OOB.RidgeWest", SectionId.Approach }, { "OOB.RidgeSouth", SectionId.Approach },
            { "OOB.GroveWest", SectionId.Approach }, { "OOB.GroveSouth", SectionId.Approach },
            { "OOB.SaltPanNorth", SectionId.Pitch }, { "OOB.SaltPanEast", SectionId.Pitch },
            { "OOB.RoofsNorth", SectionId.Roofs }, { "OOB.RoofsSouth", SectionId.Roofs },
            { "OOB.MarketRoofs", SectionId.Market }, { "OOB.InsertionApron", SectionId.Approach },
            // Gates (GDD §7.13) next to their doors; blockers (GDD §6.7) in the section that stays loaded when they close.
            { "Gate.Weighbridge", SectionId.Approach }, { "Gate.CinemaSideDoor", SectionId.Square },
            { "Gate.LadderL", SectionId.Roofs }, { "Gate.R4Stair", SectionId.Roofs }, { "Gate.HallDoor", SectionId.StationExt },
            { "Gate.ScreeningDoor", SectionId.StationInt }, { "Gate.WestStairDoor", SectionId.StationInt },
            { "Gate.TunnelDoor", SectionId.StationInt }, { "Gate.HeliDoor", SectionId.Pitch },
            { "Block.MarketSouth", SectionId.Market }, { "Block.MarketArch", SectionId.Square }, { "Block.SideAlley", SectionId.Square },
            { "Block.R4Stair", SectionId.Roofs }, { "Block.Breach", SectionId.StationExt }, { "Block.Postern", SectionId.StationExt },
            // B4 markers south of the square arch and the arcade gap live with the market or square that hold them in Beat 4.
            { "B4.ArcadeGap", SectionId.Market },
            // B5 exceptions
            { "B5.Perch.Mast", SectionId.TownFar }, { "B5.BreachPoint", SectionId.StationExt }, { "B5.Ridge", SectionId.TownFar },
            // B6: the alley belongs to S4; the wall, breach and yard to S5.
            { "CP6", SectionId.Roofs }, { "B6.T.Alley", SectionId.Roofs }, { "B6.StandClear", SectionId.Roofs },
            { "B6.T.StandClear", SectionId.Roofs },
            // B7: the door stack checkpoint is outside the Hall.
            { "CP7", SectionId.StationExt },
            // B9: the postern is in the north compound wall.
            { "B9.Postern", SectionId.StationExt },
            // B10: Magpie's splines ride with the helicopter in S8.
            { "B10.HeliApproach", SectionId.Heli }, { "B10.HeliWaveOff", SectionId.Heli },
        };

        /// <summary>
        /// Section that holds a marker, or false for ids WP10 Level does not place (booth ids and <c>INS.Cam.*</c> are
        /// WP09's).
        /// </summary>
        public static bool TryGetSection(string id, string beat, out SectionId section)
        {
            section = SectionId.TownFar;
            if (string.IsNullOrEmpty(id)) return false;
            if (s_explicit.TryGetValue(id, out section)) return true;
            if (id.StartsWith("BOOTH.") || id.StartsWith("INS.Cam")) return false;
            if (id.StartsWith("CritPath.")) { section = SectionId.TownFar; return true; }
            if (id.StartsWith("HELI.")) { section = SectionId.Heli; return true; }
            if (id.StartsWith("INS.")) { section = SectionId.Approach; return true; }
            if (id.StartsWith("OOB.Block."))
                return TryGetSection(id.Substring("OOB.".Length), beat, out section);
            if (id.StartsWith("CAM.Look.")) return TryParseLookSection(id, out section);
            switch (BeatOf(id, beat))
            {
                case 1:
                case 2: section = SectionId.Approach; return true;
                case 3: section = SectionId.Market; return true;
                case 4: section = SectionId.Square; return true;
                case 5: section = SectionId.Roofs; return true;
                case 6: section = SectionId.StationExt; return true;
                case 7:
                case 8:
                case 9: section = SectionId.StationInt; return true;
                case 10: section = SectionId.Pitch; return true;
                case 11: section = SectionId.Heli; return true;
            }
            return false;
        }

        /// <summary>Beat number of an id ("B3.T.Z185", "SC_B3_BAL", "E3-01", "CP3b") from its prefix or the CSV beat column.</summary>
        public static int BeatOf(string id, string beatColumn)
        {
            var b = ParseBeat(beatColumn);
            if (b > 0) return b;
            if (string.IsNullOrEmpty(id)) return 0;
            if (id.StartsWith("SC_B")) return LeadingInt(id, 4);
            if (id.StartsWith("CP")) return LeadingInt(id, 2);
            if (id.StartsWith("E") && id.Length > 1 && char.IsDigit(id[1])) return LeadingInt(id, 1);
            if (id.StartsWith("B") && id.Length > 1 && char.IsDigit(id[1])) return LeadingInt(id, 1);
            return 0;
        }

        private static int ParseBeat(string beat)
        {
            if (string.IsNullOrEmpty(beat) || beat[0] != 'B' || beat.Length < 2 || !char.IsDigit(beat[1])) return 0;
            return LeadingInt(beat, 1);
        }

        private static int LeadingInt(string s, int start)
        {
            var v = 0;
            var any = false;
            for (var i = start; i < s.Length && char.IsDigit(s[i]); i++)
            {
                v = v * 10 + (s[i] - '0');
                any = true;
            }
            return any ? v : 0;
        }

        /// <summary>Beauty-shot markers are named "CAM.Look.S2.01" (two or more per section, Appendix A).</summary>
        public static bool TryParseLookSection(string id, out SectionId section)
        {
            section = SectionId.TownFar;
            var rest = id.Substring("CAM.Look.".Length);
            var dot = rest.IndexOf('.');
            var key = dot > 0 ? rest.Substring(0, dot) : rest;
            return Sections.TryParse(key, out section);
        }
    }
}
