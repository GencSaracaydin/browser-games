using LongShadow.Audio.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Audio
{
    /// <summary>WP06_SFX notes: round-robin variants never repeat the last (2–3 variants) or the last two (4+).</summary>
    public class VariantPickerTests
    {
        [Test]
        public void TwoVariantsAlternate()
        {
            var p = new VariantPicker(5);
            var last = p.Next("a", 2);
            for (var i = 0; i < 50; i++)
            {
                var n = p.Next("a", 2);
                Assert.That(n, Is.Not.EqualTo(last));
                last = n;
            }
        }

        [Test]
        public void FourOrMoreAvoidTheLastTwo()
        {
            var p = new VariantPicker(9);
            int a = p.Next("fs", 12), b = p.Next("fs", 12);
            var seen = new bool[12];
            for (var i = 0; i < 500; i++)
            {
                var n = p.Next("fs", 12);
                Assert.That(n, Is.Not.EqualTo(a));
                Assert.That(n, Is.Not.EqualTo(b));
                Assert.That(n, Is.InRange(0, 11));
                seen[n] = true;
                a = b;
                b = n;
            }
            foreach (var s in seen) Assert.That(s, Is.True, "every variant is used");
        }

        [Test]
        public void KeysAreIndependentAndSingleIsZero()
        {
            var p = new VariantPicker(3);
            Assert.That(p.Next("x", 1), Is.EqualTo(0));
            Assert.That(p.Next("x", 0), Is.EqualTo(0));
            Assert.That(p.Next(null, 3), Is.InRange(0, 2));
            p.Next("k1", 2);
            Assert.DoesNotThrow(() => p.Next("k2", 4));
        }
    }
}
