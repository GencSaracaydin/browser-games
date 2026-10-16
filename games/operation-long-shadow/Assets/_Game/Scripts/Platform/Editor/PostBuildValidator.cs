using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Post-build checks of a web build (TECH_PLAN §9.1 step 5, D16; webgl.md §6.2). File rules (also in
    /// <c>Tools/build/validate_build.py</c>, same <see cref="BuildLimits"/>): every file ≤ 14.5 MB (text ≤ 16 MB),
    /// ≤ 250 files, ≤ 230 MB (warning above 220 MB), every bundle ≤ 12 MB, a compressed (release) wasm, an
    /// index.html that never calls alert(). Content rules (editor only): nothing from an EditorOnly folder in a bundle
    /// or the boot data, no HDR texture formats (RGBAHalf/Float, BC6H), no cubemap except baked reflection probes, and
    /// no reflection probe larger than 256 px per face.
    /// </summary>
    public static class PostBuildValidator
    {
        /// <summary>Outcome of a validation.</summary>
        public sealed class Result
        {
            /// <summary>Blocking problems.</summary>
            public readonly List<string> Errors = new List<string>();
            /// <summary>Non-blocking problems.</summary>
            public readonly List<string> Warnings = new List<string>();
            /// <summary>Information lines (sizes).</summary>
            public readonly List<string> Notes = new List<string>();
            /// <summary>Files in the build.</summary>
            public int FileCount;
            /// <summary>Bytes in the build.</summary>
            public long TotalBytes;
            /// <summary>Largest file (bytes).</summary>
            public long LargestBytes;
            /// <summary>Largest file (relative path).</summary>
            public string LargestPath = string.Empty;
            /// <summary>Assets checked by the content pass.</summary>
            public int AssetsChecked;

            /// <summary>True when there is no error.</summary>
            public bool Ok => Errors.Count == 0;

            /// <summary>Human-readable report.</summary>
            public string ToText()
            {
                var sb = new StringBuilder();
                sb.AppendLine((Ok ? "PASS" : "FAIL") + ": " + FileCount + " files, " + Mb(TotalBytes) + " MB, largest " + LargestPath + " " + Mb(LargestBytes) +
                              " MB" + (AssetsChecked > 0 ? ", " + AssetsChecked + " assets checked" : string.Empty));
                foreach (var e in Errors) sb.AppendLine("ERROR   " + e);
                foreach (var w in Warnings) sb.AppendLine("WARNING " + w);
                foreach (var n in Notes) sb.AppendLine("        " + n);
                return sb.ToString();
            }
        }

        /// <summary>Validates the files of a web build folder (no editor state needed; used by EditMode tests).</summary>
        public static Result ValidateOutput(string buildDir, BuildLimits limits)
        {
            limits = limits ?? new BuildLimits();
            var r = new Result();
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
            {
                r.Errors.Add("Build folder not found: " + buildDir);
                return r;
            }
            var root = Path.GetFullPath(buildDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var served = PublishedNames.ParseServedList(string.Join(",", limits.served_extensions ?? new string[0])) ?? PublishedNames.DefaultServedSet();
            var textExt = new HashSet<string>(new[] { "html", "css", "js", "mjs", "json", "txt", "xml", "svg", "md", "csv", "map", "hash" }, StringComparer.OrdinalIgnoreCase);

            bool hasIndex = false, hasLoader = false, hasData = false, hasFramework = false, hasCode = false;
            foreach (var abs in files)
            {
                var rel = abs.Substring(root.Length).Replace('\\', '/');
                var size = new FileInfo(abs).Length;
                var name = Path.GetFileName(rel);
                r.FileCount++;
                r.TotalBytes += size;
                if (size > r.LargestBytes)
                {
                    r.LargestBytes = size;
                    r.LargestPath = rel;
                }

                if (MatchesAny(name, limits.strip_patterns)) r.Warnings.Add(rel + ": should have been removed after the build (" + string.Join(", ", limits.strip_patterns) + ")");
                if (!StartsWithAny(rel, limits.allowed_prefixes)) r.Warnings.Add(rel + ": foreign file (not part of a Unity web build)");

                var ext = PublishedNames.ExtensionOf(rel);
                var isText = textExt.Contains(ext);
                var cap = isText ? limits.max_text_file_bytes : limits.max_file_bytes;
                if (size > cap) r.Errors.Add(rel + ": " + Mb(size) + " MB is over the " + Mb(cap) + " MB per-file limit");
                if (rel.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) && size > limits.max_bundle_bytes)
                    r.Errors.Add(rel + ": bundle " + Mb(size) + " MB is over " + Mb(limits.max_bundle_bytes) + " MB (split the group, TECH_PLAN §8.10)");

                if (rel == "index.html")
                {
                    hasIndex = true;
                    var html = File.ReadAllText(abs);
                    if (ContainsAlertCall(html)) r.Errors.Add("index.html calls alert(): the template must never show an alert dialog (TECH_PLAN §7.1)");
                    if (html.IndexOf("errorHandler", StringComparison.Ordinal) < 0) r.Warnings.Add("index.html has no errorHandler (not the CovertOps template?)");
                }
                if (rel.StartsWith("Build/", StringComparison.Ordinal))
                {
                    if (name.EndsWith(".loader.js", StringComparison.Ordinal)) hasLoader = true;
                    else if (name.Contains(".data")) hasData = true;
                    else if (name.Contains(".framework.js")) hasFramework = true;
                    else if (name.Contains(".wasm")) hasCode = true;
                    if (name.EndsWith(".wasm", StringComparison.Ordinal) && !name.Contains(".unityweb") && size > limits.max_file_bytes / 2)
                        r.Errors.Add(rel + ": uncompressed wasm (development build or compression off); only compressed release builds are hostable (webgl.md §7.1)");
                }
                if (!served.Contains(ext)) r.Notes.Add(rel + ": not served by the host as is; publish_plan.py publishes it as '" + name + limits.publish_suffix + "'");
            }

            if (!hasIndex) r.Errors.Add("index.html is missing");
            if (!hasLoader) r.Errors.Add("Build/*.loader.js is missing");
            if (!hasData) r.Errors.Add("Build/*.data* is missing");
            if (!hasFramework) r.Errors.Add("Build/*.framework.js* is missing");
            if (!hasCode) r.Errors.Add("Build/*.wasm* is missing");
            if (r.FileCount > limits.max_files) r.Errors.Add(r.FileCount + " files is over the " + limits.max_files + " file limit");
            if (r.TotalBytes > limits.max_total_bytes) r.Errors.Add("Total " + Mb(r.TotalBytes) + " MB is over the " + Mb(limits.max_total_bytes) + " MB cap");
            else if (r.TotalBytes > limits.warn_total_bytes) r.Warnings.Add("Total " + Mb(r.TotalBytes) + " MB is over the planned " + Mb(limits.warn_total_bytes) + " MB");
            return r;
        }

        /// <summary>
        /// Content rules for a set of asset paths (bundle contents and the boot data). Adds errors to <paramref name="r"/>.
        /// </summary>
        public static void ValidateContent(Result r, IEnumerable<string> assetPaths, BuildLimits limits, string where)
        {
            limits = limits ?? new BuildLimits();
            foreach (var path in assetPaths.Distinct())
            {
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                r.AssetsChecked++;
                if (path.IndexOf("/EditorOnly/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    r.Errors.Add(where + ": EditorOnly asset shipped: " + path);
                    continue;
                }
                var importer = AssetImporter.GetAtPath(path);
                if (importer is TextureImporter ti)
                {
                    if (ti.textureType == TextureImporterType.Lightmap || ti.textureType == TextureImporterType.DirectionalLightmap ||
                        ti.textureType == TextureImporterType.Shadowmask) continue; // encoded per the project's lightmap encoding
                    var probe = IsReflectionProbe(path);
                    var format = WebGLFormat(ti);
                    if (IsHdrFormat(format) && !probe)
                        r.Errors.Add(where + ": HDR texture format " + format + " in " + path + " (WebGL2 has no BC6H; TECH_PLAN §9.1)");
                    if (ti.textureShape == TextureImporterShape.TextureCube)
                    {
                        if (!probe) r.Errors.Add(where + ": cubemap " + path + " (only baked reflection probes may ship)");
                        else CheckProbeSize(r, path, limits, where);
                    }
                }
                else if (path.EndsWith(".cubemap", StringComparison.OrdinalIgnoreCase))
                {
                    r.Errors.Add(where + ": cubemap asset " + path);
                }
                else if (IsReflectionProbe(path))
                {
                    CheckProbeSize(r, path, limits, where);
                }
            }
        }

        /// <summary>Every asset that ends up in an included Addressables group (explicit entries plus dependencies).</summary>
        public static List<string> CollectBundleAssets(AddressableAssetSettings settings)
        {
            var roots = new List<string>();
            if (settings == null) return roots;
            foreach (var g in settings.groups)
            {
                if (g == null) continue;
                var schema = g.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild) continue;
                var entries = new List<AddressableAssetEntry>();
                g.GatherAllAssets(entries, true, true, true);
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.AssetPath)) roots.Add(e.AssetPath);
            }
            return Dependencies(roots);
        }

        /// <summary>Assets packed into the main data file (S_Boot and its dependencies).</summary>
        public static List<string> CollectBootAssets(string bootScenePath) =>
            File.Exists(bootScenePath) ? Dependencies(new List<string> { bootScenePath }) : new List<string>();

        /// <summary>Menu: validates Builds/Web (files) and the current Addressables/S_Boot content; writes Builds/Web_validation.txt.</summary>
        [MenuItem("LongShadow/Build/Validate Last Web Build", priority = 120)]
        public static void ValidateLastBuildMenu()
        {
            var r = ValidateLastBuild();
            Debug.Log("[LS:Platform] " + r.ToText());
        }

        /// <summary>Validates <see cref="WebBuild.OutputFolder"/> plus the content rules; writes the report file.</summary>
        public static Result ValidateLastBuild()
        {
            var limits = BuildLimits.Load();
            var root = BuilderUtil.ProjectRoot;
            var r = ValidateOutput(Path.Combine(root, WebBuild.OutputFolder), limits);
            ValidateContent(r, CollectBundleAssets(UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings), limits, "bundles");
            ValidateContent(r, CollectBootAssets(BootSceneBuilder.ScenePath), limits, "boot data");
            File.WriteAllText(Path.Combine(root, WebBuild.ValidationReportPath), r.ToText());
            return r;
        }

        /// <summary>True when the HTML contains an alert( call outside comments ("alert(" inside a // or /* */ comment is ignored).</summary>
        public static bool ContainsAlertCall(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;
            var lines = html.Split('\n');
            var inBlock = false;
            foreach (var raw in lines)
            {
                var line = raw;
                if (inBlock)
                {
                    var end = line.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0) continue;
                    line = line.Substring(end + 2);
                    inBlock = false;
                }
                var start = line.IndexOf("/*", StringComparison.Ordinal);
                if (start >= 0)
                {
                    var end = line.IndexOf("*/", start + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        inBlock = true;
                        line = line.Substring(0, start);
                    }
                    else line = line.Substring(0, start) + line.Substring(end + 2);
                }
                var slash = line.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0 && (slash == 0 || line[slash - 1] != ':')) line = line.Substring(0, slash);
                var i = line.IndexOf("alert(", StringComparison.Ordinal);
                while (i >= 0)
                {
                    var prev = i > 0 ? line[i - 1] : ' ';
                    if (!char.IsLetterOrDigit(prev) && prev != '_' && prev != '$') return true;
                    i = line.IndexOf("alert(", i + 1, StringComparison.Ordinal);
                }
            }
            return false;
        }

        private static List<string> Dependencies(List<string> roots)
        {
            if (roots.Count == 0) return roots;
            var deps = AssetDatabase.GetDependencies(roots.ToArray(), true);
            return deps.Where(p => !p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                       .Distinct().ToList();
        }

        private static void CheckProbeSize(Result r, string path, BuildLimits limits, string where)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (tex != null && tex.width > limits.max_reflection_probe_px)
                r.Errors.Add(where + ": reflection probe " + path + " is " + tex.width + " px per face (max " + limits.max_reflection_probe_px + ", D16)");
        }

        private static string WebGLFormat(TextureImporter ti)
        {
            var ps = ti.GetPlatformTextureSettings("WebGL");
            var f = ps != null && ps.overridden ? ps.format : ti.GetAutomaticFormat("WebGL");
            return f.ToString();
        }

        /// <summary>True for HDR texture formats WebGL2 cannot ship efficiently (TECH_PLAN §9.1).</summary>
        public static bool IsHdrFormat(string format)
        {
            if (string.IsNullOrEmpty(format)) return false;
            return format.IndexOf("Half", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   format.IndexOf("Float", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   format.Equals("BC6H", StringComparison.OrdinalIgnoreCase) ||
                   format.IndexOf("RGB48", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   format.IndexOf("RGBA64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   format.IndexOf("RGB9e5", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Unity names baked reflection probes "ReflectionProbe-N.exr" next to the scene.</summary>
        public static bool IsReflectionProbe(string path) =>
            Path.GetFileName(path).StartsWith("ReflectionProbe-", StringComparison.OrdinalIgnoreCase);

        private static bool MatchesAny(string name, string[] patterns)
        {
            if (patterns == null) return false;
            foreach (var p in patterns)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p.StartsWith("*", StringComparison.Ordinal))
                {
                    if (name.EndsWith(p.Substring(1), StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (string.Equals(name, p, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool StartsWithAny(string rel, string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0) return true;
            foreach (var p in prefixes)
                if (!string.IsNullOrEmpty(p) && (rel == p || rel.StartsWith(p, StringComparison.Ordinal))) return true;
            return false;
        }

        private static string Mb(long bytes) => (bytes / 1_000_000.0).ToString("0.00");
    }
}
