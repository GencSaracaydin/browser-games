using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Checks every managed texture, model and audio clip under a root against its import rule and writes
    /// Logs/import_rules_report.txt (one line per asset: rule, then any differences). Differences mean the asset was
    /// imported before the rules existed, was opted out, or a package postprocessor changed it afterwards; run
    /// "LongShadow/Import/Reapply Rules (All Game Assets)" to fix the first case.
    /// </summary>
    public static class ImportRuleReport
    {
        /// <summary>Report file (project-relative).</summary>
        public const string ReportPath = "Logs/import_rules_report.txt";

        /// <summary>Summary of one run.</summary>
        public struct Summary
        {
            /// <summary>Assets checked.</summary>
            public int Checked;
            /// <summary>Assets whose settings differ from their rule.</summary>
            public int Mismatched;
            /// <summary>Opted-out assets.</summary>
            public int OptedOut;
            /// <summary>Report file.</summary>
            public string File;

            /// <inheritdoc />
            public override string ToString() => Checked + " checked, " + Mismatched + " mismatched, " + OptedOut + " opted out (" + File + ")";
        }

        /// <summary>Runs the check for every asset under <paramref name="root"/> (default Assets/_Game/).</summary>
        public static Summary Run(string root = ImportRulePaths.GameRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Import rule report " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " root " + root);
            var sum = new Summary { File = ReportPath };
            var paths = new List<string>();
            foreach (var p in AssetDatabase.GetAllAssetPaths())
                if (p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) paths.Add(p);
            paths.Sort(StringComparer.Ordinal);
            foreach (var p in paths)
            {
                var line = Check(p, out var mismatch, out var optedOut);
                if (line == null) continue;
                sum.Checked++;
                if (optedOut) sum.OptedOut++;
                if (mismatch) sum.Mismatched++;
                sb.AppendLine(line);
            }
            sb.AppendLine().AppendLine("# " + sum);
            var full = Path.Combine(BuilderUtil.ProjectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllText(full, sb.ToString());
            return sum;
        }

        /// <summary>One report line for an asset, or null when no rule manages it.</summary>
        public static string Check(string assetPath, out bool mismatch, out bool optedOut)
        {
            mismatch = false;
            optedOut = false;
            var importer = AssetImporter.GetAtPath(assetPath);
            List<string> diffs;
            string rule;
            switch (importer)
            {
                case TextureImporter ti:
                {
                    var r = TextureRules.Resolve(assetPath, TextureSourceProbe.Probe(assetPath, ti));
                    if (r == null) return null;
                    rule = r.ToString();
                    diffs = TextureRuleApplier.Diff(ti, r);
                    break;
                }
                case ModelImporter mi:
                {
                    var r = ModelRules.Resolve(assetPath, ModelSourceProbe.Probe(assetPath));
                    if (r == null) return null;
                    rule = r.ToString();
                    diffs = ModelRuleApplier.Diff(mi, r);
                    break;
                }
                case AudioImporter ai:
                {
                    var r = AudioRules.Resolve(assetPath);
                    if (r == null) return null;
                    rule = r + " | imported for " + EditorUserBuildSettings.activeBuildTarget + ": " + AudioRuleApplier.ReadOutput(ai);
                    diffs = AudioRuleApplier.Diff(ai, r);
                    break;
                }
                default:
                    return null;
            }
            optedOut = ImportRulePaths.IsOptedOut(importer);
            mismatch = diffs.Count > 0 && !optedOut;
            var status = optedOut ? "OPTED-OUT" : mismatch ? "MISMATCH" : "ok";
            return status + "  " + assetPath + "  [" + rule + "]" + (diffs.Count > 0 ? "\n      " + string.Join("\n      ", diffs) : "");
        }
    }
}
