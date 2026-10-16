using System.Collections.Generic;
using System.Reflection;
using LongShadow.Characters.Animation;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Characters
{
    public class CharacterAnimIdsTests
    {
        [Test]
        public void HitDirectionFromShotDirection()
        {
            var fwd = Vector3.forward;
            // A bullet travelling backwards along the body came from the front.
            Assert.That(CharacterAnimIds.HitDirFrom(fwd, Vector3.back), Is.EqualTo(CharacterAnimIds.HitFront));
            Assert.That(CharacterAnimIds.HitDirFrom(fwd, Vector3.forward), Is.EqualTo(CharacterAnimIds.HitBack));
            Assert.That(CharacterAnimIds.HitDirFrom(fwd, Vector3.left), Is.EqualTo(CharacterAnimIds.HitRight), "travelling left = shot from the right");
            Assert.That(CharacterAnimIds.HitDirFrom(fwd, Vector3.right), Is.EqualTo(CharacterAnimIds.HitLeft));
            Assert.That(CharacterAnimIds.HitDirFrom(fwd, Vector3.zero), Is.EqualTo(CharacterAnimIds.HitFront));
        }

        [Test]
        public void DeathChoice()
        {
            var fwd = Vector3.forward;
            var blast = new DamageInfo(200f, DamageType.Explosion, HitZone.Torso, Vector3.zero, Vector3.back, null, Team.Player);
            Assert.That(CharacterAnimIds.DeathFor(blast, fwd, 0.9f), Is.EqualTo(CharacterAnimIds.DeathBlown));
            var head = new DamageInfo(50f, DamageType.Bullet, HitZone.Head, Vector3.zero, Vector3.back, null, Team.Player);
            Assert.That(CharacterAnimIds.DeathFor(head, fwd, 0.9f), Is.EqualTo(CharacterAnimIds.DeathHead));
            var back = new DamageInfo(50f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.forward, null, Team.Player);
            Assert.That(CharacterAnimIds.DeathFor(back, fwd, 0.9f), Is.EqualTo(CharacterAnimIds.DeathForward));
            var chest = new DamageInfo(50f, DamageType.Bullet, HitZone.Torso, Vector3.zero, Vector3.back, null, Team.Player);
            Assert.That(CharacterAnimIds.DeathFor(chest, fwd, 0.1f), Is.EqualTo(CharacterAnimIds.DeathCrumple));
            Assert.That(CharacterAnimIds.DeathFor(chest, fwd, 0.9f), Is.EqualTo(CharacterAnimIds.DeathBackward));
        }

        [Test]
        public void ControllersCarryEveryCoreParameter()
        {
            var names = new HashSet<string>();
            foreach (var p in CharacterAnimIds.AllParameters) Assert.That(names.Add(p.Name), Is.True, "duplicate parameter " + p.Name);
            foreach (var f in typeof(AnimatorParams).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string))
                    Assert.That(names.Contains((string)f.GetRawConstantValue()), Is.True, "AllParameters lacks Core AnimatorParams." + f.Name);
            foreach (var n in new[] { CharacterAnimIds.HandPose, CharacterAnimIds.Hold, CharacterAnimIds.ActionHold, CharacterAnimIds.ArmsAction,
                                      CharacterAnimIds.ArmsTrigger, CharacterAnimIds.ArmsHold, CharacterAnimIds.Turn, CharacterAnimIds.IdleSet })
                Assert.That(names.Contains(n), Is.True, n);
        }

        [Test]
        public void ClipKeyHelpersCoverEveryValue()
        {
            for (var d = 1; d <= CharacterAnimIds.DeathCount; d++) Assert.That(ClipKeys.ForDeath(d), Is.Not.Null);
            Assert.That(ClipKeys.ForDeath(CharacterAnimIds.DeathBlown), Is.EqualTo(ClipKeys.DeathBlown));
            Assert.That(ClipKeys.ForHitDir(CharacterAnimIds.HitLeft), Is.EqualTo(ClipKeys.HitLeft));
            Assert.That(ClipKeys.ForHandPose(HandPose.RifleGrip), Is.EqualTo("A_Pose_RifleGrip"));
            Assert.That(ClipKeys.ForHandPose(HandPose.Flat), Is.EqualTo("A_Pose_FlatHands"));
        }
    }
}
