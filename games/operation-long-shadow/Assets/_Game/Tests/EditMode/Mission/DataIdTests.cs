using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using LongShadow.Mission.Data;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: every line id a beat script uses exists in DIALOGUE.json; every marker id it uses exists in
    /// markup_ids.csv (Appendix A); the Appendix A fallbacks (marker positions, trigger shapes, critical-path knots) match
    /// the CSV.
    /// </summary>
    public class DataIdTests
    {
        [Test]
        public void EveryLineIdConstantExistsInDialogue()
        {
            var dialogue = GddTables.Dialogue;
            Assert.That(dialogue.Count, Is.GreaterThan(150), "DIALOGUE.json parsed");
            foreach (var id in LineIds.All) Assert.IsTrue(dialogue.ContainsKey(id), id + " is not in DIALOGUE.json");
        }

        [Test]
        public void LineIdsAllListsEveryConstant()
        {
            var all = new HashSet<string>(LineIds.All);
            foreach (var f in typeof(LineIds).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                Assert.IsTrue(all.Contains((string)f.GetRawConstantValue()), "LineIds.All misses " + f.Name);
            }
        }

        [Test]
        public void LineDurationsCoverEveryDialogueLine()
        {
            foreach (var id in GddTables.Dialogue.Keys) Assert.IsTrue(LineDurations.Contains(id), "LineDurations misses " + id);
            Assert.That(LineDurations.D039TokenOnsets.Length, Is.EqualTo(15), "D039 is 15 tokens on a 0.9 s grid");
        }

        [Test]
        public void BeatSourcesUseNoLiteralLineIds()
        {
            var folder = TestPaths.BeatSourceFolder;
            Assume.That(Directory.Exists(folder), "beat sources not found at " + folder);
            var rx = new Regex("\"(D\\d{3}|DI_\\d{2}|CO_[A-Z]{3}_\\d{2}|BK_[A-Z]{3}_\\d{2})\"");
            foreach (var file in Directory.GetFiles(folder, "*.cs"))
            {
                var m = rx.Match(File.ReadAllText(file));
                Assert.IsFalse(m.Success, Path.GetFileName(file) + " has the literal line id " + m.Value + " (use LineIds)");
            }
        }

        [Test]
        public void EveryMissionMarkerIdExistsInTheMarkupCsv()
        {
            var csv = MarkupCsv.Table;
            Assert.That(csv.Rows.Count, Is.GreaterThan(200), "markup_ids.csv parsed");
            var missing = new List<string>();
            foreach (var id in MissionMarkup.AllIds())
                if (!csv.Contains(id)) missing.Add(id);
            Assert.IsEmpty(missing, "ids not in markup_ids.csv: " + string.Join(", ", missing));
        }

        [Test]
        public void PendingIdsAreRequestedFromWp10()
        {
            Assert.That(MissionMarkers.PendingIds.Length, Is.EqualTo(2), "only the two Beat 11 trucks are pending");
            var requests = TestPaths.Read("Docs", "REQUESTS.md");
            foreach (var id in MissionMarkers.PendingIds)
                Assert.IsTrue(MarkupCsv.Table.Contains(id) || requests.Contains(id), id + " is neither in the CSV nor requested in Docs/REQUESTS.md");
        }

        [Test]
        public void FallbackPositionsMatchAppendixA()
        {
            var csv = MarkupCsv.Table;
            foreach (var id in MissionMarkers.FallbackIds)
            {
                Assert.IsTrue(csv.TryGet(id, out var row), id + " not in the CSV");
                Assert.IsTrue(row.HasPosition, id + " has no CSV position");
                MissionMarkers.TryGetFallback(id, out var p);
                Assert.That(Vector3.Distance(p, row.Position), Is.LessThan(0.05f), id + ": " + p + " vs CSV " + row.Position);
            }
            foreach (var cp in BeatTable.Checkpoints)
            {
                Assert.IsTrue(csv.TryGet(cp.Id, out var row), cp.Id);
                Assert.That(Vector3.Distance(cp.Position, row.Position), Is.LessThan(0.05f), cp.Id);
            }
        }

        [Test]
        public void TriggerShapesMatchAppendixA()
        {
            var csv = MarkupCsv.Table;
            foreach (var id in MissionTriggers.Ids)
            {
                Assert.IsTrue(csv.TryGet(id, out var row), id + " not in the CSV");
                Assert.IsTrue(MissionTriggers.TryGetShape(id, out var s), id);
                switch (s.Kind)
                {
                    case TriggerShapeKind.Sphere:
                        Assert.IsTrue(row.TryGetRadius(out var r), id + " is not a sphere in the CSV");
                        Assert.That(r, Is.EqualTo(s.Radius).Within(0.01f), id);
                        Assert.That(Vector3.Distance(s.Center, row.Position), Is.LessThan(0.05f), id);
                        break;
                    case TriggerShapeKind.Box:
                        Assert.IsTrue(row.TryGetBox(out var size), id + " is not a box in the CSV");
                        Assert.That(Vector3.Distance(s.Size, size), Is.LessThan(0.05f), id + " size " + s.Size + " vs " + size);
                        Assert.That(Vector3.Distance(s.Center, row.Position), Is.LessThan(0.05f), id + " centre " + s.Center + " vs " + row.Position);
                        break;
                    default:
                        Assert.That(row.Points.Count, Is.EqualTo(2), id + " segment");
                        Assert.That(Vector3.Distance(s.A, row.Points[0]), Is.LessThan(0.05f), id);
                        Assert.That(Vector3.Distance(s.B, row.Points[1]), Is.LessThan(0.05f), id);
                        break;
                }
            }
        }

        [Test]
        public void CriticalPathKnotsMatchAppendixA()
        {
            var csv = MarkupCsv.Table;
            for (var b = 1; b <= 10; b++)
            {
                Assert.IsTrue(csv.TryGet("CritPath.B" + b, out var row), "CritPath.B" + b);
                var k = CriticalPathKnots.For(b);
                Assert.That(k.Length, Is.EqualTo(row.Points.Count), "CritPath.B" + b + " knot count");
                for (var i = 0; i < k.Length; i++)
                    Assert.That(Vector3.Distance(k[i], row.Points[i]), Is.LessThan(0.05f), "CritPath.B" + b + " knot " + i);
            }
        }

        [Test]
        public void MissionCueIdsExistInTheManifest()
        {
            var manifest = TestPaths.Read("Docs", "ASSET_MANIFEST.json");
            foreach (var f in typeof(MissionCues).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                var id = (string)f.GetRawConstantValue();
                if (id == MissionCues.MusicMarket || id == MissionCues.MusicStation) id += id == MissionCues.MusicMarket ? "_LO" : "_LOW";
                StringAssert.Contains("\"id\": \"" + id + "\"", manifest, "manifest has no " + id);
            }
        }

        [Test]
        public void TheMenuValidatorFindsNoProblems()
        {
            var problems = LongShadow.Mission.Editor.MissionDataValidator.Validate(TestPaths.Root);
            Assert.IsEmpty(problems, LongShadow.Mission.Editor.MissionDataValidator.Report(problems));
        }

        [Test]
        public void IntelPhotosAndPropsExistInTheManifest()
        {
            var manifest = TestPaths.Read("Docs", "ASSET_MANIFEST.json");
            foreach (var i in IntelTable.All)
            {
                StringAssert.Contains("\"id\": \"" + i.PhotoManifestId + "\"", manifest, i.Id);
                if (i.PropManifestId != null) StringAssert.Contains("\"id\": \"" + i.PropManifestId + "\"", manifest, i.Id);
            }
        }
    }
}
