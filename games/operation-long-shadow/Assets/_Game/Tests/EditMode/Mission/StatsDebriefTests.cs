using LongShadow.Core;
using LongShadow.Mission.Data;
using LongShadow.Mission.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>WP07 DoD: stats feed the debrief model (GDD §8.9).</summary>
    public class StatsDebriefTests
    {
        [TearDown]
        public void TearDown() => GameServices.Clear();

        [Test]
        public void TrackerCountsPlayerEventsOnly()
        {
            var t = new MissionStatsTracker { Counting = true };
            t.Attach();
            try
            {
                GameEvents.RaiseActorKilled(new KillInfo { VictimId = "E3-01", VictimTeam = Team.Legion, ByPlayer = true, Headshot = true });
                GameEvents.RaiseActorKilled(new KillInfo { VictimId = "E3-02", VictimTeam = Team.Legion, ByPlayer = false });
                GameEvents.RaiseActorKilled(new KillInfo { VictimId = "E4-07", VictimTeam = Team.Greycoat, ByPlayer = true });
                for (var i = 0; i < 4; i++) GameEvents.RaiseShotFired(new ShotEvent { IsPlayer = true, WeaponId = WeaponIds.Commando });
                GameEvents.RaiseShotFired(new ShotEvent { IsPlayer = false, Team = Team.Legion, WeaponId = WeaponIds.AK });
                GameEvents.RaiseHitConfirmed(false, false);
                GameEvents.RaiseHitConfirmed(true, true);
                GameEvents.RaiseGrenadeThrown(Vector3.zero, Team.Player);
                GameEvents.RaiseGrenadeThrown(Vector3.zero, Team.Legion);
                GameEvents.RaisePlayerDied(new DamageInfo());
                t.Tick(10f);
                GameEvents.RaisePauseChanged(true);
                t.Tick(100f);
                GameEvents.RaisePauseChanged(false);
                t.Counting = false;
                t.Tick(100f);
            }
            finally
            {
                t.Detach();
            }
            var s = t.Stats;
            Assert.That(s.Kills, Is.EqualTo(2));
            Assert.That(s.Headshots, Is.EqualTo(1));
            Assert.That(s.ShotsFired, Is.EqualTo(4));
            Assert.That(s.ShotsHit, Is.EqualTo(2));
            Assert.That(s.Accuracy, Is.EqualTo(0.5f));
            Assert.That(s.GrenadesThrown, Is.EqualTo(1));
            Assert.That(s.Deaths, Is.EqualTo(1));
            Assert.That(s.MissionSeconds, Is.EqualTo(10f), "paused time and the booth do not count");
        }

        [Test]
        public void DebriefModelFormatsTheGddExample()
        {
            var s = new MissionStats { MissionSeconds = 14 * 60 + 2, Kills = 44, Headshots = 17, ShotsFired = 100, ShotsHit = 38, GrenadesThrown = 6, Deaths = 2, Intel = 3, OptionalIntel = 1 };
            var lines = DebriefModel.Build(s);
            Assert.That(lines[0], Is.EqualTo(DebriefModel.Header));
            StringAssert.StartsWith("MISSION TIME ....... 14:02", lines[1]);
            StringAssert.Contains("KILLS ........ 44", lines[1]);
            StringAssert.Contains("HEADSHOTS .... 17", lines[1]);
            StringAssert.StartsWith("ACCURACY ........... 38 %", lines[2]);
            StringAssert.Contains("GRENADES ..... 6", lines[2]);
            StringAssert.Contains("DEATHS ....... 2", lines[2]);
            StringAssert.Contains("LEDGERS [X]  REELS [X]  CODEBOOK [X]   OPTIONAL 1/2", lines[3]);
            StringAssert.Contains("HOLLISTER, F.", lines[4]);
            StringAssert.Contains("KIA (HOSTILE FIRE)", lines[4]);
            StringAssert.Contains("ASSET STATUS: ACTIVE", lines[7]);
            Assert.That(DebriefModel.MissionTime(59.9f), Is.EqualTo("00:59"));
        }

        [Test]
        public void FullRunFeedsTheDebrief()
        {
            FullRun.AssertFinished();
            var d = FullRun.Get().Debrief;
            Assert.IsNotNull(d);
            Assert.That(d.Intel, Is.EqualTo(IntelTable.MainTotal));
            Assert.That(d.MissionSeconds, Is.GreaterThan(60f), "gameplay time counted in beats 1-11");
            Assert.That(d.Kills, Is.GreaterThan(30), "the fake world credits the player with most kills");
        }
    }
}
