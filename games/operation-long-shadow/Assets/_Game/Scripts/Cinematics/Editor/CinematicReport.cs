using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core.Editor;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Logs/cinematics_report.txt: what every WP09 builder made, the placeholders it used, the shot lists of the
    /// timelines and the validator's findings. Sections replace earlier sections of the same title.
    /// </summary>
    public static class CinematicReport
    {
        private static readonly Dictionary<string, List<string>> s_sections = new Dictionary<string, List<string>>();
        private static readonly List<string> s_order = new List<string>();

        /// <summary>Sets a section and rewrites the report file.</summary>
        public static void Section(string title, IEnumerable<string> lines)
        {
            if (!s_sections.ContainsKey(title)) s_order.Add(title);
            s_sections[title] = new List<string>(lines);
            Write();
        }

        /// <summary>Report text.</summary>
        public static string Text()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LongShadow WP09 cinematics report — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            foreach (var t in s_order)
            {
                sb.AppendLine();
                sb.AppendLine("== " + t);
                foreach (var l in s_sections[t]) sb.AppendLine("  " + l);
            }
            return sb.ToString();
        }

        private static void Write()
        {
            var path = Path.Combine(BuilderUtil.ProjectRoot, CinematicPaths.Report);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Text());
        }
    }
}
