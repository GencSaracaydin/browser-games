using LongShadow.Audio.VO;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06 DoD: dry/radio weights at 6/10/12/20 m (GDD §10.1: dry within 8 m, radio beyond 15 m).</summary>
    public class RadioMixTests
    {
        [TestCase(6f, 1f, 0f)]
        [TestCase(8f, 1f, 0f)]
        [TestCase(10f, 5f / 7f, 2f / 7f)]
        [TestCase(12f, 3f / 7f, 4f / 7f)]
        [TestCase(15f, 0f, 1f)]
        [TestCase(20f, 0f, 1f)]
        public void WeightsByDistance(float distance, float expectedDry, float expectedRadio)
        {
            RadioMix.Weights(distance, out var dry, out var radio);
            Assert.That(dry, Is.EqualTo(expectedDry).Within(1e-5f));
            Assert.That(radio, Is.EqualTo(expectedRadio).Within(1e-5f));
            Assert.That(dry + radio, Is.EqualTo(1f).Within(1e-5f), "synced takes: linear crossfade keeps the level");
        }

        [Test]
        public void UnknownDistanceIsRadio()
        {
            Assert.That(RadioMix.RadioShare(float.PositiveInfinity), Is.EqualTo(1f));
            Assert.That(RadioMix.RadioShare(float.NaN), Is.EqualTo(1f));
        }
    }
}
