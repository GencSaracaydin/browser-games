using LongShadow.Player.Bounds;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>GDD §17 out-of-bounds countdown rules (<see cref="OutOfBoundsCountdown"/>).</summary>
    public class OutOfBoundsCountdownTests
    {
        [Test]
        public void SixSecondCountdownShowsEachSecondThenExpiresOnce()
        {
            var c = new OutOfBoundsCountdown();
            Assert.IsTrue(c.Enter("OOB.RidgeWest", 6f));
            Assert.That(c.Tick(0f), Is.EqualTo(OutOfBoundsCountdown.Result.SecondChanged));
            Assert.That(c.SecondsLeft, Is.EqualTo(6));
            var seconds = 1;
            var expired = 0;
            for (var i = 0; i < 700; i++)
            {
                var r = c.Tick(0.01f);
                if (r == OutOfBoundsCountdown.Result.SecondChanged) seconds++;
                if (r == OutOfBoundsCountdown.Result.Expired) expired++;
            }
            Assert.That(seconds, Is.EqualTo(6), "6, 5, 4, 3, 2, 1");
            Assert.That(expired, Is.EqualTo(1));
            Assert.IsTrue(c.HasFailed);
            Assert.IsFalse(c.IsCounting);
            Assert.IsFalse(c.Enter("OOB.RidgeSouth", 6f), "no new countdown until the reload resets it");
            c.Reset();
            Assert.IsFalse(c.HasFailed);
            Assert.That(c.InsideCount, Is.EqualTo(0));
        }

        [Test]
        public void LeavingInTimeCancels()
        {
            var c = new OutOfBoundsCountdown();
            c.Enter("OOB.A", 6f);
            c.Tick(3f);
            Assert.IsTrue(c.Exit("OOB.A"));
            Assert.IsFalse(c.IsCounting);
            Assert.That(c.Tick(10f), Is.EqualTo(OutOfBoundsCountdown.Result.None));
            Assert.IsFalse(c.HasFailed);
            Assert.IsTrue(c.Enter("OOB.A", 6f), "re-entering starts a fresh countdown");
            Assert.That(c.Remaining, Is.EqualTo(6f));
        }

        [Test]
        public void OverlappingVolumesKeepOneCountdown()
        {
            var c = new OutOfBoundsCountdown();
            Assert.IsTrue(c.Enter("OOB.A", 6f));
            Assert.IsFalse(c.Enter("OOB.B", 6f), "already counting");
            Assert.IsFalse(c.Enter("OOB.B", 6f), "duplicate enter ignored");
            c.Tick(2f);
            Assert.IsFalse(c.Exit("OOB.A"), "still inside B");
            Assert.IsTrue(c.IsCounting);
            Assert.That(c.Remaining, Is.EqualTo(4f).Within(1e-4f), "the countdown does not restart");
            Assert.IsTrue(c.Exit("OOB.B"));
            Assert.IsFalse(c.Exit("OOB.C"), "unknown volumes are ignored");
        }

        [Test]
        public void CancelKeepsOccupancy()
        {
            var c = new OutOfBoundsCountdown();
            c.Enter("OOB.A", 6f);
            c.Cancel();
            Assert.IsFalse(c.IsCounting);
            Assert.That(c.InsideCount, Is.EqualTo(1));
        }
    }
}
