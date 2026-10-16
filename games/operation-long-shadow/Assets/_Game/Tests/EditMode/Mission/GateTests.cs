using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: every gate of GDD §7.13 and every backtrack blocker of GDD §6.7 exists, opens and closes at its rule
    /// (observed in the full autopiloted run) and survives a checkpoint round-trip.
    /// </summary>
    public class GateTests
    {
        [TearDown]
        public void TearDown() => GameServices.Clear();

        [Test]
        public void GateTableMatchesGdd713()
        {
            var rows = GddTables.Table("7.13 Progression gates");
            Assert.That(rows.Count, Is.EqualTo(10), "GDD §7.13 rows");
            var matched = new HashSet<string>();
            foreach (var row in rows)
            {
                GateDef found = null;
                foreach (var g in GateTable.All)
                    if (GateTable.GddName(g.Id) == row[0]) found = g;
                Assert.IsNotNull(found, "no gate for GDD row '" + row[0] + "'");
                Assert.That(found.Blocks, Is.EqualTo(row[1]), found.Id + " blocks");
                Assert.That(found.Rule, Is.EqualTo(row[2]), found.Id + " opens when");
                matched.Add(found.Id);
            }
            Assert.That(matched.Count, Is.EqualTo(10));
        }

        [Test]
        public void BlockersMatchGdd67()
        {
            var text = GddTables.Section("6.7 Streaming sections and bundles");
            StringAssert.Contains("Beat 4 start: the Weighbridge gate swings shut", text);
            StringAssert.Contains("Beat 5 start: PlayerClip across the market arch (z 250) and the side alley (z 246)", text);
            StringAssert.Contains("Beat 6 start: the R4 stair top is clipped", text);
            StringAssert.Contains("Beat 7 start: PlayerClip in the breach hole", text);
            StringAssert.Contains("Beat 10 start: the postern door closes", text);
            var expected = new Dictionary<string, int>
            {
                { GateTable.BlockMarketSouth, 4 }, { GateTable.Weighbridge, 4 }, { GateTable.BlockMarketArch, 5 }, { GateTable.BlockSideAlley, 5 },
                { GateTable.BlockR4Stair, 6 }, { GateTable.BlockBreach, 7 }, { GateTable.BlockPostern, 10 },
            };
            foreach (var kv in expected)
            {
                Assert.That(GateTable.Get(kv.Key).ClosesInBeat, Is.EqualTo(kv.Value), kv.Key);
                CollectionAssert.Contains(BeatTable.Get(kv.Value).CloseOnEnter, kv.Key, kv.Key + " closes at beat " + kv.Value + " start");
            }
        }

        [Test]
        public void EveryGateOpensInItsBeatInTheFullRun()
        {
            FullRun.AssertFinished();
            var changes = FullRun.Get().GateChanges;
            foreach (var g in GateTable.All)
            {
                if (g.Kind == GateKind.Blocker) continue;
                if (g.Id == GateTable.UseSideDoor || g.Id == GateTable.UseHallDoor || g.Id == GateTable.UseProjector || g.Id == GateTable.UseHeliDoor ||
                    g.Id == GateTable.ArmLanding || g.Id == GateTable.ArmBasementFoot || g.Kind == GateKind.Gate || g.Kind == GateKind.Invulnerable)
                {
                    var opened = changes.Find(c => c.Id == g.Id && c.Open);
                    Assert.IsNotNull(opened.Id, g.Id + " never opened");
                    Assert.That(opened.Beat, Is.EqualTo(g.OpensInBeat), g.Id + " opened in the wrong beat");
                }
            }
        }

        [Test]
        public void EveryBlockerClosesAtItsBeatStartInTheFullRun()
        {
            FullRun.AssertFinished();
            var changes = FullRun.Get().GateChanges;
            foreach (var g in GateTable.All)
            {
                if (g.ClosesInBeat <= 0) continue;
                if (g.Kind == GateKind.Interaction) continue; // interactions close when used (or at the end of their beat)
                var closed = changes.Find(c => c.Id == g.Id && !c.Open && c.Beat == g.ClosesInBeat);
                Assert.IsNotNull(closed.Id, g.Id + " did not close at beat " + g.ClosesInBeat + " (" + string.Join(", ", changes) + ")");
            }
        }

        [Test]
        public void GatesBlockUntilTheirRuleAtEveryBeatStart()
        {
            var gates = new Gates();
            for (var b = 0; b <= BeatTable.Outro; b++)
            {
                gates.ResetToBeat(b, false);
                foreach (var g in GateTable.All)
                    Assert.That(gates.IsOpen(g.Id), Is.EqualTo(g.OpenAtBeatStart(b)), g.Id + " at beat " + b);
            }
            gates.ResetToBeat(9, false);
            Assert.IsFalse(gates.IsOpen(GateTable.Racks), "racks invulnerable at the Beat 9 start");
            Assert.IsFalse(gates.IsOpen(GateTable.TunnelDoor));
            Assert.IsTrue(gates.IsOpen(GateTable.WestStairDoor), "opened in Beat 8");
            Assert.IsFalse(gates.IsOpen(GateTable.BlockBreach), "closed since Beat 7");
        }

        [Test]
        public void GateStateSurvivesACheckpointRoundTrip()
        {
            var gates = new Gates();
            gates.ResetToBeat(4, false);
            gates.Set(GateTable.CinemaSideDoor, true);
            gates.Set(GateTable.LadderL, true);
            var rec = new SaveRecord { Key = Gates.Key };
            gates.Capture(new SaveWriter(rec));
            var expected = new Dictionary<string, bool>();
            foreach (var g in GateTable.All) expected[g.Id] = gates.IsOpen(g.Id);
            gates.ResetToBeat(10, false);
            gates.Set(GateTable.CinemaSideDoor, false);
            gates.Restore(new SaveReader(rec));
            foreach (var g in GateTable.All) Assert.That(gates.IsOpen(g.Id), Is.EqualTo(expected[g.Id]), g.Id);
        }

        [Test]
        public void ReloadRestoresGatesToTheCheckpoint()
        {
            using (var w = new FakeWorld())
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP9);
                Assert.IsTrue(w.RunUntil(() => w.Director.Gates.IsOpen(GateTable.Racks), 300f), "racks released on D065");
                w.AutopilotOn = false;
                w.Director.Checkpoints.Reload(false);
                Assert.IsTrue(w.RunUntil(() => !w.Director.IsReloading, 10f));
                Assert.IsFalse(w.Director.Gates.IsOpen(GateTable.Racks), "racks invulnerable again after reloading CP9");
                Assert.IsTrue(w.Destructibles[MissionMarkers.B9R1].Invulnerable, "pushed to the destructible");
            }
        }
    }
}
