using System.Collections.Generic;
using System.Text;
using LongShadow.Core;
using LongShadow.Mission.Data;
using UnityEngine;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// The after-action dossier of GDD §8.9 as text lines, built from <see cref="MissionStats"/> and the collected intel:
    /// the debrief screen (WP08) shows <c>IHud.ShowDebrief(stats)</c>; this model is the reference formatting used by the
    /// Mission fallback (log) and the EditMode tests ("stats feed the debrief model").
    /// </summary>
    public static class DebriefModel
    {
        /// <summary>Header line.</summary>
        public const string Header = "AFTER-ACTION REPORT // OPERATION LONG SHADOW // EYES ONLY";
        /// <summary>The stamp that lands after the redaction bars.</summary>
        public const string Stamp = "PROGRAM LONG SHADOW — ASSETS ACTIVE: 13";

        /// <summary>"14:02" for a mission time in seconds.</summary>
        public static string MissionTime(float seconds)
        {
            var s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        /// <summary>Accuracy as a whole percentage ("38 %").</summary>
        public static string Accuracy(MissionStats stats) => Mathf.RoundToInt(stats.Accuracy * 100f) + " %";

        /// <summary>
        /// The dossier lines (GDD §8.9 layout). <paramref name="hasIntel"/> answers "was this intel id collected"
        /// (null = derive the three main items from <see cref="MissionStats.Intel"/> in order).
        /// </summary>
        public static List<string> Build(MissionStats stats, System.Func<string, bool> hasIntel = null)
        {
            stats = stats ?? new MissionStats();
            bool Has(string id, int order) => hasIntel != null ? hasIntel(id) : stats.Intel >= order;
            var lines = new List<string>(12) { Header };
            lines.Add(Row("MISSION TIME", MissionTime(stats.MissionSeconds), 20) + "  " + Row("KILLS", stats.Kills.ToString(), 14) +
                      "  " + Row("HEADSHOTS", stats.Headshots.ToString(), 14));
            lines.Add(Row("ACCURACY", Accuracy(stats), 20) + "  " + Row("GRENADES", stats.GrenadesThrown.ToString(), 14) + "  " +
                      Row("DEATHS", stats.Deaths.ToString(), 14));
            var sb = new StringBuilder();
            sb.Append(Dots("INTEL", 20)).Append(' ');
            sb.Append("LEDGERS ").Append(Box(Has(IntelTable.Ledgers, 1))).Append("  ");
            sb.Append("REELS ").Append(Box(Has(IntelTable.Reels, 2))).Append("  ");
            sb.Append("CODEBOOK ").Append(Box(Has(IntelTable.Codebook, 3))).Append("   ");
            sb.Append("OPTIONAL ").Append(stats.OptionalIntel).Append('/').Append(IntelTable.OptionalTotal);
            lines.Add(sb.ToString());
            lines.Add(Dots("HOLLISTER, F.", 20) + " KIA (HOSTILE FIRE)");
            lines.Add(Dots("DUQUESNE, W.", 20) + " RTB");
            lines.Add(Dots("CASTELLANOS, M.", 20) + " PENDING");
            lines.Add(Dots("CALDER, N.", 20) + " ASSET STATUS: ACTIVE");
            return lines;
        }

        /// <summary>The struck-through word and its retype on the Hollister line (GDD §8.9).</summary>
        public const string StruckWord = "HOSTILE";
        /// <summary>Replacement typed after the strike-through.</summary>
        public const string Retype = "UNDER REVIEW";

        private static string Box(bool on) => on ? "[X]" : "[ ]";

        private static string Row(string label, string value, int width) => Dots(label, width) + " " + value;

        private static string Dots(string label, int width)
        {
            var sb = new StringBuilder(label).Append(' ');
            while (sb.Length < width) sb.Append('.');
            return sb.ToString();
        }
    }
}
