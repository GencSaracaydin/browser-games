using LongShadow.UI.Common;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>The allocation-free text builder behind every HUD number.</summary>
    public sealed class TextBufferTests
    {
        [Test]
        public void IntegersPaddingAndSigns()
        {
            var b = new TextBuffer(4);
            Assert.AreEqual("0", b.Clear().Append(0).ToString());
            Assert.AreEqual("-42", b.Clear().Append(-42).ToString());
            Assert.AreEqual("007", b.Clear().AppendPadded(7, 3).ToString());
            Assert.AreEqual("  7", b.Clear().AppendPadded(7, 3, ' ').ToString());
            Assert.AreEqual("-07", b.Clear().AppendPadded(-7, 3).ToString());
            Assert.AreEqual("2147483647", b.Clear().Append(int.MaxValue).ToString(), "grows past its capacity");
        }

        [Test]
        public void FixedPointFloats()
        {
            var b = new TextBuffer();
            Assert.AreEqual("3.0", b.Clear().Append(3f, 1).ToString());
            Assert.AreEqual("0.85", b.Clear().Append(0.85f, 2).ToString());
            Assert.AreEqual("-0.10", b.Clear().Append(-0.1f, 2).ToString());
            Assert.AreEqual("+0.10", b.Clear().Append(0.1f, 2, true).ToString());
            Assert.AreEqual("0.00", b.Clear().Append(-0.001f, 2).ToString(), "no negative zero");
            Assert.AreEqual("10", b.Clear().Append(9.6f, 0).ToString());
        }

        [Test]
        public void ClockAndHex()
        {
            var b = new TextBuffer();
            Assert.AreEqual("14:02", b.Clear().AppendClock(842f).ToString());
            Assert.AreEqual("E3A33B", b.Clear().AppendHex(UIColors.MarkerAmber).ToString());
            Assert.AreEqual("<color=#A6E3B4>x</color>", b.Clear().OpenColor(UIColors.MarkerGreen).Append('x').CloseColor().ToString());
        }

        [Test]
        public void ContentComparisonsAndCopy()
        {
            var a = new TextBuffer().Append("OBJ 4/11");
            var c = new TextBuffer();
            a.CopyTo(c);
            Assert.IsTrue(a.ContentEquals(c));
            Assert.IsTrue(a.ContentEquals("OBJ 4/11"));
            Assert.IsFalse(a.ContentEquals("OBJ 4/12"));
            c.Append('!');
            Assert.IsFalse(a.ContentEquals(c));
            Assert.AreEqual("abc", new TextBuffer().Append("xabcx", 1, 3).ToString());
        }

        [Test]
        public void ColoursAreExactGddValues()
        {
            Assert.AreEqual(new Color(0xE3 / 255f, 0xA3 / 255f, 0x3B / 255f), UIColors.MarkerAmber);
            Assert.AreEqual(new Color(0xA6 / 255f, 0xE3 / 255f, 0xB4 / 255f), UIColors.MarkerGreen);
            Assert.AreEqual(new Color(0xE0 / 255f, 0x4B / 255f, 0x3B / 255f), UIColors.Red);
            Assert.AreEqual(UIColors.MarkerAmber, UIColors.MarkerPalette(1.99f));
            Assert.AreEqual(UIColors.MarkerGreen, UIColors.MarkerPalette(2f), "GDD §11: pale green from s 2.0");
        }
    }
}
