using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Beats;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// WP07 DoD: every beat script runs start to end against a scripted fake world, each beat can resume from each of its
    /// checkpoints, no beat can be skipped (reaching its exit before its objectives does not end it), the death flow
    /// reloads the checkpoint in place, the intro → B1 … B11 → outro → debrief state machine runs end to end.
    /// </summary>
    public class MissionFlowTests
    {
        private static readonly string[] s_checkpoints = SequenceIds.Checkpoints;

        [TearDown]
        public void TearDown() => GameServices.Clear();

        [Test]
        public void FullMissionRunsFromTheIntroToTheDebrief()
        {
            FullRun.AssertFinished();
            var r = FullRun.Get();
            var expected = new List<int>();
            for (var b = 0; b <= BeatTable.Outro; b++) expected.Add(b);
            CollectionAssert.AreEqual(expected, r.BeatsStarted, "beats started in order");
            CollectionAssert.AreEqual(expected, r.BeatsCompleted, "beats completed in order");
            Assert.IsNotNull(r.Debrief, "debrief shown");
            Assert.That(r.IntelCount, Is.EqualTo(3), "INTEL 3/3");
            Assert.That(r.OptionalCount, Is.EqualTo(0), "the autopilot never detours for optional intel");
            Assert.That(r.HighestCheckpoint, Is.EqualTo(SequenceIds.CP10), "highest checkpoint stored");
            Assert.That(r.LiveTimeScales, Is.EqualTo(0), "every time-scale request popped");
        }

        [Test]
        public void EveryRequiredObjectiveCompletes()
        {
            FullRun.AssertFinished();
            var r = FullRun.Get();
            foreach (var spec in ObjectiveTable.All)
                if (!spec.Optional) Assert.IsTrue(r.Completed.Contains(spec.Id), spec.Id + " never completed");
        }

        [Test]
        public void SetPieceEventsAreRaised()
        {
            FullRun.AssertFinished();
            var e = FullRun.Get().MissionEvents;
            foreach (var id in new[]
                     {
                         SequenceIds.IntroControlToPlayer, MissionEvents.WeighbridgeOpen, MissionEvents.TechnicalOpenFire,
                         MissionEvents.CinemaSkySwap, SequenceIds.MidAStart, SequenceIds.MidAEnd, MissionEvents.B6Blast,
                         MissionEvents.FloodsOn, MissionEvents.HallBreach, MissionEvents.DrillStarted, MissionEvents.TransmitterDead,
                         MissionEvents.HeliApproach, MissionEvents.HeliWheelsDown, MissionEvents.ExfilStart, MissionEvents.ClimaxStart,
                         MissionEvents.Debrief, MissionMarkers.B10FL1, MissionMarkers.B10FL2,
                     })
                CollectionAssert.Contains(e, id, id);
            CollectionAssert.Contains(e, MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate), "gate team alerted");
        }

        [TestCaseSource(nameof(s_checkpoints))]
        public void EveryCheckpointResumesAndItsBeatRunsToTheEnd(string cp)
        {
            var beat = BeatTable.BeatOf(cp);
            using (var w = new FakeWorld())
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(cp);
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == beat, 5f), cp + " entered beat " + beat);
                var ok = w.RunUntil(() => w.BeatsCompleted.Contains(beat), 900f);
                Assert.IsTrue(ok, cp + ": beat " + beat + " did not finish: " + FullRun.Describe(w) + "\n" + string.Join("\n", w.Pilot.Log));
                foreach (var id in BeatTable.Get(beat).Objectives)
                {
                    var spec = ObjectiveTable.Get(id);
                    if (!spec.Optional) Assert.IsTrue(w.Director.Objectives.IsComplete(id), cp + ": " + id + " not complete");
                }
            }
        }

        [Test]
        public void Checkpoint3bResumesAtTheCrossing()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP3b);
                w.Run(3f);
                Assert.That(w.Squad.CurrentSet, Is.EqualTo(MissionMarkers.B3NodeSetM3), "squad at M3");
                Assert.IsTrue(w.Encounters.Has("TriggerWave", "E3/B"), "wave B triggered at the Crossing");
                Assert.IsFalse(w.Director.Lines.WasRequested(LineIds.D021), "the entry line does not replay at CP3b");
                Assert.IsTrue(w.Director.Lines.WasRequested(LineIds.D025), "the Crossing PA plays");
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void NoBeatCanBeSkippedByReachingItsExitFirst(int beat)
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(BeatTable.StartCheckpoint(beat));
                w.Run(1f);
                Assert.That(w.Director.Beat, Is.EqualTo(beat));
                ReachExit(w, beat);
                w.Run(8f);
                Assert.That(w.Director.Beat, Is.EqualTo(beat), "beat " + beat + " ended when the player reached its exit first");
                var required = new List<string>();
                foreach (var id in BeatTable.Get(beat).Objectives)
                    if (!ObjectiveTable.Get(id).Optional) required.Add(id);
                Assert.IsFalse(w.Director.Objectives.IsComplete(required[required.Count - 1]), "the last objective completed early");

                w.AutopilotOn = true;
                var ok = w.RunUntil(() => w.BeatsCompleted.Contains(beat), 900f);
                Assert.IsTrue(ok, "beat " + beat + " never finished after the early exit: " + FullRun.Describe(w));
                foreach (var id in required) Assert.IsTrue(w.Director.Objectives.IsComplete(id), id);
            }
        }

        [Test]
        public void Beat11RunsOnRailsUntilTheRpgLaunch()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP10);
                w.AutopilotOn = true;
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == 11, 600f), "beat 11 reached");
                var start = w.Now;
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == 12, 120f), "climax → outro");
                Assert.That(w.Now - start, Is.GreaterThanOrEqualTo(ExfilTimeline.T3Fire), "the climax waits for t 30");
                Assert.IsTrue(w.Rig.Has("DamageModifiers"), "door-gun damage modifiers");
                Assert.IsTrue(w.Director.Lines.WasRequested(LineIds.D081));
                Assert.IsTrue(w.Cinematics.Has("Cinematic", SequenceIds.Climax));
            }
        }

        [Test]
        public void DeathReloadsTheCheckpointInPlace()
        {
            using (var w = new FakeWorld())
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP4);
                Assert.IsTrue(w.RunUntil(() => w.Director.Objectives.IsComplete(ObjectiveTable.B4Flank), 200f), "side door opened");
                Assert.IsTrue(w.Director.Checkpoints.HasSnapshot(SequenceIds.CP4), "CP4 saved");
                var killedBefore = w.Encounters.Killed(EncounterIds.E4);
                w.AutopilotOn = false;
                GameEvents.RaisePlayerDied(new DamageInfo());
                Assert.IsTrue(w.Director.IsReloading, "death flow started");
                Assert.IsTrue(w.RunUntil(() => !w.Director.IsReloading, 10f), "reload finished");
                Assert.IsTrue(w.Inserts.Has("Death"), "death insert played");
                Assert.IsTrue(w.TimeScale.Has("Push", "Death"), "death slow-down");
                Assert.That(w.TimeScale.Live, Is.EqualTo(0), "death slow-down released");
                Assert.IsTrue(w.Hud.Has("Banner", MissionDirector.ResumingText));
                Assert.That(w.Director.Beat, Is.EqualTo(4));
                Assert.IsFalse(w.Director.Objectives.IsComplete(ObjectiveTable.B4Flank), "objectives back to CP4");
                Assert.IsFalse(w.Director.Gates.IsOpen(GateTable.CinemaSideDoor), "the side door is closed again");
                Assert.That(w.Encounters.Killed(EncounterIds.E4), Is.LessThanOrEqualTo(killedBefore));
                Assert.That(w.InputLock.Live, Is.EqualTo(0), "input unlocked");
                w.AutopilotOn = true;
                Assert.IsTrue(w.RunUntil(() => w.BeatsCompleted.Contains(4), 600f), "beat 4 completes after the reload");
            }
        }

        [Test]
        public void RestartCheckpointReloadsWithoutTheDeathInsert()
        {
            using (var w = new FakeWorld())
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP6);
                Assert.IsTrue(w.RunUntil(() => w.Director.Flag(MissionFlags.B6Blown), 200f), "wall blown");
                w.AutopilotOn = false;
                w.Director.Checkpoints.Reload(false);
                Assert.IsTrue(w.RunUntil(() => !w.Director.IsReloading, 10f));
                Assert.IsFalse(w.Inserts.Has("Death"), "no death insert");
                Assert.IsFalse(w.Director.Flag(MissionFlags.B6Blown), "flags back to CP6");
                w.AutopilotOn = true;
                Assert.IsTrue(w.RunUntil(() => w.BeatsCompleted.Contains(6), 600f), "beat 6 completes after the restart");
            }
        }

        [Test]
        public void TheCleanSyncShotPlaysInsertI1()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP2);
                w.Run(1f);
                w.Enter(MissionTriggers.B2FiringPos);
                // The player kills the left sentry with the suppressed pistol on "Mark".
                Assert.IsTrue(w.RunUntil(() => w.Squad.Has("ScriptedKill"), 20f), "Hollis fires on Mark");
                GameEvents.RaiseShotFired(new ShotEvent { IsPlayer = true, WeaponId = WeaponIds.HushPuppy, Team = Team.Player });
                w.Encounters.Kill(EncounterIds.SentryLeft);
                Assert.IsTrue(w.RunUntil(() => w.Inserts.Has("Insert", SequenceIds.InsertI1 + "+" + LineIds.D017), 5f), "I1 + D017");
                Assert.IsTrue(w.Director.Flag(MissionFlags.B2Clean));
                Assert.IsFalse(w.Director.Lines.WasRequested(LineIds.D016), "no D016 on the clean branch");
                CollectionAssert.DoesNotContain(w.MissionEvents, MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate),
                    "the gate team starts unaware");
            }
        }

        [Test]
        public void FiringBeforeTheMarkTakesTheFailureBranch()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP2);
                w.Run(1f);
                GameEvents.RaiseShotFired(new ShotEvent { IsPlayer = true, WeaponId = WeaponIds.Commando, Team = Team.Player });
                w.Run(1f);
                Assert.IsTrue(w.Director.Lines.WasRequested(LineIds.D016), "D016");
                CollectionAssert.Contains(w.MissionEvents, MissionEvents.Run(EncounterIds.SentryLeft));
                CollectionAssert.Contains(w.MissionEvents, MissionEvents.Alert(EncounterIds.E2, EncounterIds.E2Gate));
                Assert.IsFalse(w.Director.Flag(MissionFlags.B2Clean));
            }
        }

        [Test]
        public void ColdJumpsRebuildEarlierProgress()
        {
            using (var w = new FakeWorld())
            {
                w.Director.ResumeFrom(SequenceIds.CP9);
                w.Run(1f);
                Assert.That(w.Director.Objectives.IntelCount, Is.EqualTo(2), "ledgers and reels before beat 9");
                Assert.IsTrue(w.Director.Objectives.IsComplete(ObjectiveTable.B8Film));
                Assert.IsTrue(w.Director.Gates.IsOpen(GateTable.WestStairDoor), "gates of earlier beats open");
                Assert.IsFalse(w.Director.Gates.IsOpen(GateTable.TunnelDoor), "the tunnel door is shut until D066");
                Assert.IsFalse(w.Director.Gates.IsOpen(GateTable.Racks), "racks invulnerable until D065");
                Assert.IsTrue(w.Destructibles[MissionMarkers.B9R1].Invulnerable, "racks invulnerable (destructible state)");
                foreach (var s in BeatTable.SectionsAtStart(9)) Assert.IsTrue(w.Streamer.IsLoaded(s), s + " loaded");
                Assert.That(w.Light.S, Is.EqualTo(2.80f).Within(0.001f), "LightStage at the beat start");
            }
        }

        [Test]
        public void EveryLineTheScriptsRequestExistsInDialogue()
        {
            FullRun.AssertFinished();
            var dialogue = GddTables.Dialogue;
            foreach (var id in FullRun.Get().LinesRequested) Assert.IsTrue(dialogue.ContainsKey(id), id + " is not in DIALOGUE.json");
            // Lines on the other branches (clean sync shot, failure branch) are covered by their own tests; LineIds.All by DataIdTests.
        }

        private static void ReachExit(FakeWorld w, int beat)
        {
            var exit = BeatTable.Get(beat).ExitTrigger;
            if (MissionTriggers.TryGetShape(exit, out _))
            {
                w.Enter(exit);
                return;
            }
            if (beat == 4)
            {
                MissionMarkers.TryGetFallback(SequenceIds.CP5, out var top);
                w.PlayerPos = top;
                return;
            }
            GameEvents.RaiseInteracted(exit); // B6.HallDoor, B10.HeliDoor
        }
    }
}
