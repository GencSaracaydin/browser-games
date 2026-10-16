using LongShadow.Audio.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §3.12: loops play their body [lead-in][body][tail-out] sample-accurately.</summary>
    public class LoopScheduleTests
    {
        [Test]
        public void SecondsToSamples()
        {
            // AMB beds: lead-in 2048, body 960000 at 48 kHz.
            Assert.That(LoopSchedule.ToSamples(2048f / 48000f, 962048f / 48000f, 48000, 964096, out var s, out var e), Is.True);
            Assert.That(s, Is.EqualTo(2048));
            Assert.That(e, Is.EqualTo(962048));
            Assert.That(LoopSchedule.ToSamples(0f, 0f, 48000, 1000, out s, out e), Is.False);
            Assert.That(e, Is.EqualTo(1000));
            Assert.That(LoopSchedule.ToSamples(0.5f, 0.51f, 48000, 48000, out _, out _), Is.False, "a 10 ms body is not usable");
        }

        [Test]
        public void StartPositionsWrapIntoTheBody()
        {
            Assert.That(LoopSchedule.ClampStart(-5, 100, 1100), Is.EqualTo(0));
            Assert.That(LoopSchedule.ClampStart(500, 100, 1100), Is.EqualTo(500));
            Assert.That(LoopSchedule.ClampStart(1150, 100, 1100), Is.EqualTo(150), "the tail-out wraps back to the body");
        }

        [Test]
        public void PositionAfterWraps()
        {
            Assert.That(LoopSchedule.PositionAfter(0, 0.5, 100, 1100, 1000), Is.EqualTo(500));
            Assert.That(LoopSchedule.PositionAfter(1000, 0.2, 100, 1100, 1000), Is.EqualTo(200));
            Assert.That(LoopSchedule.BodySeconds(100, 1100, 1000), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(LoopSchedule.FirstSegmentSeconds(600, 100, 1100, 1000), Is.EqualTo(0.5).Within(1e-9));
        }
    }
}
