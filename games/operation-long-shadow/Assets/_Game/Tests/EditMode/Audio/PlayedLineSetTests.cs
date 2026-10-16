using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06 DoD / GDD §17: lines played before a checkpoint do not repeat after its reload.</summary>
    public class PlayedLineSetTests
    {
        private static DialogueLine L(string id, bool critical = false, bool repeatable = false) =>
            new DialogueLine(id, SpeakerIds.Hollis, "SUNDIAL-ACTUAL", "x", null, false)
                .WithRules(3, VOPriority.Scripted, VOChannel.Net, critical, repeatable);

        [Test]
        public void NoRepeatAfterCheckpoint()
        {
            var s = new PlayedLineSet();
            s.MarkPlayed("D021");
            s.MarkPlayed("D022");
            var snapshot = new System.Collections.Generic.List<string>();
            s.CopyTo(snapshot);
            s.MarkPlayed("D023"); // after the checkpoint
            s.Restore(snapshot);
            Assert.That(s.ShouldSkip(L("D021")), Is.True);
            Assert.That(s.ShouldSkip(L("D022")), Is.True);
            Assert.That(s.ShouldSkip(L("D023")), Is.False, "lines after the checkpoint play again as the beat re-runs");
            Assert.That(s.HasPlayed("D023"), Is.False);
            Assert.That(s.HasPlayed("D021"), Is.True);
        }

        [Test]
        public void ObjectiveCriticalAndSystemicLinesRepeat()
        {
            var s = new PlayedLineSet();
            s.Restore(new[] { "D029", "CO_HOL_01", "D093" });
            Assert.That(s.ShouldSkip(L("D029", critical: true)), Is.False);
            Assert.That(s.ShouldSkip(L("CO_HOL_01", repeatable: true)), Is.False);
            Assert.That(s.ShouldSkip(L("D093", repeatable: true)), Is.False);
        }

        [Test]
        public void SnapshotIsSortedAndClearable()
        {
            var s = new PlayedLineSet();
            s.MarkPlayed("D030");
            s.MarkPlayed("D007");
            var ids = new System.Collections.Generic.List<string>();
            s.CopyTo(ids);
            Assert.That(ids, Is.EqualTo(new[] { "D007", "D030" }));
            s.Clear();
            Assert.That(s.Count, Is.EqualTo(0));
        }
    }
}
