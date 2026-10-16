using LongShadow.Core;
using LongShadow.Platform;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>TECH_PLAN D21 / GDD §9.8: auto render scale = largest of {1, 0.9, 0.8, 0.75} with px x s^2 &lt;= 2.1 MP.</summary>
    public class RenderScalePolicyTests
    {
        [TestCase(1920, 1080, 1.0f)]   // 2.07 MP fits at full scale
        [TestCase(1680, 1050, 1.0f)]
        [TestCase(2160, 1350, 0.8f)]   // 1440x900 CSS at DPR 1.5: 0.9 gives 2.36 MP, 0.8 gives 1.87 MP
        [TestCase(2400, 1350, 0.8f)]
        [TestCase(2880, 1620, 0.75f)]  // 1920x1080 CSS at DPR 1.5: nothing fits, smallest step
        [TestCase(0, 0, 1.0f)]
        public void AutoScale(int w, int h, float expected)
        {
            Assert.That(RenderScalePolicy.AutoScale(w, h), Is.EqualTo(expected));
        }

        [Test]
        public void AutoScaleNeverExceedsTheBudgetWhenAStepFits()
        {
            for (var w = 800; w <= 3000; w += 97)
                for (var h = 500; h <= 1800; h += 83)
                {
                    var s = RenderScalePolicy.AutoScale(w, h);
                    if ((double)w * h * 0.75 * 0.75 <= RenderScalePolicy.MaxAutoPixels)
                        Assert.That((double)w * h * s * s, Is.LessThanOrEqualTo(RenderScalePolicy.MaxAutoPixels + 1), w + "x" + h);
                }
        }

        [Test]
        public void PlayerChoiceWinsOverAuto()
        {
            var s = new GameSettings { RenderScaleAuto = false, RenderScale = 0.9f };
            Assert.That(RenderScalePolicy.Effective(s, 2880, 1620), Is.EqualTo(0.9f));
            s.RenderScale = 0.2f;
            Assert.That(RenderScalePolicy.Effective(s, 2880, 1620), Is.EqualTo(GameSettings.MinRenderScale));
        }

        [Test]
        public void PerformanceModeCapsAt08()
        {
            var s = new GameSettings { PerformanceMode = true };
            Assert.That(RenderScalePolicy.Effective(s, 1280, 720), Is.EqualTo(RenderScalePolicy.PerformanceModeScale));
            s.RenderScaleAuto = false;
            s.RenderScale = 0.75f;
            Assert.That(RenderScalePolicy.Effective(s, 1280, 720), Is.EqualTo(0.75f));
        }

        [Test]
        public void BrightnessIsAnEvGainClampedToTheOptionRange()
        {
            Assert.That(RenderScalePolicy.BrightnessGain(0f), Is.EqualTo(1f));
            Assert.That(RenderScalePolicy.BrightnessGain(0.5f), Is.EqualTo(1.41421f).Within(1e-4f));
            Assert.That(RenderScalePolicy.BrightnessGain(-0.5f), Is.EqualTo(0.70711f).Within(1e-4f));
            Assert.That(RenderScalePolicy.BrightnessGain(3f), Is.EqualTo(RenderScalePolicy.BrightnessGain(GameSettings.MaxBrightness)));
        }
    }
}
