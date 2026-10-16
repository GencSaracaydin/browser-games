using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Mission.Data;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>
    /// GDD §8.2 (+ CINEMATIC_DIRECTION §2), §8.3, §8.6, §8.8: with WP09's Timelines the mission hands over to
    /// ICinematicService; without them the Mission fallback flows play the lines on the GDD timings. Both paths are tested.
    /// </summary>
    public class SequenceTimingTests
    {
        private const float Tol = 0.3f;

        [TearDown]
        public void TearDown() => GameServices.Clear();

        private static Dictionary<string, float> Starts(FakeWorld w, float t0)
        {
            var d = new Dictionary<string, float>();
            foreach (var c in w.VO.Calls)
                if (c.Name == "Started" && !d.ContainsKey(c.Arg)) d[c.Arg] = c.Time - t0;
            return d;
        }

        [Test]
        public void IntroFallbackPlaysTheGddScheduleAndHandsOverAtShot9()
        {
            using (var w = new FakeWorld(withCinematics: false))
            {
                w.Director.StartMission();
                var t0 = w.Now;
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == 1, 200f), "Beat 1 after the intro");
                var beat1 = w.Now - t0;
                Assert.That(beat1, Is.EqualTo(SequenceTimelines.IntroControl).Within(Tol), "control at shot 9 + 2 s");
                var s = Starts(w, t0);
                Assert.That(s[LineIds.D001], Is.EqualTo(25f).Within(Tol), "D001 at 0:25");
                Assert.That(s[LineIds.D002], Is.EqualTo(44f).Within(Tol), "D002 at 0:44");
                Assert.That(s[LineIds.D003], Is.EqualTo(51f).Within(Tol), "D003 at 0:51");
                Assert.That(s[LineIds.D004], Is.EqualTo(59f).Within(Tol), "D004 at 0:59");
                Assert.That(s[LineIds.D005], Is.EqualTo(68f).Within(Tol), "D005 at 1:08");
                Assert.That(s[LineIds.D006], Is.EqualTo(79f).Within(Tol), "D006 at 1:19");
                Assert.That(s[LineIds.D200], Is.EqualTo(93f + 5f).Within(Tol), "D200 at INSERTION 0:05");
                Assert.That(s[LineIds.D204], Is.EqualTo(93f + 29f).Within(Tol), "D204 at INSERTION 0:29");
                Assert.That(s[LineIds.D206], Is.EqualTo(93f + 44f).Within(Tol), "D206 at INSERTION 0:44");
                Assert.That(s[LineIds.D203], Is.EqualTo(s[LineIds.D202] + w.VO.LengthOf(LineIds.D202) + 0.3f).Within(Tol), "D203 0.3 s after D202");
                Assert.IsTrue(w.Music.Has("Cue", MissionCues.MusicSession), "MUS_01_SESSION");
                Assert.That(w.PlayerPos, Is.EqualTo(new UnityEngine.Vector3(42f, 22f, 30f)), "prone on the ridge at CP1");
                // Title card fallback (GDD §8.3): 8 s of look-only, then D007.
                w.Run(0.2f);
                Assert.IsTrue(w.Hud.Has("TitleCard"), "title card");
                Assert.IsTrue(w.InputLock.Has("Lock", "Title card"));
                Assert.IsTrue(w.RunUntil(() => w.Director.Lines.WasRequested(LineIds.D007), 15f), "D007 when the card ends");
                Assert.That(w.Now - t0 - beat1, Is.EqualTo(SequenceTimelines.TitleCardSeconds + 0.8f).Within(0.5f), "card 8 s + kneel 0.8 s");
                Assert.That(w.InputLock.Live, Is.EqualTo(0), "control returns");
            }
        }

        [Test]
        public void IntroWithTimelinesWaitsForTheControlSignal()
        {
            using (var w = new FakeWorld())
            {
                w.Cinematics.Length = 30f;
                w.Director.StartMission();
                w.Run(10f);
                Assert.That(w.Director.Beat, Is.EqualTo(0), "the intro runs until TL_Intro raises Intro.ControlToPlayer");
                GameEvents.RaiseMissionEvent(SequenceIds.IntroControlToPlayer);
                w.Run(0.5f);
                Assert.That(w.Director.Beat, Is.EqualTo(1));
                Assert.IsTrue(w.Cinematics.Has("Cinematic", SequenceIds.Intro));
                Assert.That(w.Settings.GetValue(StorageKeys.IntroSeen), Is.EqualTo("1"));
            }
        }

        [Test]
        public void OutroFallbackPlaysTheGddScheduleThenTheDebrief()
        {
            using (var w = new FakeWorld(withCinematics: false))
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP10);
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == BeatTable.Outro, 900f), "outro reached");
                var t0 = w.Now;
                Assert.IsTrue(w.RunUntil(() => w.Director.Finished, 200f), "debrief");
                Assert.That(w.Now - t0, Is.EqualTo(SequenceTimelines.OutroSeconds).Within(0.5f), "outro 1:50");
                var s = Starts(w, t0);
                Assert.That(s[LineIds.D083], Is.EqualTo(4f).Within(Tol), "D083 at 0:04");
                Assert.That(s[LineIds.D085], Is.EqualTo(15f).Within(Tol), "D085 at 0:15");
                Assert.That(s[LineIds.D087], Is.EqualTo(40f).Within(Tol), "D087 at 0:40");
                Assert.That(s[LineIds.D092], Is.EqualTo(100f).Within(Tol), "D092 at 1:40");
                Assert.That(s[LineIds.D084], Is.EqualTo(s[LineIds.D083] + w.VO.LengthOf(LineIds.D083) + 1.2f).Within(Tol), "D084 1.2 s after D083");
                Assert.IsTrue(w.Music.Has("Cue", MissionCues.MusicSixForty));
                Assert.IsNotNull(w.Hud.Debrief, "ShowDebrief(stats)");
                Assert.IsFalse(w.Director.IsRunning, "IsRunning ends at the debrief");
                Assert.That(w.Settings.GetValue(StorageKeys.CompletedOnce), Is.EqualTo("1"), "SELECT BEAT unlocks");
            }
        }

        [Test]
        public void MidBFallbackPlaysI3WithD054ThenD055()
        {
            using (var w = new FakeWorld(withCinematics: false))
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP8);
                Assert.IsTrue(w.RunUntil(() => w.Director.Lines.WasRequested(LineIds.D055), 200f), "MID-B ran");
                Assert.IsTrue(w.Inserts.Has("Insert", SequenceIds.InsertI3 + "+" + LineIds.D054), "I3 + D054 at shot 7");
                var i3 = w.Inserts.FirstTime("Insert", SequenceIds.InsertI3 + "+" + LineIds.D054);
                var d055 = w.VO.FirstTime("Started", LineIds.D055);
                Assert.That(d055 - i3, Is.EqualTo(4f).Within(Tol), "D055 at 0:40, 4 s after I3 at 0:36");
                Assert.IsTrue(w.RunUntil(() => w.BeatsCompleted.Contains(8), 200f));
            }
        }

        [Test]
        public void ClimaxFallbackRaisesThePistolAndCutsToWhite()
        {
            using (var w = new FakeWorld(withCinematics: false))
            {
                w.AutopilotOn = true;
                w.Director.ResumeFrom(SequenceIds.CP10);
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == 11, 600f));
                Assert.IsTrue(w.RunUntil(() => w.Director.Beat == 12, 120f), "climax done");
                var climax = w.VO.FirstTime("Started", LineIds.D081);
                Assert.That(w.FpBody.FirstTime("Pistol") - climax, Is.EqualTo(ExfilTimeline.ClimaxPistol).Within(Tol), "pistol rise at 0:06");
                Assert.That(w.VO.FirstTime("Started", LineIds.D082) - climax, Is.EqualTo(ExfilTimeline.ClimaxD082).Within(Tol), "D082 at chest height");
                Assert.That(w.Inserts.FirstTime("Insert", SequenceIds.InsertI4) - climax, Is.EqualTo(ExfilTimeline.ClimaxI4).Within(Tol), "I4 cascade at 0:10");
                Assert.IsTrue(w.ScreenFX.Has("Flash"), "cut to white");
                Assert.IsTrue(w.Music.Has("HardCut"), "music hard cut on the RPG launch");
                Assert.IsFalse(w.Weapons.IsDoorGunMounted, "door gun released");
            }
        }
    }
}
