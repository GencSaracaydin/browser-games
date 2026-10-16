using System.Collections.Generic;
using System.IO;
using LongShadow.AI.Encounters;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using LongShadow.Core.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>EncounterTable (E2-E11) against GDD §7 / §14.1 and the Appendix A id contract (markup_ids.csv).</summary>
    public class EncounterTableTests
    {
        [Test]
        public void FiftyNineEnemiesInTheRightBeats()
        {
            Assert.That(EncounterTable.TotalEnemies, Is.EqualTo(EncounterTable.MissionTotal));
            Assert.That(EncounterTable.MissionTotal, Is.EqualTo(59));
            var expected = new Dictionary<string, int[]>
            {
                // id: { beat, enemies, max alive } (GDD §7.0 pacing table and the encounter headers)
                { EncounterTable.E2, new[] { 2, 6, 4 } },
                { EncounterTable.E3, new[] { 3, 9, 6 } },
                { EncounterTable.E4, new[] { 4, 8, 6 } },
                { EncounterTable.E5, new[] { 5, 3, 3 } },
                { EncounterTable.E6, new[] { 6, 8, 6 } },
                { EncounterTable.E7, new[] { 7, 5, 3 } },
                { EncounterTable.E9, new[] { 9, 9, 5 } },
                { EncounterTable.E10, new[] { 10, 7, 7 } },
                { EncounterTable.E11, new[] { 11, 4, 4 } },
            };
            Assert.That(EncounterTable.All.Count, Is.EqualTo(expected.Count));
            foreach (var kv in expected)
            {
                var e = EncounterTable.Get(kv.Key);
                Assert.IsNotNull(e, kv.Key);
                Assert.That(e.Beat, Is.EqualTo(kv.Value[0]), kv.Key + " beat");
                Assert.That(e.Enemies.Count, Is.EqualTo(kv.Value[1]), kv.Key + " enemies");
                Assert.That(e.MaxAlive, Is.EqualTo(kv.Value[2]), kv.Key + " max alive");
                Assert.That(e.MaxAlive, Is.LessThanOrEqualTo(EncounterTable.HardCap));
            }
            Assert.That(EncounterTable.HardCap, Is.EqualTo(12));
        }

        [Test]
        public void ArchetypeCountsMatchGdd14()
        {
            var counts = new Dictionary<ArchetypeId, int>();
            foreach (var e in EncounterTable.All)
                foreach (var d in e.Enemies)
                    counts[d.Archetype] = (counts.TryGetValue(d.Archetype, out var n) ? n : 0) + 1;
            foreach (var a in ArchetypeTable.All)
                Assert.That(counts.TryGetValue(a.Id, out var n) ? n : 0, Is.EqualTo(a.MissionCount), a.Name + " count (GDD §14.1)");
        }

        [Test]
        public void IdsAreUniqueAndWavesExist()
        {
            var ids = new HashSet<string>();
            foreach (var e in EncounterTable.All)
            {
                var waves = new HashSet<string>();
                foreach (var w in e.Waves) Assert.IsTrue(waves.Add(w.Id), e.Id + " duplicate wave " + w.Id);
                foreach (var d in e.Enemies)
                {
                    Assert.IsTrue(ids.Add(d.Id), "duplicate enemy id " + d.Id);
                    Assert.IsTrue(d.Id.StartsWith(e.Id + "-"), d.Id + " belongs to " + e.Id);
                    Assert.IsTrue(waves.Contains(d.Wave), d.Id + " wave " + d.Wave);
                    if (d.Kind == SpawnKind.Closet) Assert.IsNotNull(d.ClosetId, d.Id);
                    if (d.Kind == SpawnKind.Vehicle) Assert.IsNotNull(d.VehicleId, d.Id);
                    if (d.Role != PairRole.None) Assert.IsNotNull(d.PairId, d.Id);
                }
            }
        }

        [Test]
        public void GreycoatPairsHaveOneFlankerWithARouteAndOneSuppressor()
        {
            foreach (var e in EncounterTable.All)
            {
                var pairs = new Dictionary<string, List<EnemySpawnDef>>();
                foreach (var d in e.Enemies)
                {
                    if (d.PairId == null) continue;
                    if (!pairs.TryGetValue(d.PairId, out var list)) pairs[d.PairId] = list = new List<EnemySpawnDef>();
                    list.Add(d);
                }
                foreach (var kv in pairs)
                {
                    Assert.That(kv.Value.Count, Is.EqualTo(2), kv.Key);
                    var flankers = kv.Value.FindAll(d => d.Role == PairRole.Flanker);
                    var suppressors = kv.Value.FindAll(d => d.Role == PairRole.Suppressor);
                    Assert.That(flankers.Count, Is.EqualTo(1), kv.Key);
                    Assert.That(suppressors.Count, Is.EqualTo(1), kv.Key);
                    if (e.Id != EncounterTable.E9) Assert.IsNotNull(flankers[0].FlankRouteId, kv.Key + " flank route");
                }
            }
        }

        [Test]
        public void EveryReferencedIdIsInTheMarkupCsv()
        {
            var csv = MarkupIds.Load();
            Assert.That(csv.Rows.Count, Is.GreaterThan(200));
            var problems = EncounterValidation.Check(csv.Contains);
            Assert.That(problems, Is.Empty, "Appendix A ids missing from markup_ids.csv:\n" + string.Join("\n", problems));
        }

        [Test]
        public void TablePositionsMatchTheCsvWhereBothExist()
        {
            var csv = MarkupIds.Load();
            foreach (var e in EncounterTable.All)
                foreach (var d in e.Enemies)
                {
                    if (d.Kind == SpawnKind.Closet || d.Kind == SpawnKind.Vehicle) continue;
                    var id = d.PerchId ?? d.MarkerId;
                    if (id == null || !csv.TryGet(id, out var row) || !row.HasPosition) continue;
                    Assert.That((row.Position - d.Position).magnitude, Is.LessThan(0.6f), d.Id + " vs " + id);
                }
        }

        [Test]
        public void SpecialRowsCarryTheirGddRules()
        {
            var e2 = EncounterTable.Get(EncounterTable.E2);
            Assert.IsTrue(e2.GetEnemy("E2-01").Sentry && e2.GetEnemy("E2-02").Sentry, "GDD §7.2 smoking sentries");
            Assert.That(e2.GetEnemy("E2-06").DropWeapon, Is.EqualTo(false), "E2-06's RPK stays on its mount");
            Assert.That(EncounterTable.Get(EncounterTable.E3).GetEnemy("E3-05").DropWeapon, Is.EqualTo(true), "E3-05 drops the first RPK");
            Assert.That(EncounterTable.Get(EncounterTable.E6).GetEnemy("E6-05").DropWeapon, Is.EqualTo(true));
            var e4 = EncounterTable.Get(EncounterTable.E4);
            Assert.That(e4.GetEnemy("E4-01").Archetype, Is.EqualTo(ArchetypeId.TechnicalGunner));
            Assert.That(e4.GetEnemy("E4-05").HoldUntil, Is.Not.Empty, "lobby ambush holds until the side door opens");
            Assert.That(e4.GetEnemy("E4-03").PreferSquadUntil, Is.EqualTo("B4.T.Marquee"));
            Assert.That(EncounterTable.Get(EncounterTable.E9).GetEnemy("E9-04").FragOnArrival, Is.True);
            var e10 = EncounterTable.Get(EncounterTable.E10);
            Assert.That(e10.GetEnemy("E10-04").Nvg, Is.True);
            Assert.That(e10.GetEnemy("E10-07").StaysOnVehicle, Is.True);
            Assert.That(e10.GetEnemy("E10-07").Archetype, Is.EqualTo(ArchetypeId.LegionRPG));
            Assert.That(EncounterTable.Get(EncounterTable.E5).GetEnemy("E5-01").Archetype, Is.EqualTo(ArchetypeId.GreycoatMarksman));
            Assert.That(EncounterTable.Get(EncounterTable.E11).GetEnemy("E11-T3").WeaponOverride, Is.EqualTo(WeaponIds.RPG7));
        }

        [Test]
        public void EveryVariantExistsForEveryRow()
        {
            foreach (var e in EncounterTable.All)
                foreach (var d in e.Enemies)
                {
                    var id = d.Variant ?? EnemyVariants.DefaultFor(d.Archetype, d.Id);
                    var v = EnemyVariants.Get(id);
                    Assert.IsNotNull(v, d.Id + " variant " + id);
                    Assert.That(v.Archetype, Is.EqualTo(d.Archetype), d.Id);
                }
        }

        [Test]
        public void VariantBodiesAreManifestCharacters()
        {
            var manifest = File.ReadAllText(Path.Combine(BuilderUtil.ProjectRoot, "Docs", "ASSET_MANIFEST.json"));
            foreach (var v in EnemyVariants.All)
                Assert.That(manifest, Does.Contain("\"" + v.BodyManifestId + "\""), v.Id + " body " + v.BodyManifestId);
        }
    }
}
