using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Checks the content agents' import-ready outputs (<c>RawAssets/&lt;wp&gt;/ready/</c>, ORCHESTRATION §6) against the
    /// import rules before anything is copied into Assets/: every row of every <c>import_index*.json</c> (and of other
    /// row files with "import_notes") is resolved with the real rules, and the settings its free-text notes ask for
    /// (Max Size, sRGB, DXT1/DXT5, crunch quality, mips, wrap, coverage, alpha-is-transparency, aniso; Force To Mono,
    /// load type, preload, quality, sample rate; Mesh LOD, mesh compression, lightmap UVs, Humanoid) are compared.
    /// Differences are either a rule gap or a note that disagrees with TECH_PLAN; the rules win on import.
    /// <see cref="ExportRuleTable"/> writes the resolved texture rules for <c>Tools/build/validate_assets.py --check-mirror</c>.
    /// </summary>
    public static class ReadyContentCheck
    {
        /// <summary>Report of <see cref="CompareNotes"/> (project-relative).</summary>
        public const string ReportPath = "Logs/import_rules_vs_ready_notes.txt";
        /// <summary>Output of <see cref="ExportRuleTable"/> (project-relative).</summary>
        public const string RuleTablePath = "Logs/import_rules_table.tsv";

        private static readonly string[] s_textureExt = { ".png", ".jpg", ".jpeg", ".tga", ".exr", ".hdr", ".psd", ".tif", ".tiff" };
        private static readonly string[] s_audioExt = { ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" };
        private static readonly string[] s_modelExt = { ".fbx", ".obj" };

        /// <summary>Result of a comparison.</summary>
        public struct Summary
        {
            /// <summary>Rows with notes that a rule manages.</summary>
            public int Checked;
            /// <summary>Rows whose notes ask for something the rule does not do.</summary>
            public int Differences;
            /// <summary>Rows whose source file is missing from the ready folder.</summary>
            public int MissingSources;

            /// <inheritdoc />
            public override string ToString() => Checked + " checked, " + Differences + " with differences, " + MissingSources + " missing sources (" + ReportPath + ")";
        }

        private sealed class Row
        {
            public string ReadyRoot, File, Target, Notes, Index;
        }

        /// <summary>Compares every ready row with its rule and writes <see cref="ReportPath"/>.</summary>
        public static Summary CompareNotes()
        {
            TextureBudgets.Reload();
            var sum = new Summary();
            var sb = new StringBuilder();
            sb.AppendLine("# Import rules vs content import notes " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("# A difference means the rule will import the file differently from what its notes say (the rule wins).");
            foreach (var row in Rows())
            {
                var src = Path.Combine(row.ReadyRoot, row.File);
                if (!File.Exists(src))
                {
                    sum.MissingSources++;
                    sb.AppendLine("MISSING-SOURCE  " + row.Target + "  (" + row.Index + ")");
                    continue;
                }
                var ext = Path.GetExtension(row.Target).ToLowerInvariant();
                List<string> diffs;
                string rule;
                if (Array.IndexOf(s_textureExt, ext) >= 0)
                {
                    var r = TextureRules.Resolve(row.Target, TextureSourceProbe.ProbeFile(src, true));
                    if (r == null) continue;
                    rule = r.ToString();
                    diffs = CompareTexture(r, row.Notes);
                }
                else if (Array.IndexOf(s_audioExt, ext) >= 0)
                {
                    var r = AudioRules.Resolve(row.Target);
                    if (r == null) continue;
                    rule = r.ToString();
                    diffs = CompareAudio(r, row.Notes);
                }
                else if (Array.IndexOf(s_modelExt, ext) >= 0)
                {
                    var r = ModelRules.Resolve(row.Target, ModelSourceProbe.Probe(src));
                    if (r == null) continue;
                    rule = r.ToString();
                    diffs = CompareModel(r, row.Notes);
                }
                else continue;
                sum.Checked++;
                if (diffs.Count == 0) continue;
                sum.Differences++;
                sb.AppendLine("DIFF  " + row.Target + "  [" + rule + "]");
                foreach (var d in diffs) sb.AppendLine("        " + d);
            }
            sb.AppendLine().AppendLine("# " + sum);
            Write(ReportPath, sb.ToString());
            return sum;
        }

        /// <summary>
        /// Writes the resolved rule of every texture under RawAssets/*/ready/Assets and Assets/_Game as TSV
        /// (target path, rule, cap source, type, sRGB, max size, WebGL format, quality, mips, wrap, coverage,
        /// alpha-is-transparency, aniso, source size, alpha used, bits, editor-only, origin, NPOT None, sprite). Returns the row count.
        /// </summary>
        public static int ExportRuleTable(string outPath = RuleTablePath)
        {
            TextureBudgets.Reload();
            var sb = new StringBuilder();
            sb.AppendLine("target\trule\tcap_source\ttype\tsrgb\tmax\tformat\tquality\tmips\twrap\tcoverage\talpha_is_transparency\taniso\tsource\talpha_used\tbits\teditor_only\torigin\tnpot_none\tsprite");
            var count = 0;
            var root = BuilderUtil.ProjectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            var files = new List<KeyValuePair<string, string>>(); // target, full path
            var raw = Path.Combine(BuilderUtil.ProjectRoot, "RawAssets");
            if (Directory.Exists(raw))
                foreach (var wp in Directory.GetDirectories(raw))
                {
                    var ready = Path.Combine(wp, "ready", "Assets");
                    if (!Directory.Exists(ready)) continue;
                    foreach (var f in Directory.GetFiles(ready, "*", SearchOption.AllDirectories))
                    {
                        var p = f.Replace('\\', '/');
                        var i = p.IndexOf("/ready/Assets/", StringComparison.Ordinal);
                        files.Add(new KeyValuePair<string, string>(p.Substring(i + "/ready/".Length), p));
                    }
                }
            var game = Path.Combine(BuilderUtil.ProjectRoot, ImportRulePaths.GameRoot);
            if (Directory.Exists(game))
                foreach (var f in Directory.GetFiles(game, "*", SearchOption.AllDirectories))
                {
                    var p = f.Replace('\\', '/');
                    files.Add(new KeyValuePair<string, string>(p.Substring(root.Length), p));
                }
            foreach (var kv in files)
            {
                if (Array.IndexOf(s_textureExt, Path.GetExtension(kv.Key).ToLowerInvariant()) < 0) continue;
                var info = TextureSourceProbe.ProbeFile(kv.Value, true);
                var r = TextureRules.Resolve(kv.Key, info);
                if (r == null) continue;
                count++;
                sb.Append(kv.Key).Append('\t').Append(r.Name).Append('\t').Append(r.CapSource).Append('\t').Append(r.Type).Append('\t')
                  .Append(r.SRGB ? 1 : 0).Append('\t').Append(r.MaxSize).Append('\t').Append(r.WebGLFormat).Append('\t').Append(r.Compressed ? r.Quality : -1).Append('\t')
                  .Append(r.Mipmaps ? 1 : 0).Append('\t').Append(r.WrapU).Append('/').Append(r.WrapV).Append('\t').Append(r.PreserveCoverage ? 1 : 0).Append('\t')
                  .Append(r.AlphaIsTransparency ? 1 : 0).Append('\t').Append(r.Aniso).Append('\t').Append(info.Width).Append('x').Append(info.Height).Append('\t')
                  .Append(info.AlphaUsed ? 1 : 0).Append('\t').Append(info.BitsPerChannel).Append('\t').Append(r.EditorOnly ? 1 : 0).Append('\t')
                  .Append(kv.Value.StartsWith(root + "RawAssets/", StringComparison.Ordinal) ? "ready" : "assets").Append('\t')
                  .Append(r.NpotScale == TextureImporterNPOTScale.None ? 1 : 0).Append('\t').Append(r.Type == TextureImporterType.Sprite ? 1 : 0).AppendLine();
            }
            Write(outPath, sb.ToString());
            return count;
        }

        // ------------------------------------------------------------------ note parsing

        private static readonly Regex s_maxSize = new Regex(@"Max Size (\d{2,5})", RegexOptions.IgnoreCase);
        private static readonly Regex s_crunchQ = new Regex(@"Crunched q(\d{1,3})", RegexOptions.IgnoreCase);
        private static readonly Regex s_aniso = new Regex(@"aniso (\d{1,2})", RegexOptions.IgnoreCase);
        private static readonly Regex s_quality = new Regex(@"Quality (\d{1,3})");
        private static readonly Regex s_rate = new Regex(@"Override (\d{4,6}) Hz");
        private static readonly Regex s_meshCompression = new Regex(@"Mesh Compression (Off|Low|Medium|High)", RegexOptions.IgnoreCase);

        private static List<string> CompareTexture(TextureRule r, string n)
        {
            var d = new List<string>();
            Match m;
            if ((m = s_maxSize.Match(n)).Success && int.Parse(m.Groups[1].Value) != r.MaxSize) d.Add("Max Size: notes " + m.Groups[1].Value + ", rule " + r.MaxSize + " (" + r.CapSource + ")");
            var normal = r.Type == TextureImporterType.NormalMap;
            if (!normal && Regex.IsMatch(n, @"sRGB (ON|on)\b") && !r.SRGB) d.Add("sRGB: notes on, rule off");
            if (Regex.IsMatch(n, @"sRGB (OFF|off)\b") && r.SRGB) d.Add("sRGB: notes off, rule on");
            if (!normal && n.Contains("DXT1") && r.WebGLFormat == TextureImporterFormat.DXT5Crunched) d.Add("format: notes DXT1, rule DXT5");
            if (Regex.IsMatch(n, @"\bDXT5 ") && (r.WebGLFormat == TextureImporterFormat.DXT1Crunched || r.WebGLFormat == TextureImporterFormat.DXT1)) d.Add("format: notes DXT5, rule DXT1");
            if (r.Compressed && r.Crunched && (m = s_crunchQ.Match(n)).Success && int.Parse(m.Groups[1].Value) != r.Quality) d.Add("crunch quality: notes " + m.Groups[1].Value + ", rule " + r.Quality);
            if (Regex.IsMatch(n, @"mips (OFF|off)|no mips|Mip Maps OFF") && r.Mipmaps) d.Add("mips: notes off, rule on");
            if (Regex.IsMatch(n, @"wrap Clamp") && r.WrapU != TextureWrapMode.Clamp) d.Add("wrap: notes Clamp, rule " + r.WrapU);
            if (Regex.IsMatch(n, @"wrap Repeat") && r.WrapU != TextureWrapMode.Repeat) d.Add("wrap: notes Repeat, rule " + r.WrapU);
            if (Regex.IsMatch(n, @"Preserve Coverage") && !r.PreserveCoverage) d.Add("coverage: notes on, rule off");
            if (Regex.IsMatch(n, @"Alpha Is Transparency ON") && !r.AlphaIsTransparency) d.Add("alpha-is-transparency: notes on, rule off");
            if (Regex.IsMatch(n, @"^Normal map|Normal map,") && !normal) d.Add("type: notes normal map, rule " + r.Type);
            if ((m = s_aniso.Match(n)).Success && int.Parse(m.Groups[1].Value) != r.Aniso) d.Add("aniso: notes " + m.Groups[1].Value + ", rule " + r.Aniso);
            return d;
        }

        private static List<string> CompareAudio(AudioRule r, string n)
        {
            var d = new List<string>();
            Match m;
            if (Regex.IsMatch(n, @"Force To Mono ON") && !r.Mono) d.Add("Force To Mono: notes on, rule off");
            if (Regex.IsMatch(n, @"Force To Mono OFF") && r.Mono) d.Add("Force To Mono: notes off, rule on");
            if (n.Contains("Decompress On Load") && r.LoadType != AudioClipLoadType.DecompressOnLoad) d.Add("load type: notes Decompress On Load, rule " + r.LoadType);
            if (n.Contains("Compressed In Memory") && r.LoadType != AudioClipLoadType.CompressedInMemory) d.Add("load type: notes Compressed In Memory, rule " + r.LoadType);
            if (Regex.IsMatch(n, @"Preload Audio Data ON") && !r.Preload) d.Add("preload: notes on, rule off");
            if (Regex.IsMatch(n, @"Preload Audio Data OFF") && r.Preload) d.Add("preload: notes off, rule on");
            if ((m = s_quality.Match(n)).Success && int.Parse(m.Groups[1].Value) != r.Quality) d.Add("quality: notes " + m.Groups[1].Value + ", rule " + r.Quality);
            if ((m = s_rate.Match(n)).Success && int.Parse(m.Groups[1].Value) != r.SampleRate) d.Add("sample rate: notes " + m.Groups[1].Value + ", rule " + r.SampleRate);
            if (n.Contains("no sample-rate override") && r.SampleRate != 0) d.Add("sample rate: notes native, rule " + r.SampleRate);
            return d;
        }

        private static List<string> CompareModel(ModelRule r, string n)
        {
            var d = new List<string>();
            Match m;
            if (Regex.IsMatch(n, @"(turn OFF|disable) Unity Mesh LOD|Mesh LOD generation off", RegexOptions.IgnoreCase) && r.MeshLods) d.Add("Mesh LOD: notes off, rule on");
            if ((m = s_meshCompression.Match(n)).Success && !string.Equals(m.Groups[1].Value, r.Compression.ToString(), StringComparison.OrdinalIgnoreCase))
                d.Add("mesh compression: notes " + m.Groups[1].Value + ", rule " + r.Compression);
            if (Regex.IsMatch(n, @"Generate Lightmap UVs ON") && !r.LightmapUVs) d.Add("lightmap UVs: notes on, rule off");
            if (Regex.IsMatch(n, @"Generate Lightmap UVs OFF") && r.LightmapUVs) d.Add("lightmap UVs: notes off, rule on");
            if (Regex.IsMatch(n, @"\bHumanoid\b") && r.Animation != ModelImporterAnimationType.Human) d.Add("rig: notes Humanoid, rule " + r.Animation);
            return d;
        }

        // ------------------------------------------------------------------ rows

        private static IEnumerable<Row> Rows()
        {
            var raw = Path.Combine(BuilderUtil.ProjectRoot, "RawAssets");
            if (!Directory.Exists(raw)) yield break;
            foreach (var wp in Directory.GetDirectories(raw))
            {
                var ready = Path.Combine(wp, "ready");
                if (!Directory.Exists(ready)) continue;
                var mirrored = Path.Combine(ready, "Assets") + Path.DirectorySeparatorChar;
                foreach (var f in Directory.GetFiles(ready, "*.json", SearchOption.AllDirectories))
                {
                    if (f.StartsWith(mirrored, StringComparison.Ordinal)) continue;
                    List<object> rows;
                    try
                    {
                        rows = MiniJson.Parse(File.ReadAllText(f)) as List<object>;
                    }
                    catch (Exception e)
                    {
                        LSLog.Warn(LogCategory.Assets, "ReadyContentCheck: cannot parse " + f + ": " + e.Message);
                        continue;
                    }
                    if (rows == null) continue;
                    foreach (var o in rows)
                    {
                        if (!(o is Dictionary<string, object> d)) continue;
                        var target = MiniJson.Str(d, "target_path");
                        var notes = MiniJson.Str(d, "import_notes");
                        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(notes)) continue;
                        yield return new Row
                        {
                            ReadyRoot = ready, File = MiniJson.Str(d, "file") ?? target, Target = target, Notes = notes,
                            Index = f.Substring(BuilderUtil.ProjectRoot.Length + 1),
                        };
                    }
                }
            }
        }

        private static void Write(string relPath, string text)
        {
            var full = Path.Combine(BuilderUtil.ProjectRoot, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            File.WriteAllText(full, text);
        }
    }
}
