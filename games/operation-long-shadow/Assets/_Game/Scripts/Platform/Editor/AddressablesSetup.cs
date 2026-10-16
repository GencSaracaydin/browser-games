using System;
using System.Linq;
using System.Text;
using LongShadow.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Creates and configures the Addressables settings for the web build (TECH_PLAN §7.1, §8.10, §9.2; webgl.md §6):
    /// a JSON catalog (the host serves .json but refuses .bin; Addressables 2.x needs the <c>ENABLE_JSON_CATALOG</c>
    /// define for that, which this script adds to the WebGL target and which takes effect after the next script
    /// compilation), no remote catalog, local LZ4 bundles named <c>*.bundle</c>, UnityWebRequest for local bundles,
    /// no Caching API (WebGL caches bundles in IndexedDB through the loader) and no CRC checks (main-thread time).
    /// Content is built by <see cref="WebBuild"/>, never implicitly with the player.
    /// </summary>
    public static class AddressablesSetup
    {
        /// <summary>Scripting define that switches Addressables 2.x to JSON catalogs.</summary>
        public const string JsonCatalogDefine = "ENABLE_JSON_CATALOG";

        /// <summary>
        /// True when this editor domain was compiled with <see cref="JsonCatalogDefine"/> (the Addressables build code
        /// is compiled with the same defines, so a content build now writes catalog.json).
        /// </summary>
        public static bool JsonCatalogCompiled
        {
            get
            {
#if ENABLE_JSON_CATALOG
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>True when the define is present in the WebGL player settings (it may still need a recompile).</summary>
        public static bool JsonCatalogDefined
        {
            get
            {
                PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL, out var defines);
                return defines != null && defines.Contains(JsonCatalogDefine);
            }
        }

        /// <summary>Menu: create/configure the settings and log what changed.</summary>
        [MenuItem("LongShadow/Platform/Setup Addressables", priority = 210)]
        public static void SetupMenu()
        {
            var log = new StringBuilder();
            Ensure(log, out var definesChanged);
            UnityEngine.Debug.Log("[LS:Platform] Addressables setup:\n" + log + (definesChanged
                ? "\nENABLE_JSON_CATALOG was added: wait for the script compilation to finish before building content."
                : string.Empty));
        }

        /// <summary>Creates (if needed) and configures the settings; returns them. Idempotent.</summary>
        public static AddressableAssetSettings Ensure(StringBuilder log, out bool definesChanged)
        {
            log = log ?? new StringBuilder();
            var created = AddressableAssetSettingsDefaultObject.Settings == null;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) throw new InvalidOperationException("Addressables settings could not be created");
            if (created) log.AppendLine("created " + AssetDatabase.GetAssetPath(settings));

            Set(log, "EnableJsonCatalog", settings.EnableJsonCatalog, true, v => settings.EnableJsonCatalog = v);
            Set(log, "BuildRemoteCatalog", settings.BuildRemoteCatalog, false, v => settings.BuildRemoteCatalog = v);
            Set(log, "DisableCatalogUpdateOnStartup", settings.DisableCatalogUpdateOnStartup, true, v => settings.DisableCatalogUpdateOnStartup = v);
            Set(log, "UniqueBundleIds", settings.UniqueBundleIds, false, v => settings.UniqueBundleIds = v);
            Set(log, "ContiguousBundles", settings.ContiguousBundles, true, v => settings.ContiguousBundles = v);
            Set(log, "NonRecursiveBuilding", settings.NonRecursiveBuilding, true, v => settings.NonRecursiveBuilding = v);
            Set(log, "MaxConcurrentWebRequests", settings.MaxConcurrentWebRequests, 4, v => settings.MaxConcurrentWebRequests = v);
            Set(log, "IgnoreUnsupportedFilesInBuild", settings.IgnoreUnsupportedFilesInBuild, true, v => settings.IgnoreUnsupportedFilesInBuild = v);
            Set(log, "BuildAddressablesWithPlayerBuild", settings.BuildAddressablesWithPlayerBuild,
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer, v => settings.BuildAddressablesWithPlayerBuild = v);

            var dg = settings.DefaultGroup;
            if (dg != null && AddressablesGroupRules.ConfigureSchema(settings, dg)) log.AppendLine("configured " + dg.Name);

            definesChanged = EnsureJsonDefine(log);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>Adds <see cref="JsonCatalogDefine"/> to the WebGL target. Returns true if it changed.</summary>
        public static bool EnsureJsonDefine(StringBuilder log)
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL, out var defines);
            defines = defines ?? new string[0];
            if (defines.Contains(JsonCatalogDefine)) return false;
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, defines.Concat(new[] { JsonCatalogDefine }).ToArray());
            log?.AppendLine("added scripting define " + JsonCatalogDefine + " (WebGL)");
            LSLog.Info(LogCategory.Platform, "Added " + JsonCatalogDefine + " to the WebGL scripting defines (JSON catalog, TECH_PLAN §9.2).");
            return true;
        }

        private static void Set<T>(StringBuilder log, string name, T current, T wanted, Action<T> set)
        {
            if (Equals(current, wanted)) return;
            set(wanted);
            log.AppendLine(name + ": " + current + " -> " + wanted);
        }
    }
}
