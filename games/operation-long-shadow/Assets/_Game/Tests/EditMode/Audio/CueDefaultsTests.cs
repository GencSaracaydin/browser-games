using LongShadow.Audio.Editor;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>The mix design written into LIB_Audio (TECH_PLAN §7.9, D9).</summary>
    public class CueDefaultsTests
    {
        [Test]
        public void WeaponShotsCarryAndCrossfadeToFar()
        {
            var s = CueDefaults.Resolve("SFX_WPN_AK_SHOT", "SFX/Weapons");
            Assert.That(s.Category, Is.EqualTo(AudioCategories.Effects));
            Assert.That(s.SpatialBlend, Is.EqualTo(1f));
            Assert.That(s.SpeedOfSound, Is.True);
            Assert.That(s.FarCrossfade.x, Is.EqualTo(40f));
            Assert.That(s.FarCrossfade.y, Is.EqualTo(120f));
            Assert.That(s.MaxDistance, Is.GreaterThanOrEqualTo(200f));
        }

        [Test]
        public void ExplosionsAreBigAndDelayed()
        {
            var s = CueDefaults.Resolve("SFX_EXP_FRAG", "SFX/Explosions");
            Assert.That(s.MinDistance, Is.EqualTo(10f));
            Assert.That(s.MaxDistance, Is.EqualTo(400f));
            Assert.That(s.SpeedOfSound, Is.True);
            Assert.That(s.Priority, Is.GreaterThan(90));
        }

        [Test]
        public void CategoriesByFolder()
        {
            Assert.That(CueDefaults.Resolve("AMB_CICADAS", "Ambience").Category, Is.EqualTo(AudioCategories.Ambience));
            Assert.That(CueDefaults.Resolve("AMB_CICADAS", "Ambience").SpatialBlend, Is.EqualTo(0f));
            Assert.That(CueDefaults.Resolve("MUS_02_MARKET_LO", "Music").Category, Is.EqualTo(AudioCategories.Music));
            Assert.That(CueDefaults.Resolve("SFX_UI_HITMARKER", "SFX/UI").Category, Is.EqualTo(AudioCategories.UI));
            Assert.That(CueDefaults.Resolve("SFX_UI_HITMARKER", "SFX/UI").SpatialBlend, Is.EqualTo(0f));
        }

        [Test]
        public void DiegeticRadioSoundsAreVoices()
        {
            var num = CueDefaults.Resolve("NUM_BASEMENT_LOOP", "Music");
            Assert.That(num.Category, Is.EqualTo(AudioCategories.Voice), "the numbers loop must not duck itself under the music duck");
            Assert.That(num.SpatialBlend, Is.EqualTo(1f));
            Assert.That(CueDefaults.Resolve("INTERVAL_SIGNAL", "Music").SpatialBlend, Is.EqualTo(1f));
            Assert.That(CueDefaults.Resolve("INTERVAL_SIGNAL_HF_AM", "Music").SpatialBlend, Is.EqualTo(0f));
        }

        [Test]
        public void BoothFoleyIsSmallAndSirenIsHuge()
        {
            Assert.That(CueDefaults.Resolve("SFX_PEN_TAP", "SFX/SetPieces").MaxDistance, Is.LessThanOrEqualTo(10f));
            Assert.That(CueDefaults.Resolve("SFX_CURFEW_SIREN", "SFX/SetPieces").MaxDistance, Is.GreaterThan(1000f));
            Assert.That(CueDefaults.Resolve("SFX_TINNITUS", "SFX/Synth").SpatialBlend, Is.EqualTo(0f));
            Assert.That(CueDefaults.Resolve("SFX_HEARTBEAT", "SFX/SetPieces").SpatialBlend, Is.EqualTo(0f));
        }

        [Test]
        public void SlicedFoleyIsCloseAndUnjittered()
        {
            var r = CueDefaults.Resolve("SFX_WPN_COMMANDO_RELOAD_MAGIN", "SFX/Weapons");
            Assert.That(r.MaxDistance, Is.LessThanOrEqualTo(15f));
            Assert.That(r.PitchJitter, Is.LessThan(0.02f));
            Assert.That(CueDefaults.Resolve("SFX_WPN_COMMANDO_MECH", "SFX/Weapons").Priority, Is.GreaterThan(80));
        }
    }
}
