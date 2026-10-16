using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LongShadow.Platform;
using LongShadow.Platform.Editor;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>
    /// The non-code web pieces against their contracts (TECH_PLAN §7.1): the CovertOps template (body fragment, DPR cap,
    /// errorHandler, never alert(), pointer lock with unadjustedMovement, publish markers), the jslib (every function
    /// <c>WebNative</c> imports), and link.xml (every LongShadow runtime assembly preserved under High stripping).
    /// Reads the landed copies, or the staged ones before <c>land.py --assets</c> ran.
    /// </summary>
    public class WebContractTests
    {
        private static string Root => Directory.GetParent(Application.dataPath).FullName;

        private static string Read(string landed)
        {
            var a = Path.Combine(Root, landed);
            if (File.Exists(a)) return File.ReadAllText(a);
            var staged = Path.Combine(Root, "Staging", landed);
            if (File.Exists(staged)) return File.ReadAllText(staged);
            Assert.Ignore(landed + " is neither landed nor staged");
            return null;
        }

        private static string Template => Read("Assets/WebGLTemplates/CovertOps/index.html");

        [Test]
        public void TemplateIsABodyFragmentWithItsOwnTitleAndStyle()
        {
            var t = Template;
            Assert.IsFalse(Regex.IsMatch(t, @"<\s*(html|head|body)[\s>]", RegexOptions.IgnoreCase), "the host wraps the page; no html/head/body tags");
            Assert.That(t.TrimStart().StartsWith("<title>"), "title first");
            Assert.That(t.IndexOf("<style>"), Is.LessThan(t.IndexOf("<script")), "style before scripts");
        }

        [Test]
        public void TemplateNeverAlertsAndHandlesBrowserRefusals()
        {
            var t = Template;
            Assert.IsFalse(PostBuildValidator.ContainsAlertCall(t));
            StringAssert.Contains("errorHandler", t);
            foreach (var name in new[] { "WrongDocumentError", "NotAllowedError", "SecurityError" }) StringAssert.Contains(name, t);
            StringAssert.Contains("unhandledrejection", t);
        }

        [Test]
        public void TemplateCapsTheDevicePixelRatioAt15()
        {
            var t = Template;
            StringAssert.Contains("DPR_CAP = 1.5", t);
            StringAssert.Contains("devicePixelRatio: Math.min(window.devicePixelRatio || 1, DPR_CAP)", t);
        }

        [Test]
        public void TemplateWrapsPointerLockWithRawMouseFirst()
        {
            var t = Template;
            StringAssert.Contains("HTMLCanvasElement.prototype", t);
            StringAssert.Contains("unadjustedMovement: true", t);
            StringAssert.Contains("NotSupportedError", t);
        }

        [Test]
        public void TemplateCarriesThePublishMarkers()
        {
            var t = Template;
            StringAssert.Contains("/*LS:ASSET_SUFFIX*/\"\"", t);
            StringAssert.Contains("/*LS:SERVED_EXT*/\"\"", t);
            StringAssert.Contains(WebBuild.VariantMarker + "\"release\"", t);
            var qa = WebBuild.PatchVariant(t, "qa");
            StringAssert.Contains(WebBuild.VariantMarker + "\"qa\"", qa);
            Assert.That(qa.Length, Is.EqualTo(t.Length - "release".Length + "qa".Length));
        }

        [Test]
        public void TemplateLoadsTheBuildThroughUnityVariables()
        {
            var t = Template;
            foreach (var v in new[] { "{{{ LOADER_FILENAME }}}", "{{{ DATA_FILENAME }}}", "{{{ FRAMEWORK_FILENAME }}}", "{{{ CODE_FILENAME }}}" })
                StringAssert.Contains(v, t);
            StringAssert.Contains("streamingAssetsUrl: 'StreamingAssets'", t);
        }

        [Test]
        public void TemplateUsesOnlyKnownUnityVariablesAndConditions()
        {
            // Unity evaluates every {{{ ... }}} as a JavaScript expression at build time; an empty or unknown one fails the build.
            var t = Template;
            var variables = new[]
            {
                "LOADER_FILENAME", "DATA_FILENAME", "FRAMEWORK_FILENAME", "CODE_FILENAME", "SYMBOLS_FILENAME",
                "JSON.stringify(COMPANY_NAME)", "JSON.stringify(PRODUCT_NAME)", "JSON.stringify(PRODUCT_VERSION)",
            };
            var found = 0;
            foreach (Match m in Regex.Matches(t, @"\{\{\{(.*?)\}\}\}"))
            {
                found++;
                CollectionAssert.Contains(variables, m.Groups[1].Value.Trim(), "unknown template expression " + m.Value);
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(7));
            foreach (Match m in Regex.Matches(t, @"^#if\s+(\S+)", RegexOptions.Multiline))
                CollectionAssert.Contains(new[] { "USE_WASM", "SYMBOLS_FILENAME", "SHOW_DIAGNOSTICS" }, m.Groups[1].Value);
            Assert.That(Regex.Matches(t, @"^#if\b", RegexOptions.Multiline).Count, Is.EqualTo(Regex.Matches(t, @"^#endif\b", RegexOptions.Multiline).Count));
        }

        [Test]
        public void TemplateShowsTheGddClickGateLine()
        {
            // GDD §8.1: "SESSION 12 // SUBJECT S-3 // CLICK TO BEGIN" and the footer line.
            var plain = Regex.Replace(Template, "<[^>]+>", string.Empty);
            StringAssert.Contains("SESSION 12 // SUBJECT S-3 // CLICK TO BEGIN", plain);
            StringAssert.Contains("HEADPHONES RECOMMENDED &middot; MOUSE + KEYBOARD &middot; ESC PAUSES", plain);
        }

        [Test]
        public void JslibExportsEveryImportedFunction()
        {
            var js = Read("Assets/_Game/Plugins/WebGL/LongShadowWeb.jslib");
            foreach (var f in WebNative.ExportedFunctions)
                Assert.IsTrue(Regex.IsMatch(js, @"\b" + f + @"\s*:\s*function"), f + " missing from the jslib");
            Assert.IsFalse(PostBuildValidator.ContainsAlertCall(js));
            StringAssert.Contains("mergeInto(LibraryManager.library", js);
            StringAssert.Contains("unadjustedMovement: true", js);
            foreach (var cb in new[] { "OnPointerLockChanged", "OnPointerLockError", "OnVisibilityChanged", "OnFullscreenChanged" })
                StringAssert.Contains(cb, js);
        }

        [Test]
        public void WebBridgeHasEveryCallbackTheJslibSends()
        {
            foreach (var m in new[] { "OnPointerLockChanged", "OnPointerLockError", "OnVisibilityChanged", "OnFullscreenChanged", "OnHtmlGateClicked" })
                Assert.IsNotNull(typeof(WebBridge).GetMethod(m, new[] { typeof(string) }), m);
        }

        [Test]
        public void LinkXmlPreservesEveryLongShadowRuntimeAssembly()
        {
            var xml = Read("Assets/_Game/link.xml");
            var runtime = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                                             .Select(a => a.name)
                                             .Where(n => n.StartsWith("LongShadow.") && !n.EndsWith(".Editor"))
                                             .ToList();
            Assert.That(runtime, Is.Not.Empty);
            foreach (var name in runtime)
                StringAssert.Contains("fullname=\"" + name + "\"", xml, name + " missing from link.xml");
            foreach (var pkg in new[] { "Unity.Addressables", "Unity.ResourceManager" })
                StringAssert.Contains("fullname=\"" + pkg + "\"", xml);
        }
    }
}
