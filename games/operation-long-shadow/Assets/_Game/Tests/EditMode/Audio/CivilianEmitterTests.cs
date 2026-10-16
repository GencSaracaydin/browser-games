using LongShadow.Audio.Ambience;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §0.3 G5: civilians heard, never seen.</summary>
    public class CivilianEmitterTests
    {
        [Test]
        public void CivilianKindsHaveTheirCues()
        {
            CivilianEmitter.CuesOf(CivilianSound.Radio, out var loop, out var latch, out _);
            Assert.That(loop, Is.EqualTo("AMB_CASSETTE_TUNE"));
            Assert.That(latch, Is.EqualTo("SFX_RADIO_TURNED_DOWN"));
            CivilianEmitter.CuesOf(CivilianSound.MutedTv, out loop, out latch, out _);
            Assert.That(loop, Is.EqualTo("SFX_TV_MUFFLED"));
            Assert.That(latch, Is.Null);
            CivilianEmitter.CuesOf(CivilianSound.Pigeons, out loop, out latch, out _);
            Assert.That(loop, Is.Null);
            Assert.That(latch, Is.EqualTo("SFX_PIGEONS"));
        }
    }
}
