using LongShadow.UI.Common;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>Five-digit cipher groups (GDD §8.1, §8.3).</summary>
    public sealed class NumberGroupsTests
    {
        [Test]
        public void GroupsAreFiveDigitsSeparatedBySpaces()
        {
            var buffer = new char[64];
            var seed = 1234u;
            var end = NumberGroups.Write(buffer, 0, 3, ref seed);
            var s = new string(buffer, 0, end);
            Assert.AreEqual(17, s.Length);
            Assert.AreEqual(' ', s[5]);
            Assert.AreEqual(' ', s[11]);
            foreach (var part in s.Split(' '))
            {
                Assert.AreEqual(5, part.Length);
                foreach (var c in part) Assert.IsTrue(char.IsDigit(c));
            }
        }

        [Test]
        public void SameSeedSameDigits()
        {
            var a = new char[20];
            var b = new char[20];
            var s1 = NumberGroups.Seed("SESSION 12");
            var s2 = NumberGroups.Seed("SESSION 12");
            NumberGroups.WritePattern(a, 20, ref s1);
            NumberGroups.WritePattern(b, 20, ref s2);
            Assert.AreEqual(new string(a), new string(b));
            Assert.AreNotEqual(0u, NumberGroups.Seed(""), "seeds are never zero");
        }

        [Test]
        public void ZeroStateRecovers()
        {
            var state = 0u;
            Assert.AreNotEqual(0u, NumberGroups.Next(ref state));
        }
    }
}
