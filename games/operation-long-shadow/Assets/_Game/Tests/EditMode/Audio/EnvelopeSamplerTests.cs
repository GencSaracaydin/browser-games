using LongShadow.Audio.VO;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN D10: 30 Hz envelopes drive the VU needle and the jaw flap.</summary>
    public class EnvelopeSamplerTests
    {
        [Test]
        public void EncodesToThePeak()
        {
            var b = EnvelopeSampler.Encode(new[] { 0f, 0.1f, 0.2f }, 0.2f);
            Assert.That(b, Is.EqualTo(new byte[] { 0, 128, 255 }));
            var auto = EnvelopeSampler.Encode(new[] { 0.05f, 0.1f }, 0f);
            Assert.That(auto[1], Is.EqualTo(255));
            Assert.That(EnvelopeSampler.Encode(null, 1f), Is.Empty);
        }

        [Test]
        public void SamplesWithInterpolation()
        {
            var env = new byte[] { 0, 255, 0 };
            Assert.That(EnvelopeSampler.Sample(env, 0f), Is.EqualTo(0f));
            Assert.That(EnvelopeSampler.Sample(env, 1f / 30f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(EnvelopeSampler.Sample(env, 0.5f / 30f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(EnvelopeSampler.Sample(env, 1f), Is.EqualTo(0f), "past the end is silence");
            Assert.That(EnvelopeSampler.Sample(env, -1f), Is.EqualTo(0f));
            Assert.That(EnvelopeSampler.Sample(null, 0.1f), Is.EqualTo(0f));
        }
    }
}
