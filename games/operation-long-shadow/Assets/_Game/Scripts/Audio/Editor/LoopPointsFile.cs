using System;
using System.Collections.Generic;
using System.IO;
using LongShadow.Core.Editor;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// Reader of <c>Assets/_Game/Audio/Music/LOOP_POINTS.json</c> (WP06_SFX): <c>{ "&lt;asset path&gt;": { clip, sr,
    /// loop_start_sample, loop_end_sample, loop_seconds, bpm_grid?, bar_seconds?, loop_bars?, has_intro? } }</c>. Loop
    /// clips are laid out [lead-in][loop body][tail-out] and are played over the body only (TECH_PLAN §3.12).
    /// </summary>
    public sealed class LoopPointsFile
    {
        /// <summary>Default location.</summary>
        public const string DefaultPath = "Assets/_Game/Audio/Music/LOOP_POINTS.json";

        private readonly Dictionary<string, LoopPoint> _byPath = new Dictionary<string, LoopPoint>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoopPoint> _byName = new Dictionary<string, LoopPoint>(StringComparer.Ordinal);

        /// <summary>Rows read.</summary>
        public int Count => _byPath.Count;

        /// <summary>Parses the JSON text (throws <see cref="FormatException"/> on bad JSON).</summary>
        public static LoopPointsFile Parse(string json)
        {
            var f = new LoopPointsFile();
            if (string.IsNullOrEmpty(json)) return f;
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return f;
            foreach (var kv in root)
            {
                if (!(kv.Value is Dictionary<string, object> o)) continue;
                var lp = new LoopPoint
                {
                    SampleRate = (int)(MiniJson.Num(o, "sr") ?? 48000d),
                    LoopStartSample = (long)(MiniJson.Num(o, "loop_start_sample") ?? 0d),
                    LoopEndSample = (long)(MiniJson.Num(o, "loop_end_sample") ?? 0d),
                    Bpm = (float)(MiniJson.Num(o, "bpm_grid") ?? 0d),
                    BarSeconds = (float)(MiniJson.Num(o, "bar_seconds") ?? 0d),
                    LoopBars = (int)(MiniJson.Num(o, "loop_bars") ?? 0d),
                    HasIntro = MiniJson.Bool(o, "has_intro") ?? false,
                };
                var key = kv.Key.Replace('\\', '/');
                f._byPath[key] = lp;
                var clip = MiniJson.Str(o, "clip");
                if (string.IsNullOrEmpty(clip)) clip = Path.GetFileNameWithoutExtension(key);
                if (!string.IsNullOrEmpty(clip)) f._byName[clip] = lp;
            }
            return f;
        }

        /// <summary>Loads a file from disk (empty when missing).</summary>
        public static LoopPointsFile Load(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath)) return new LoopPointsFile();
            return Parse(File.ReadAllText(absolutePath));
        }

        /// <summary>The row of a clip, by asset path first, then by file name (clips moved to another folder).</summary>
        public bool TryGet(string assetPath, out LoopPoint point)
        {
            if (assetPath != null && _byPath.TryGetValue(assetPath.Replace('\\', '/'), out point)) return true;
            var name = assetPath != null ? Path.GetFileNameWithoutExtension(assetPath) : null;
            if (name != null && _byName.TryGetValue(name, out point)) return true;
            point = default;
            return false;
        }
    }
}
