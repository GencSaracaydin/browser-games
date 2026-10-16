using LongShadow.Audio.VO;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>GDD §11 radio subtitle box: "[SUNDIAL-ACTUAL] HOLLIS: text", "[LEGION] (shouting) text".</summary>
    public class SubtitleBuilderTests
    {
        [Test]
        public void SpeakerLine()
        {
            var line = new DialogueLine("D021", SpeakerIds.Hollis, "SUNDIAL-ACTUAL", "Off the middle of the street.", null, false);
            var s = SubtitleBuilder.Build(line, 7, true, false, 2f);
            Assert.That(s.Speaker, Is.EqualTo("HOLLIS"));
            Assert.That(s.Callsign, Is.EqualTo("SUNDIAL-ACTUAL"));
            Assert.That(s.Text, Is.EqualTo("Off the middle of the street."));
            Assert.That(s.Radio, Is.True);
            Assert.That(s.Handle, Is.EqualTo(7));
            Assert.That(s.Color, Is.EqualTo(SpeakerIds.StyleOf(SpeakerIds.Hollis).Color));
            Assert.That(s.IsBark, Is.False);
        }

        [Test]
        public void BarkUsesTheTranslation()
        {
            var bark = new DialogueLine("BK_LEG_02", SpeakerIds.Legion, "LEGION", "Varrek! Varrek!", "(shouting) Push! Push!", false)
                .WithRules(-1, VOPriority.Bark, VOChannel.Bark, false, true);
            var s = SubtitleBuilder.Build(bark, 1, false, false, 1f);
            Assert.That(s.Text, Is.EqualTo("(shouting) Push! Push!"));
            Assert.That(s.Speaker, Is.Empty);
            Assert.That(s.Callsign, Is.EqualTo("LEGION"));
            Assert.That(s.IsBark, Is.True);
        }

        [Test]
        public void BoothItalicsAndClockmakerMonospace()
        {
            var i = new DialogueLine("D001", SpeakerIds.Interrogator, "UNKNOWN", "Sergeant Calder.", null, false);
            Assert.That(SubtitleBuilder.Build(i, 1, false, true, 1f).Italic, Is.True);
            Assert.That(SubtitleBuilder.Build(i, 1, false, false, 1f).Italic, Is.False);
            var c = new DialogueLine("D039", SpeakerIds.Clockmaker, "CLOCKMAKER", "Attention.", null, true);
            var s = SubtitleBuilder.Build(c, 1, true, false, 1f);
            Assert.That(s.Monospace, Is.True);
            Assert.That(s.Callsign, Is.Null, "no bracket when the callsign equals the name");
        }
    }
}
