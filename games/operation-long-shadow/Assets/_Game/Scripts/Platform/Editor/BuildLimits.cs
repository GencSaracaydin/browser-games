using System;
using System.IO;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Web build and host limits (TECH_PLAN §8.10, §9.1-9.2), loaded from <c>Tools/build/build_limits.json</c>, the file
    /// the Python tools (validate_build.py, publish_plan.py) read too, so C# and Python never disagree. Field names
    /// mirror the JSON keys; the defaults below apply when the file is missing.
    /// </summary>
    [Serializable]
    public sealed class BuildLimits
    {
        /// <summary>Project-relative path of the shared limits file.</summary>
        public const string RelativePath = "Tools/build/build_limits.json";

#pragma warning disable IDE1006 // JSON field names
        /// <summary>Largest file in the build (TECH_PLAN §9.1: 14.5 MB; host limit 15 MB).</summary>
        public long max_file_bytes = 14_500_000;
        /// <summary>Largest text file (host: 16 MB).</summary>
        public long max_text_file_bytes = 16_000_000;
        /// <summary>Largest Addressables bundle (TECH_PLAN §8.10: 12 MB).</summary>
        public long max_bundle_bytes = 12_000_000;
        /// <summary>Most files in a build (TECH_PLAN §9.1: 250).</summary>
        public int max_files = 250;
        /// <summary>Hard size cap (TECH_PLAN §8.10: 230 MB).</summary>
        public long max_total_bytes = 230_000_000;
        /// <summary>Planned size; warn above (220 MB).</summary>
        public long warn_total_bytes = 220_000_000;
        /// <summary>Largest baked reflection probe face (D16: 256 px).</summary>
        public int max_reflection_probe_px = 256;
        /// <summary>Suffix publish_plan.py gives to files the host does not serve.</summary>
        public string publish_suffix = ".wasm";
        /// <summary>Extensions the host serves.</summary>
        public string[] served_extensions = PublishedNames.DefaultServedExtensions;
        /// <summary>Files removed from the output after a build.</summary>
        public string[] strip_patterns = { "*.manifest", ".DS_Store", "Thumbs.db" };
        /// <summary>Paths a web build may contain (anything else is foreign and removed).</summary>
        public string[] allowed_prefixes = { "index.html", "Build/", "StreamingAssets/aa/", "StreamingAssets/video/", "TemplateData/" };
#pragma warning restore IDE1006

        /// <summary>Loads the shared file (defaults with a warning when it is missing or invalid).</summary>
        public static BuildLimits Load(string projectRoot = null)
        {
            var root = projectRoot ?? Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            var path = Path.Combine(root, RelativePath);
            var limits = new BuildLimits();
            if (!File.Exists(path))
            {
                LSLog.Warn(LogCategory.Platform, "Missing " + RelativePath + "; using built-in limits.");
                return limits;
            }
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), limits);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Platform, RelativePath + " is invalid (" + e.Message + "); using built-in limits.");
                return new BuildLimits();
            }
            return limits;
        }
    }
}
