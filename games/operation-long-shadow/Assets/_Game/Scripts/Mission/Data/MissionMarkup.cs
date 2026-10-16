using System.Collections.Generic;
using LongShadow.Core;

namespace LongShadow.Mission.Data
{
    /// <summary>
    /// Every scene id the mission addresses (markers, triggers, node sets, splines, destructibles, gates, checkpoints,
    /// crates, intel spots), from the Data tables: the id tests check them against <c>markup_ids.csv</c> and the smoke
    /// test checks that a beat's ids exist in the loaded scenes (TECH_PLAN §10.3 "required marker ids present").
    /// </summary>
    public static class MissionMarkup
    {
        /// <summary>Ids that are not plain markers in a fallback table (node sets, splines, heli frame, destructible art).</summary>
        public static readonly string[] Extra =
        {
            MissionMarkers.B1SquadStart, MissionMarkers.B2NodeSetA, MissionMarkers.B2NodeSetB, MissionMarkers.B3NodeSetM1,
            MissionMarkers.B3NodeSetM2, MissionMarkers.B3NodeSetM3, MissionMarkers.B3NodeSetM4, MissionMarkers.B4NodeSetQ1,
            MissionMarkers.B4NodeSetQ2, MissionMarkers.B5NodeSetR1, MissionMarkers.B5NodeSetR2, MissionMarkers.B5NodeSetR3,
            MissionMarkers.B5NodeSetMidA, MissionMarkers.B6NodeSetY1, MissionMarkers.B6NodeSetY2, MissionMarkers.B6NodeSetY3,
            MissionMarkers.B8NodeSetReel, MissionMarkers.B9NodeSetDefend, MissionMarkers.B10NodeSetP1, MissionMarkers.B6BreachWall,
            MissionMarkers.B9R1, MissionMarkers.B9R2, MissionMarkers.B9R3, MissionMarkers.B9R4, MissionMarkers.B10HeliApproach,
            MissionMarkers.B10HeliWaveOff, MissionMarkers.B11Exfil, MissionMarkers.B11TruckSquare, MissionMarkers.HeliDoorGunPivot,
            MissionMarkers.HeliHollis, MissionMarkers.HeliSparrow, MissionMarkers.HeliTinker,
            "B3.AW01", "B3.AW02", "B3.AW03", "B3.AW04", "B3.AW05", "B3.AW06", "B3.AW07", "B3.AW08",
        };

        /// <summary>Every id the mission uses (sorted, distinct).</summary>
        public static List<string> AllIds()
        {
            var set = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var id in MissionMarkers.FallbackIds) set.Add(id);
            foreach (var id in MissionTriggers.Ids) set.Add(id);
            foreach (var id in Extra) set.Add(id);
            foreach (var g in GateTable.All)
            {
                if (g.Kind == GateKind.Invulnerable)
                    foreach (var m in g.Members) set.Add(m);
                else set.Add(g.Id);
            }
            foreach (var cp in BeatTable.Checkpoints)
            {
                set.Add(cp.Id);
                if (!string.IsNullOrEmpty(cp.SquadNodeSet)) set.Add(cp.SquadNodeSet);
            }
            foreach (var b in BeatTable.Beats)
                foreach (var c in b.Crates) set.Add(c);
            foreach (var i in IntelTable.All)
                if (i.MarkerId != null) set.Add(i.MarkerId);
            foreach (var spec in ObjectiveTable.All)
                foreach (var t in spec.Targets)
                    if (t.Kind == ObjectiveTargetKind.Marker || t.Kind == ObjectiveTargetKind.Destructible) set.Add(t.Id);
            foreach (var p in MissionMarkers.PendingIds) set.Remove(p);
            return new List<string>(set);
        }

        /// <summary>
        /// Ids the loaded scenes must contain while a beat runs (the beat's own "Bn." ids, its checkpoints and crates;
        /// booth and heli frames excluded).
        /// </summary>
        public static List<string> Required(int beat)
        {
            var prefix = "B" + beat + ".";
            var def = BeatTable.Get(beat);
            var result = new List<string>();
            foreach (var id in AllIds())
            {
                if (id.StartsWith(prefix, System.StringComparison.Ordinal) && !IsSpline(id)) result.Add(id);
            }
            foreach (var cp in def.Checkpoints) result.Add(cp);
            foreach (var c in def.Crates) result.Add(c);
            return result;
        }

        private static bool IsSpline(string id) =>
            id == MissionMarkers.B10HeliApproach || id == MissionMarkers.B10HeliWaveOff || id == MissionMarkers.B11Exfil ||
            id == MissionMarkers.B11TruckSquare;

        /// <summary>The GDD §7.0 peak of living enemies per beat (E2: the six placed enemies, WP04 deviation logged).</summary>
        public static int PeakAlive(int beat)
        {
            switch (beat)
            {
                case 2: return 6;
                case 3: return 6;
                case 4: return 6;
                case 5: return 3;
                case 6: return 6;
                case 7: return 3;
                case 9: return 5;
                case 10: return 7;
                case 11: return 4;
                default: return 0;
            }
        }

        /// <summary>Hard cap on living enemies (GDD §14.3).</summary>
        public const int HardCap = 12;

        /// <summary>The section names a beat needs loaded (for the smoke test).</summary>
        public static List<SectionId> Sections(int beat) => new List<SectionId>(BeatTable.SectionsAtStart(beat));
    }
}
