using LongShadow.Combat.Data;
using LongShadow.Combat.Viewmodel;
using LongShadow.Combat.Weapons;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Combat
{
    /// <summary>Procedural view model: ADS sight alignment, kick and sway springs, reload choreography, hand actions.</summary>
    public class ViewmodelTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void AdsPutsBothSightsOnTheViewAxis()
        {
            foreach (var w in WeaponTable.PlayerWeapons)
            {
                var s = w.Viewmodel.Sockets;
                ViewmodelMotion.AdsPose(s.SightRear, s.SightFront, w.Viewmodel.EyeRelief, Vector3.zero, out var pos, out var rot);
                var rear = pos + rot * s.SightRear;
                var front = pos + rot * s.SightFront;
                Assert.That(rear.x, Is.EqualTo(0f).Within(1e-4f), w.Id);
                Assert.That(rear.y, Is.EqualTo(0f).Within(1e-4f), w.Id);
                Assert.That(rear.z, Is.EqualTo(w.Viewmodel.EyeRelief).Within(1e-4f), w.Id);
                Assert.That(front.x, Is.EqualTo(0f).Within(1e-4f), w.Id + " front sight");
                Assert.That(front.y, Is.EqualTo(0f).Within(1e-4f), w.Id + " front sight");
                Assert.That(front.z, Is.GreaterThan(rear.z), w.Id);
            }
        }

        [Test]
        public void RestingAdsPoseEqualsTheAlignedPose()
        {
            var w = WeaponTable.AK;
            var motion = new ViewmodelMotion(new ViewmodelTuning { BreathAmplitude = Vector2.zero, BreathRotation = 0f });
            var pose = ViewmodelMotion.PoseSet.From(w);
            var input = new ViewmodelInput { Dt = Dt, AdsBlend = 1f, WalkSpeed = 4.4f, Reload = ReloadPose.Rest };
            Vector3 p = default;
            Quaternion r = default;
            for (var i = 0; i < 60; i++) motion.Step(input, pose, 0.4f, out p, out r);
            ViewmodelMotion.AdsPose(pose.SightRear, pose.SightFront, pose.EyeRelief, pose.AdsOffset, out var ap, out var ar);
            Assert.That(Vector3.Distance(p, ap), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(r, ar), Is.LessThan(0.01f));
        }

        [Test]
        public void KickPushesBackAndSpringsHome()
        {
            var w = WeaponTable.Commando;
            var motion = new ViewmodelMotion(new ViewmodelTuning());
            var pose = ViewmodelMotion.PoseSet.From(w);
            motion.AddKick(w.Recoil, new Vector2(0.55f, 0.1f), 0f, 0.5f, 0.5f);
            Assert.That(motion.KickPosition.z, Is.EqualTo(-0.022f).Within(1e-4f), "GDD §13.1: 2.2 cm back");
            var input = new ViewmodelInput { Dt = Dt, WalkSpeed = 4.4f, Reload = ReloadPose.Rest };
            for (var i = 0; i < 60; i++) motion.Step(input, pose, 0.3f, out _, out _);
            Assert.That(motion.KickPosition.magnitude, Is.LessThan(0.001f), "back home within a second");
            Assert.That(motion.KickEuler.magnitude, Is.LessThan(0.05f));
        }

        [Test]
        public void SwayIsBounded()
        {
            var tuning = new ViewmodelTuning();
            var motion = new ViewmodelMotion(tuning);
            var w = WeaponTable.Commando;
            var pose = ViewmodelMotion.PoseSet.From(w);
            var still = new ViewmodelInput { Dt = Dt, WalkSpeed = 4.4f, Reload = ReloadPose.Rest };
            motion.Step(still, pose, 0.3f, out _, out var rest);
            var fast = still;
            fast.LookDelta = new Vector2(0f, 30f); // 1800°/s flick
            var maxAngle = 0f;
            for (var i = 0; i < 30; i++)
            {
                motion.Step(fast, pose, 0.3f, out _, out var r);
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(r, rest));
            }
            Assert.That(maxAngle, Is.LessThan(tuning.SwayMaxDegrees * 2f + 2f));
            Assert.That(maxAngle, Is.GreaterThan(0.5f), "the gun lags a fast turn");
        }

        [Test]
        public void EmptyReloadHidesTheDroppedMagazineUntilTheNewOneAppears()
        {
            var w = WeaponTable.AK;
            var spec = w.EmptyReload;
            var during = ReloadChoreography.Evaluate(w.Kind, spec, (spec.MagOut + spec.MagGrab) * 0.5f, true);
            Assert.That(during.MagVisible, Is.False, "the old magazine fell free");
            Assert.That(during.LeftOnMag, Is.GreaterThan(0.9f), "the hand goes for a fresh one");
            var tr = w.TacticalReload;
            var tactical = ReloadChoreography.Evaluate(w.Kind, tr, tr.MagOut + (tr.MagGrab - tr.MagOut) * 0.3f, false);
            Assert.That(tactical.MagOffset.y, Is.LessThan(0f), "a tactical reload pulls the magazine down");
            var seated = ReloadChoreography.Evaluate(w.Kind, spec, spec.MagIn + 1e-3f, true);
            Assert.That(seated.MagOffset.magnitude, Is.LessThan(1e-3f), "seated at MagIn");
            Assert.That(seated.MagVisible, Is.True);
        }

        [Test]
        public void ChargeOnlyOnEmptyReloads()
        {
            var w = WeaponTable.Commando;
            var mid = w.EmptyReload.ChargeStart + 0.7f * (w.EmptyReload.ChargeEnd - w.EmptyReload.ChargeStart);
            var empty = ReloadChoreography.Evaluate(w.Kind, w.EmptyReload, mid, true);
            Assert.That(empty.LeftOnCharge, Is.GreaterThan(0.9f));
            Assert.That(empty.ChargeTravel, Is.GreaterThan(0.3f));
            for (var t = 0f; t < w.TacticalReload.Duration; t += 0.05f)
                Assert.That(ReloadChoreography.Evaluate(w.Kind, w.TacticalReload, t, false).LeftOnCharge, Is.EqualTo(0f));
        }

        [Test]
        public void ReloadPoseStartsAndEndsAtRest()
        {
            foreach (var w in new[] { WeaponTable.Commando, WeaponTable.HushPuppy, WeaponTable.AK, WeaponTable.RPK })
            {
                foreach (var spec in new[] { w.TacticalReload, w.EmptyReload })
                {
                    var start = ReloadChoreography.Evaluate(w.Kind, spec, 0f, spec == w.EmptyReload);
                    Assert.That(start.WeaponOffset.magnitude, Is.LessThan(1e-4f), w.Id);
                    var end = ReloadChoreography.Evaluate(w.Kind, spec, spec.Duration - 1e-4f, spec == w.EmptyReload);
                    Assert.That(end.WeaponOffset.magnitude, Is.LessThan(2e-3f), w.Id + " ends at rest");
                    Assert.That(end.LeftOnMag + end.LeftOnCharge, Is.LessThan(0.05f), w.Id + " hand back on the handguard");
                    var after = ReloadChoreography.Evaluate(w.Kind, spec, spec.Duration, spec == w.EmptyReload);
                    Assert.That(after.Active, Is.False);
                }
            }
        }

        [Test]
        public void KnifeIsInHandAtTheHit()
        {
            var k = WeaponTable.Knife;
            var hit = HandActions.Knife(k.HitTime, k, false);
            Assert.That(hit.Active && hit.ItemVisible, Is.True);
            Assert.That(hit.HandWeight, Is.EqualTo(1f));
            var lunge = HandActions.Knife(k.HitTime + 0.04f, k, true);
            var plain = HandActions.Knife(k.HitTime + 0.04f, k, false);
            Assert.That(lunge.HandPosition.z, Is.GreaterThan(plain.HandPosition.z), "the lunge reaches further");
            Assert.That(HandActions.Knife(k.Duration, k, false).Active, Is.False);
        }

        [Test]
        public void GrenadeLeavesTheHandAtRelease()
        {
            var f = WeaponTable.Frag;
            Assert.That(HandActions.Grenade(GrenadePhase.Cooking, 1f, f, 0f).ItemVisible, Is.True);
            Assert.That(HandActions.Grenade(GrenadePhase.Release, f.ReleaseTime, f, 0f).ItemVisible, Is.False);
            Assert.That(HandActions.Grenade(GrenadePhase.Recover, 0.1f, f, 0f).ItemVisible, Is.False);
            Assert.That(HandActions.Grenade(GrenadePhase.Recover, f.RecoverTime, f, 0f).HandWeight, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(HandActions.Grenade(GrenadePhase.None, 0f, f, 0f).Active, Is.False);
        }

        [Test]
        public void ViewmodelPointsMapToTheWorldCameraByTheFovRatio()
        {
            var p = new Vector3(0.1f, -0.1f, 0.5f);
            var same = ViewmodelSpace.ToWorldLocal(p, 55f, 55f);
            Assert.That(Vector3.Distance(same, p), Is.LessThan(1e-6f));
            var wider = ViewmodelSpace.ToWorldLocal(p, 55f, 70f);
            Assert.That(Mathf.Abs(wider.x), Is.GreaterThan(Mathf.Abs(p.x)));
            Assert.That(wider.z, Is.EqualTo(p.z));
        }

        [Test]
        public void AdsFovKeepsTheZoomAtAnyFovSetting()
        {
            var w = WeaponTable.Commando;
            Assert.That(PlayerWeapon.AdsVerticalFov(w, WeaponTable.DefaultVerticalFov), Is.EqualTo(47f).Within(1e-3f), "GDD §13.1 at the default FOV");
            var defaultZoom = Mathf.Tan(WeaponTable.DefaultVerticalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(47f * 0.5f * Mathf.Deg2Rad);
            var hipWide = MathX.HorizontalToVerticalFov(105f, 16f / 9f);
            var adsWide = PlayerWeapon.AdsVerticalFov(w, hipWide);
            var zoom = Mathf.Tan(hipWide * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(adsWide * 0.5f * Mathf.Deg2Rad);
            Assert.That(zoom, Is.EqualTo(defaultZoom).Within(1e-3f), "same magnification at FOV 105");
            Assert.That(adsWide, Is.GreaterThan(47f));
        }
    }
}
