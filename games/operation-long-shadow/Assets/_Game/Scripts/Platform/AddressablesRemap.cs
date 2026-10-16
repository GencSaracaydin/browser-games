using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace LongShadow.Platform
{
    /// <summary>
    /// Addressables <c>InternalIdTransformFunc</c> for the published web build (TECH_PLAN §9.2, R3). The artifact host
    /// refuses <c>.bundle</c>/<c>.hash</c>, so <c>publish_plan.py</c> renames such files with a suffix (".wasm") and
    /// sets <c>window.LS_ASSET_SUFFIX</c> (and the served-extension list) in the published index.html. When that suffix
    /// is present, every network location whose extension the host does not serve (bundles, catalog hashes) is mapped
    /// through <see cref="PublishedNames.Map"/>. Local test serving of <c>Builds/Web</c> leaves the suffix empty and
    /// nothing changes. Installed before the first scene loads so no Addressables request can precede it.
    /// </summary>
    public static class AddressablesRemap
    {
        private static bool s_installed;
        private static string s_suffix;
        private static HashSet<string> s_served;
        private static int s_mapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_installed = false;
            s_suffix = null;
            s_served = null;
            s_mapped = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall() => Install();

        /// <summary>True when bundle URLs are being renamed (published build).</summary>
        public static bool Active => s_installed && !string.IsNullOrEmpty(s_suffix);
        /// <summary>Suffix in use (null when inactive).</summary>
        public static string Suffix => s_suffix;
        /// <summary>Number of ids rewritten so far (diagnostics).</summary>
        public static int MappedCount => s_mapped;

        /// <summary>Reads the page's suffix and installs the transform (idempotent; web players only).</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            if (!WebNative.Available) return;
            Install(WebNative.AssetSuffix(), WebNative.ServedExtensions());
        }

        /// <summary>Installs the transform for an explicit suffix/served list (tests, tools). An empty suffix uninstalls.</summary>
        public static void Install(string suffix, string servedList)
        {
            s_installed = true;
            s_suffix = string.IsNullOrEmpty(suffix) ? null : suffix;
            s_served = PublishedNames.ParseServedList(servedList) ?? PublishedNames.DefaultServedSet();
            if (s_suffix == null)
            {
                if (Addressables.InternalIdTransformFunc == Transform) Addressables.InternalIdTransformFunc = null;
                return;
            }
            Addressables.InternalIdTransformFunc = Transform;
            LSLog.Info(LogCategory.Streaming, "Published-name remap active: unserved bundle/hash URLs get '" + s_suffix + "'");
        }

        /// <summary>The transform (public for tests): rewrites network ids of unserved file types, leaves asset ids alone.</summary>
        public static string Transform(IResourceLocation location)
        {
            if (location == null) return null;
            var id = location.InternalId;
            if (s_suffix == null || string.IsNullOrEmpty(id)) return id;
            var isBundle = location.ResourceType == typeof(IAssetBundleResource);
            if (!isBundle && !PublishedNames.IsNetworkPath(id)) return id;
            var mapped = PublishedNames.Map(id, s_suffix, s_served);
            if (!ReferenceEquals(mapped, id) && mapped != id) s_mapped++;
            return mapped;
        }
    }
}
