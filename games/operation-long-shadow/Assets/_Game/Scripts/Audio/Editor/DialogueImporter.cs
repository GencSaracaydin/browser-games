using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// Builds <c>LIB_Dialogue</c> (TECH_PLAN §7.10) from <c>Docs/DIALOGUE.json</c>, the ASSET_MANIFEST <c>VO_&lt;id&gt;</c>
    /// entries (chains in <c>variants</c>; <c>{A|B}</c> in the target path marks the two Legion bark voices), the clips
    /// <c>Assets/_Game/Audio/VO/&lt;CHAIN&gt;/VO_&lt;id&gt;_&lt;CHAIN&gt;[_A|_B].wav</c> and the envelopes
    /// <c>Assets/_Game/Audio/Envelopes/VO_&lt;id&gt;.json</c> (30 Hz RMS, D10). Per line: speaker, callsign, voice, subtitle
    /// (the <c>subtitle</c> field if present, else <c>text</c>), clips per chain and voice, durations, envelopes, the
    /// beat window, priority, channel, objective-critical and repeatable flags (<see cref="DialogueRules"/>) and the G9
    /// switch time of D039 (the first "Seven" in the token onsets).
    /// <para>A line without a clip in every chain its manifest entry lists is an <b>error</b> (logged, counted, listed in
    /// <c>Logs/dialogue_import_report.txt</c>); the library is still written so the game plays (silent lines keep their
    /// subtitle and an estimated length). <b>LongShadow/Audio/Validate Dialogue</b> checks without writing.</para>
    /// </summary>
    public static class DialogueImporter
    {
        /// <summary>The library asset.</summary>
        public const string LibraryPath = Libraries.Folder + "/LIB_Dialogue.asset";
        /// <summary>DIALOGUE.json, project-relative.</summary>
        public const string DialoguePath = "Docs/DIALOGUE.json";
        /// <summary>Root of the VO chain folders.</summary>
        public const string VORoot = "Assets/_Game/Audio/VO";
        /// <summary>Report file (project-relative).</summary>
        public const string ReportPath = "Logs/dialogue_import_report.txt";

        /// <summary>Imports and writes LIB_Dialogue; returns the report.</summary>
        [MenuItem("LongShadow/Audio/Import Dialogue", priority = 111)]
        public static string Import() => Run(true, out _);

        /// <summary>Checks DIALOGUE.json against the clips and envelopes without writing; returns the report.</summary>
        [MenuItem("LongShadow/Audio/Validate Dialogue", priority = 112)]
        public static string Validate() => Run(false, out _);

        /// <summary>Runs the import; <paramref name="errors"/> = lines missing a clip of a listed chain (and unreadable data).</summary>
        public static string Run(bool write, out int errors)
        {
            var log = new StringBuilder();
            log.AppendLine("[Audio] " + (write ? "LIB_Dialogue import " : "dialogue validation ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            var errs = new List<string>();
            var warns = new List<string>();
            var root = BuilderUtil.ProjectRoot;
            var dialogueFile = Path.Combine(root, DialoguePath);
            List<object> rows = null;
            try
            {
                rows = MiniJson.Parse(File.ReadAllText(dialogueFile)) as List<object>;
            }
            catch (Exception e)
            {
                errs.Add("Cannot read " + DialoguePath + ": " + e.Message);
            }
            if (rows == null)
            {
                errs.Add(DialoguePath + " is not a JSON array.");
                return Finish(log, errs, warns, null, write, out errors);
            }

            var lines = BuildLines(rows, ManifestLookup, LoadClip, id => LoadEnvelope(root, id, errs), errs, warns);
            FindStrayClips(lines, warns);
            return Finish(log, errs, warns, lines, write, out errors);
        }

        /// <summary>
        /// Builds the lines (pure apart from the three lookups; tests pass fakes). <paramref name="manifest"/> returns the
        /// manifest entry of a <c>VO_&lt;id&gt;</c> id or null; <paramref name="loadClip"/> a clip by project path or null;
        /// <paramref name="loadEnvelope"/> a line's envelope or null.
        /// </summary>
        public static List<DialogueLine> BuildLines(List<object> rows, Func<string, ManifestEntry> manifest, Func<string, AudioClip> loadClip,
                                                    Func<string, EnvelopeFile> loadEnvelope, List<string> errors, List<string> warnings)
        {
            var lines = new List<DialogueLine>(rows != null ? rows.Count : 0);
            if (rows == null) return lines;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (!(r is Dictionary<string, object> o)) continue;
                var id = MiniJson.Str(o, "id");
                if (string.IsNullOrEmpty(id))
                {
                    errors?.Add("A DIALOGUE.json row has no id.");
                    continue;
                }
                if (!seen.Add(id))
                {
                    errors?.Add(id + ": duplicate id in DIALOGUE.json.");
                    continue;
                }
                var speaker = MiniJson.Str(o, "speaker") ?? "";
                if (SpeakerIds.StyleOf(speaker).Id != speaker) warnings?.Add(id + ": speaker '" + speaker + "' has no subtitle style in SpeakerIds.");
                var text = MiniJson.Str(o, "text") ?? "";
                var line = new DialogueLine(id, speaker, MiniJson.Str(o, "callsign"), text, MiniJson.Str(o, "subtitle"),
                                            MiniJson.Bool(o, "radio_filter") ?? false);

                var entry = manifest?.Invoke("VO_" + id);
                var env = loadEnvelope?.Invoke(id);
                var chains = ChainsOf(entry, env);
                if (chains.Count == 0)
                {
                    errors?.Add(id + ": no chain (no manifest entry VO_" + id + " and no envelope).");
                    chains.Add(VOChains.Dry);
                }
                var voices = VoicesOf(entry, env);
                var clips = new List<DialogueClip>();
                var longest = 0f;
                foreach (var chain in chains)
                {
                    foreach (var v in voices)
                    {
                        var name = "VO_" + id + "_" + chain + (v.Length > 0 ? "_" + v : "");
                        var path = VORoot + "/" + chain + "/" + name + ".wav";
                        var clip = loadClip?.Invoke(path);
                        EnvelopeClip ec = null;
                        if (env != null) env.Clips.TryGetValue(name, out ec);
                        if (clip == null) errors?.Add(id + ": missing clip " + path);
                        else if (ec == null) warnings?.Add(id + ": no envelope for " + name + " (amplitude reads 0).");
                        var duration = ec != null && ec.Duration > 0f ? ec.Duration : clip != null ? clip.length : 0f;
                        if (duration > longest) longest = duration;
                        var bytes = ec != null ? EnvelopeSampler.Encode(ec.Rms, ec.RmsMax) : new byte[0];
                        clips.Add(new DialogueClip(chain, v, clip, duration, ec != null ? ec.SpeechOffset : 0f, bytes));
                    }
                }

                var estimate = longest > 0f ? longest : DialogueRules.EstimateSeconds(text);
                var switchTime = 0f;
                if (id == DialogueRules.PositionalLine)
                {
                    var t = env != null ? env.FirstOnset(DialogueRules.PositionalToken) : -1f;
                    switchTime = t > 0f ? t : DialogueRules.PositionalFallbackSeconds;
                }
                line.WithRules(DialogueRules.BeatOf(id, MiniJson.Str(o, "context")), DialogueRules.PriorityOf(id),
                               DialogueRules.ChannelOf(id, speaker, chains[0]), DialogueRules.IsObjectiveCritical(id), DialogueRules.IsRepeatable(id))
                    .WithAudio(MiniJson.Str(o, "voice_id"), chains.ToArray(), clips, estimate, switchTime);
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Chains of a line: the manifest variants that are chain names, else the envelope's chains in VOChains order.</summary>
        public static List<string> ChainsOf(ManifestEntry entry, EnvelopeFile env)
        {
            var chains = new List<string>();
            if (entry != null && entry.variants != null)
            {
                foreach (var v in entry.variants)
                    if (VOChains.IsKnown(v) && !chains.Contains(v)) chains.Add(v);
            }
            if (chains.Count == 0 && env != null)
            {
                foreach (var c in VOChains.All)
                {
                    foreach (var ec in env.Clips.Values)
                    {
                        if (ec.Chain != c) continue;
                        chains.Add(c);
                        break;
                    }
                }
            }
            return chains;
        }

        /// <summary>Voice suffixes of a line: A and B for two-voice barks (<c>{A|B}</c> in the target path or A/B envelope clips), else "".</summary>
        public static List<string> VoicesOf(ManifestEntry entry, EnvelopeFile env)
        {
            var two = entry != null && entry.target_path != null && entry.target_path.IndexOf("{A|B}", StringComparison.Ordinal) >= 0;
            if (!two && env != null)
            {
                foreach (var ec in env.Clips.Values)
                {
                    if (ec.Variant != "A" && ec.Variant != "B") continue;
                    two = true;
                    break;
                }
            }
            return two ? new List<string> { "A", "B" } : new List<string> { "" };
        }

        private static ManifestEntry ManifestLookup(string id) => AssetManifest.TryGet(id, out var e) ? e : null;

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
            // Same name in another audio format (.ogg/.mp3/.aif) in the same chain folder.
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets(name + " t:AudioClip", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }
            return null;
        }

        private static EnvelopeFile LoadEnvelope(string root, string id, List<string> errors)
        {
            var env = EnvelopeFile.Load(root, id, out var error);
            if (error != null) errors.Add(id + ": " + error);
            return env;
        }

        private static void FindStrayClips(List<DialogueLine> lines, List<string> warnings)
        {
            if (!AssetDatabase.IsValidFolder(VORoot)) return;
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in lines)
                foreach (var c in l.Clips)
                    if (c.Clip != null) known.Add(AssetDatabase.GetAssetPath(c.Clip));
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { VORoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!known.Contains(p)) warnings.Add("stray VO clip (no DIALOGUE line/chain uses it): " + p);
            }
        }

        private static string Finish(StringBuilder log, List<string> errs, List<string> warns, List<DialogueLine> lines, bool write, out int errors)
        {
            errors = errs.Count;
            if (lines != null)
            {
                var perBeat = new SortedDictionary<int, int>();
                var withAudio = 0;
                foreach (var l in lines)
                {
                    perBeat.TryGetValue(l.Beat, out var n);
                    perBeat[l.Beat] = n + 1;
                    if (l.HasAudio) withAudio++;
                }
                log.AppendLine("  lines: " + lines.Count + " (" + withAudio + " with audio)");
                var sb = new StringBuilder("  per beat:");
                foreach (var kv in perBeat) sb.Append(' ').Append(kv.Key < 0 ? "always" : kv.Key.ToString()).Append('=').Append(kv.Value);
                log.AppendLine(sb.ToString());
                if (write)
                {
                    BuilderUtil.EnsureFolder(Libraries.Folder);
                    var lib = BuilderUtil.LoadOrCreateAsset<DialogueLibrary>(LibraryPath);
                    lib.EditorSetEntries(lines);
                    EditorUtility.SetDirty(lib);
                    AssetDatabase.SaveAssets();
                    log.AppendLine("  written: " + LibraryPath);
                }
            }
            log.AppendLine("  errors: " + errs.Count + ", warnings: " + warns.Count);
            foreach (var e in errs) log.AppendLine("  ERROR " + e);
            foreach (var w in warns) log.AppendLine("  warning " + w);
            var text = log.ToString();
            AudioLibraryBuilder.WriteReport(ReportPath, text);
            if (errs.Count > 0)
            {
                LSLog.Error(LogCategory.VO, "Dialogue " + (write ? "import" : "validation") + ": " + errs.Count + " error(s), first: " + errs[0] +
                                             " (full list: " + ReportPath + ")");
            }
            else LSLog.Info(LogCategory.VO, "Dialogue " + (write ? "import" : "validation") + ": OK, " + (lines?.Count ?? 0) + " lines, " +
                                             warns.Count + " warning(s) (" + ReportPath + ")");
            return text;
        }
    }
}
