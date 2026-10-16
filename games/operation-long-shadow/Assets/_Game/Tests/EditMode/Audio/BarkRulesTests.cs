using LongShadow.Audio.VO;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06 DoD / GDD §11: barks are subtitled only within 25 m and only with "Translate enemy chatter" on.</summary>
    public class BarkRulesTests
    {
        [Test]
        public void SubtitleRule()
        {
            Assert.That(BarkRules.ShouldSubtitle(10f, true, true), Is.True);
            Assert.That(BarkRules.ShouldSubtitle(25f, true, true), Is.True);
            Assert.That(BarkRules.ShouldSubtitle(25.1f, true, true), Is.False);
            Assert.That(BarkRules.ShouldSubtitle(10f, true, false), Is.False);
            Assert.That(BarkRules.ShouldSubtitle(10f, false, true), Is.False);
        }

        [Test]
        public void FarChainAndStableVoices()
        {
            Assert.That(BarkRules.UseFarChain(20f), Is.False);
            Assert.That(BarkRules.UseFarChain(45f), Is.True);
            Assert.That(BarkRules.VoiceOf(42), Is.EqualTo(BarkRules.VoiceOf(42)));
            Assert.That(BarkRules.VoiceOf(2), Is.Not.EqualTo(BarkRules.VoiceOf(3)));
        }
    }
}
