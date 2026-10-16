using LongShadow.Combat.Data;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>Every number of GDD §13 as it appears in WeaponTable (table-driven; TECH_PLAN §11.4 WP03 DoD).</summary>
    public class WeaponTableTests
    {
        // id, close, far, falloffStart, falloffEnd, rpm, mag, reserve, tactical, empty, adsTime, adsFov, hip, ads, move, noise
        private static readonly object[] PlayerRows =
        {
            new object[] { WeaponIds.Commando, 34f, 26f, 25f, 45f, 750f, 30, 150, 2.1f, 2.7f, 0.22f, 47f, 3.0f, 0.15f, 1.00f, 60f },
            new object[] { WeaponIds.HushPuppy, 40f, 25f, 12f, 30f, 300f, 8, 48, 1.6f, 2.0f, 0.15f, 52f, 2.0f, 0.25f, 1.05f, 6f },
            new object[] { WeaponIds.AK, 42f, 33f, 22f, 45f, 600f, 30, 120, 2.4f, 3.0f, 0.26f, 46f, 3.6f, 0.30f, 0.97f, 60f },
            new object[] { WeaponIds.RPK, 42f, 35f, 25f, 50f, 600f, 75, 150, 4.2f, 5.0f, 0.38f, 46f, 4.5f, 0.35f, 0.88f, 60f },
        };

        [TestCaseSource(nameof(PlayerRows))]
        public void PlayerWeaponMatchesGdd131(string id, float close, float far, float start, float end, float rpm, int mag, int reserve,
                                              float tactical, float empty, float adsTime, float adsFov, float hip, float ads, float move, float noise)
        {
            var w = WeaponTable.Get(id);
            Assert.That(w, Is.Not.Null, id);
            Assert.That(w.DamageClose, Is.EqualTo(close));
            Assert.That(w.DamageFar, Is.EqualTo(far));
            Assert.That(w.FalloffStart, Is.EqualTo(start));
            Assert.That(w.FalloffEnd, Is.EqualTo(end));
            Assert.That(w.Rpm, Is.EqualTo(rpm));
            Assert.That(w.MagazineSize, Is.EqualTo(mag));
            Assert.That(w.MaxReserve, Is.EqualTo(reserve));
            Assert.That(w.TacticalReload.Duration, Is.EqualTo(tactical).Within(1e-4f));
            Assert.That(w.EmptyReload.Duration, Is.EqualTo(empty).Within(1e-4f));
            Assert.That(w.AdsTime, Is.EqualTo(adsTime).Within(1e-4f));
            Assert.That(w.AdsFovVertical, Is.EqualTo(adsFov));
            Assert.That(w.Spread.Hip, Is.EqualTo(hip).Within(1e-4f));
            Assert.That(w.Spread.Ads, Is.EqualTo(ads).Within(1e-4f));
            Assert.That(w.MoveMultiplier, Is.EqualTo(move).Within(1e-4f));
            Assert.That(w.NoiseRadius, Is.EqualTo(noise));
            Assert.That(w.Range, Is.EqualTo(500f), "TECH_PLAN §7.5: hitscan 500 m");
        }

        [Test]
        public void FireModesAndMultipliers()
        {
            Assert.That(WeaponTable.Commando.FireMode, Is.EqualTo(FireMode.Auto));
            Assert.That(WeaponTable.AK.FireMode, Is.EqualTo(FireMode.Auto));
            Assert.That(WeaponTable.RPK.FireMode, Is.EqualTo(FireMode.Auto));
            Assert.That(WeaponTable.HushPuppy.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(WeaponTable.HushPuppy.HeadMultiplierOverride, Is.EqualTo(3.0f), "GDD §13.1: Hush Puppy head x3.0");
            foreach (var w in new[] { WeaponTable.Commando, WeaponTable.AK, WeaponTable.RPK })
            {
                Assert.That(w.HeadMultiplierOverride, Is.EqualTo(0f), w.Id + " uses the hitbox's x2.0");
                Assert.That(w.LimbMultiplierOverride, Is.EqualTo(0f), w.Id + " uses the hitbox's x0.85");
            }
            Assert.That(Hitbox.DefaultMultiplier(HitZone.Head), Is.EqualTo(2.0f));
            Assert.That(Hitbox.DefaultMultiplier(HitZone.Limb), Is.EqualTo(0.85f));
            Assert.That(WeaponTable.HushPuppy.NoiseKind, Is.EqualTo(NoiseKind.SuppressedShot));
        }

        [TestCase(WeaponIds.Commando, 1.25f)]
        [TestCase(WeaponIds.HushPuppy, 1.13f)]
        [TestCase(WeaponIds.AK, 1.28f)]
        [TestCase(WeaponIds.RPK, 1.28f)]
        public void AdsZoomMatchesGdd(string id, float zoom) => Assert.That(WeaponTable.Get(id).AdsZoom, Is.EqualTo(zoom).Within(0.01f));

        [TestCase(WeaponIds.Commando, 8f, 0.08f, 8, 0.8f)]
        [TestCase(WeaponIds.HushPuppy, 12f, -1f, 0, 1.4f)]
        [TestCase(WeaponIds.AK, 6f, -1f, 6, 1.1f)]
        [TestCase(WeaponIds.RPK, 7f, -1f, 8, -1f)]
        public void RecoilRowsMatchGdd(string id, float rate, float delay, int pattern, float firstShot)
        {
            var r = WeaponTable.Get(id).Recoil;
            Assert.That(r.RecoveryRate, Is.EqualTo(rate), "GDD §13.1 recovery");
            if (delay >= 0f) Assert.That(r.RecoveryDelay, Is.EqualTo(delay).Within(1e-5f));
            Assert.That(r.PatternLength, Is.EqualTo(pattern));
            if (firstShot > 0f)
            {
                var first = pattern > 0 ? r.Pattern[0].x : r.FirstShotPitch;
                Assert.That(first, Is.EqualTo(firstShot).Within(1e-4f), "first shot kick");
            }
            Assert.That(r.CameraShare, Is.EqualTo(0.3f), "GDD §13.1: camera takes 30 %");
            Assert.That(r.ModelKickBack, Is.GreaterThan(0.01f));
        }

        [Test]
        public void SprintOutTimesMatchGdd12()
        {
            Assert.That(WeaponTable.Commando.SprintOutTime, Is.EqualTo(0.2f));
            Assert.That(WeaponTable.HushPuppy.SprintOutTime, Is.EqualTo(0.2f));
            Assert.That(WeaponTable.AK.SprintOutTime, Is.EqualTo(0.2f));
            Assert.That(WeaponTable.RPK.SprintOutTime, Is.EqualTo(0.32f));
        }

        [Test]
        public void TracersMatchGdd134()
        {
            Assert.That(WeaponTable.Commando.TracerEvery, Is.EqualTo(5));
            Assert.That(WeaponTable.AK.TracerEvery, Is.EqualTo(5));
            Assert.That(WeaponTable.RPK.TracerEvery, Is.EqualTo(3));
            Assert.That(WeaponTable.DoorGun.TracerEvery, Is.EqualTo(3));
            Assert.That(WeaponTable.HushPuppy.TracerEvery, Is.EqualTo(0));
        }

        [Test]
        public void DoorGunMatchesGdd133()
        {
            var g = WeaponTable.DoorGun;
            Assert.That(g.Rpm, Is.EqualTo(550f));
            Assert.That(g.DamageClose, Is.EqualTo(60f));
            Assert.That(g.DamageFar, Is.EqualTo(60f));
            Assert.That(g.InfiniteAmmo, Is.True);
            Assert.That(g.Spread.Hip, Is.EqualTo(0.6f).Within(1e-5f));
            var m = WeaponTable.DoorGunMount;
            Assert.That(m.HeatPerShot, Is.EqualTo(1f));
            Assert.That(m.OverheatAt, Is.EqualTo(55f));
            Assert.That(m.OverheatRecovery, Is.EqualTo(2.0f));
            Assert.That(m.IdleRecovery, Is.EqualTo(1.0f));
            Assert.That(m.YawLimit, Is.EqualTo(70f));
            Assert.That(m.PitchUp, Is.EqualTo(15f));
            Assert.That(m.PitchDown, Is.EqualTo(60f));
            Assert.That(m.IncomingDamageScale, Is.EqualTo(0.25f));
            Assert.That(m.HealthFloor, Is.EqualTo(1f));
        }

        [Test]
        public void GrenadeAndKnifeMatchGdd132()
        {
            var f = WeaponTable.Frag;
            Assert.That(f.Fuse, Is.EqualTo(4.0f));
            Assert.That(f.ThrowRange, Is.EqualTo(18f));
            Assert.That(f.Damage, Is.EqualTo(220f));
            Assert.That(f.LethalRadius, Is.EqualTo(4.0f));
            Assert.That(f.MaxRadius, Is.EqualTo(8.0f));
            Assert.That(f.StartCount, Is.EqualTo(3));
            Assert.That(f.MaxCount, Is.EqualTo(4));
            Assert.That(f.CheckpointMinimum, Is.EqualTo(2), "GDD §17");
            Assert.That(f.NoiseRadius, Is.EqualTo(80f), "GDD §14.2");
            var k = WeaponTable.Knife;
            Assert.That(k.Damage, Is.EqualTo(150f));
            Assert.That(k.Reach, Is.EqualTo(1.6f));
            Assert.That(k.Lunge, Is.EqualTo(1.0f));
            Assert.That(k.Duration, Is.EqualTo(0.6f));
            Assert.That(k.HitTime, Is.LessThan(k.Duration));
        }

        [TestCase(WeaponIds.AK, 20f)]
        [TestCase(WeaponIds.RPK, 16f)]
        [TestCase(WeaponIds.AKMS, 24f)]
        [TestCase(WeaponIds.SVD, 70f)]
        [TestCase(WeaponIds.RPG7, 90f)]
        [TestCase(WeaponIds.DShK, 35f)]
        public void EnemyDamageMatchesGdd141(string id, float damage) => Assert.That(WeaponTable.GetEnemy(id).DamageToPlayer, Is.EqualTo(damage));

        [Test]
        public void RocketMatchesGdd141()
        {
            Assert.That(WeaponTable.Rocket.DirectDamage, Is.EqualTo(90f));
            Assert.That(WeaponTable.Rocket.SplashRadius, Is.EqualTo(4f));
        }

        [Test]
        public void EveryReloadSpecIsOrderedAndFitsItsDuration()
        {
            foreach (var w in WeaponTable.PlayerWeapons)
            {
                if (w.InfiniteAmmo) continue;
                Assert.That(w.TacticalReload.IsValid(), Is.True, w.Id + " tactical");
                Assert.That(w.EmptyReload.IsValid(), Is.True, w.Id + " empty");
                Assert.That(w.TacticalReload.HasCharge, Is.False, w.Id + ": a tactical reload keeps the round in the chamber");
                Assert.That(w.EmptyReload.HasCharge, Is.True, w.Id + ": an empty reload charges the weapon");
                Assert.That(w.EmptyReload.Duration, Is.GreaterThan(w.TacticalReload.Duration), w.Id);
            }
        }

        [Test]
        public void LookupsAndIds()
        {
            foreach (var w in WeaponTable.PlayerWeapons) Assert.That(WeaponTable.Get(w.Id), Is.SameAs(w));
            foreach (var e in WeaponTable.EnemyWeapons) Assert.That(WeaponTable.GetEnemy(e.Id), Is.SameAs(e));
            Assert.That(WeaponTable.Get("NOPE"), Is.Null);
            Assert.That(WeaponTable.Get(null), Is.Null);
            Assert.That(WeaponTable.ArtName(WeaponIds.HushPuppy), Is.EqualTo("HushPuppy"));
            Assert.That(WeaponTable.ArtName(WeaponIds.DoorGun), Is.EqualTo("M60D"));
            Assert.That(WeaponTable.DisplayName(WeaponIds.Commando), Is.EqualTo("COMMANDO"));
            Assert.That(WeaponTable.MaxDroppedWeapons, Is.EqualTo(6), "GDD §13.1");
            Assert.That(WeaponTable.PickupHoldSeconds, Is.EqualTo(0.4f));
            Assert.That(WeaponTable.ResupplyHoldSeconds, Is.EqualTo(1.0f));
        }
    }
}
