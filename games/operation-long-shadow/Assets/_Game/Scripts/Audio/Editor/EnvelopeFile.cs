using System;
using System.Collections.Generic;
using System.IO;
using LongShadow.Core.Editor;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// Reader of the WP06_VO envelope files <c>Assets/_Game/Audio/Envelopes/VO_&lt;id&gt;.json</c> (TECH_PLAN D10):
    /// <c>{ id, rate_hz: 30, kind: "rms_linear", clips: { &lt;clip&gt;: { chain, variant, duration_s, speech_offset_s,
    /// rms_max, rms[] } }, token_onsets_s?: [ { token, t } ] }</c>.
    /// </summary>
    public sealed class EnvelopeFile
    {
        /// <summary>Folder of the envelope files.</summary>
        public const string Folder = "Assets/_Game/Audio/Envelopes";

        /// <summary>Line id.</summary>
        public string Id;
        /// <summary>Envelope rate.</summary>
        public float RateHz = 30f;
        /// <summary>Clips by name.</summary>
        public readonly Dictionary<string, EnvelopeClip> Clips = new Dictionary<string, EnvelopeClip>(StringComparer.Ordinal);
        /// <summary>Clockmaker token onsets (token, seconds), in order.</summary>
        public readonly List<KeyValuePair<string, float>> TokenOnsets = new List<KeyValuePair<string, float>>();

        /// <summary>Project path of a line's envelope.</summary>
        public static string PathOf(string lineId) => Folder + "/VO_" + lineId + ".json";

        /// <summary>Parses JSON text (throws <see cref="FormatException"/> on bad JSON).</summary>
        public static EnvelopeFile Parse(string json)
        {
            var f = new EnvelopeFile();
            if (string.IsNullOrEmpty(json) || !(MiniJson.Parse(json) is Dictionary<string, object> root)) return f;
            f.Id = MiniJson.Str(root, "id");
            f.RateHz = (float)(MiniJson.Num(root, "rate_hz") ?? 30d);
            var clips = MiniJson.Obj(root, "clips");
            if (clips != null)
            {
                foreach (var kv in clips)
                {
                    if (!(kv.Value is Dictionary<string, object> o)) continue;
                    var c = new EnvelopeClip
                    {
                        Name = kv.Key,
                        Chain = MiniJson.Str(o, "chain"),
                        Variant = MiniJson.Str(o, "variant") ?? "",
                        Duration = (float)(MiniJson.Num(o, "duration_s") ?? 0d),
                        SpeechOffset = (float)(MiniJson.Num(o, "speech_offset_s") ?? 0d),
                        RmsMax = (float)(MiniJson.Num(o, "rms_max") ?? 0d),
                    };
                    var rms = MiniJson.Arr(o, "rms");
                    if (rms != null)
                    {
                        c.Rms = new float[rms.Count];
                        for (var i = 0; i < rms.Count; i++) c.Rms[i] = rms[i] is double d ? (float)d : 0f;
                    }
                    f.Clips[kv.Key] = c;
                }
            }
            var onsets = MiniJson.Arr(root, "token_onsets_s");
            if (onsets != null)
            {
                foreach (var item in onsets)
                {
                    if (!(item is Dictionary<string, object> o)) continue;
                    var token = MiniJson.Str(o, "token");
                    var t = MiniJson.Num(o, "t");
                    if (token != null && t.HasValue) f.TokenOnsets.Add(new KeyValuePair<string, float>(token, (float)t.Value));
                }
            }
            return f;
        }

        /// <summary>Loads a line's envelope from disk (null when missing or unreadable; the error is in <paramref name="error"/>).</summary>
        public static EnvelopeFile Load(string projectRoot, string lineId, out string error)
        {
            error = null;
            var full = Path.Combine(projectRoot, PathOf(lineId));
            if (!File.Exists(full)) return null;
            try
            {
                return Parse(File.ReadAllText(full));
            }
            catch (Exception e)
            {
                error = PathOf(lineId) + ": " + e.Message;
                return null;
            }
        }

        /// <summary>Seconds of the first onset of <paramref name="token"/> (case-insensitive), or -1.</summary>
        public float FirstOnset(string token)
        {
            for (var i = 0; i < TokenOnsets.Count; i++)
                if (string.Equals(TokenOnsets[i].Key, token, StringComparison.OrdinalIgnoreCase)) return TokenOnsets[i].Value;
            return -1f;
        }
    }
}
