using LongShadow.Audio.Music;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §10.3: layer and cue changes land on bar lines of the running cue.</summary>
    public class MusicClockTests
    {
        [Test]
        public void NextBarFollowsTheLoopGrid()
        {
            // MUS_02: loop body from 2048/48000 s, bar 3.334 s, started at DSP 100.
            var start = 2048.0 / 48000.0;
            var c = new MusicClock(100.0, start, start + 33.34, 3.334);
            Assert.That(c.HasGrid, Is.True);
            var next = c.NextBar(100.0 + 5.0, 0.05);
            var musicTime = next - 100.0 - start;
            Assert.That(musicTime / 3.334, Is.EqualTo(2.0).Within(1e-6), "the bar after 5 s is bar 2");
            Assert.That(c.NextBar(100.0 + start + 3.334 - 0.01, 0.05) - (100.0 + start), Is.EqualTo(6.668).Within(1e-6), "respects the minimum lead");
        }

        [Test]
        public void BeatsAreQuarterBars()
        {
            var c = new MusicClock(0.0, 0.0, 40.0, 2.5);
            Assert.That(c.NextBeat(1.0, 0.0), Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void ClipTimeWrapsInsideTheLoop()
        {
            var c = new MusicClock(10.0, 46.7, 76.7, 3.333);
            Assert.That(c.ClipTimeAt(10.0 + 20.0), Is.EqualTo(20.0).Within(1e-9), "still in the intro");
            Assert.That(c.ClipTimeAt(10.0 + 80.0), Is.EqualTo(46.7 + 3.3).Within(1e-6), "wrapped once");
        }

        [Test]
        public void NoGridMeansNow()
        {
            var c = new MusicClock(0.0, 0.0, 0.0, 0.0);
            Assert.That(c.HasGrid, Is.False);
            Assert.That(c.NextBar(5.0, 0.1), Is.EqualTo(5.1).Within(1e-9));
        }
    }
}
