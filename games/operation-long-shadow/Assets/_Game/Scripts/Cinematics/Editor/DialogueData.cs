using System.Collections.Generic;
using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEngine;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Line data for the timeline builders: speaker, callsign and subtitle text from Docs/DIALOGUE.json (the subtitle
    /// fallback of VOLineClip), and clip durations and syllable onsets from the WP06 envelopes (30 Hz RMS, TECH_PLAN
    /// D10; Assets/_Game/Audio/Envelopes, else the ready tree). Durations measured on 2026-10-16 are baked in as a
    /// fallback so the shot tables keep their timing before the VO lands.
    /// </summary>
    public sealed class DialogueData
    {
        /// <summary>One line.</summary>
        public sealed class Line
        {
            public string Id;
            public string Speaker;
            public string Callsign;
            public string Text;
            public float Duration;
        }

        // Measured from RawAssets/audio/ready/.../Envelopes (longest chain) on 2026-10-16.
        private static readonly Dictionary<string, float> s_measured = new Dictionary<string, float>
        {
            { "D001", 7.03f }, { "D002", 8.43f }, { "D003", 7.42f }, { "D004", 5.34f }, { "D005", 2.10f }, { "D006", 4.14f },
            { "D007", 4.57f }, { "D017", 4.21f }, { "D042", 3.52f }, { "D054", 2.50f }, { "D055", 3.37f }, { "D080", 6.39f },
            { "D081", 2.29f }, { "D082", 2.41f }, { "D083", 2.52f }, { "D084", 4.53f }, { "D085", 2.65f }, { "D086", 6.32f },
            { "D087", 3.96f }, { "D088", 3.12f }, { "D089", 3.78f }, { "D090", 2.37f }, { "D091", 3.65f }, { "D092", 2.73f },
            { "DI_01", 3.79f }, { "DI_02", 4.05f }, { "DI_03", 3.59f }, { "DI_04", 2.00f }, { "DI_05", 4.11f }, { "DI_06", 3.71f },
            { "D200", 4.48f }, { "D201", 1.36f }, { "D202", 2.60f }, { "D203", 2.23f }, { "D204", 1.49f }, { "D205", 3.08f },
            { "D206", 5.93f }, { "D207", 1.07f },
        };

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>();
        private readonly string _root;

        /// <summary>Loads DIALOGUE.json (logs and stays empty when missing).</summary>
        public DialogueData()
        {
            _root = BuilderUtil.ProjectRoot;
            var path = Path.Combine(_root, CinematicPaths.Dialogue);
            if (!File.Exists(path))
            {
                LSLog.Warn(LogCategory.Cinematics, "DIALOGUE.json not found at " + path + "; subtitles in timelines stay empty.");
                return;
            }
            if (!(MiniJson.Parse(File.ReadAllText(path)) is List<object> rows)) return;
            foreach (var r in rows)
            {
                if (!(r is Dictionary<string, object> o)) continue;
                var id = MiniJson.Str(o, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var subtitle = MiniJson.Str(o, "subtitle");
                _lines[id] = new Line
                {
                    Id = id,
                    Speaker = MiniJson.Str(o, "speaker"),
                    Callsign = MiniJson.Str(o, "callsign"),
                    Text = string.IsNullOrEmpty(subtitle) ? MiniJson.Str(o, "text") : subtitle,
                };
            }
        }

        /// <summary>Number of lines read.</summary>
        public int Count => _lines.Count;

        /// <summary>True if the id exists in DIALOGUE.json.</summary>
        public bool Has(string id) => _lines.ContainsKey(id);

        /// <summary>A line (with its duration resolved), or a stub for unknown ids.</summary>
        public Line Get(string id)
        {
            if (!_lines.TryGetValue(id, out var l))
            {
                l = new Line { Id = id, Speaker = "", Text = "" };
                _lines[id] = l;
            }
            if (l.Duration <= 0f) l.Duration = DurationOf(id);
            return l;
        }

        /// <summary>Clip length in seconds: envelope, measured table, or a words-per-second estimate.</summary>
        public float DurationOf(string id)
        {
            var env = ReadEnvelope(id, out _);
            if (env > 0f) return env;
            if (s_measured.TryGetValue(id, out var d)) return d;
            var words = _lines.TryGetValue(id, out var l) && l.Text != null ? l.Text.Split(' ').Length : 6;
            return Mathf.Max(1f, words / 2.6f + 0.4f);
        }

        /// <summary>
        /// Up to <paramref name="count"/> syllable onsets (seconds) of a line from its envelope's RMS rises, for pen taps on
        /// digits (outro shot 5) and the silent lips; null when no envelope exists.
        /// </summary>
        public float[] Onsets(string id, int count)
        {
            ReadEnvelope(id, out var rms);
            if (rms == null || rms.Count < 8) return null;
            var peak = 0f;
            foreach (var v in rms) peak = Mathf.Max(peak, v);
            if (peak <= 0f) return null;
            var result = new List<float>();
            var armed = true;
            for (var i = 1; i < rms.Count && result.Count < count; i++)
            {
                var a = rms[i] / peak;
                if (armed && a > 0.45f && rms[i] > rms[i - 1])
                {
                    result.Add(i / 30f);
                    armed = false;
                }
                else if (!armed && a < 0.18f) armed = true;
            }
            return result.Count == count ? result.ToArray() : null;
        }

        private float ReadEnvelope(string id, out List<float> rms)
        {
            rms = null;
            foreach (var folder in new[] { CinematicPaths.Envelopes, CinematicPaths.EnvelopesReady })
            {
                var path = Path.Combine(_root, folder, "VO_" + id + ".json");
                if (!File.Exists(path)) continue;
                if (!(MiniJson.Parse(File.ReadAllText(path)) is Dictionary<string, object> o)) continue;
                var clips = MiniJson.Obj(o, "clips");
                if (clips == null) continue;
                var best = 0f;
                foreach (var kv in clips)
                {
                    if (!(kv.Value is Dictionary<string, object> c)) continue;
                    var d = (float)(MiniJson.Num(c, "duration_s") ?? 0d);
                    if (d <= best) continue;
                    best = d;
                    var arr = MiniJson.Arr(c, "rms");
                    if (arr == null) continue;
                    rms = new List<float>(arr.Count);
                    foreach (var v in arr) rms.Add(v is double dv ? (float)dv : 0f);
                }
                if (best > 0f) return best;
            }
            return 0f;
        }
    }
}
