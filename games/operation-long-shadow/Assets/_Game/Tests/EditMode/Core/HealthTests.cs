using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Core
{
    public class HealthTests
    {
        private GameObject _go;
        private Health _h;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Rifleman");
            _h = _go.AddComponent<Health>();
            _h.ActorId = "E3-01";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            GameEvents.ResetAll();
        }

        private static DamageInfo Shot(float amount, HitZone zone = HitZone.Torso) =>
            new DamageInfo(amount, DamageType.Bullet, zone, Vector3.zero, Vector3.forward, null, Team.Player, "COMMANDO");

        [Test]
        public void ThreeCommandoBodyHitsKillARifleman()
        {
            // GDD §13.1: Commando 34 close, Legion rifleman 100 HP -> 3 body hits.
            KillInfo kill = default;
            var killed = 0;
            GameEvents.ActorKilled += k => { kill = k; killed++; };
            _h.ApplyDamage(Shot(34f));
            _h.ApplyDamage(Shot(34f));
            Assert.IsTrue(_h.IsAlive);
            Assert.That(_h.Current, Is.EqualTo(32f));
            _h.ApplyDamage(Shot(34f, HitZone.Head));
            Assert.IsFalse(_h.IsAlive);
            Assert.That(killed, Is.EqualTo(1));
            Assert.That(kill.VictimId, Is.EqualTo("E3-01"));
            Assert.IsTrue(kill.Headshot);
            Assert.IsTrue(kill.ByPlayer);
            _h.ApplyDamage(Shot(34f));
            Assert.That(killed, Is.EqualTo(1), "dead actors ignore damage");
        }

        [Test]
        public void InvulnerableAndSuppressiveHitsDoNoDamage()
        {
            var events = 0;
            _h.Damaged += (h, d) => events++;
            _h.Invulnerable = true;
            _h.ApplyDamage(Shot(500f));
            Assert.IsTrue(_h.IsAlive);
            _h.Invulnerable = false;
            var s = Shot(500f);
            s.Suppressive = true;
            _h.ApplyDamage(s);
            Assert.That(_h.Current, Is.EqualTo(100f));
            Assert.That(events, Is.EqualTo(2));
        }

        [Test]
        public void OverrideMaxForUnawareSentries()
        {
            _h.OverrideMax(30f); // GDD §14.1 sentries at 30 HP while unaware
            Assert.That(_h.Max, Is.EqualTo(30f));
            _h.ApplyDamage(Shot(10f));
            _h.RestoreMax();
            Assert.That(_h.Max, Is.EqualTo(100f));
            Assert.That(_h.Current, Is.EqualTo(90f));
        }

        [Test]
        public void SaveRoundTripAndArmour()
        {
            _h.ApplyDamage(Shot(40f));
            var rec = new SaveRecord { Key = _h.SaveKey };
            _h.Capture(new SaveWriter(rec));
            _h.ResetFull();
            Assert.That(_h.Current, Is.EqualTo(100f));
            _h.Restore(new SaveReader(rec));
            Assert.That(_h.Current, Is.EqualTo(60f));
            Assert.That(_h.SaveKey, Is.EqualTo("Health.E3-01"));
            Assert.That(_h.ArmourFactor(HitZone.Head), Is.EqualTo(1f));
            Assert.IsNotNull(_h.AimPoint);
        }

        [Test]
        public void HitboxForwardsZone()
        {
            var bone = new GameObject("Head");
            bone.transform.SetParent(_go.transform);
            bone.AddComponent<SphereCollider>();
            var hb = bone.AddComponent<Hitbox>();
            hb.Configure(HitZone.Head);
            Assert.AreSame(_h, hb.Owner);
            hb.ApplyHit(Shot(50f));
            Assert.That(_h.Current, Is.EqualTo(50f));
        }
    }
}
