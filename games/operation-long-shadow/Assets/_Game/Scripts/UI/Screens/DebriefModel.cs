using LongShadow.Core;
using LongShadow.UI.Common;
using UnityEngine;

namespace LongShadow.UI.Screens
{
    /// <summary>
    /// The after-action dossier of GDD §8.9 as text: header, live stat lines (rendered for any count-up fraction so the
    /// numbers roll while they type), intel flags and the four personnel lines, in fixed monospace columns
    /// (Courier Prime). Pure and allocation-free after construction; covered by DebriefModelTests.
    /// </summary>
    public sealed class DebriefModel
    {
        /// <summary>Width of the label + dot leader column ("MISSION TIME .......").</summary>
        public const int LabelWidth = 20;
        /// <summary>Second stat column start.</summary>
        public const int Column2 = 32;
        /// <summary>Third stat column start.</summary>
        public const int Column3 = 54;
        /// <summary>Width of the 2nd/3rd column labels with leaders ("KILLS ........").</summary>
        public const int SmallLabelWidth = 14;

        /// <summary>Number of dossier lines.</summary>
        public const int LineCount = 10;
        /// <summary>Header line.</summary>
        public const int HeaderLine = 0;
        /// <summary>Rule under the header.</summary>
        public const int RuleLine = 1;
        /// <summary>First stats line (time, kills, headshots).</summary>
        public const int StatsLineA = 2;
        /// <summary>Second stats line (accuracy, grenades, deaths).</summary>
        public const int StatsLineB = 3;
        /// <summary>Intel line.</summary>
        public const int IntelLine = 4;
        /// <summary>First personnel line (Hollister).</summary>
        public const int PersonnelFirst = 6;
        /// <summary>Hollister's line, whose "HOSTILE" is struck and retyped.</summary>
        public const int HollisterLine = 6;

        /// <summary>"HOSTILE" position on Hollister's line.</summary>
        public const int StrikeStart = LabelWidth + 1 + 5; // "KIA ("
        /// <summary>"HOSTILE" length.</summary>
        public const int StrikeLength = 7;
        /// <summary>Text typed after the strike (GDD §8.9: "HOSTILE" is struck through and retyped "UNDER REVIEW").</summary>
        public const string Retype = " UNDER REVIEW";

        /// <summary>The header (GDD §8.9).</summary>
        public const string Header = "AFTER-ACTION REPORT // OPERATION LONG SHADOW // EYES ONLY";

        /// <summary>The stamp that lands at the end (GDD §8.9).</summary>
        public const string StampText = "PROGRAM LONG SHADOW — ASSETS ACTIVE: 13";

        private static readonly string[] s_names = { "HOLLISTER, F.", "DUQUESNE, W.", "CASTELLANOS, M.", "CALDER, N." };
        private static readonly string[] s_status = { "KIA (HOSTILE FIRE)", "RTB", "PENDING", "ASSET STATUS: ACTIVE" };

        private readonly MissionStats _stats;
        private readonly bool _ledgers, _reels, _codebook;

        /// <summary>Creates the model for the given stats and main intel flags.</summary>
        public DebriefModel(MissionStats stats, bool ledgers, bool reels, bool codebook)
        {
            _stats = stats != null ? stats.Clone() : new MissionStats();
            _ledgers = ledgers;
            _reels = reels;
            _codebook = codebook;
        }

        /// <summary>
        /// Intel flags from the stats when no objective state is available: main intel is collected in order (ledgers in
        /// Beat 7, reels in Beat 8, codebook in Beat 9).
        /// </summary>
        public static DebriefModel FromStats(MissionStats stats)
        {
            var n = stats != null ? stats.Intel : 0;
            return new DebriefModel(stats, n >= 1, n >= 2, n >= 3);
        }

        /// <summary>The stats shown.</summary>
        public MissionStats Stats => _stats;

        /// <summary>Name length on a personnel line (for the redaction bar), 0 for other lines.</summary>
        public static int NameLength(int line)
        {
            var p = line - PersonnelFirst;
            return p >= 0 && p < s_names.Length ? s_names[p].Length : 0;
        }

        /// <summary>
        /// Writes a dossier line. <paramref name="countUp01"/> rolls the numbers of the stat lines from 0 to their value;
        /// <paramref name="retyped"/> appends <see cref="Retype"/> to Hollister's line.
        /// </summary>
        public void Write(int line, TextBuffer b, float countUp01 = 1f, bool retyped = false)
        {
            b.Clear();
            var f = Mathf.Clamp01(countUp01);
            switch (line)
            {
                case HeaderLine:
                    b.Append(Header);
                    break;
                case RuleLine:
                    b.Append('=', 72);
                    break;
                case StatsLineA:
                    Leader(b, "MISSION TIME", LabelWidth);
                    b.AppendClock(_stats.MissionSeconds * f);
                    PadTo(b, Column2);
                    Leader(b, "KILLS", SmallLabelWidth);
                    b.Append(Roll(_stats.Kills, f));
                    PadTo(b, Column3);
                    Leader(b, "HEADSHOTS", SmallLabelWidth);
                    b.Append(Roll(_stats.Headshots, f));
                    break;
                case StatsLineB:
                    Leader(b, "ACCURACY", LabelWidth);
                    b.Append(Mathf.RoundToInt(_stats.Accuracy * 100f * f)).Append(" %");
                    PadTo(b, Column2);
                    Leader(b, "GRENADES", SmallLabelWidth);
                    b.Append(Roll(_stats.GrenadesThrown, f));
                    PadTo(b, Column3);
                    Leader(b, "DEATHS", SmallLabelWidth);
                    b.Append(Roll(_stats.Deaths, f));
                    break;
                case IntelLine:
                    Leader(b, "INTEL", LabelWidth);
                    b.Append("LEDGERS ").Append(_ledgers ? "[X]" : "[ ]");
                    b.Append("  REELS ").Append(_reels ? "[X]" : "[ ]");
                    b.Append("  CODEBOOK ").Append(_codebook ? "[X]" : "[ ]");
                    b.Append("   OPTIONAL ").Append(Mathf.Clamp(_stats.OptionalIntel, 0, 2)).Append("/2");
                    break;
                default:
                    var p = line - PersonnelFirst;
                    if (p >= 0 && p < s_names.Length)
                    {
                        Leader(b, s_names[p], LabelWidth);
                        b.Append(s_status[p]);
                        if (line == HollisterLine && retyped) b.Append(Retype);
                    }
                    break;
            }
        }

        /// <summary>Length of a personnel line without the retype (0 for other lines).</summary>
        public static int PersonnelBaseLength(int line)
        {
            var p = line - PersonnelFirst;
            return p >= 0 && p < s_status.Length ? LabelWidth + 1 + s_status[p].Length : 0;
        }

        /// <summary>Length of a line before the retype (what is typed first).</summary>
        public int BaseLength(int line, TextBuffer scratch)
        {
            Write(line, scratch, 1f, false);
            return scratch.Length;
        }

        private static int Roll(int value, float f) => Mathf.RoundToInt(Mathf.Max(0, value) * f);

        /// <summary>Appends "LABEL ....." padded with a space and dots to <paramref name="width"/>, then a space.</summary>
        internal static void Leader(TextBuffer b, string label, int width)
        {
            var start = b.Length;
            b.Append(label).Append(' ');
            var dots = width - (b.Length - start);
            if (dots < 1) dots = 1;
            b.Append('.', dots).Append(' ');
        }

        private static void PadTo(TextBuffer b, int column)
        {
            var pad = column - b.Length;
            b.Append(' ', pad < 2 ? 2 : pad);
        }
    }
}
