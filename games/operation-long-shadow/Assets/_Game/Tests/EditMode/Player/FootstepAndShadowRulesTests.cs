using LongShadow.Core;
using LongShadow.Player.Body;
using LongShadow.Player.Footsteps;
using LongShadow.Player.Movement;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Footstep noise radii (GDD §14.2), surface mapping (TECH_PLAN §7.3) and shadow-proxy rules (D19).</summary>
    public class FootstepAndShadowRulesTests
    {
        [Test]
        public void FootstepNoiseRadiiMatchGdd14_2()
        {
            Assert.That(FootstepMath.NoiseRadius(Stance.Sprint), Is.EqualTo(12f));
            Assert.That(FootstepMath.NoiseRadius(Stance.Slide), Is.EqualTo(12f));
            Assert.That(FootstepMath.NoiseRadius(Stance.Stand), Is.EqualTo(4f));
            Assert.That(FootstepMath.NoiseRadius(Stance.Crouch), Is.EqualTo(1.5f));
            Assert.That(FootstepMath.Volume(Stance.Sprint, 0f), Is.GreaterThan(FootstepMath.Volume(Stance.Stand, 0f)));
            Assert.That(FootstepMath.Volume(Stance.Stand, 1f), Is.LessThan(FootstepMath.Volume(Stance.Stand, 0f)), "ADS walking is quieter");
            Assert.That(FootstepMath.Volume(Stance.Crouch, 0f), Is.LessThan(FootstepMath.Volume(Stance.Stand, 1f)));
        }

        [Test]
        public void SurfacesMapToTheTechPlanFootstepSets()
        {
            Assert.That(AudioCues.Footstep(SurfaceType.Sand), Is.EqualTo("SFX_FS_SAND"));
            Assert.That(AudioCues.Footstep(SurfaceType.Concrete), Is.EqualTo("SFX_FS_STONE"));
            Assert.That(AudioCues.Footstep(SurfaceType.Plaster), Is.EqualTo("SFX_FS_ROOF"));
            Assert.That(AudioCues.Footstep(SurfaceType.Metal), Is.EqualTo("SFX_FS_METALSTAIR"));
            Assert.That(AudioCues.Footstep(SurfaceType.SaltCrust), Is.EqualTo("SFX_FS_SALTCRUST"));
            Assert.That(AudioCues.FootstepFallback, Is.EqualTo("SFX_FS_SAND"), "SALTCRUST falls back to SAND while missing");
            Assert.That(FootstepMath.ZoneDefaultSurface(ZoneType.Roof), Is.EqualTo(SurfaceType.Plaster));
            Assert.That(FootstepMath.ZoneDefaultSurface(ZoneType.Interior), Is.EqualTo(SurfaceType.Tile));
            Assert.That(FootstepMath.ZoneDefaultSurface(ZoneType.OpenDesert), Is.EqualTo(SurfaceType.Sand));
            Assert.That(FootstepMath.ZoneDefaultSurface(ZoneType.Cabin), Is.EqualTo(SurfaceType.Metal));
        }

        [Test]
        public void ShadowProxyHidesInBoothCabinDeathAndProne()
        {
            Assert.IsTrue(PlayerShadowProxy.ShouldShow(true, true, true, false, 1.65f));
            Assert.IsTrue(PlayerShadowProxy.ShouldShow(true, true, true, false, 1.05f), "crouch and slide keep the shadow");
            Assert.IsFalse(PlayerShadowProxy.ShouldShow(false, true, true, false, 1.65f), "SetBodyVisibility");
            Assert.IsFalse(PlayerShadowProxy.ShouldShow(true, false, true, false, 1.65f), "booth / cinematics");
            Assert.IsFalse(PlayerShadowProxy.ShouldShow(true, true, false, false, 1.65f), "dead");
            Assert.IsFalse(PlayerShadowProxy.ShouldShow(true, true, true, true, 1.65f), "mounted in the cabin");
            Assert.IsFalse(PlayerShadowProxy.ShouldShow(true, true, true, false, 0.35f), "scripted prone");
        }

        [Test]
        public void ShadowProxyPoseFollowsStance()
        {
            var s = new MotorSettings();
            Assert.IsTrue(PlayerShadowProxy.IsCrouchPose(Stance.Crouch, true));
            Assert.IsTrue(PlayerShadowProxy.IsCrouchPose(Stance.Slide, true));
            Assert.IsFalse(PlayerShadowProxy.IsCrouchPose(Stance.Sprint, false));
            Assert.IsFalse(PlayerShadowProxy.IsCrouchPose(Stance.Stand, false));
            Assert.IsTrue(PlayerShadowProxy.IsAirbornePose(Stance.Airborne));
            Assert.IsTrue(PlayerShadowProxy.IsAirbornePose(Stance.Mantle));
            Assert.IsTrue(PlayerShadowProxy.IsAirbornePose(Stance.Ladder));
            Assert.IsFalse(PlayerShadowProxy.IsAirbornePose(Stance.Sprint));
            Assert.That(PlayerShadowProxy.GreyboxHeight(Stance.Stand, false, s), Is.EqualTo(1.8f));
            Assert.That(PlayerShadowProxy.GreyboxHeight(Stance.Crouch, true, s), Is.EqualTo(1.1f));
            Assert.That(PlayerShadowProxy.GreyboxHeight(Stance.Slide, true, s), Is.EqualTo(0.9f));
        }
    }
}
