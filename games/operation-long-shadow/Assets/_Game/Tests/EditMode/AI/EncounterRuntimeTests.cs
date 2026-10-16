using System.Collections.Generic;
using LongShadow.AI.Encounters;
using LongShadow.AI.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>Spawner rules without a scene: max alive per encounter, hard cap 12, queueing, waves, morale, clear, persistence.</summary>
    public class EncounterRuntimeTests
    {
        [Test]
        public void MaxAliveIsNeverExceededAndQueuedSpawnsFillIn()
        {
            foreach (var def in EncounterTable.All)
            {
                var host = new FakeSpawnHost();
                var r = host.Start(def);
                // "Placed means the enemy exists when the beat loads" (GDD §7): only E2 places more than its max alive.
                var cap = Mathf.Max(def.MaxAlive, r.PlacedAtLoadCount);
                if (def.Id != EncounterTable.E2) Assert.That(r.PlacedAtLoadCount, Is.LessThanOrEqualTo(def.MaxAlive), def.Id);
                var now = 0f;
                for (var step = 0; step < 4000 && !r.Cleared; step++)
                {
                    now += 0.2f;
                    // Fire every manual/conditional wave eventually.
                    if (step == 200) foreach (var w in def.Waves) r.StartWave(w.Id, now, true);
                    r.Tick(now);
                    Assert.That(r.Alive(), Is.LessThanOrEqualTo(cap), def.Id + " max alive (GDD §7)");
                    if (r.Alive() > def.MaxAlive)
                        foreach (var s in r.Slots)
                            if (s.State == SlotState.Alive)
                                Assert.IsTrue(r.IsPlacedAtLoad(s), def.Id + ": only enemies placed at load may exceed max alive (" + s.Id + ")");
                    if (step % 3 == 0) FakeSpawnHost.Kill(r, null, 1, now);
                }
                r.Tick(now + 0.2f);                                                              // the cleared edge fires on the next tick
                Assert.IsTrue(r.Cleared, def.Id + " clears");
                Assert.That(host.Cleared, Is.EqualTo(1), def.Id + " cleared edge once");
                Assert.That(r.Killed, Is.EqualTo(def.Enemies.Count), def.Id);
            }
        }

        [Test]
        public void ReinforcementsAfterTheOpenerRespectE2MaxAlive()
        {
            var host = new FakeSpawnHost();
            var r = host.Start(EncounterTable.Get(EncounterTable.E2));
            r.Tick(0.2f);
            Assert.That(r.Alive(), Is.EqualTo(6), "both sentries and the gate team exist when Beat 2 loads");
            Assert.That(r.PlacedAtLoadCount, Is.EqualTo(6));
        }

        [Test]
        public void HardCapOfTwelveHoldsAcrossEncounters()
        {
            var host = new FakeSpawnHost { ExtraAlive = 10 };
            var e10 = EncounterTable.Get(EncounterTable.E10);
            var r = host.Start(e10);
            for (var t = 0f; t < 80f; t += 0.2f) r.Tick(t);
            Assert.That(host.AliveTotal, Is.EqualTo(EncounterTable.HardCap), "GDD §14.3: hard alive cap 12");
            Assert.That(r.QueuedCount, Is.GreaterThan(0), "the rest queue");
            host.ExtraAlive = 0;
            r.Tick(81f);
            Assert.That(r.Alive(), Is.EqualTo(7), "E10's own max alive 7 once the others are gone");
        }

        [Test]
        public void WaveDelaysAreRespected()
        {
            var host = new FakeSpawnHost();
            var e3 = EncounterTable.Get(EncounterTable.E3);
            var r = host.Start(e3);
            r.Tick(0f);
            Assert.That(r.Alive("A"), Is.EqualTo(4));
            r.RecordEvent("B3.T.Crossing", 1f);
            r.Tick(1f);
            Assert.That(r.Alive("B"), Is.EqualTo(1), "E3-07 at +0 s");
            r.Tick(1.6f);
            Assert.That(r.Alive("B"), Is.EqualTo(1));
            r.Tick(2.05f);
            Assert.That(r.Alive("B"), Is.EqualTo(2), "E3-08 at +1 s");
            r.Tick(3.05f);
            Assert.That(r.Alive("B"), Is.EqualTo(2), "E3 max alive 6 holds E3-09 back (4 + 2)");
            FakeSpawnHost.Kill(r, "A", 1, 3.1f);
            r.Tick(3.2f);
            Assert.That(r.Alive("B"), Is.EqualTo(3), "E3-09 spawns once a slot frees");
        }

        [Test]
        public void ClosetWaitsDoNotBlockOtherSpawns()
        {
            var host = new FakeSpawnHost();
            host.Decide = s => s.Def.Kind == SpawnKind.Closet ? SpawnResult.Wait : SpawnResult.Spawned;
            var e3 = EncounterTable.Get(EncounterTable.E3);
            var r = host.Start(e3);
            r.StartWave("Balcony", 0.1f, true);
            r.Tick(0.2f);
            Assert.That(r.Alive("A"), Is.EqualTo(4));
            Assert.That(r.Alive("Balcony"), Is.EqualTo(0), "closets in view wait");
            var slot = r.Find("E3-05");
            Assert.That(slot.State, Is.EqualTo(SlotState.Queued));
            Assert.That(slot.WaitingSince, Is.EqualTo(0.2f).Within(1e-4f));
            host.Decide = s => SpawnResult.Spawned;
            r.Tick(0.4f);
            Assert.That(r.Alive("Balcony"), Is.EqualTo(2));
        }

        [Test]
        public void MoraleBreaksOnceAtSixtyPercent()
        {
            var host = new FakeSpawnHost();
            var r = host.Start(EncounterTable.Get(EncounterTable.E3));
            r.Tick(0f);
            FakeSpawnHost.Kill(r, "A", 2, 1f);
            Assert.That(host.MoraleBreaks, Is.EqualTo(0), "2 of 4 = 50 %");
            FakeSpawnHost.Kill(r, "A", 1, 2f);
            Assert.That(host.MoraleBreaks, Is.EqualTo(1), "3 of 4 = 75 % ≥ 60 %: the survivor falls back");
            Assert.That(host.LastBrokenWave, Is.EqualTo("A"));
            FakeSpawnHost.Kill(r, "A", 1, 3f);
            Assert.That(host.MoraleBreaks, Is.EqualTo(1), "a wave breaks once");
        }

        [Test]
        public void MoraleRuleTable()
        {
            Assert.IsFalse(MoraleRule.ShouldBreak(1, 0, false));
            Assert.IsFalse(MoraleRule.ShouldBreak(2, 1, false), "50 %");
            Assert.IsTrue(MoraleRule.ShouldBreak(3, 2, false), "67 %");
            Assert.IsTrue(MoraleRule.ShouldBreak(5, 3, false), "60 %");
            Assert.IsFalse(MoraleRule.ShouldBreak(5, 2, false), "40 %");
            Assert.IsFalse(MoraleRule.ShouldBreak(5, 5, false), "nobody left");
            Assert.IsFalse(MoraleRule.ShouldBreak(5, 4, true), "already broken");
        }

        [Test]
        public void ConditionalWavesFollowTheGdd()
        {
            var host = new FakeSpawnHost();
            // E7 stair wave: breach dead AND player at the office door, OR 25 s after the breach (GDD §7.7).
            var r = host.Start(EncounterTable.Get(EncounterTable.E7), 0f);
            r.Tick(0.2f);
            Assert.That(r.Alive("Breach"), Is.EqualTo(3));
            FakeSpawnHost.Kill(r, "Breach", 3, 5f);
            r.Tick(5.2f);
            Assert.IsFalse(r.WaveStarted("Stair"), "breach dead but player not at the office door");
            host.Inside.Add("B7.T.OfficeDoor");
            r.Tick(5.4f);
            Assert.IsTrue(r.WaveStarted("Stair"));

            host = new FakeSpawnHost();
            r = host.Start(EncounterTable.Get(EncounterTable.E7), 0f);
            r.Tick(24.8f);
            Assert.IsFalse(r.WaveStarted("Stair"));
            r.Tick(25.0f);
            Assert.IsTrue(r.WaveStarted("Stair"), "25 s after the breach");

            // E9: wave 2 at t 30 or wave 1 ≤ 1 alive (GDD §7.9).
            host = new FakeSpawnHost();
            r = host.Start(EncounterTable.Get(EncounterTable.E9), 0f);
            for (var t = 0f; t < 3f; t += 0.2f) r.Tick(t);
            Assert.That(r.Alive("W1"), Is.EqualTo(3));
            FakeSpawnHost.Kill(r, "W1", 2, 3f);
            r.Tick(3.2f);
            Assert.IsTrue(r.WaveStarted("W2"), "wave 1 ≤ 1 alive");
            Assert.IsFalse(r.WaveStarted("W3"));
            for (var t = 3.4f; t < 61f; t += 0.2f) r.Tick(t);
            Assert.IsTrue(r.WaveStarted("W3"), "t 60");

            // E4 Greycoats: technical dead + 4 s (GDD §7.4).
            host = new FakeSpawnHost();
            r = host.Start(EncounterTable.Get(EncounterTable.E4), 0f);
            r.Tick(0.2f);
            r.NotifyDeath(r.Find("E4-01"), 10f);
            r.Tick(13.9f);
            Assert.IsFalse(r.WaveStarted("Greycoat"));
            r.Tick(14.0f);
            Assert.IsTrue(r.WaveStarted("Greycoat"));
        }

        [Test]
        public void GatehouseWaveOnlyStartsWhileThreeOrFewerAreAlive()
        {
            var host = new FakeSpawnHost();
            var r = host.Start(EncounterTable.Get(EncounterTable.E6), 0f);
            r.Tick(0.2f);
            Assert.That(r.Alive(), Is.EqualTo(5));
            r.RecordEvent("B6.Blast", 1f);
            r.Tick(30f);
            Assert.IsFalse(r.WaveStarted("Gatehouse"), "25 s passed but 5 alive (> 3)");
            FakeSpawnHost.Kill(r, "Yard", 2, 31f);
            r.Tick(31.2f);
            Assert.IsTrue(r.WaveStarted("Gatehouse"), "3 alive, and ≥ 3 conditions (25 s) hold");
        }

        [Test]
        public void AlertRulesAlertLivingMembersOnce()
        {
            var host = new FakeSpawnHost();
            var r = host.Start(EncounterTable.Get(EncounterTable.E2), 0f);
            r.Tick(0.2f);
            r.RecordEvent("B2.T.GateFight", 1f);
            r.Tick(1.2f);
            Assert.That(host.Alerted, Is.EqualTo(4), "E2-03..06 alerted by the gate-fight trigger");
            r.Tick(1.4f);
            Assert.That(host.Alerted, Is.EqualTo(4));
        }

        [Test]
        public void KilledIdsStayDeadAfterARestore()
        {
            var host = new FakeSpawnHost();
            var killed = new HashSet<string> { "E3-01", "E3-02" };
            var r = host.Start(EncounterTable.Get(EncounterTable.E3), 0f, killed);
            r.Tick(0.2f);
            Assert.That(r.Alive("A"), Is.EqualTo(2), "GDD §17: enemies killed before the checkpoint stay dead");
            Assert.That(r.Killed, Is.EqualTo(2));
        }

        [Test]
        public void DeactivateDespawnsTheLiving()
        {
            var host = new FakeSpawnHost();
            var r = host.Start(EncounterTable.Get(EncounterTable.E5), 0f);
            r.Tick(0.2f);
            Assert.That(r.Alive(), Is.EqualTo(3));
            r.Deactivate();
            Assert.That(host.Despawned, Is.EqualTo(3));
            Assert.That(r.Alive(), Is.EqualTo(0));
        }

        [Test]
        public void SpawnRuleHelpers()
        {
            Assert.IsTrue(SpawnRules.ClosetAllowed(15f, 15f, false, false));
            Assert.IsFalse(SpawnRules.ClosetAllowed(14.9f, 15f, false, false), "closer than 15 m");
            Assert.IsFalse(SpawnRules.ClosetAllowed(40f, 15f, true, false), "door in view");
            Assert.IsFalse(SpawnRules.ClosetAllowed(40f, 15f, false, true), "spawn point in view");
            Assert.IsTrue(SpawnRules.UnderCaps(5, 6, 11, 12));
            Assert.IsFalse(SpawnRules.UnderCaps(6, 6, 6, 12));
            Assert.IsFalse(SpawnRules.UnderCaps(1, 6, 12, 12));
        }
    }
}
