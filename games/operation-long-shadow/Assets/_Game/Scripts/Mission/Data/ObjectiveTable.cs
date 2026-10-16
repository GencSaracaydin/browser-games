using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// GDD §7.12 objective master list as code data (TECH_PLAN D12): ids, HUD texts, marker kinds and targets (Appendix A
    /// ids with their coordinates as fallbacks), completion rules. The EditMode tests compare every row with the GDD table.
    /// HUD line: <c>OBJ &lt;beat&gt;/11 — &lt;group&gt; — &lt;TEXT&gt;</c> (<see cref="ObjectiveFormat"/>).
    /// </summary>
    public static class ObjectiveTable
    {
        /// <summary>Radius within which optional intel icons show (GDD §7.12).</summary>
        public const float OptionalIntelRadius = 6f;

        public const string B1Observe = "B1.Observe";
        public const string B1Follow = "B1.Follow";
        public const string B1Grove = "B1.Grove";
        public const string B2Sentries = "B2.Sentries";
        public const string B2Weighbridge = "B2.Weighbridge";
        public const string B2Enter = "B2.Enter";
        public const string B3Push = "B3.Push";
        public const string B4Flank = "B4.Flank";
        public const string B4Technical = "B4.Technical";
        public const string B4Regroup = "B4.Regroup";
        public const string B4UpTop = "B4.UpTop";
        public const string B5Wall = "B5.Wall";
        public const string B5Breach = "B5.Breach";
        public const string B6StandClear = "B6.StandClear";
        public const string B6Yard = "B6.Yard";
        public const string B6Hall = "B6.Hall";
        public const string B7Breach = "B7.Breach";
        public const string B7Ledgers = "B7.Ledgers";
        public const string B7Upstairs = "B7.Upstairs";
        public const string B7O1 = "B7.O1";
        public const string B8Thread = "B8.Thread";
        public const string B8Film = "B8.Film";
        public const string B8Basement = "B8.Basement";
        public const string B8O2 = "B8.O2";
        public const string B9Defend = "B9.Defend";
        public const string B9Codebook = "B9.Codebook";
        public const string B9Transmitter = "B9.Transmitter";
        public const string B9Exfil = "B9.Exfil";
        public const string B10LZ = "B10.LZ";
        public const string B10Defend = "B10.Defend";
        public const string B10Rpg = "B10.RPG";
        public const string B10Board = "B10.Board";
        public const string B11Cover = "B11.Cover";

        private static readonly ObjectiveSpec[] s_all = Build();
        private static Dictionary<string, ObjectiveSpec> s_byId;

        /// <summary>Every row in GDD order.</summary>
        public static IReadOnlyList<ObjectiveSpec> All => s_all;

        /// <summary>A row by id (null if unknown).</summary>
        public static ObjectiveSpec Get(string id)
        {
            if (id == null) return null;
            if (s_byId == null)
            {
                var d = new Dictionary<string, ObjectiveSpec>(s_all.Length);
                foreach (var s in s_all) d[s.Id] = s;
                s_byId = d;
            }
            return s_byId.TryGetValue(id, out var spec) ? spec : null;
        }

        /// <summary>Fills <paramref name="results"/> (cleared first) with the rows of a beat.</summary>
        public static int ForBeat(int beat, List<ObjectiveSpec> results)
        {
            results.Clear();
            foreach (var s in s_all)
                if (s.Beat == beat) results.Add(s);
            return results.Count;
        }

        /// <summary>Non-optional objectives of a beat (LightStage progress denominator, GDD §9.1).</summary>
        public static int RequiredCount(int beat)
        {
            var n = 0;
            for (var i = 0; i < s_all.Length; i++)
                if (s_all[i].Beat == beat && !s_all[i].Optional) n++;
            return n;
        }

        private static ObjectiveSpec O(string id, int beat, string text, MarkerKind? kind, string label, string rule,
                                       params ObjectiveTarget[] targets)
        {
            var dist = kind == MarkerKind.Diamond || kind == MarkerKind.DiamondHollow || kind == MarkerKind.HighPriority;
            var walls = kind == MarkerKind.HighPriority;
            return new ObjectiveSpec(id, beat, text, kind, targets, label, dist, walls, false, 0f, true, rule);
        }

        private static ObjectiveSpec Manual(ObjectiveSpec s) =>
            new ObjectiveSpec(s.Id, s.Beat, s.Text, s.Kind, s.Targets, s.Label, s.ShowDistance, s.ThroughWalls, s.Optional,
                s.ProximityOnly, false, s.CompletionRule);

        private static ObjectiveSpec OptionalIntel(string id, int beat, string rule, ObjectiveTarget target) =>
            new ObjectiveSpec(id, beat, "—", MarkerKind.Intel, new[] { target }, "INTEL", false, false, true,
                OptionalIntelRadius, true, rule);

        private static ObjectiveTarget M(string id, float x, float y, float z) => ObjectiveTarget.Marker(id, x, y, z);

        private static ObjectiveSpec[] Build()
        {
            const MarkerKind D = MarkerKind.Diamond;
            const MarkerKind H = MarkerKind.HighPriority;
            return new[]
            {
                // ---- Beat 1 THE SHOULDER (GDD §7.1)
                O(B1Observe, 1, "OBSERVE ZAROUD 0/3", MarkerKind.DiamondHollow, "", "3 POIs glassed, or 40 s",
                    M("B1.POI1", 378f, 45f, 362f), M("B1.POI2", 220f, 18f, 275f), M("B1.POI3", 183f, 4f, 162f)),
                O(B1Follow, 1, "FOLLOW HOLLISTER", MarkerKind.Follow, "HOLLISTER", "player enters `B1.T.WadiMid`",
                    ObjectiveTarget.Member(SquadIds.Hollis)),
                O(B1Grove, 1, "REACH THE GROVE", D, "GROVE", "`B1.T.GroveEnter`", M("B1.GroveTarget", 108f, 0f, 96f)),

                // ---- Beat 2 PUMP HOUSE SIX (GDD §7.2); the TargetX markers on E2-01/02 are added within 3 m of the position
                O(B2Sentries, 2, "ELIMINATE THE SENTRIES", D, "FIRING POSITION", "both sentries dead",
                    M("B2.FiringPos", 136f, 0f, 112f)),
                O(B2Weighbridge, 2, "CLEAR THE WEIGHBRIDGE", D, "TRUCK", "E2-03…06 dead", M("B2.TruckTarget", 190f, 3f, 150f)),
                O(B2Enter, 2, "ENTER ZAROUD", D, "GATE", "`B2.T.Exit`", M("B2.ArchTarget", 183f, 2f, 166f)),

                // ---- Beat 3 THE LONG MARKET (GDD §7.3)
                O(B3Push, 3, "PUSH THROUGH THE LONG MARKET", D, "ARCH", "`B3.T.Exit` with the squad at M4",
                    M("B3.ArchTarget", 185f, 4f, 252f)),

                // ---- Beat 4 CLOCK SQUARE / CINEMA ZENITH (GDD §7.4)
                O(B4Flank, 4, "FLANK THROUGH THE CINEMA", D, "SIDE DOOR", "side door opened", M("B4.SideDoor", 186f, 1.2f, 307f)),
                O(B4Technical, 4, "NEUTRALIZE THE TECHNICAL", H, "TECHNICAL", "E4-01 dead",
                    ObjectiveTarget.Actor(EncounterIds.TechnicalGunner, 232f, 1.6f, 286f, 1f)),
                O(B4Regroup, 4, "REGROUP WITH HOLLISTER", MarkerKind.Follow, "HOLLISTER", "every E4 enemy dead",
                    ObjectiveTarget.Member(SquadIds.Hollis)),
                O(B4UpTop, 4, "UP TOP", D, "LADDER", "ladder top (`CP5`)", M("B4.Ladder", 236f, 0f, 301f)),

                // ---- Beat 5 LAST LIGHT (GDD §7.5)
                O(B5Wall, 5, "REACH THE STATION WALL", D, "STATION WALL", "MID-A starts", M("B5.R4Parapet", 291f, 13f, 328f)),
                O(B5Breach, 5, "BREACH POINT", D, "BREACH", "`B6.T.Alley`", M("B5.BreachPoint", 300f, 1.5f, 322f)),

                // ---- Beat 6 THE RELAY YARD (GDD §7.6); flood markers appear after D046 (script-managed)
                O(B6StandClear, 6, "STAND CLEAR", MarkerKind.Ring, "STAND CLEAR", "player in the ring (`D044`)",
                    M("B6.StandClear", 296f, 0f, 310f)),
                Manual(O(B6Yard, 6, "CLEAR THE YARD", H, "FLOOD", "every E6 enemy dead",
                    ObjectiveTarget.Destructible("B6.F1", 306f, 8f, 296f), ObjectiveTarget.Destructible("B6.F2", 386f, 8f, 296f),
                    ObjectiveTarget.Destructible("B6.F3", 306f, 8f, 376f), ObjectiveTarget.Destructible("B6.F4", 386f, 8f, 376f))),
                O(B6Hall, 6, "ENTER THE RELAY HALL", D, "DOOR", "door kicked", M("B6.HallDoor", 338f, 1.2f, 330f)),

                // ---- Beat 7 THE RELAY HALL (GDD §7.7)
                O(B7Breach, 7, "BREACH", null, "", "E7-01…03 dead"),
                O(B7Ledgers, 7, "RECOVER THE LEDGERS", MarkerKind.Intel, "LEDGERS", "hold E 1.2 s (INTEL 1/3)",
                    M("B7.Ledgers", 351f, 1.1f, 355f)),
                O(B7Upstairs, 7, "SEARCH UPSTAIRS", D, "STAIRS", "`B7.T.Landing`", M("B7.Spiral", 334.5f, 0.3f, 358f)),
                OptionalIntel(B7O1, 7, "hold E 1.2 s (OPTIONAL)", M("B7.O1", 341f, 1.2f, 351f)),

                // ---- Beat 8 THE REEL (GDD §7.8)
                O(B8Thread, 8, "THREAD THE PROJECTOR", D, "PROJECTOR", "MID-B ends", M("B8.Projector", 323f, 5.6f, 352f)),
                O(B8Film, 8, "RECOVER THE FILM", MarkerKind.Intel, "FILM", "hold E 1.2 s (INTEL 2/3)",
                    M("B8.CanRack", 324f, 5.6f, 350.5f)),
                O(B8Basement, 8, "GET TO THE BASEMENT", D, "BASEMENT", "`B8.T.BasementFoot`", M("B8.WestStair", 319f, 0.3f, 337f)),
                OptionalIntel(B8O2, 8, "hold E 1.2 s (OPTIONAL)", M("B8.O2", 319.5f, 5.8f, 350.5f)),

                // ---- Beat 9 THE CLOCKMAKER (GDD §7.9)
                O(B9Defend, 9, "DEFEND CASTELLANOS", MarkerKind.Ring, "SAFE", "drill 100 %", M("B9.Safe", 345.8f, -3.4f, 342f)),
                O(B9Codebook, 9, "RECOVER THE CODEBOOK", null, "", "completes with `B9.Defend` (INTEL 3/3, automatic)"),
                O(B9Transmitter, 9, "DESTROY THE TRANSMITTER 0/4", H, "RACK", "4 racks destroyed",
                    ObjectiveTarget.Destructible("B9.R1", 324f, -3f, 357f), ObjectiveTarget.Destructible("B9.R2", 329f, -3f, 357f),
                    ObjectiveTarget.Destructible("B9.R3", 335f, -3f, 357f), ObjectiveTarget.Destructible("B9.R4", 340f, -3f, 357f)),
                O(B9Exfil, 9, "EXFIL: THE PITCH", D, "TUNNEL", "`B9.T.Tunnel`", M("B9.TunnelDoor", 344f, -3f, 359.8f)),

                // ---- Beat 10 THE PITCH (GDD §7.10)
                O(B10LZ, 10, "GET TO THE LZ", D, "LZ", "`B10.T.LZ`", M("B10.LZLine", 340f, -1f, 390.5f)),
                O(B10Defend, 10, "DEFEND THE LZ", MarkerKind.Ring, "MAGPIE", "wheels down", M("B10.Pad", 350f, -1f, 406f)),
                O(B10Rpg, 10, "KILL THE RPG GUNNER", H, "RPG", "E10-07 dead",
                    ObjectiveTarget.Actor(EncounterIds.RpgGunner, 362f, 1f, 424f)),
                O(B10Board, 10, "BOARD THE HELICOPTER", D, "HELICOPTER", "E pressed", M("B10.HeliDoor", 350f, 0.2f, 404f)),

                // ---- Beat 11 MAGPIE (GDD §7.11); markers follow the target windows (script-managed)
                Manual(O(B11Cover, 11, "COVER THE EXFIL", H, "", "climax starts (t 30)",
                    ObjectiveTarget.Actor("E11-T1", 346f, 6f, 294f), ObjectiveTarget.Actor("E11-T2", 285f, 9f, 268f),
                    ObjectiveTarget.Actor("E11-T3", 224f, 10f, 226f), ObjectiveTarget.Actor("E11-T4", 236f, 9f, 238f),
                    ObjectiveTarget.Destructible(B11Truck1, 300f, 1.5f, 276f), ObjectiveTarget.Destructible(B11Truck2, 312f, 1.5f, 276f))),
            };
        }

        /// <summary>Destructible id of the first Clock Square truck in Beat 11 (requested from WP03/WP10 in Docs/REQUESTS.md).</summary>
        public const string B11Truck1 = "B11.Truck1";
        /// <summary>Destructible id of the second Clock Square truck in Beat 11.</summary>
        public const string B11Truck2 = "B11.Truck2";
    }
}
