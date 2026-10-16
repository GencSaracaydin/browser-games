using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>TECH_PLAN §5.4: the highest-priority request wins; equal priorities: the most recent wins; empty = 1.</summary>
    public class TimeScaleStackTests
    {
        [Test]
        public void EmptyStackIsRealTime()
        {
            var s = new TimeScaleStack();
            Assert.That(s.Evaluate(), Is.EqualTo(1f));
            Assert.That(s.Count, Is.Zero);
        }

        [Test]
        public void HighestPriorityWinsRegardlessOfOrder()
        {
            var s = new TimeScaleStack();
            var slow = s.Push(0.35f, TimePriority.SlowMo, "breach");      // GDD: breach 0.35
            var pause = s.Push(0f, TimePriority.Pause, "pause");
            var death = s.Push(0.3f, TimePriority.Death, "death");
            Assert.That(s.Evaluate(out var reason), Is.EqualTo(0f));
            Assert.That(reason, Is.EqualTo("pause"));
            s.Pop(pause);
            Assert.That(s.Evaluate(), Is.EqualTo(0.3f));
            s.Pop(death);
            Assert.That(s.Evaluate(), Is.EqualTo(0.35f));
            s.Pop(slow);
            Assert.That(s.Evaluate(), Is.EqualTo(1f));
        }

        [Test]
        public void EqualPriorityMostRecentWins()
        {
            var s = new TimeScaleStack();
            var a = s.Push(0.5f, TimePriority.Cinematic, "a");
            s.Push(0.8f, TimePriority.Cinematic, "b");
            Assert.That(s.Evaluate(), Is.EqualTo(0.8f));
            s.Pop(a);
            Assert.That(s.Evaluate(), Is.EqualTo(0.8f));
        }

        [Test]
        public void HandlesAreUniqueAndPopIsIdempotent()
        {
            var s = new TimeScaleStack();
            var a = s.Push(0.5f, 1, "a");
            Assert.IsTrue(s.Pop(a));
            var b = s.Push(0.5f, 1, "b");
            Assert.That(b, Is.Not.EqualTo(a));
            Assert.IsFalse(s.Pop(a));
            Assert.IsFalse(s.Pop(12345));
            Assert.That(a, Is.GreaterThan(0));
        }

        [Test]
        public void SetScaleRampsALiveRequest()
        {
            var s = new TimeScaleStack();
            var h = s.Push(1f, TimePriority.Death, "death");
            Assert.IsTrue(s.SetScale(h, 0.3f));
            Assert.That(s.Evaluate(), Is.EqualTo(0.3f));
            Assert.IsFalse(s.SetScale(h + 99, 0.1f));
        }

        [Test]
        public void InvalidScalesAreSanitised()
        {
            var s = new TimeScaleStack();
            var nan = s.Push(float.NaN, 5, "nan");
            Assert.That(s.Evaluate(), Is.EqualTo(1f));
            s.Pop(nan);
            var neg = s.Push(-2f, 5, "neg");
            Assert.That(s.Evaluate(), Is.EqualTo(0f));
            s.Pop(neg);
            s.Push(1e6f, 5, "huge");
            Assert.That(s.Evaluate(), Is.EqualTo(TimeScaleStack.MaxScale));
        }
    }
}
