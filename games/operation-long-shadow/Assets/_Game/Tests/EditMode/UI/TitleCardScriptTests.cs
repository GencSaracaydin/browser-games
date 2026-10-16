using LongShadow.UI.Common;
using LongShadow.UI.Screens;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>GDD §8.3: number groups print, then resolve into the four lines; line 3 reads 18:40 for 6 frames first.</summary>
    public sealed class TitleCardScriptTests
    {
        [Test]
        public void NothingPrintsBeforeTheBarsAreIn()
        {
            var s = new TitleCardScript();
            var b = new TextBuffer();
            Assert.AreEqual(0, s.Evaluate(0, 0.2f, b));
            Assert.AreEqual(TitleCardScript.BarsSeconds, s.LineStart(0), 1e-4f);
        }

        [Test]
        public void LinesFirstPrintAsFiveDigitGroups()
        {
            var s = new TitleCardScript();
            var b = new TextBuffer();
            var t = (s.LineStart(0) + s.PrintEnd(0)) * 0.5f;
            var visible = s.Evaluate(0, t, b);
            Assert.Greater(visible, 0);
            var text = b.ToString();
            for (var i = 0; i < text.Length; i++)
            {
                if (NumberGroups.IsGroupGap(i)) Assert.AreEqual(' ', text[i], "group gap at " + i);
                else Assert.IsTrue(char.IsDigit(text[i]), "digit at " + i + " in '" + text + "'");
            }
        }

        [Test]
        public void EveryLineResolvesIntoTheGddText()
        {
            var s = new TitleCardScript();
            var b = new TextBuffer();
            for (var i = 0; i < TitleCardScript.Lines.Length; i++)
            {
                var visible = s.Evaluate(i, s.BarsOutAt - 0.01f, b);
                Assert.AreEqual(TitleCardScript.Lines[i].Length, visible);
                Assert.AreEqual(TitleCardScript.Lines[i], b.ToString());
            }
            StringAssert.Contains("17:58", TitleCardScript.Lines[2]);
        }

        [Test]
        public void LineThreeReads1840ForSixFramesThenSettles()
        {
            var s = new TitleCardScript();
            var b = new TextBuffer();
            s.Evaluate(TitleCardScript.FlashLine, s.FlashStart + 0.05f, b);
            StringAssert.Contains("18:40", b.ToString());
            s.Evaluate(TitleCardScript.FlashLine, s.FlashStart + TitleCardScript.FlashSeconds + 0.01f, b);
            StringAssert.Contains("17:58", b.ToString());
            s.Evaluate(TitleCardScript.FlashLine, s.FlashStart + 1f, b, true);
            StringAssert.Contains("18:40", b.ToString(), "the view can hold the flash for two rendered frames");
            Assert.AreEqual(0.1f, TitleCardScript.FlashSeconds, 1e-6f, "6 frames at 60 fps");
        }

        [Test]
        public void TheCardFitsInEightSecondsAndCompressesWhenShorter()
        {
            var s = new TitleCardScript(8f);
            Assert.AreEqual(1f, s.Scale, 1e-4f);
            Assert.Less(s.CorrectEnd(3), s.BarsOutAt);
            Assert.AreEqual(7.5f, s.BarsOutAt, 1e-4f);

            var shortCard = new TitleCardScript(5f);
            Assert.Less(shortCard.Scale, 1f);
            Assert.Less(shortCard.CorrectEnd(3), shortCard.BarsOutAt + 1e-3f);
        }

        [Test]
        public void DigitsAreStableBetweenFrames()
        {
            var s = new TitleCardScript();
            var a = new TextBuffer();
            var b = new TextBuffer();
            s.Evaluate(1, s.PrintEnd(1) - 0.01f, a);
            s.Evaluate(1, s.PrintEnd(1) - 0.005f, b);
            Assert.IsTrue(a.ContentEquals(b), "no flicker while printing");
        }
    }
}
