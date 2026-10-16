using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.Build.Reporting;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Writes <c>Builds/Web_report.txt</c> (TECH_PLAN §9.1 step 6): every file of the output with its size (largest
    /// first) and, for bundles, its Addressables group; size per group; size per asset type of the main data file
    /// (from the BuildReport's packed assets, which Unity leaves empty for incremental builds); and the validation.
    /// </summary>
    public static class WebBuildReport
    {
        /// <summary>Writes the report and returns its text.</summary>
        public static string Write(string reportPath, string outputDir, BuildReport player, AddressablesPlayerBuildResult content,
                                   PostBuildValidator.Result validation, WebBuildOptions options)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# OPERATION LONG SHADOW web build report");
            sb.AppendLine("When:    " + DateTime.Now.ToString("s"));
            sb.AppendLine("Variant: " + (options != null && options.Qa ? "QA (diagnostics)" : "Release"));
            sb.AppendLine("Output:  " + outputDir);
            if (player != null)
                sb.AppendLine("Player:  " + player.summary.result + ", " + player.summary.totalTime.TotalSeconds.ToString("0") + " s, " +
                              player.summary.totalErrors + " errors, " + player.summary.totalWarnings + " warnings");

            var groupOfBundle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (content != null)
                foreach (var b in content.AssetBundleBuildResults)
                    if (!string.IsNullOrEmpty(b.FilePath))
                        groupOfBundle[Path.GetFileName(b.FilePath)] = b.SourceAssetGroup != null ? b.SourceAssetGroup.Name : "?";

            // Files.
            var files = new List<(string rel, long size)>();
            if (Directory.Exists(outputDir))
            {
                var root = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    files.Add((f.Substring(root.Length).Replace('\\', '/'), new FileInfo(f).Length));
            }
            var total = files.Sum(f => f.size);
            sb.AppendLine();
            sb.AppendLine("## Files (" + files.Count + ", " + Mb(total) + " MB)");
            foreach (var f in files.OrderByDescending(f => f.size).ThenBy(f => f.rel, StringComparer.Ordinal))
            {
                groupOfBundle.TryGetValue(Path.GetFileName(f.rel), out var g);
                sb.AppendLine(Mb(f.size).PadLeft(9) + " MB  " + f.rel + (g != null ? "  [" + g + "]" : string.Empty));
            }

            // Groups.
            if (content != null && content.AssetBundleBuildResults.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Addressables groups");
                var perGroup = new Dictionary<string, (int count, long bytes)>(StringComparer.Ordinal);
                foreach (var b in content.AssetBundleBuildResults)
                {
                    var g = b.SourceAssetGroup != null ? b.SourceAssetGroup.Name : "?";
                    var size = !string.IsNullOrEmpty(b.FilePath) && File.Exists(b.FilePath) ? new FileInfo(b.FilePath).Length : 0L;
                    perGroup.TryGetValue(g, out var acc);
                    perGroup[g] = (acc.count + 1, acc.bytes + size);
                }
                foreach (var kv in perGroup.OrderByDescending(k => k.Value.bytes))
                    sb.AppendLine(Mb(kv.Value.bytes).PadLeft(9) + " MB  " + kv.Key + " (" + kv.Value.count + " bundle" + (kv.Value.count == 1 ? "" : "s") + ")");
            }

            // Main data by asset type.
            if (player != null)
            {
                var perType = new Dictionary<string, long>(StringComparer.Ordinal);
                var top = new List<(string path, string type, ulong size)>();
                foreach (var pa in player.packedAssets)
                    foreach (var c in pa.contents)
                    {
                        var t = c.type != null ? c.type.Name : "?";
                        perType.TryGetValue(t, out var acc);
                        perType[t] = acc + (long)c.packedSize;
                        top.Add((c.sourceAssetPath, t, c.packedSize));
                    }
                sb.AppendLine();
                sb.AppendLine("## Main data by asset type (uncompressed; empty for incremental builds)");
                foreach (var kv in perType.OrderByDescending(k => k.Value))
                    sb.AppendLine(Mb(kv.Value).PadLeft(9) + " MB  " + kv.Key);
                if (top.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Largest packed assets in the main data");
                    foreach (var a in top.OrderByDescending(a => a.size).Take(25))
                        sb.AppendLine(Mb((long)a.size).PadLeft(9) + " MB  " + a.type + "  " + a.path);
                }
            }

            if (validation != null)
            {
                sb.AppendLine();
                sb.AppendLine("## Validation");
                sb.Append(validation.ToText());
            }

            var text = sb.ToString();
            var dir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(reportPath, text);
            return text;
        }

        private static string Mb(long bytes) => (bytes / 1_000_000.0).ToString("0.000");
    }
}
