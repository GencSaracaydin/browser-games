using LongShadow.Combat.Ballistics;
using LongShadow.Combat.Data;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>Falloff, zone multipliers, armour and the GDD §13.1 "body hits to kill a Legion rifleman" rows.</summary>
    public class DamageFalloffTests
    {
        [Test]
        public void FlatLinearFlat()
        {
            Assert.That(DamageFalloff.Evaluate(34f, 26f, 25f, 45f, 0f), Is.EqualTo(34f));
            Assert.That(DamageFalloff.Evaluate(34f, 26f, 25f, 45f, 25f), Is.EqualTo(34f));
            Assert.That(DamageFalloff.Evaluate(34f, 26f, 25f, 45f, 35f), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(DamageFalloff.Evaluate(34f, 26f, 25f, 45f, 45f), Is.EqualTo(26f));
            Assert.That(DamageFalloff.Evaluate(34f, 26f, 25f, 45f, 400f), Is.EqualTo(26f));
        }

        [Test]
        public void ZeroLengthRangeIsAStep()
        {
            Assert.That(DamageFalloff.Evaluate(60f, 60f, 0f, 0f, 120f), Is.EqualTo(60f));
            Assert.That(DamageFalloff.Evaluate(10f, 5f, 20f, 20f, 19f), Is.EqualTo(10f));
            Assert.That(DamageFalloff.Evaluate(10f, 5f, 20f, 20f, 21f), Is.EqualTo(5f));
        }

        [Test]
        public void FalloffNeverIncreasesWithDistance()
        {
            foreach (var w in WeaponTable.PlayerWeapons)
            {
                var prev = float.MaxValue;
                for (var d = 0f; d <= 120f; d += 0.5f)
                {
                    var v = DamageFalloff.Evaluate(w, d);
                    Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-4f), w.Id + " at " + d);
                    Assert.That(v, Is.InRange(w.DamageFar, w.DamageClose), w.Id + " at " + d);
                    prev = v;
                }
            }
        }

        // GDD §13.1: body hits to kill a Legion rifleman (100 HP), close / far.
        [TestCase(WeaponIds.Commando, 3, 4)]
        [TestCase(WeaponIds.HushPuppy, 3, 4)]
        [TestCase(WeaponIds.AK, 3, 4)]
        [TestCase(WeaponIds.RPK, 3, 3)]
        public void BodyHitsToKillMatchGdd(string id, int close, int far)
        {
            var w = WeaponTable.Get(id);
            Assert.That(DamageResolver.HitsToKill(w, w.FalloffStart, 100f), Is.EqualTo(close), id + " close");
            Assert.That(DamageResolver.HitsToKill(w, w.FalloffEnd + 10f, 100f), Is.EqualTo(far), id + " far");
            Assert.That(w.BodyHitsToKillClose, Is.EqualTo(close));
            Assert.That(w.BodyHitsToKillFar, Is.EqualTo(far));
        }

        [Test]
        public void HushPuppyHeadshotKillsCloseInOne()
        {
            var w = WeaponTable.HushPuppy;
            Assert.That(DamageResolver.HitsToKill(w, 5f, 100f, HitZone.Head), Is.EqualTo(1), "GDD §13.1: 1 headshot");
            Assert.That(DamageResolver.Resolve(w, 5f, HitZone.Head, 2f, 1f), Is.EqualTo(120f).Within(1e-4f), "override x3.0 beats the hitbox's x2.0");
        }

        [Test]
        public void ZoneMultipliers()
        {
            var w = WeaponTable.Commando;
            Assert.That(DamageResolver.Resolve(w, 0f, HitZone.Head, 2f, 1f), Is.EqualTo(68f).Within(1e-4f));
            Assert.That(DamageResolver.Resolve(w, 0f, HitZone.Limb, 0.85f, 1f), Is.EqualTo(28.9f).Within(1e-3f));
            Assert.That(DamageResolver.ZoneMultiplier(w, HitZone.Limb, 0f), Is.EqualTo(0.85f), "0 = default");
            Assert.That(DamageResolver.ZoneMultiplier(w, HitZone.Torso, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void GreycoatVestReducesTorsoOnly()
        {
            var w = WeaponTable.Commando;
            Assert.That(DamageResolver.Resolve(w, 0f, HitZone.Torso, 1f, 0.8f), Is.EqualTo(27.2f).Within(1e-3f)); // GDD §14.1 torso x0.8
            Assert.That(DamageResolver.HitsToKill(w, 0f, 150f, HitZone.Torso, 0.8f), Is.EqualTo(6));
            Assert.That(DamageResolver.HitsToKill(w, 0f, 150f, HitZone.Head, 1f), Is.EqualTo(3));
        }

        [Test]
        public void PenetrationScalesDamage()
        {
            var w = WeaponTable.AK;
            var full = DamageResolver.Resolve(w, 10f, HitZone.Torso, 1f, 1f);
            var through = DamageResolver.Resolve(w, 10f, HitZone.Torso, 1f, 1f, HitscanRules.PenetrationDamageScale);
            Assert.That(through, Is.EqualTo(full * 0.85f).Within(1e-4f));
            Assert.That(HitscanRules.Penetrates(SurfaceType.Fabric), Is.True);
            Assert.That(HitscanRules.Penetrates(SurfaceType.Glass), Is.True);
            Assert.That(HitscanRules.Penetrates(SurfaceType.Concrete), Is.False);
            Assert.That(HitscanRules.Penetrates(SurfaceType.Flesh), Is.False);
        }

        [Test]
        public void DoorGunDoesSixtyAtAnyRange()
        {
            Assert.That(DamageFalloff.Evaluate(WeaponTable.DoorGun, 5f), Is.EqualTo(60f));
            Assert.That(DamageFalloff.Evaluate(WeaponTable.DoorGun, 250f), Is.EqualTo(60f));
        }
    }
}
