using LongShadow.AI.Common;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.AI
{
    /// <summary>GDD §14.3 hit chance: base × range × motion × cover × time-on-target × flood × difficulty (× 0.5 suppressed).</summary>
    public class HitChanceTests
    {
        private static HitChanceInputs Base() => new HitChanceInputs
        {
            BaseAccuracy = 0.30f, Distance = 20f, RangeMax = 35f, TargetMotion = MotionClass.Still, ApplyMotionFactor = true,
            TimeOnTarget = 2f, DifficultyMultiplier = 1f,
        };

        [Test]
        public void ReferenceShotIsTheArchetypeAccuracy()
        {
            Assert.That(HitChance.Compute(Base()), Is.EqualTo(0.30f).Within(1e-5f));
        }

        [Test]
        public void EveryFactorMultipliesAsSpecified()
        {
            var i = Base();
            i.TargetMotion = MotionClass.Walk;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.8f).Within(1e-5f));
            i.TargetMotion = MotionClass.Sprint;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.6f).Within(1e-5f));
            i.TargetMotion = MotionClass.Slide;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.5f).Within(1e-5f));
            i = Base();
            i.TargetInCover = true;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.6f).Within(1e-5f));
            i = Base();
            i.TimeOnTarget = 0f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.5f).Within(1e-5f));
            i.TimeOnTarget = 1f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 0.75f).Within(1e-5f));
            i = Base();
            i.TargetInFlood = true;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 1.25f).Within(1e-5f));
            i = Base();
            i.DifficultyMultiplier = 1.3f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.30f * 1.3f).Within(1e-5f));
            i = Base();
            i.ShooterSuppressed = true;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void CombinedProduct()
        {
            var i = Base();
            i.TargetMotion = MotionClass.Walk;
            i.TargetInCover = true;
            i.TimeOnTarget = 0.5f;
            i.TargetInFlood = true;
            i.DifficultyMultiplier = 0.7f;
            var expected = 0.30f * 0.8f * 0.6f * (0.5f + 0.5f * 0.25f) * 1.25f * 0.7f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void RangeCurveFallsWithDistanceAndIsCapped()
        {
            Assert.That(HitChance.RangeFactor(20f, 35f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(HitChance.RangeFactor(10f, 35f), Is.GreaterThan(1f));
            Assert.That(HitChance.RangeFactor(1f, 35f), Is.EqualTo(HitChance.MaxRangeFactor).Within(1e-5f));
            var prev = float.MaxValue;
            for (var d = 2f; d < 200f; d += 3f)
            {
                var f = HitChance.RangeFactor(d, 35f);
                Assert.That(f, Is.LessThanOrEqualTo(prev + 1e-6f));
                Assert.That(f, Is.GreaterThanOrEqualTo(HitChance.MinRangeFactor));
                prev = f;
            }
        }

        [Test]
        public void ResultIsClampedAndNeverCertain()
        {
            var i = Base();
            i.BaseAccuracy = 5f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(HitChance.MaxHitChance));
            i.BaseAccuracy = -1f;
            Assert.That(HitChance.Compute(i), Is.EqualTo(0f));
        }

        [Test]
        public void MarksmanUsesItsMotionTableWithoutRangeFalloff()
        {
            Assert.That(HitChance.MarksmanAccuracy(MotionClass.Still), Is.EqualTo(0.8f));
            Assert.That(HitChance.MarksmanAccuracy(MotionClass.Walk), Is.EqualTo(0.35f));
            Assert.That(HitChance.MarksmanAccuracy(MotionClass.Sprint), Is.EqualTo(0.2f));
            var i = new HitChanceInputs
            {
                BaseAccuracy = HitChance.MarksmanAccuracy(MotionClass.Still), Distance = 120f, RangeMax = 160f, FlatRange = true,
                TargetMotion = MotionClass.Still, ApplyMotionFactor = false, TimeOnTarget = 2f, DifficultyMultiplier = 1f,
            };
            Assert.That(HitChance.Compute(i), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void MotionClassifier()
        {
            Assert.That(LongShadow.AI.Common.MotionClassifier.Classify(LongShadow.Core.Stance.Stand, UnityEngine.Vector3.zero), Is.EqualTo(MotionClass.Still));
            Assert.That(LongShadow.AI.Common.MotionClassifier.Classify(LongShadow.Core.Stance.Stand, new UnityEngine.Vector3(2f, 0f, 0f)), Is.EqualTo(MotionClass.Walk));
            Assert.That(LongShadow.AI.Common.MotionClassifier.Classify(LongShadow.Core.Stance.Sprint, new UnityEngine.Vector3(6.9f, 0f, 0f)), Is.EqualTo(MotionClass.Sprint));
            Assert.That(LongShadow.AI.Common.MotionClassifier.Classify(LongShadow.Core.Stance.Slide, new UnityEngine.Vector3(8f, 0f, 0f)), Is.EqualTo(MotionClass.Slide));
        }
    }
}
