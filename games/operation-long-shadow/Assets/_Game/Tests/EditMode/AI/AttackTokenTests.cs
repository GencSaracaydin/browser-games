using LongShadow.AI.Common;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>GDD §14.3 / §16 attack tokens: never more than N firing at the player; rotation 2-4 s; the aimed-at enemy first.</summary>
    public class AttackTokenTests
    {
        [Test]
        public void NeverMoreHoldersThanCapacity()
        {
            foreach (var capacity in new[] { 2, 3, 4, 5 })
            {
                var m = new AttackTokenManager(capacity, 7u);
                var maxSeen = 0;
                for (var frame = 0; frame < 3000; frame++)
                {
                    var now = frame * 0.05f;
                    for (var agent = 1; agent <= 9; agent++)
                    {
                        if ((agent + frame) % 4 == 0) continue;                                   // agents ask at a decision rate
                        m.Request(agent, now, agent == 1 + (frame / 60) % 9);
                    }
                    m.Tick(now);
                    if (m.HolderCount > maxSeen) maxSeen = m.HolderCount;
                    Assert.That(m.HolderCount, Is.LessThanOrEqualTo(capacity));
                }
                Assert.That(maxSeen, Is.EqualTo(capacity), "tokens are used when wanted");
            }
        }

        [Test]
        public void AimedAtEnemyPreemptsTheLongestHolder()
        {
            var m = new AttackTokenManager(3);
            Assert.IsTrue(m.Request(1, 0f, false));
            Assert.IsTrue(m.Request(2, 0.1f, false));
            Assert.IsTrue(m.Request(3, 0.2f, false));
            Assert.IsFalse(m.Request(4, 0.3f, false));
            Assert.IsTrue(m.Request(4, 0.4f, true), "the enemy under the crosshair gets one first");
            Assert.IsFalse(m.Has(1), "the oldest holder lost its token");
            Assert.That(m.HolderCount, Is.EqualTo(3));
        }

        [Test]
        public void TokensRotateWhenOthersWait()
        {
            var m = new AttackTokenManager(1, 3u);
            var t = 0f;
            Assert.IsTrue(m.Request(1, t, false));
            var rotated = false;
            for (var i = 0; i < 100 && !rotated; i++)
            {
                t += 0.1f;
                m.Request(1, t, false);
                m.Request(2, t, false);
                rotated = m.Has(2);
            }
            Assert.IsTrue(rotated);
            Assert.That(t, Is.InRange(AttackTokenManager.MinHoldSeconds - 0.11f, AttackTokenManager.MaxHoldSeconds + 0.11f));
        }

        [Test]
        public void SilentHoldersAreReleased()
        {
            var m = new AttackTokenManager(1);
            Assert.IsTrue(m.Request(1, 0f, false));
            m.Tick(AttackTokenManager.StaleSeconds + 0.1f);
            Assert.IsFalse(m.Has(1));
            Assert.IsTrue(m.Request(2, AttackTokenManager.StaleSeconds + 0.2f, false));
        }

        [Test]
        public void LoweringCapacityReleasesHolders()
        {
            var m = new AttackTokenManager(5);
            for (var i = 1; i <= 5; i++) m.Request(i, i * 0.01f, false);
            Assert.That(m.HolderCount, Is.EqualTo(5));
            m.Capacity = 2;
            Assert.That(m.HolderCount, Is.EqualTo(2));
            Assert.IsTrue(m.Has(4) && m.Has(5), "the newest holders keep theirs");
        }
    }
}
