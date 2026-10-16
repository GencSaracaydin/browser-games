using LongShadow.Core;
using LongShadow.UI.Common;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>Objective and subtitle strings exactly as GDD §7.12 and §11 print them.</summary>
    public sealed class FormatterTests
    {
        [Test]
        public void ObjectiveLineUsesCounterFnvGroupAndText()
        {
            var b = new TextBuffer();
            var o = new ObjectiveDef { Id = "B4.Technical", Text = "NEUTRALIZE THE TECHNICAL", Index = 4, Total = 11 };
            ObjectiveFormatter.WritePlain(b, o);
            Assert.AreEqual("OBJ 4/11 — 75810 — NEUTRALIZE THE TECHNICAL", b.ToString());
            Assert.AreEqual(75810, MathX.FlavourGroup("B4.Technical"));
        }

        [Test]
        public void ObjectiveGroupIsZeroPaddedAndOverridable()
        {
            var b = new TextBuffer();
            ObjectiveFormatter.WriteGroup(b, new ObjectiveDef { Id = "X", FlavourGroup = "" });
            Assert.AreEqual(5, b.Length, "five digits");
            b.Clear();
            ObjectiveFormatter.WriteGroup(b, new ObjectiveDef { Id = "X", FlavourGroup = "47219" });
            Assert.AreEqual("47219", b.ToString());
        }

        [Test]
        public void IntelLine()
        {
            var b = new TextBuffer();
            ObjectiveFormatter.WriteIntel(b, 2, 1);
            Assert.AreEqual("INTEL 2/3  ·  OPTIONAL 1/2", b.ToString());
        }

        [Test]
        public void RadioSubtitleHasCallsignNameAndColours()
        {
            var b = new TextBuffer();
            var d = new SubtitleData
            {
                Speaker = "HOLLIS", Callsign = "SUNDIAL-ACTUAL", Text = "Off the middle of the street.",
                Color = SpeakerIds.StyleOf(SpeakerIds.Hollis).Color, Radio = true,
            };
            SubtitleFormatter.Write(b, d, UIColors.Ink);
            var s = b.ToString();
            StringAssert.StartsWith("<color=#E8C77A>[SUNDIAL-ACTUAL] HOLLIS:</color> <color=#", s);
            StringAssert.EndsWith("Off the middle of the street.</color>", s);
        }

        [Test]
        public void CallsignEqualToTheSpeakerIsNotRepeated()
        {
            var b = new TextBuffer();
            SubtitleFormatter.Write(b, new SubtitleData { Speaker = "MAGPIE", Callsign = "MAGPIE", Text = "How hot?", Color = Color.white }, UIColors.Ink);
            StringAssert.StartsWith("<color=#FFFFFF>MAGPIE:</color>", b.ToString());
        }

        [Test]
        public void BarkIsBracketedFactionAndTranslation()
        {
            var b = new TextBuffer();
            SubtitleFormatter.Write(b, new SubtitleData { Speaker = "LEGION", Callsign = "LEGION", Text = "(shouting) Push! Push!", IsBark = true, Color = SpeakerIds.StyleOf("LEGION").Color }, UIColors.Ink);
            var s = b.ToString();
            StringAssert.StartsWith("<color=#E0705A>[LEGION]</color> ", s);
            StringAssert.Contains("(shouting) Push! Push!", s);
        }

        [Test]
        public void InterrogatorInTheBoothIsItalic()
        {
            var b = new TextBuffer();
            SubtitleFormatter.Write(b, new SubtitleData { Speaker = "INTERROGATOR", Callsign = "UNKNOWN", Text = "Again.", Italic = true, Color = Color.white }, UIColors.Ink);
            StringAssert.Contains("<i>Again.</i>", b.ToString());
        }

        [Test]
        public void SubtitleOptionsFilterLinesAndBarks()
        {
            var line = new SubtitleData { Text = "x" };
            var bark = new SubtitleData { Text = "x", IsBark = true };
            var s = new GameSettings();
            Assert.IsTrue(SubtitleFormatter.ShouldShow(line, s));
            Assert.IsTrue(SubtitleFormatter.ShouldShow(bark, s), "translate enemy chatter is on by default (GDD §11)");
            s.TranslateEnemyChatter = false;
            Assert.IsFalse(SubtitleFormatter.ShouldShow(bark, s));
            Assert.IsTrue(SubtitleFormatter.ShouldShow(line, s));
            s.Subtitles = false;
            Assert.IsFalse(SubtitleFormatter.ShouldShow(line, s));
        }

        [Test]
        public void SpeakerColourFallsBackToTheGddStyle()
        {
            var c = SubtitleFormatter.ColorOf(new SubtitleData { Speaker = SpeakerIds.Clockmaker });
            Assert.AreEqual(SpeakerIds.StyleOf(SpeakerIds.Clockmaker).Color, c);
        }

        [Test]
        public void SubtitleSizesGrow()
        {
            Assert.Less(SubtitleFormatter.FontSize(SubtitleSize.Small), SubtitleFormatter.FontSize(SubtitleSize.Medium));
            Assert.Less(SubtitleFormatter.FontSize(SubtitleSize.Medium), SubtitleFormatter.FontSize(SubtitleSize.Large));
        }
    }
}
