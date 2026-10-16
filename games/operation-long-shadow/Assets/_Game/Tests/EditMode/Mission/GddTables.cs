using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>Reads Markdown tables and paragraphs out of Docs/GDD.md for the table-driven tests.</summary>
    internal static class GddTables
    {
        private static string s_gdd;
        private static Dictionary<string, string> s_dialogue;

        /// <summary>GDD text.</summary>
        public static string Gdd => s_gdd ?? (s_gdd = TestPaths.Read("Docs", "GDD.md"));

        /// <summary>The text of a section (from its heading line to the next heading of the same or higher level).</summary>
        public static string Section(string headingStart)
        {
            var lines = Gdd.Split('\n');
            var start = -1;
            var level = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i].TrimEnd('\r');
                if (start < 0)
                {
                    if (!l.StartsWith("#")) continue;
                    var hashes = l.Length - l.TrimStart('#').Length;
                    if (l.Substring(hashes).Trim().StartsWith(headingStart, StringComparison.Ordinal))
                    {
                        start = i;
                        level = hashes;
                    }
                    continue;
                }
                if (l.StartsWith("#"))
                {
                    var hashes = l.Length - l.TrimStart('#').Length;
                    if (hashes <= level) return string.Join("\n", lines, start, i - start);
                }
            }
            return start < 0 ? string.Empty : string.Join("\n", lines, start, lines.Length - start);
        }

        /// <summary>Rows (cells trimmed) of the first table in a section, header and separator rows removed.</summary>
        public static List<string[]> Table(string headingStart)
        {
            var rows = new List<string[]>();
            var header = true;
            foreach (var raw in Section(headingStart).Split('\n'))
            {
                var l = raw.Trim();
                if (!l.StartsWith("|"))
                {
                    if (rows.Count > 0 || !header) break;
                    continue;
                }
                if (header)
                {
                    header = false;
                    continue;
                }
                if (Regex.IsMatch(l, @"^\|[\s\-:|]+\|$")) continue;
                var cells = l.Trim('|').Split('|');
                for (var i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                rows.Add(cells);
            }
            return rows;
        }

        /// <summary>Text inside the first pair of backticks, or the trimmed cell.</summary>
        public static string Code(string cell)
        {
            var m = Regex.Match(cell, "`([^`]+)`");
            return m.Success ? m.Groups[1].Value : cell.Trim();
        }

        /// <summary>Every backticked token in a cell.</summary>
        public static List<string> Codes(string cell)
        {
            var r = new List<string>();
            foreach (Match m in Regex.Matches(cell, "`([^`]+)`")) r.Add(m.Groups[1].Value);
            return r;
        }

        /// <summary>DIALOGUE.json line id → text (a minimal reader: every "id" with its "text").</summary>
        public static Dictionary<string, string> Dialogue
        {
            get
            {
                if (s_dialogue != null) return s_dialogue;
                var json = TestPaths.Read("Docs", "DIALOGUE.json");
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match m in Regex.Matches(json, "\"id\"\\s*:\\s*\"([^\"]+)\"[^{}]*?\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                    d[m.Groups[1].Value] = m.Groups[2].Value;
                return s_dialogue = d;
            }
        }
    }
}
