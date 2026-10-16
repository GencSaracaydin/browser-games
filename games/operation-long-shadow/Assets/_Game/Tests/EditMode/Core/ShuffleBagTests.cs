using System;
using System.Collections.Generic;
using LongShadow.Core;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Core
{
    public class ShuffleBagTests
    {
        private static readonly string[] DeathLines = { "DI_01", "DI_02", "DI_03", "DI_04", "DI_05", "DI_06" };

        [Test]
        public void EveryItemOncePerCycle()
        {
            var bag = new ShuffleBag<string>(DeathLines, seed: 7);
            for (var cycle = 0; cycle < 50; cycle++)
            {
                var seen = new HashSet<string>();
                for (var i = 0; i < DeathLines.Length; i++) Assert.IsTrue(seen.Add(bag.Next()), "repeat inside a cycle");
                Assert.That(seen.Count, Is.EqualTo(DeathLines.Length));
            }
        }

        [Test]
        public void NeverRepeatsBackToBack()
        {
            var bag = new ShuffleBag<string>(DeathLines, seed: 11);
            var last = bag.Next();
            for (var i = 0; i < 5000; i++)
            {
                var n = bag.Next();
                Assert.AreNotEqual(last, n);
                last = n;
            }
        }

        [Test]
        public void SingleItemAndEmpty()
        {
            var one = new ShuffleBag<int>(new[] { 42 }, seed: 1);
            Assert.That(one.Next(), Is.EqualTo(42));
            Assert.That(one.Next(), Is.EqualTo(42));
            var empty = new ShuffleBag<int>(null, seed: 1);
            Assert.Throws<InvalidOperationException>(() => empty.Next());
        }

        [Test]
        public void AddJoinsNextCycle()
        {
            var bag = new ShuffleBag<int>(new[] { 1, 2 }, seed: 3);
            bag.Next();
            bag.Add(3);
            Assert.That(bag.Count, Is.EqualTo(3));
            var seen = new HashSet<int>();
            bag.Reset();
            for (var i = 0; i < 3; i++) seen.Add(bag.Next());
            Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }
    }
}
