using LongShadow.Audio.VO;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>G9 (GDD §8.5): D039 starts at Tinker's pack and jumps onto the net at the first "Seven".</summary>
    public class PositionalRadioTests
    {
        [Test]
        public void SwitchesAtTheGivenTime()
        {
            PositionalRadio.Weights(1.0f, 1.83f, out var pos, out var radio);
            Assert.That(pos, Is.EqualTo(1f));
            Assert.That(radio, Is.EqualTo(0f));
            PositionalRadio.Weights(1.83f + PositionalRadio.Crossfade * 0.5f, 1.83f, out pos, out radio);
            Assert.That(radio, Is.EqualTo(0.5f).Within(1e-4f));
            PositionalRadio.Weights(3f, 1.83f, out pos, out radio);
            Assert.That(pos, Is.EqualTo(0f));
            Assert.That(radio, Is.EqualTo(1f));
        }

        [Test]
        public void NoSwitchStaysPositional()
        {
            PositionalRadio.Weights(10f, 0f, out var pos, out var radio);
            Assert.That(pos, Is.EqualTo(1f));
            Assert.That(radio, Is.EqualTo(0f));
        }
    }
}
