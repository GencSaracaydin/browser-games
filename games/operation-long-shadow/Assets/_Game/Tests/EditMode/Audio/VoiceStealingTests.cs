using LongShadow.Audio.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>TECH_PLAN §7.9: 24 audible voices with priority stealing.</summary>
    public class VoiceStealingTests
    {
        private static VoiceInfo V(int prio, float aud, float start, bool prot = false) =>
            new VoiceInfo { Active = true, Priority = prio, Audibility = aud, StartedAt = start, Protected = prot };

        [Test]
        public void LowestPriorityThenQuietestThenOldest()
        {
            var v = new[] { V(80, 0.9f, 1f), V(30, 0.5f, 2f), V(30, 0.2f, 3f), V(30, 0.2f, 0.5f), V(90, 0.1f, 0f) };
            Assert.That(VoiceStealing.PickVictim(v, v.Length, 50, 0.5f), Is.EqualTo(3), "priority 30, quietest, oldest");
        }

        [Test]
        public void HigherPriorityIsNeverStolen()
        {
            var v = new[] { V(80, 0.1f, 0f), V(90, 0.1f, 0f) };
            Assert.That(VoiceStealing.PickVictim(v, v.Length, 50, 1f), Is.EqualTo(-1));
        }

        [Test]
        public void EqualPriorityOnlyIfNotLouder()
        {
            var v = new[] { V(50, 0.8f, 0f) };
            Assert.That(VoiceStealing.PickVictim(v, 1, 50, 0.5f), Is.EqualTo(-1));
            Assert.That(VoiceStealing.PickVictim(v, 1, 50, 0.9f), Is.EqualTo(0));
        }

        [Test]
        public void ProtectedAndInactiveAreSkipped()
        {
            var v = new[] { V(0, 0f, 0f, true), new VoiceInfo { Active = false }, V(10, 0.3f, 1f) };
            Assert.That(VoiceStealing.PickVictim(v, v.Length, 100, 1f), Is.EqualTo(2));
            Assert.That(VoiceStealing.ActiveCount(v, v.Length), Is.EqualTo(2));
        }
    }
}
