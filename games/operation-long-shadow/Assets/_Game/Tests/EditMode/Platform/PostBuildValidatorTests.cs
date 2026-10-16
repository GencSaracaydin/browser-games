using System.IO;
using LongShadow.Platform.Editor;
using NUnit.Framework;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>
    /// TECH_PLAN §9.1 step 5 (WP01 DoD: "the validator passes and fails correctly on a synthetic 15 MB file"). The same
    /// rules live in Tools/build/validate_build.py (its --selftest covers the Python side).
    /// </summary>
    public class PostBuildValidatorTests
    {
        private string _dir;

        [SetUp]
        public void MakeBuild()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ls_validator_" + System.Guid.NewGuid().ToString("N"));
            Write("index.html", "<title>x</title><script>var config = { errorHandler: function () { return true; } };</script>");
            Write("Build/abc.loader.js", "function createUnityInstance(){}");
            WriteBytes("Build/abc.data.unityweb", 4_000_000);
            WriteBytes("Build/abc.framework.js.unityweb", 70_000);
            WriteBytes("Build/abc.wasm.unityweb", 6_900_000);
            Write("StreamingAssets/aa/catalog.json", "{}");
            Write("StreamingAssets/aa/settings.json", "{}");
            WriteBytes("StreamingAssets/aa/WebGL/core_persistent_assets_all_00.bundle", 3_000_000);
        }

        [TearDown]
        public void Remove()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void CleanBuildPasses()
        {
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits());
            Assert.IsTrue(r.Ok, r.ToText());
            Assert.That(r.FileCount, Is.EqualTo(8));
            Assert.That(r.LargestPath, Is.EqualTo("Build/abc.wasm.unityweb"));
        }

        [Test]
        public void Synthetic15MbFileFails()
        {
            WriteBytes("StreamingAssets/video/VID_BIG.mp4", 15_000_000);
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits());
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("VID_BIG.mp4", string.Join("\n", r.Errors));
        }

        [Test]
        public void OversizedBundleFails()
        {
            WriteBytes("StreamingAssets/aa/WebGL/s2_market_scenes_all_01.bundle", 12_500_000);
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits());
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("split the group", string.Join("\n", r.Errors));
        }

        [Test]
        public void AlertInTheTemplateFails()
        {
            Write("index.html", "<script>var config = { errorHandler: function (e) { alert(e); return true; } };</script>");
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits());
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("alert", string.Join("\n", r.Errors));
        }

        [Test]
        public void TooManyFilesAndTooManyBytesFail()
        {
            var limits = new BuildLimits { max_files = 5, max_total_bytes = 10_000_000, warn_total_bytes = 9_000_000 };
            var r = PostBuildValidator.ValidateOutput(_dir, limits);
            Assert.IsFalse(r.Ok);
            var text = string.Join("\n", r.Errors);
            StringAssert.Contains("file limit", text);
            StringAssert.Contains("cap", text);
        }

        [Test]
        public void UncompressedWasmFails()
        {
            File.Delete(Path.Combine(_dir, "Build/abc.wasm.unityweb"));
            WriteBytes("Build/abc.wasm", 30_000_000);
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits { max_file_bytes = 40_000_000, max_total_bytes = 400_000_000 });
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("uncompressed wasm", string.Join("\n", r.Errors));
        }

        [Test]
        public void MissingPiecesAndForeignFilesAreReported()
        {
            File.Delete(Path.Combine(_dir, "Build/abc.loader.js"));
            Write("timing.html", "x");
            Write("StreamingAssets/aa/WebGL/x.manifest", "x");
            var r = PostBuildValidator.ValidateOutput(_dir, new BuildLimits());
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("loader.js is missing", string.Join("\n", r.Errors));
            var warnings = string.Join("\n", r.Warnings);
            StringAssert.Contains("timing.html", warnings);
            StringAssert.Contains("x.manifest", warnings);
        }

        [Test]
        public void MissingFolderFails()
        {
            Assert.IsFalse(PostBuildValidator.ValidateOutput(_dir + "_nope", new BuildLimits()).Ok);
        }

        [TestCase("alert('x')", true)]
        [TestCase("window.alert(1)", true)]
        [TestCase("  // alert(1)", false)]
        [TestCase("/* alert(1) */ var a = 1;", false)]
        [TestCase("myalert(1)", false)]
        [TestCase("var url = 'https://x/alert(';", true)]
        [TestCase("", false)]
        public void AlertDetection(string html, bool expected)
        {
            Assert.That(PostBuildValidator.ContainsAlertCall(html), Is.EqualTo(expected));
        }

        [Test]
        public void MultiLineBlockCommentsAreSkipped()
        {
            Assert.IsFalse(PostBuildValidator.ContainsAlertCall("/*\n alert(1)\n*/\nok();"));
            Assert.IsTrue(PostBuildValidator.ContainsAlertCall("/*\n x\n*/ alert(1);"));
        }

        [TestCase("RGBAHalf", true)]
        [TestCase("RGBAFloat", true)]
        [TestCase("BC6H", true)]
        [TestCase("DXT5Crunched", false)]
        [TestCase("DXT1", false)]
        [TestCase("", false)]
        public void HdrFormats(string format, bool hdr)
        {
            Assert.That(PostBuildValidator.IsHdrFormat(format), Is.EqualTo(hdr));
        }

        private void Write(string rel, string text)
        {
            var p = Path.Combine(_dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, text);
        }

        private void WriteBytes(string rel, long size)
        {
            var p = Path.Combine(_dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            using (var f = new FileStream(p, FileMode.Create, FileAccess.Write)) f.SetLength(size);
        }
    }
}
