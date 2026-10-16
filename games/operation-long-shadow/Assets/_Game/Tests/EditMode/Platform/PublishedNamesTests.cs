using LongShadow.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace LongShadow.Tests.EditMode.Platform
{
    /// <summary>
    /// The artifact host's served-extension rule (TECH_PLAN §9.2, measured 2026-10-16) and its runtime mirror: every
    /// file the host would refuse gets ".wasm" appended by publish_plan.py, and the Addressables transform maps bundle
    /// URLs the same way.
    /// </summary>
    public class PublishedNamesTests
    {
        private const string Suffix = PublishedNames.DefaultSuffix;

        [TearDown]
        public void Uninstall() => AddressablesRemap.Install(string.Empty, null);

        [TestCase("StreamingAssets/aa/WebGL/core_persistent_assets_all_0f1e.bundle", "StreamingAssets/aa/WebGL/core_persistent_assets_all_0f1e.bundle.wasm")]
        [TestCase("Build/a1b2c3.data.unityweb", "Build/a1b2c3.data.unityweb.wasm")]
        [TestCase("StreamingAssets/aa/catalog.hash", "StreamingAssets/aa/catalog.hash.wasm")]
        [TestCase("Build/noextension", "Build/noextension.wasm")]
        [TestCase("https://h.example/x/StreamingAssets/aa/WebGL/a.bundle?v=3#f", "https://h.example/x/StreamingAssets/aa/WebGL/a.bundle.wasm?v=3#f")]
        [TestCase("StreamingAssets/aa/catalog.json", "StreamingAssets/aa/catalog.json")]
        [TestCase("Build/x.loader.js", "Build/x.loader.js")]
        [TestCase("StreamingAssets/video/VID_HARROU.mp4", "StreamingAssets/video/VID_HARROU.mp4")]
        [TestCase("index.html", "index.html")]
        public void MapAppendsTheSuffixOnlyToUnservedFiles(string input, string expected)
        {
            Assert.That(PublishedNames.Map(input, Suffix, PublishedNames.DefaultServedSet()), Is.EqualTo(expected));
        }

        [Test]
        public void MapIsIdempotent()
        {
            var once = PublishedNames.Map("StreamingAssets/aa/WebGL/a.bundle", Suffix, null);
            Assert.That(PublishedNames.Map(once, Suffix, null), Is.EqualTo(once));
        }

        [Test]
        public void EmptySuffixChangesNothing()
        {
            Assert.That(PublishedNames.Map("a.bundle", null, null), Is.EqualTo("a.bundle"));
            Assert.That(PublishedNames.Map("a.bundle", string.Empty, null), Is.EqualTo("a.bundle"));
        }

        [TestCase("a/b/c.Bundle", "bundle")]
        [TestCase("a/b.c/d", "")]
        [TestCase("a/b/c.", "")]
        [TestCase("https://x/y.json?z=1.bundle", "json")]
        [TestCase("", "")]
        public void ExtensionOf(string path, string ext)
        {
            Assert.That(PublishedNames.ExtensionOf(path), Is.EqualTo(ext));
        }

        [Test]
        public void ServedListParsesTheTemplateFormat()
        {
            var set = PublishedNames.ParseServedList(" html, .css js;wasm\n");
            Assert.That(set.Count, Is.EqualTo(4));
            Assert.IsTrue(set.Contains("CSS"));
            Assert.IsNull(PublishedNames.ParseServedList("  ,; "));
        }

        [Test]
        public void TransformMapsBundleLocationsAndLeavesAssetsAlone()
        {
            AddressablesRemap.Install(Suffix, null);
            Assert.IsTrue(AddressablesRemap.Active);
            var bundle = new ResourceLocationBase("b", "https://h/StreamingAssets/aa/WebGL/s2_market_scenes_all_ab12.bundle",
                                                  typeof(AssetBundleProvider).FullName, typeof(IAssetBundleResource));
            Assert.That(AddressablesRemap.Transform(bundle), Is.EqualTo("https://h/StreamingAssets/aa/WebGL/s2_market_scenes_all_ab12.bundle.wasm"));

            var scene = new ResourceLocationBase("s", "Assets/_Game/Scenes/Sections/S2_Market.unity",
                                                 typeof(SceneProvider).FullName, typeof(SceneInstance));
            Assert.That(AddressablesRemap.Transform(scene), Is.EqualTo("Assets/_Game/Scenes/Sections/S2_Market.unity"));

            var catalog = new ResourceLocationBase("c", "https://h/StreamingAssets/aa/catalog.json", "p", typeof(object));
            Assert.That(AddressablesRemap.Transform(catalog), Is.EqualTo("https://h/StreamingAssets/aa/catalog.json"));

            var hash = new ResourceLocationBase("h", "https://h/StreamingAssets/aa/catalog.hash", "p", typeof(string));
            Assert.That(AddressablesRemap.Transform(hash), Is.EqualTo("https://h/StreamingAssets/aa/catalog.hash.wasm"));
        }

        [Test]
        public void UninstalledTransformIsIdentity()
        {
            AddressablesRemap.Install(string.Empty, null);
            Assert.IsFalse(AddressablesRemap.Active);
            var bundle = new ResourceLocationBase("b", "https://h/aa/x.bundle", "p", typeof(IAssetBundleResource));
            Assert.That(AddressablesRemap.Transform(bundle), Is.EqualTo("https://h/aa/x.bundle"));
            Assert.That(Addressables.InternalIdTransformFunc, Is.Null);
        }

        [Test]
        public void DefaultServedListMatchesBuildLimits()
        {
            var limits = LongShadow.Platform.Editor.BuildLimits.Load();
            CollectionAssert.AreEquivalent(PublishedNames.DefaultServedExtensions, limits.served_extensions,
                "PublishedNames.DefaultServedExtensions must match Tools/build/build_limits.json served_extensions");
            Assert.That(limits.publish_suffix, Is.EqualTo(PublishedNames.DefaultSuffix));
            Debug.Log("served: " + string.Join(",", limits.served_extensions));
        }
    }
}
