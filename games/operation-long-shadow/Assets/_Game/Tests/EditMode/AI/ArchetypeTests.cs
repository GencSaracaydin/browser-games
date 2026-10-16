using LongShadow.AI.Common;
using LongShadow.AI.Enemy;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>ArchetypeTable against GDD §14.1 / §16 and the burst cadence of <see cref="EnemyShooter"/>.</summary>
    public class ArchetypeTests
    {
        [Test]
        public void Gdd14Values()
        {
            var r = ArchetypeTable.Get(ArchetypeId.LegionRifleman);
            Assert.That(new[] { r.Hp, r.DamageToPlayer, r.Accuracy20, r.RangeMin, r.RangeMax }, Is.EqualTo(new[] { 100f, 20f, 0.30f, 10f, 35f }));
            Assert.That(new[] { r.BurstMin, r.BurstMax }, Is.EqualTo(new[] { 3, 6 }));
            Assert.That(new[] { r.PauseMin, r.PauseMax }, Is.EqualTo(new[] { 0.6f, 1.2f }));
            Assert.That(r.WeaponId, Is.EqualTo(WeaponIds.AK));
            var g = ArchetypeTable.Get(ArchetypeId.LegionGunner);
            Assert.That(new[] { g.Hp, g.DamageToPlayer, g.Accuracy20 }, Is.EqualTo(new[] { 130f, 16f, 0.22f }));
            Assert.That(new[] { g.BurstMin, g.BurstMax }, Is.EqualTo(new[] { 8, 15 }));
            Assert.IsFalse(g.UsesNavMesh, "perch nodes only");
            var gc = ArchetypeTable.Get(ArchetypeId.GreycoatAdvisor);
            Assert.That(new[] { gc.Hp, gc.TorsoArmour, gc.DamageToPlayer, gc.Accuracy20 }, Is.EqualTo(new[] { 150f, 0.8f, 24f, 0.45f }));
            Assert.That(new[] { gc.CoverReevalMin, gc.CoverReevalMax }, Is.EqualTo(new[] { 4f, 6f }));
            Assert.That(gc.FleeGrenadeChance, Is.EqualTo(1f));
            Assert.That(r.FleeGrenadeChance, Is.EqualTo(0.6f));
            Assert.That(new[] { r.CoverReevalMin, r.CoverReevalMax }, Is.EqualTo(new[] { 6f, 9f }));
            var t = ArchetypeTable.Get(ArchetypeId.TechnicalGunner);
            Assert.That(new[] { t.DamageToPlayer, t.Accuracy20, t.Rpm }, Is.EqualTo(new[] { 35f, 0.25f, 550f }));
            var m = ArchetypeTable.Get(ArchetypeId.GreycoatMarksman);
            Assert.That(new[] { m.Hp, m.FixedSightRange, m.PauseMin }, Is.EqualTo(new[] { 120f, 160f, 3.5f }));
            var rpg = ArchetypeTable.Get(ArchetypeId.LegionRPG);
            Assert.That(rpg.DamageToPlayer, Is.EqualTo(90f));
            var rt = ArchetypeTable.Get(ArchetypeId.RooftopTarget);
            Assert.That(rt.Hp, Is.EqualTo(60f));
            Assert.IsTrue(rt.AlwaysAware);
        }

        [Test]
        public void DifficultyScalesDamageAndTheMarksmanUsesHisRow()
        {
            var r = ArchetypeTable.Get(ArchetypeId.LegionRifleman);
            var m = ArchetypeTable.Get(ArchetypeId.GreycoatMarksman);
            var expected = new[] { 45f, 70f, 90f, 100f };                                          // GDD §16 marksman row
            var levels = new[] { Difficulty.Recruit, Difficulty.Regular, Difficulty.Hardened, Difficulty.Veteran };
            for (var i = 0; i < levels.Length; i++)
            {
                var d = DifficultyProfile.For(levels[i]);
                Assert.That(ArchetypeTable.DamageToPlayer(r, d), Is.EqualTo(20f * d.EnemyDamageMultiplier).Within(1e-4f), levels[i].ToString());
                Assert.That(ArchetypeTable.DamageToPlayer(m, d), Is.EqualTo(expected[i]).Within(1e-4f), levels[i].ToString());
            }
        }

        [Test]
        public void BurstCadence()
        {
            var a = ArchetypeTable.Get(ArchetypeId.LegionRifleman);
            var s = new EnemyShooter(a);
            var rng = new AIRandom(42u);
            var now = 0f;
            var shots = 0;
            var burst = 0;
            var lastShot = -10f;
            var cutByReload = false;
            for (var f = 0; f < 60 * 30; f++)
            {
                now += 1f / 60f;
                if (s.Tick(now, true, false, ref rng))
                {
                    if (now - lastShot > 0.5f && burst > 0)
                    {
                        // A burst that emptied the magazine is legitimately short.
                        if (!cutByReload) Assert.That(burst, Is.InRange(a.BurstMin, a.BurstMax), "rounds per burst (GDD §14.1)");
                        Assert.That(now - lastShot, Is.InRange(a.PauseMin - 0.05f, a.PauseMax + 0.15f), "pause between bursts");
                        burst = 0;
                        cutByReload = false;
                    }
                    burst++;
                    shots++;
                    lastShot = now;
                }
                if (s.NeedsReload)
                {
                    s.Refill();
                    cutByReload = true;
                }
            }
            Assert.That(shots, Is.GreaterThan(60));
        }

        [Test]
        public void SuppressionHalvesTheRate()
        {
            var a = ArchetypeTable.Get(ArchetypeId.LegionRifleman);
            int Count(bool suppressed)
            {
                var s = new EnemyShooter(a);
                var rng = new AIRandom(9u);
                var n = 0;
                var now = 0f;
                for (var f = 0; f < 60 * 60; f++)
                {
                    now += 1f / 60f;
                    if (s.Tick(now, true, suppressed, ref rng)) n++;
                    if (s.NeedsReload) s.Refill();
                }
                return n;
            }
            var normal = Count(false);
            var slow = Count(true);
            Assert.That(slow / (float)normal, Is.InRange(0.4f, 0.62f), "GDD §14.3: fire rate ×0.5 while suppressed");
        }

        [Test]
        public void MagazineEmptiesAndRefills()
        {
            var a = ArchetypeTable.Get(ArchetypeId.LegionRifleman);
            var s = new EnemyShooter(a);
            var rng = new AIRandom(1u);
            var now = 0f;
            var shots = 0;
            while (!s.NeedsReload && now < 120f)
            {
                now += 1f / 60f;
                if (s.Tick(now, true, false, ref rng)) shots++;
            }
            Assert.That(shots, Is.EqualTo(a.Magazine));
            Assert.IsFalse(s.Tick(now + 5f, true, false, ref rng), "no rounds while empty");
            s.Refill();
            Assert.That(s.Magazine, Is.EqualTo(a.Magazine));
        }

        [Test]
        public void NoFireWithoutPermission()
        {
            var s = new EnemyShooter(ArchetypeTable.Get(ArchetypeId.GreycoatAdvisor));
            var rng = new AIRandom(5u);
            for (var t = 0f; t < 5f; t += 0.02f) Assert.IsFalse(s.Tick(t, false, false, ref rng));
        }
    }
}
