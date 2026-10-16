using System.Collections.Generic;
using System.Text;

namespace LongShadow.Mission.Runtime
{
    /// <summary>
    /// Compact id-list encoding for the mission's saveables ("a|b|c"). Ids (objective, gate, flag, intel, line ids) never
    /// contain '|'. Unlike <c>SaveWriter.WriteStrings</c> it needs no JsonUtility, so the snapshot round-trip of the
    /// mission state also runs in the offline test harness.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>Separator between ids.</summary>
        public const char Separator = '|';

        private static readonly StringBuilder s_sb = new StringBuilder(256);

        /// <summary>Joins ids (null or empty ids are skipped).</summary>
        public static string Join(IEnumerable<string> ids)
        {
            s_sb.Clear();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (s_sb.Length > 0) s_sb.Append(Separator);
                    s_sb.Append(id);
                }
            }
            return s_sb.ToString();
        }

        /// <summary>Splits into <paramref name="results"/> (cleared first). Returns the count.</summary>
        public static int Split(string text, List<string> results)
        {
            results.Clear();
            if (string.IsNullOrEmpty(text)) return 0;
            var start = 0;
            for (var i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != Separator) continue;
                if (i > start) results.Add(text.Substring(start, i - start));
                start = i + 1;
            }
            return results.Count;
        }
    }
}
