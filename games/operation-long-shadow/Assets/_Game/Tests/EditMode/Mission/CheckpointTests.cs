using LongShadow.Core;
using LongShadow.Mission.Checkpoints;
using LongShadow.Mission.Data;
using LongShadow.Mission.Objectives;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>GDD §17 / TECH_PLAN §5.5: deferred saves, snapshots, highest checkpoint, in-place restore of the mission state.</summary>
    public class CheckpointTests
    {
        private sealed class Counter : ISaveable
        {
            public int Value;
            public string SaveKey => "Test.Counter";
            public void Capture(SaveWriter w) => w.Write("v", Value);
            public void Restore(SaveReader r) => Value = r.ReadInt("v");
        }

        [TearDown]
        public void TearDown() => GameServices.Clear();

        [Test]
        public void DeferralRule()
        {
            Assert.That(CheckpointDeferral.Evaluate(1, false, 100f), Is.EqualTo(CheckpointDeferral.Reason.EnemyNear));
            Assert.That(CheckpointDeferral.Evaluate(0, true, 100f), Is.EqualTo(CheckpointDeferral.Reason.GrenadeLive));
            Assert.That(CheckpointDeferral.Evaluate(0, false, 49f), Is.EqualTo(CheckpointDeferral.Reason.LowHealth));
            Assert.That(CheckpointDeferral.Evaluate(0, false, 50f), Is.EqualTo(CheckpointDeferral.Reason.None));
            Assert.IsFalse(CheckpointDeferral.ShouldCommit(CheckpointDeferral.Reason.EnemyNear, 9.9f));
            Assert.IsTrue(CheckpointDeferral.ShouldCommit(CheckpointDeferral.Reason.EnemyNear, 10f), "at most 10 s");
        }

        [Test]
        public void SaveIsDeferredWhileAnEnemyIsNearThenCommitsAfter10Seconds()
        {
            var now = 0f;
            var reason = CheckpointDeferral.Reason.EnemyNear;
            var cs = new CheckpointService(() => now, () => reason, null);
            var c = new Counter { Value = 1 };
            cs.Register(c);
            cs.Save(SequenceIds.CP3b);
            Assert.IsTrue(cs.SavePending);
            now = 5f;
            cs.Tick();
            Assert.IsTrue(cs.SavePending, "still deferred at 5 s");
            c.Value = 2;
            now = 10f;
            cs.Tick();
            Assert.IsFalse(cs.SavePending, "committed at 10 s");
            Assert.That(cs.Last, Is.EqualTo(SequenceIds.CP3b));
            c.Value = 3;
            cs.Restore(SequenceIds.CP3b);
            Assert.That(c.Value, Is.EqualTo(2), "the snapshot holds the state at commit time");
        }

        [Test]
        public void PendingSaveCommitsBeforeTheNextBeat()
        {
            var cs = new CheckpointService(() => 0f, () => CheckpointDeferral.Reason.LowHealth, null);
            cs.Save(SequenceIds.CP5);
            Assert.IsTrue(cs.SavePending);
            cs.CommitPending();
            Assert.IsFalse(cs.SavePending);
            Assert.IsTrue(cs.HasSnapshot(SequenceIds.CP5));
        }

        [Test]
        public void DirectorDefersOnLiveGrenadesAndLowHealth()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP3);
                w.Health.Current = 30f;
                w.Run(0.5f);
                w.Director.Checkpoints.Save(SequenceIds.CP3b);
                w.Run(1f);
                Assert.IsTrue(w.Director.Checkpoints.SavePending, "deferred below 50 HP");
                w.Health.Current = 100f;
                GameEvents.RaiseGrenadeThrown(UnityEngine.Vector3.zero, Team.Legion);
                w.Run(1f);
                Assert.IsTrue(w.Director.Checkpoints.SavePending, "deferred while a grenade is live");
                GameEvents.RaiseGrenadeExploded(UnityEngine.Vector3.zero, 8f);
                w.Run(0.5f);
                Assert.IsFalse(w.Director.Checkpoints.SavePending, "committed once safe");
            }
        }

        [Test]
        public void HighestCheckpointGoesToStorage()
        {
            using (var w = new FakeWorld())
            {
                var cs = w.Director.Checkpoints;
                cs.Save(SequenceIds.CP1);
                cs.Save(SequenceIds.CP3);
                cs.Save(SequenceIds.CP2);
                Assert.That(w.Settings.GetValue(StorageKeys.HighestCheckpoint), Is.EqualTo(SequenceIds.CP3));
            }
        }

        [Test]
        public void MissionObjectivesAndFlagsRoundTrip()
        {
            using (var w = new FakeWorld())
            {
                var d = w.Director;
                d.ResumeFrom(SequenceIds.CP7);
                w.Run(0.5f);
                d.SetFlag("Test.Flag", true);
                d.Objectives.CollectIntel(IntelTable.Ledgers);
                GameEvents.RaiseActorKilled(new KillInfo { VictimId = "E7-01", VictimTeam = Team.Legion });
                var snap = CheckpointService.Capture("T", d.Checkpoints.Saveables, 0f);
                d.SetFlag("Test.Flag", false);
                d.Objectives.ResetProgress();
                d.Log.Clear();
                snap.RestoreInto(d.Checkpoints.Saveables);
                Assert.IsTrue(d.Flag("Test.Flag"));
                Assert.IsTrue(d.Objectives.HasIntel(IntelTable.Ledgers));
                Assert.IsTrue(d.Objectives.IsComplete(ObjectiveTable.B7Ledgers));
                Assert.That(d.Objectives.IntelCount, Is.EqualTo(1), "a cold jump to CP7 has no earlier main intel; the ledgers make 1/3");
                Assert.IsTrue(d.Log.Happened(EventChannel.Killed, "E7-01"), "kills before the checkpoint stay dead");
            }
        }

        [Test]
        public void SaveKeysAreUnique()
        {
            using (var w = new FakeWorld())
            {
                var keys = new System.Collections.Generic.HashSet<string>();
                foreach (var s in w.Director.Checkpoints.Saveables) Assert.IsTrue(keys.Add(s.SaveKey), "duplicate save key " + s.SaveKey);
                CollectionAssert.IsSupersetOf(keys, new[] { MissionDirector.Key, ObjectiveService.Key, Gates.Key });
            }
        }
    }
}
