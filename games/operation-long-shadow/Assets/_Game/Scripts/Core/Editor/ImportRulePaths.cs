using System;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Path helpers shared by the import rules (TECH_PLAN §3.10–3.12). Rules are keyed by the path below the game root
    /// <c>Assets/_Game/</c>, e.g. "Art/Textures/Env/sand_01/T_Sand01_BC.png". <see cref="TestRoot"/> mirrors that layout
    /// for the rule self-test and the EditMode tests: a sample copied to <c>Assets/__ImportRuleTest/Art/...</c> imports
    /// with exactly the rules of <c>Assets/_Game/Art/...</c> and can be deleted afterwards without touching real content.
    /// An importer whose <c>userData</c> contains <see cref="OptOutTag"/> is left alone by every rule (for package
    /// importers such as WP05's RocketboxImporter that own their settings).
    /// </summary>
    public static class ImportRulePaths
    {
        /// <summary>Root of shipped game content.</summary>
        public const string GameRoot = "Assets/_Game/";
        /// <summary>Temporary mirror root used by <see cref="ImportRuleSelfTest"/> and the EditMode tests.</summary>
        public const string TestRoot = "Assets/__ImportRuleTest/";
        /// <summary>Put this text into an importer's userData to opt the asset out of every LongShadow import rule.</summary>
        public const string OptOutTag = "ls-import:manual";

        /// <summary>
        /// Path below the rule root ("Art/Textures/Env/sand_01/T_Sand01_BC.png") or null when the asset is outside
        /// <see cref="GameRoot"/> and <see cref="TestRoot"/> (research scaffolding, packages: never touched).
        /// </summary>
        public static string ToRuleRelative(string assetPath)
        {
            var root = RootOf(assetPath);
            return root == null ? null : Normalize(assetPath).Substring(root.Length);
        }

        /// <summary><see cref="GameRoot"/>, <see cref="TestRoot"/> or null.</summary>
        public static string RootOf(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var p = Normalize(assetPath);
            if (p.StartsWith(GameRoot, StringComparison.OrdinalIgnoreCase)) return GameRoot;
            if (p.StartsWith(TestRoot, StringComparison.OrdinalIgnoreCase)) return TestRoot;
            return null;
        }

        /// <summary>Maps a rule-relative path back to a project path under the same root as <paramref name="sameRootAs"/>.</summary>
        public static string ToAssetPath(string ruleRelative, string sameRootAs)
        {
            var root = RootOf(sameRootAs) ?? GameRoot;
            return root + ruleRelative.TrimStart('/');
        }

        /// <summary>True when the importer opted out of the rules (<see cref="OptOutTag"/> in userData).</summary>
        public static bool IsOptedOut(AssetImporter importer)
        {
            return importer != null && !string.IsNullOrEmpty(importer.userData) &&
                   importer.userData.IndexOf(OptOutTag, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Case-insensitive prefix test on a rule-relative path ("Art/Textures/Env/").</summary>
        public static bool Under(string ruleRelative, string prefix)
        {
            return ruleRelative != null && ruleRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The folder name directly below <paramref name="prefix"/> ("sand_01" for "Art/Textures/Env/sand_01/x.png"
        /// and prefix "Art/Textures/Env/"), or null when the file sits directly in the prefix folder.
        /// </summary>
        public static string FolderBelow(string ruleRelative, string prefix)
        {
            if (!Under(ruleRelative, prefix)) return null;
            var rest = ruleRelative.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            return slash <= 0 ? null : rest.Substring(0, slash);
        }

        /// <summary>Forward slashes, no leading "./".</summary>
        public static string Normalize(string path)
        {
            if (path == null) return null;
            path = path.Replace('\\', '/');
            return path.StartsWith("./", StringComparison.Ordinal) ? path.Substring(2) : path;
        }
    }
}
