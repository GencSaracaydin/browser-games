using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Audio.Core;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// Builds <c>LIB_Audio</c> (TECH_PLAN §7.9, D12) from the clips under <c>Assets/_Game/Audio/{SFX,Music,Ambience}</c>:
    /// file names give the cue ids, variants and baked far takes (<see cref="CueNaming"/>), <c>LOOP_POINTS.json</c> gives
    /// the loop bodies and the music grid, <see cref="CueDefaults"/> the mix settings. Missing files simply leave their cue
    /// out (the AudioService skips unknown cues with one log line), so the builder can be re-run whenever content lands;
    /// the report lists manifest coverage (<c>Logs/audio_library_report.txt</c>). Menu
    /// <b>LongShadow/Audio/Build Audio Library</b>; batch: <c>LongShadow.Audio.Editor.AudioLibraryBuilder.Build()</c>.
    /// </summary>
    public static class AudioLibraryBuilder
    {
        /// <summary>The library asset.</summary>
        public const string LibraryPath = Libraries.Folder + "/LIB_Audio.asset";
        /// <summary>Report file (project-relative).</summary>
        public const string ReportPath = "Logs/audio_library_report.txt";

        /// <summary>Folders scanned for clips.</summary>
        public static readonly string[] ScanFolders = { "Assets/_Game/Audio/SFX", "Assets/_Game/Audio/Music", "Assets/_Game/Audio/Ambience" };

        /// <summary>
        /// Manifest ids delivered as several sub-cues that callers ask for as one (WP03 plays <c>SFX_WPN_SHELLS</c>; WP06_SFX
        /// delivered <c>_SAND</c> and <c>_STONE</c> sets): the aggregate cue gets every sub-cue clip as a variant.
        /// </summary>
        public static readonly string[] Aggregates = { "SFX_WPN_SHELLS" };

        /// <summary>Scans, builds and saves LIB_Audio; returns the report.</summary>
        [MenuItem("LongShadow/Audio/Build Audio Library", priority = 110)]
        public static string Build()
        {
            var files = new List<CueFile>();
            var clips = new List<AudioClip>();
            var folders = new List<string>();
            foreach (var f in ScanFolders)
                if (AssetDatabase.IsValidFolder(f)) folders.Add(f);
            if (folders.Count > 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", folders.ToArray()))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!CueNaming.TryParse(path, out var file)) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    files.Add(file);
                    clips.Add(clip);
                }
            }
            var loops = LoopPointsFile.Load(Path.Combine(BuilderUtil.ProjectRoot, LoopPointsFile.DefaultPath));
            var log = new StringBuilder();
            log.AppendLine("[Audio] LIB_Audio build " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.AppendLine("  clips scanned: " + files.Count + " in " + string.Join(", ", folders) + "; loop rows: " + loops.Count);
            var cues = BuildCues(files, clips, loops, log);

            BuilderUtil.EnsureFolder(Libraries.Folder);
            var lib = BuilderUtil.LoadOrCreateAsset<AudioLibrary>(LibraryPath);
            lib.EditorSetEntries(cues);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            log.AppendLine("  cues written: " + cues.Count + " -> " + LibraryPath);
            AppendCoverage(cues, log);

            var text = log.ToString();
            WriteReport(ReportPath, text);
            LSLog.Info(LogCategory.Audio, "LIB_Audio: " + cues.Count + " cues from " + files.Count + " clips (report: " + ReportPath + ")");
            return text;
        }

        /// <summary>
        /// Groups clip files into cues (pure apart from reading clip lengths; tests pass generated clips). <paramref name="files"/>
        /// and <paramref name="clips"/> are parallel lists.
        /// </summary>
        public static List<AudioCue> BuildCues(IList<CueFile> files, IList<AudioClip> clips, LoopPointsFile loops, StringBuilder log)
        {
            loops = loops ?? new LoopPointsFile();
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < files.Count; i++)
            {
                if (clips[i] == null) continue;
                if (!groups.TryGetValue(files[i].CueId, out var list)) groups[files[i].CueId] = list = new List<int>();
                list.Add(i);
            }
            foreach (var agg in Aggregates)
            {
                if (groups.ContainsKey(agg)) continue;
                var union = new List<int>();
                foreach (var kv in groups)
                    if (kv.Key.StartsWith(agg + "_", StringComparison.Ordinal)) union.AddRange(kv.Value);
                if (union.Count > 0)
                {
                    groups[agg] = union;
                    log?.AppendLine("  aggregate " + agg + ": " + union.Count + " clips from its sub-cues");
                }
            }

            var ids = new List<string>(groups.Keys);
            ids.Sort(StringComparer.Ordinal);
            var cues = new List<AudioCue>(ids.Count);
            foreach (var id in ids)
            {
                var idx = groups[id];
                idx.Sort((a, b) =>
                {
                    var c = files[a].Variant.CompareTo(files[b].Variant);
                    return c != 0 ? c : string.CompareOrdinal(files[a].Path, files[b].Path);
                });
                var near = new List<AudioClip>();
                var nearPaths = new List<string>();
                var far = new List<AudioClip>();
                string group = null;
                string farGroup = null;
                foreach (var i in idx)
                {
                    if (files[i].Far)
                    {
                        far.Add(clips[i]);
                        farGroup = farGroup ?? files[i].Group;
                        continue;
                    }
                    near.Add(clips[i]);
                    nearPaths.Add(files[i].Path);
                    group = group ?? files[i].Group;
                }
                group = group ?? farGroup;
                var s = CueDefaults.Resolve(id, group);
                var cue = new AudioCue(id, s.Category, near.ToArray())
                    .With3D(s.SpatialBlend, s.MinDistance, s.MaxDistance, s.Rolloff, s.Spread)
                    .WithLevels(s.Volume, s.VolumeJitterDb, s.PitchJitter, s.Priority)
                    .WithLimits(s.SpeedOfSound, s.MaxInstances, s.MinInterval);
                if (far.Count > 0) cue.WithFar(far.ToArray(), s.FarCrossfade, s.FarMinDistance, s.FarMaxDistance);

                var regions = new Vector2[near.Count];
                var loop = false;
                LoopPoint grid = default;
                for (var k = 0; k < nearPaths.Count; k++)
                {
                    if (!loops.TryGet(nearPaths[k], out var lp) || !lp.Valid) continue;
                    regions[k] = new Vector2(lp.StartSeconds, lp.EndSeconds);
                    loop = true;
                    if (grid.Bar <= 0f && lp.Bar > 0f) grid = lp;
                    if (near[k] != null && lp.EndSeconds > near[k].length + 0.05f)
                        log?.AppendLine("  WARNING " + id + ": loop end " + lp.EndSeconds.ToString("0.000") + " s is past the clip (" +
                                        near[k].length.ToString("0.000") + " s); check LOOP_POINTS.json");
                }
                cue.WithLoop(loop, loop ? regions : new Vector2[0]);
                if (grid.Bar > 0f) cue.WithMusicGrid(grid.Bpm, grid.Bar, grid.LoopBars, grid.HasIntro);
                cues.Add(cue);
                log?.AppendLine("  " + id + ": " + near.Count + " clip(s)" + (far.Count > 0 ? " + " + far.Count + " far" : "") +
                                (loop ? ", loop" : "") + (grid.Bar > 0f ? ", bar " + grid.Bar.ToString("0.###") + " s" : "") +
                                " [" + s.Category + ", " + (s.SpatialBlend > 0f ? "3D " + s.MinDistance + "-" + s.MaxDistance + " m" : "2D") + "]");
            }
            return cues;
        }

        /// <summary>Lists manifest sfx/music ids with no cue (exact id, a sliced sub-cue or an aggregate).</summary>
        public static void AppendCoverage(IList<AudioCue> cues, StringBuilder log)
        {
            var have = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in cues)
                if (c != null) have.Add(c.Id);
            var missing = new List<string>();
            var covered = 0;
            foreach (var e in AssetManifest.Entries)
            {
                if (e == null || (e.category != "sfx" && e.category != "music")) continue;
                if (e.target_path != null && e.target_path.StartsWith("RawAssets", StringComparison.Ordinal)) continue;
                // EXTRA_* manifest ids drop the prefix in their file names (WP06_SFX convention).
                var id = e.id.StartsWith("EXTRA_", StringComparison.Ordinal) ? e.id.Substring(6) : e.id;
                if (have.Contains(id) || HasSlice(have, id))
                {
                    covered++;
                    continue;
                }
                missing.Add(e.id + " (" + e.priority + ")");
            }
            log.AppendLine("  manifest sfx/music coverage: " + covered + " covered, " + missing.Count + " missing" +
                           (missing.Count > 0 ? ": " + string.Join(", ", missing) : ""));
        }

        private static bool HasSlice(HashSet<string> have, string id)
        {
            var prefix = id + "_";
            foreach (var h in have)
                if (h.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        internal static void WriteReport(string relativePath, string text)
        {
            try
            {
                var full = Path.Combine(BuilderUtil.ProjectRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(full) ?? BuilderUtil.ProjectRoot);
                File.WriteAllText(full, text);
            }
            catch (Exception e)
            {
                LSLog.Warn(LogCategory.Audio, "Could not write " + relativePath + ": " + e.Message);
            }
        }
    }
}
