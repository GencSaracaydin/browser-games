using System.Collections.Generic;
using System.Text.RegularExpressions;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: every objective of GDD §7.12 exists with its exact id, text, marker kind, marker targets and completion
    /// rule (table-driven: the GDD table is parsed from Docs/GDD.md and compared cell by cell).
    /// </summary>
    public class ObjectiveTableTests
    {
        private static List<string[]> Rows => GddTables.Table("7.12 Objective master list");

        [Test]
        public void GddTableParses()
        {
            Assert.That(Rows.Count, Is.EqualTo(33), "GDD §7.12 has 33 objective rows");
        }

        [Test]
        public void SameIdsInTheSameOrder()
        {
            var rows = Rows;
            Assert.That(ObjectiveTable.All.Count, Is.EqualTo(rows.Count), "row count");
            for (var i = 0; i < rows.Count; i++)
                Assert.That(ObjectiveTable.All[i].Id, Is.EqualTo(GddTables.Code(rows[i][0])), "row " + i);
        }

        [Test]
        public void TextsMatch()
        {
            foreach (var row in Rows)
            {
                var spec = ObjectiveTable.Get(GddTables.Code(row[0]));
                Assert.IsNotNull(spec, row[0]);
                Assert.That(spec.Text, Is.EqualTo(row[1]), spec.Id);
            }
        }

        [Test]
        public void CompletionRulesMatch()
        {
            foreach (var row in Rows)
            {
                var spec = ObjectiveTable.Get(GddTables.Code(row[0]));
                Assert.That(spec.CompletionRule, Is.EqualTo(row[3]), spec.Id);
            }
        }

        [Test]
        public void OptionalFlagsAndBeatsMatch()
        {
            foreach (var row in Rows)
            {
                var id = GddTables.Code(row[0]);
                var spec = ObjectiveTable.Get(id);
                Assert.That(spec.Optional, Is.EqualTo(row[0].Contains("(optional)")), id);
                var beat = int.Parse(Regex.Match(id, @"^B(\d+)\.").Groups[1].Value);
                Assert.That(spec.Beat, Is.EqualTo(beat), id);
                if (spec.Optional) Assert.That(spec.ProximityOnly, Is.EqualTo(6f), id + ": optional intel shows within 6 m");
            }
        }

        [Test]
        public void MarkerKindsMatch()
        {
            foreach (var row in Rows)
            {
                var spec = ObjectiveTable.Get(GddTables.Code(row[0]));
                Assert.That(spec.Kind, Is.EqualTo(KindOf(row[2])), spec.Id + " marker '" + row[2] + "'");
                if (spec.Kind == MarkerKind.HighPriority) Assert.IsTrue(spec.ThroughWalls, spec.Id + " red markers show through walls");
            }
        }

        [Test]
        public void MarkerTargetsMatch()
        {
            foreach (var row in Rows)
            {
                var spec = ObjectiveTable.Get(GddTables.Code(row[0]));
                var expected = new HashSet<string>();
                foreach (var code in GddTables.Codes(row[2]))
                {
                    if (Regex.IsMatch(code, @"^D\d{3}$")) continue; // a line id, not a marker
                    if (!Regex.IsMatch(code, @"^B\d+\.[A-Za-z0-9.]+$")) continue; // prompts ("[E] Board") and other text
                    foreach (var id in Expand(code)) expected.Add(id);
                }
                foreach (Match m in Regex.Matches(row[2], @"\bE\d+-\d+\b")) expected.Add(m.Value);
                var actual = new HashSet<string>();
                foreach (var t in spec.Targets)
                    if (t.Kind != ObjectiveTargetKind.SquadMember) actual.Add(t.Id);
                if (spec.Id == ObjectiveTable.B2Sentries)
                {
                    // TargetX on E2-01/02 appear within 3 m of the firing position (script-managed markers).
                    expected.Remove("E2-01");
                    expected.Remove("E2-02");
                }
                if (spec.Id == ObjectiveTable.B6Yard || spec.Id == ObjectiveTable.B11Cover) continue; // F1-F4 / T1-T4 + trucks: script-managed
                CollectionAssert.AreEquivalent(expected, actual, spec.Id);
                if (row[2].StartsWith("Follow"))
                    Assert.That(spec.Targets[0].Kind, Is.EqualTo(ObjectiveTargetKind.SquadMember), spec.Id + " follows Hollis");
            }
        }

        [Test]
        public void ScriptManagedMarkersCoverTheirTargets()
        {
            var yard = ObjectiveTable.Get(ObjectiveTable.B6Yard);
            Assert.IsFalse(yard.AutoMarkers);
            CollectionAssert.AreEquivalent(new[] { "B6.F1", "B6.F2", "B6.F3", "B6.F4" }, Ids(yard));
            var cover = ObjectiveTable.Get(ObjectiveTable.B11Cover);
            Assert.IsFalse(cover.AutoMarkers);
            CollectionAssert.IsSubsetOf(new[] { "E11-T1", "E11-T2", "E11-T3", "E11-T4" }, Ids(cover));
        }

        [Test]
        public void HudLineFormatAndFlavourGroup()
        {
            var spec = ObjectiveTable.Get(ObjectiveTable.B4Technical);
            var def = spec.ToDef();
            Assert.That(def.Index, Is.EqualTo(4));
            Assert.That(def.Total, Is.EqualTo(11));
            Assert.That(def.FlavourGroup, Is.EqualTo((MathX.Fnv1a32("B4.Technical") % 100000u).ToString("D5")));
            Assert.That(ObjectiveFormat.HudLine(def), Is.EqualTo("OBJ 4/11 — " + def.FlavourGroup + " — NEUTRALIZE THE TECHNICAL"));
            Assert.That(ObjectiveFormat.WithCounter("OBSERVE ZAROUD 0/3", 2, 3), Is.EqualTo("OBSERVE ZAROUD 2/3"));
            Assert.That(ObjectiveFormat.WithCounter("DESTROY THE TRANSMITTER 0/4", 4, 4), Is.EqualTo("DESTROY THE TRANSMITTER 4/4"));
        }

        [Test]
        public void EveryBeatListsItsObjectivesInOrder()
        {
            var specs = new List<ObjectiveSpec>();
            for (var b = 1; b <= BeatTable.PlayableBeats; b++)
            {
                ObjectiveTable.ForBeat(b, specs);
                var ids = new List<string>();
                foreach (var s in specs) ids.Add(s.Id);
                CollectionAssert.AreEqual(ids, BeatTable.Get(b).Objectives, "beat " + b);
            }
        }

        private static IEnumerable<string> Ids(ObjectiveSpec spec)
        {
            foreach (var t in spec.Targets) yield return t.Id;
        }

        private static MarkerKind? KindOf(string cell)
        {
            var head = cell.Split('→')[0].Trim();
            if (head.StartsWith("none")) return null;
            if (head.StartsWith("Diamond (hollow)")) return MarkerKind.DiamondHollow;
            if (head.StartsWith("Diamond")) return MarkerKind.Diamond;
            if (head.StartsWith("Follow")) return MarkerKind.Follow;
            if (head.StartsWith("HighPriority")) return MarkerKind.HighPriority;
            if (head.StartsWith("Ring")) return MarkerKind.Ring;
            if (head.StartsWith("Intel")) return MarkerKind.Intel;
            if (head.StartsWith("TargetX")) return MarkerKind.TargetX;
            Assert.Fail("unknown marker kind '" + head + "'");
            return null;
        }

        private static IEnumerable<string> Expand(string code)
        {
            // "B1.POI1..3" → B1.POI1, B1.POI2, B1.POI3; "B9.R1..R4" → B9.R1 … B9.R4.
            var m = Regex.Match(code, @"^(.*?)(\d+)\.\.[A-Za-z]*(\d+)$");
            if (!m.Success)
            {
                yield return code;
                yield break;
            }
            var from = int.Parse(m.Groups[2].Value);
            var to = int.Parse(m.Groups[3].Value);
            for (var i = from; i <= to; i++) yield return m.Groups[1].Value + i;
        }
    }
}
