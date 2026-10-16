using LongShadow.Audio.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06_VO envelope files (TECH_PLAN D10).</summary>
    public class EnvelopeFileTests
    {
        private const string D039 = @"{""id"": ""D039"", ""rate_hz"": 30, ""kind"": ""rms_linear"",
 ""clips"": {""VO_D039_HF_AM"": {""chain"": ""HF_AM"", ""variant"": null, ""duration_s"": 13.53, ""speech_offset_s"": 0.03, ""rms_max"": 0.3545, ""rms"": [0.006, 0.2481, 0.3545]}},
 ""token_onsets_s"": [{""token"": ""attention"", ""t"": 0.03}, {""token"": ""attention"", ""t"": 0.93}, {""token"": ""seven"", ""t"": 1.83}, {""token"": ""seven"", ""t"": 9.0}]}";

        [Test]
        public void ParsesClipsAndOnsets()
        {
            var f = EnvelopeFile.Parse(D039);
            Assert.That(f.Id, Is.EqualTo("D039"));
            Assert.That(f.RateHz, Is.EqualTo(30f));
            Assert.That(f.Clips.TryGetValue("VO_D039_HF_AM", out var c), Is.True);
            Assert.That(c.Chain, Is.EqualTo("HF_AM"));
            Assert.That(c.Variant, Is.EqualTo(""));
            Assert.That(c.Duration, Is.EqualTo(13.53f).Within(1e-4f));
            Assert.That(c.Rms.Length, Is.EqualTo(3));
            Assert.That(f.FirstOnset("Seven"), Is.EqualTo(1.83f).Within(1e-4f), "the first Seven (G9 switch)");
            Assert.That(f.FirstOnset("zero"), Is.EqualTo(-1f));
            Assert.That(EnvelopeFile.PathOf("D039"), Is.EqualTo("Assets/_Game/Audio/Envelopes/VO_D039.json"));
        }
    }
}
