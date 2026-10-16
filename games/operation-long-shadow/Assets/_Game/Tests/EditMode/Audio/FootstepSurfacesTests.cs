using LongShadow.Audio.Foley;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.2: footsteps by surface for AI and squad bodies.</summary>
    public class FootstepSurfacesTests
    {
        [Test]
        public void TaggedSurfaceWinsElseTheZoneFloor()
        {
            Assert.That(FootstepSurfaces.Choose(SurfaceType.Wood, ZoneType.Street), Is.EqualTo(SurfaceType.Wood));
            Assert.That(FootstepSurfaces.Choose(SurfaceType.Default, ZoneType.Street), Is.EqualTo(SurfaceType.Stone));
            Assert.That(FootstepSurfaces.Choose(SurfaceType.Default, ZoneType.OpenDesert), Is.EqualTo(SurfaceType.Sand));
            Assert.That(FootstepSurfaces.Choose(SurfaceType.Default, ZoneType.Roof), Is.EqualTo(SurfaceType.Plaster));
            Assert.That(FootstepSurfaces.Choose(SurfaceType.Flesh, ZoneType.Interior), Is.EqualTo(SurfaceType.Tile));
            Assert.That(AudioCues.Footstep(FootstepSurfaces.ZoneDefault(ZoneType.Roof)), Is.EqualTo("SFX_FS_ROOF"));
            Assert.That(AudioCues.Footstep(FootstepSurfaces.ZoneDefault(ZoneType.Cabin)), Is.EqualTo("SFX_FS_METALSTAIR"));
        }

        [Test]
        public void VolumeRisesWithSpeed()
        {
            Assert.That(FootstepSurfaces.Volume(1f, 1.6f, 4.5f, 0.55f, 1f), Is.EqualTo(0.55f));
            Assert.That(FootstepSurfaces.Volume(6f, 1.6f, 4.5f, 0.55f, 1f), Is.EqualTo(1f));
            var mid = FootstepSurfaces.Volume(3.05f, 1.6f, 4.5f, 0.55f, 1f);
            Assert.That(mid, Is.EqualTo(0.775f).Within(1e-4f));
        }
    }
}
