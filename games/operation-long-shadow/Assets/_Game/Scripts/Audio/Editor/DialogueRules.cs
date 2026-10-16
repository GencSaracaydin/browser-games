using System;
using System.Text.RegularExpressions;
using LongShadow.Audio.Core;
using LongShadow.Audio.VO;
using LongShadow.Core;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// How <c>DialogueImporter</c> classifies a DIALOGUE.json line (TECH_PLAN §7.10, GDD §17, §5.5, §7.0): its beat window
    /// (from the <c>context</c> field), priority, channel, the checkpoint rules and the G9 radio switch. Pure (unit-tested).
    /// </summary>
    public static class DialogueRules
    {
        /// <summary>GDD §17: repeat after a checkpoint reload whenever their objective is still open.</summary>
        public static readonly string[] ObjectiveCritical = { "D014", "D029", "D034", "D048", "D053", "D059", "D065", "D067" };

        /// <summary>The out-of-bounds warning (any beat, any number of times).</summary>
        public const string OutOfBoundsLine = "D093";

        /// <summary>G9: the line that starts at Tinker's pack and jumps onto the net, and the token it jumps on.</summary>
        public const string PositionalLine = "D039";
        /// <summary>The token of <see cref="PositionalLine"/> where the 2D radio takes over (the first "Seven").</summary>
        public const string PositionalToken = "seven";
        /// <summary>Fallback switch time when the envelope has no token onsets (WP06_VO handoff: 1.83 s).</summary>
        public const float PositionalFallbackSeconds = 1.83f;

        private static readonly Regex s_beat = new Regex(@"\bBeat\s+(\d{1,2})\b", RegexOptions.CultureInvariant);

        /// <summary>True for systemic squad callouts (<c>CO_*</c>).</summary>
        public static bool IsCallout(string id) => id != null && id.StartsWith("CO_", StringComparison.Ordinal);
        /// <summary>True for enemy barks (<c>BK_*</c>).</summary>
        public static bool IsBark(string id) => id != null && id.StartsWith("BK_", StringComparison.Ordinal);
        /// <summary>True for the death-insert lines (<c>DI_*</c>).</summary>
        public static bool IsDeathInsert(string id) => id != null && id.StartsWith("DI_", StringComparison.Ordinal);

        /// <summary>GDD §17 objective-critical line.</summary>
        public static bool IsObjectiveCritical(string id) => Array.IndexOf(ObjectiveCritical, id) >= 0;

        /// <summary>Systemic lines that play any number of times (callouts, barks, death inserts, the OOB warning).</summary>
        public static bool IsRepeatable(string id) => IsCallout(id) || IsBark(id) || IsDeathInsert(id) || id == OutOfBoundsLine;

        /// <summary>
        /// Beat window of a line: 0 intro and insertion, 1–11, 12 outro, <see cref="AudioConstants.AlwaysBeat"/> for
        /// lines that can play at any time (callouts, barks, death inserts, the OOB warning). Inserts I1–I4 and MID-A/B
        /// map to the beat they happen in (GDD §5.5, §7.0).
        /// </summary>
        public static int BeatOf(string id, string context)
        {
            if (IsCallout(id) || IsBark(id) || IsDeathInsert(id) || id == OutOfBoundsLine) return AudioConstants.AlwaysBeat;
            var c = context ?? "";
            if (c.StartsWith("Intro", StringComparison.Ordinal) || c.StartsWith("Insertion", StringComparison.Ordinal)) return AudioConstants.IntroBeat;
            if (c.StartsWith("Outro", StringComparison.Ordinal)) return AudioConstants.OutroBeat;
            if (c.IndexOf("Glitch insert I1", StringComparison.Ordinal) >= 0) return 2;
            if (c.IndexOf("Glitch insert I2", StringComparison.Ordinal) >= 0) return 5;
            if (c.IndexOf("Glitch insert I3", StringComparison.Ordinal) >= 0) return 8;
            if (c.IndexOf("Glitch insert I4", StringComparison.Ordinal) >= 0) return 11;
            if (c.IndexOf("MID-A", StringComparison.Ordinal) >= 0) return 5;
            if (c.IndexOf("MID-B", StringComparison.Ordinal) >= 0) return 8;
            if (c.IndexOf("Exfil climax", StringComparison.Ordinal) >= 0) return 11;
            var m = s_beat.Match(c);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var beat) && beat >= 1 && beat <= 11) return beat;
            return AudioConstants.AlwaysBeat;
        }

        /// <summary>Default priority (Scripted &gt; Objective &gt; Callout &gt; Bark, TECH_PLAN §7.10).</summary>
        public static VOPriority PriorityOf(string id)
        {
            if (IsBark(id)) return VOPriority.Bark;
            if (IsCallout(id)) return VOPriority.Callout;
            if (IsObjectiveCritical(id) || id == OutOfBoundsLine) return VOPriority.Objective;
            return VOPriority.Scripted;
        }

        /// <summary>Playback channel from the id, the speaker and the primary chain.</summary>
        public static VOChannel ChannelOf(string id, string speaker, string primaryChain)
        {
            if (IsBark(id)) return VOChannel.Bark;
            return VOChains.ChannelOf(speaker, primaryChain);
        }

        /// <summary>
        /// Length estimate for a line whose audio has not landed (so silent placeholder lines keep the mission's timing):
        /// 2.6 words per second plus 0.4 s, at least 0.8 s.
        /// </summary>
        public static float EstimateSeconds(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0.8f;
            var words = 0;
            var inWord = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ws = char.IsWhiteSpace(text[i]) || text[i] == '-';
                if (!ws && !inWord) words++;
                inWord = !ws;
            }
            var s = words / 2.6f + 0.4f;
            return s < 0.8f ? 0.8f : s;
        }
    }
}
