using LongShadow.Audio.Core;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.9 and WP06_SFX notes: tails on single shots, every 4th auto round and on release.</summary>
    public class BurstTrackerTests
    {
        [Test]
        public void SingleShotPlaysItsTailAtOnce()
        {
            var b = new BurstTracker();
            Assert.That(b.OnShot(1, "AK", false, Vector3.zero, 0f, out var first), Is.True);
            Assert.That(first, Is.True);
            var rel = new BurstTracker.Release[4];
            Assert.That(b.CollectReleases(1f, rel), Is.EqualTo(0), "the tail already played");
            Assert.That(b.CountOf(1), Is.EqualTo(0));
        }

        [Test]
        public void AutoFireTailsEveryFourthAndOnRelease()
        {
            var b = new BurstTracker();
            const float interval = 60f / 750f; // 750 RPM
            var tails = 0;
            for (var i = 0; i < 7; i++)
                if (b.OnShot(1, "COMMANDO", true, Vector3.zero, i * interval, out _)) tails++;
            Assert.That(tails, Is.EqualTo(2), "round 1 and round 4");
            var rel = new BurstTracker.Release[4];
            var last = 6 * interval;
            Assert.That(b.CollectReleases(last + 0.05f, rel), Is.EqualTo(0), "not released yet");
            Assert.That(b.CollectReleases(last + 0.2f, rel), Is.EqualTo(1));
            Assert.That(rel[0].Offset, Is.EqualTo(0.2f).Within(1e-4f), "the tail starts that far in, lined up with the last round");
            Assert.That(rel[0].IsPlayer, Is.True);
        }

        [Test]
        public void ReleaseIsSkippedWhenTheLastRoundHadATail()
        {
            var b = new BurstTracker();
            for (var i = 0; i < 4; i++) b.OnShot(2, "AK", false, Vector3.zero, i * 0.1f, out _);
            var rel = new BurstTracker.Release[4];
            Assert.That(b.CollectReleases(2f, rel), Is.EqualTo(0));
        }

        [Test]
        public void ShootersAreSeparatedByPosition()
        {
            var k1 = BurstTracker.KeyOf("AK", false, new Vector3(0f, 0f, 0f));
            var k2 = BurstTracker.KeyOf("AK", false, new Vector3(10f, 0f, 0f));
            Assert.That(k1, Is.Not.EqualTo(k2));
            Assert.That(BurstTracker.KeyOf("AK", true, Vector3.zero), Is.EqualTo(BurstTracker.KeyOf("AK", true, Vector3.one * 50f)));
        }
    }
}
