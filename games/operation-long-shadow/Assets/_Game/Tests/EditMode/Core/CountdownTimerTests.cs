using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Core
{
    public class CountdownTimerTests
    {
        [Test]
        public void FiresOnceOnTheTickThatReachesZero()
        {
            var t = new CountdownTimer();
            t.Start(6f); // OOB countdown, GDD §17
            Assert.IsTrue(t.IsRunning);
            Assert.That(t.SecondsLeftCeil, Is.EqualTo(6));
            var fired = 0;
            for (var i = 0; i < 70; i++)
                if (t.Tick(0.1f)) fired++;
            Assert.That(fired, Is.EqualTo(1));
            Assert.IsFalse(t.IsRunning);
            Assert.That(t.Progress01, Is.EqualTo(1f));
        }

        [Test]
        public void StopCancels()
        {
            var t = new CountdownTimer();
            t.Start(4f);
            t.Tick(1f);
            Assert.That(t.Progress01, Is.EqualTo(0.25f).Within(1e-5f));
            t.Stop();
            Assert.IsFalse(t.Tick(10f));
        }
    }
}
