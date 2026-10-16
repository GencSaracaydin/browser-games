using LongShadow.Platform.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>Pure helpers of the build pipeline (the build itself is exercised by the integrator's Web Release run).</summary>
    public class WebBuildTests
    {
        [Test]
        public void PatchVariantReplacesOnlyTheMarkedLiteral()
        {
            var html = "a = \"release\"; window.LS_VARIANT = /*LS:VARIANT*/\"release\"; b = \"release\";";
            var qa = WebBuild.PatchVariant(html, "qa");
            Assert.That(qa, Is.EqualTo("a = \"release\"; window.LS_VARIANT = /*LS:VARIANT*/\"qa\"; b = \"release\";"));
        }

        [Test]
        public void PatchVariantWithoutMarkerIsANoOp()
        {
            Assert.That(WebBuild.PatchVariant("no marker", "qa"), Is.EqualTo("no marker"));
            Assert.That(WebBuild.PatchVariant("/*LS:VARIANT*/ release", "qa"), Is.EqualTo("/*LS:VARIANT*/ release"));
            Assert.IsNull(WebBuild.PatchVariant(null, "qa"));
        }

        [Test]
        public void OptionPresets()
        {
            Assert.IsFalse(WebBuildOptions.Release().Qa);
            Assert.IsTrue(WebBuildOptions.QaBuild().Qa);
            Assert.That(WebBuildOptions.Release().OutputFolder, Is.EqualTo(WebBuild.OutputFolder));
            Assert.IsTrue(WebBuildOptions.Release().FailOnDuplicates);
        }

        [Test]
        public void LimitsFileMatchesTechPlan()
        {
            var l = BuildLimits.Load();
            Assert.That(l.max_file_bytes, Is.EqualTo(14_500_000));   // TECH_PLAN §9.1
            Assert.That(l.max_bundle_bytes, Is.EqualTo(12_000_000)); // §8.10
            Assert.That(l.max_files, Is.EqualTo(250));
            Assert.That(l.max_total_bytes, Is.EqualTo(230_000_000));
            Assert.That(l.warn_total_bytes, Is.EqualTo(220_000_000));
            Assert.That(l.max_reflection_probe_px, Is.EqualTo(256)); // D16
        }
    }
}
