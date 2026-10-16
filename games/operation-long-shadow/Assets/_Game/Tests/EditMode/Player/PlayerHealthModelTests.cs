using LongShadow.Core;
using LongShadow.Player.Vitals;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>GDD §16 health rules (<see cref="PlayerHealthModel"/>, <see cref="PlayerDamageMath"/>).</summary>
    public class PlayerHealthModelTests
    {
        [TestCase(Difficulty.Recruit, 3.0f, 60f)]
        [TestCase(Difficulty.Regular, 4.0f, 50f)]
        [TestCase(Difficulty.Hardened, 5.0f, 40f)]
        [TestCase(Difficulty.Veteran, 5.5f, 35f)]
        public void RegenDelayAndRateMatchGdd16(Difficulty level, float delay, float rate)
        {
            var p = DifficultyProfile.For(level);
            Assert.That(p.RegenDelay, Is.EqualTo(delay), "profile row");
            Assert.That(p.RegenRate, Is.EqualTo(rate), "profile row");

            var m = new PlayerHealthModel(p);
            m.ApplyDamage(60f);
            Assert.That(m.Current, Is.EqualTo(40f));
            const float dt = 1f / 60f;
            var t = 0f;
            var regenStart = -1f;
            var full = -1f;
            while (t < 20f && full < 0f)
            {
                var before = m.Current;
                m.Tick(dt);
                t += dt;
                if (regenStart < 0f && m.Current > before) regenStart = t;
                if (m.Current >= m.Max) full = t;
            }
            Assert.That(regenStart, Is.EqualTo(delay).Within(dt + 1e-3f), "regeneration starts after the delay");
            Assert.That(full - delay, Is.EqualTo(60f / rate).Within(dt + 1e-3f), "rate in HP/s");
        }

        [Test]
        public void RegenIsFrameRateIndependent()
        {
            var a = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Regular));
            var b = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Regular));
            a.ApplyDamage(70f);
            b.ApplyDamage(70f);
            a.Tick(4.5f); // one long frame across the delay
            for (var i = 0; i < 450; i++) b.Tick(0.01f);
            Assert.That(a.Current, Is.EqualTo(30f + 50f * 0.5f).Within(1e-3f));
            Assert.That(b.Current, Is.EqualTo(a.Current).Within(0.01f));
        }

        [TestCase(Difficulty.Recruit, 10)]
        [TestCase(Difficulty.Regular, 5)]
        [TestCase(Difficulty.Hardened, 4)]
        [TestCase(Difficulty.Veteran, 3)]
        public void LegionAkHitsToDownMatchGdd16(Difficulty level, int hits)
        {
            // GDD §14.1: Legion AK 20 per hit on Regular; GDD §16 "Legion AK hits to down the player".
            Assert.That(PlayerDamageMath.HitsToDown(20f, level), Is.EqualTo(hits));
        }

        [Test]
        public void LastStandGivesImmunityOncePerRegenCycle()
        {
            var m = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Regular)); // 0.8 s
            Assert.That(m.ApplyDamage(85f), Is.EqualTo(85f), "the crossing hit lands in full");
            Assert.That(m.Current, Is.EqualTo(15f));
            Assert.That(m.ImmunityRemaining, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(m.ApplyDamage(50f), Is.EqualTo(0f), "immune");
            Assert.IsTrue(m.IsAlive);
            m.Tick(0.81f);
            Assert.That(m.ImmunityRemaining, Is.EqualTo(0f));
            m.ApplyDamage(10f);
            Assert.That(m.Current, Is.EqualTo(5f));
            Assert.That(m.ImmunityRemaining, Is.EqualTo(0f), "already used in this cycle");
            m.ApplyDamage(10f);
            Assert.IsFalse(m.IsAlive);
        }

        [Test]
        public void LastStandRearmsAtFullHealth()
        {
            var m = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Recruit)); // 1.5 s
            m.ApplyDamage(85f);
            Assert.IsTrue(m.LastStandUsed);
            m.Tick(10f);
            Assert.That(m.Current, Is.EqualTo(100f));
            Assert.IsFalse(m.LastStandUsed, "re-armed");
        }

        [Test]
        public void VeteranHasNoLastStand()
        {
            var m = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Veteran));
            m.ApplyDamage(85f);
            Assert.That(m.ImmunityRemaining, Is.EqualTo(0f));
            m.ApplyDamage(15f);
            Assert.IsFalse(m.IsAlive);
        }

        [Test]
        public void Beat11ModifiersKeepThePlayerAlive()
        {
            var m = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Veteran)) { DamageScale = 0.25f, FloorHp = 1f };
            m.ApplyDamage(40f);
            Assert.That(m.Current, Is.EqualTo(90f), "×0.25");
            m.ApplyDamage(10000f);
            Assert.That(m.Current, Is.EqualTo(1f), "floor 1 HP");
            Assert.IsTrue(m.IsAlive);
        }

        [Test]
        public void GodModeAndDeathIgnoreDamage()
        {
            var m = new PlayerHealthModel(DifficultyProfile.For(Difficulty.Regular)) { GodMode = true };
            Assert.That(m.ApplyDamage(500f), Is.EqualTo(0f));
            m.GodMode = false;
            m.ApplyDamage(500f);
            Assert.IsFalse(m.IsAlive);
            Assert.That(m.ApplyDamage(5f), Is.EqualTo(0f));
            m.Tick(10f);
            Assert.That(m.Current, Is.EqualTo(0f), "the dead do not regenerate");
            m.ResetFull();
            Assert.IsTrue(m.IsAlive);
            Assert.That(m.Current, Is.EqualTo(100f));
        }

        [Test]
        public void AiBulletsArriveScaledAndAreNotScaledTwice()
        {
            // WP04 sends AI bullet damage already scaled (ArchetypeTable.DamageToPlayer); the player passes it through.
            var vet = DifficultyProfile.For(Difficulty.Veteran);
            Assert.That(PlayerDamageMath.AiBulletDamage(20f, WeaponIds.AK, DifficultyProfile.For(Difficulty.Recruit)), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(PlayerDamageMath.AiBulletDamage(20f, WeaponIds.AK, vet), Is.EqualTo(44f).Within(1e-4f));
            var ak = new DamageInfo(44f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.forward, null, Team.Legion, WeaponIds.AK);
            Assert.That(PlayerDamageMath.ScaleIncoming(in ak, vet), Is.EqualTo(44f), "no double scaling");

            Assert.That(PlayerDamageMath.AiBulletDamage(70f, WeaponIds.SVD, DifficultyProfile.For(Difficulty.Recruit)), Is.EqualTo(45f), "marksman row");
            Assert.That(PlayerDamageMath.AiBulletDamage(70f, WeaponIds.SVD, DifficultyProfile.For(Difficulty.Hardened)), Is.EqualTo(90f));
            Assert.That(PlayerDamageMath.AiBulletDamage(70f, WeaponIds.SVD, vet), Is.EqualTo(100f), "lethal");
        }

        [Test]
        public void HostileExplosionsAreScaledByThePlayer()
        {
            var frag = new DamageInfo(60f, DamageType.Explosion, HitZone.Torso, Vector3.zero, Vector3.down, null, Team.Legion, WeaponIds.Frag);
            Assert.That(PlayerDamageMath.ScaleIncoming(in frag, DifficultyProfile.For(Difficulty.Recruit)), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(PlayerDamageMath.ScaleIncoming(in frag, DifficultyProfile.For(Difficulty.Regular)), Is.EqualTo(60f).Within(1e-4f));
            Assert.That(PlayerDamageMath.ScaleIncoming(in frag, DifficultyProfile.For(Difficulty.Veteran)), Is.EqualTo(132f).Within(1e-3f));
        }

        [Test]
        public void FallsScriptsAndOwnFireAreNotScaled()
        {
            var vet = DifficultyProfile.For(Difficulty.Veteran);
            var fall = PlayerDamageMath.Fall(45f, Vector3.zero);
            Assert.That(fall.Type, Is.EqualTo(DamageType.Fall));
            Assert.That(PlayerDamageMath.ScaleIncoming(in fall, vet), Is.EqualTo(45f));
            var ownFrag = new DamageInfo(60f, DamageType.Explosion, HitZone.Torso, Vector3.zero, Vector3.down, null, Team.Player, WeaponIds.Frag);
            Assert.That(PlayerDamageMath.ScaleIncoming(in ownFrag, vet), Is.EqualTo(60f));
        }
    }
}
