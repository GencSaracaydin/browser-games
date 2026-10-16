using LongShadow.Player.View;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Player
{
    /// <summary>Owner-based FOV requests (IPlayerRig.RequestFov/ReleaseFov, TECH_PLAN §7.3): blends, latest wins, release.</summary>
    public class FovStackTests
    {
        private const float Base = 58.72f;
        private readonly object _ads = new object();
        private readonly object _binoculars = new object();

        private static float Run(FovStack s, float seconds, float baseFov = Base, float dt = 0.01f)
        {
            var v = s.Current;
            for (var t = 0f; t < seconds - 1e-5f; t += dt) v = s.Evaluate(baseFov, dt);
            return v;
        }

        [Test]
        public void RequestBlendsWithSmoothstepOverItsTime()
        {
            var s = new FovStack();
            Assert.That(s.Evaluate(Base, 0f), Is.EqualTo(Base));
            s.Request(_ads, 30f, 0.2f);
            Assert.That(Run(s, 0.1f), Is.EqualTo((Base + 30f) * 0.5f).Within(0.05f), "smoothstep midpoint at half time");
            Assert.That(Run(s, 0.1f), Is.EqualTo(30f).Within(1e-3f), "reaches the target at the blend time");
            Assert.IsTrue(s.HasRequest);
        }

        [Test]
        public void RepeatingTheSameRequestEveryFrameDoesNotRestartTheBlend()
        {
            var s = new FovStack();
            s.Evaluate(Base, 0f);
            for (var i = 0; i < 20; i++)
            {
                s.Request(_ads, 30f, 0.2f);
                s.Evaluate(Base, 0.01f);
            }
            Assert.That(s.Current, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void LatestOwnerWinsAndReleaseReturnsToThePrevious()
        {
            var s = new FovStack();
            s.Evaluate(Base, 0f);
            s.Request(_ads, 30f, 0.2f);
            Run(s, 0.3f);
            s.Request(_binoculars, 12f, 0.3f);
            Assert.That(Run(s, 0.3f), Is.EqualTo(12f).Within(1e-3f), "latest request wins");
            s.Release(_binoculars);
            Assert.That(Run(s, 0.15f), Is.InRange(12f, 30f), "blends back over the released request's time");
            Assert.That(Run(s, 0.2f), Is.EqualTo(30f).Within(1e-3f), "back to the remaining request");
            s.Release(_ads);
            Assert.That(Run(s, 0.25f), Is.EqualTo(Base).Within(1e-3f), "back to the base");
            Assert.IsFalse(s.HasRequest);
        }

        [Test]
        public void BaseFollowsSprintKickWithoutRequests()
        {
            var s = new FovStack();
            Assert.That(s.Evaluate(Base, 0.016f), Is.EqualTo(Base));
            Assert.That(s.Evaluate(Base + 3f, 0.016f), Is.EqualTo(Base + 3f), "no request: the base is used directly");
        }

        [Test]
        public void UnknownOwnersAndNullAreIgnored()
        {
            var s = new FovStack();
            s.Request(null, 10f, 0f);
            s.Release(new object());
            Assert.That(s.Count, Is.EqualTo(0));
        }
    }
}
