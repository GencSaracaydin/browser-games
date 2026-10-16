using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06 DoD: VO priority order (Scripted &gt; Objective &gt; Callout &gt; Bark), FIFO within a priority.</summary>
    public class VOQueueTests
    {
        private static VORequest R(int handle, VOPriority p, long seq, float eligible = 0f, float expires = float.MaxValue, bool urgent = false) =>
            new VORequest { Handle = handle, Priority = p, Sequence = seq, EligibleAt = eligible, ExpiresAt = expires, Urgent = urgent };

        [Test]
        public void PriorityThenFifo()
        {
            var q = new VOQueue();
            q.Enqueue(R(1, VOPriority.Callout, 1));
            q.Enqueue(R(2, VOPriority.Scripted, 2));
            q.Enqueue(R(3, VOPriority.Objective, 3));
            q.Enqueue(R(4, VOPriority.Scripted, 4));
            q.Enqueue(R(5, VOPriority.Bark, 5));
            Assert.That(q.PopNext(0f).Handle, Is.EqualTo(2));
            Assert.That(q.PopNext(0f).Handle, Is.EqualTo(4));
            Assert.That(q.PopNext(0f).Handle, Is.EqualTo(3));
            Assert.That(q.PopNext(0f).Handle, Is.EqualTo(1));
            Assert.That(q.PopNext(0f).Handle, Is.EqualTo(5));
            Assert.That(q.PopNext(0f), Is.Null);
        }

        [Test]
        public void DelaysAndUrgency()
        {
            var q = new VOQueue();
            q.Enqueue(R(1, VOPriority.Scripted, 1, eligible: 2f));
            q.Enqueue(R(2, VOPriority.Objective, 2));
            q.Enqueue(R(3, VOPriority.Objective, 3, urgent: true));
            Assert.That(q.PeekNext(0f).Handle, Is.EqualTo(3), "urgent jumps the queue within its priority");
            q.Remove(3);
            Assert.That(q.PeekNext(0f).Handle, Is.EqualTo(2), "the scripted line is still delayed");
            Assert.That(q.PeekNext(2f).Handle, Is.EqualTo(1));
        }

        [Test]
        public void StaleCalloutsAreDropped()
        {
            var q = new VOQueue();
            q.Enqueue(R(1, VOPriority.Callout, 1, expires: VOQueue.CalloutMaxWait));
            var dropped = new System.Collections.Generic.List<VORequest>();
            Assert.That(q.DropExpired(1f, dropped), Is.EqualTo(0));
            Assert.That(q.DropExpired(2f, dropped), Is.EqualTo(1));
            Assert.That(dropped[0].Handle, Is.EqualTo(1));
            Assert.That(q.Count, Is.EqualTo(0));
        }

        [Test]
        public void InterruptRules()
        {
            Assert.That(VOQueue.ShouldInterrupt(VOPriority.Callout, VOPriority.Scripted, false), Is.True);
            Assert.That(VOQueue.ShouldInterrupt(VOPriority.Bark, VOPriority.Objective, false), Is.True);
            Assert.That(VOQueue.ShouldInterrupt(VOPriority.Objective, VOPriority.Scripted, false), Is.False, "scripted lines wait for objective lines");
            Assert.That(VOQueue.ShouldInterrupt(VOPriority.Scripted, VOPriority.Callout, false), Is.False);
            Assert.That(VOQueue.ShouldInterrupt(VOPriority.Scripted, VOPriority.Callout, true), Is.True);
        }
    }
}
