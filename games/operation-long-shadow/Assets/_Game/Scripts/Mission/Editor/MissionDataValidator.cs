using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LongShadow.Core;
using LongShadow.Mission.Data;

namespace LongShadow.Mission.Editor
{
    /// <summary>
    /// Checks the mission's data tables against the docs (the same rules as the EditMode tests, for a quick menu check):
    /// every line id in DIALOGUE.json, every scene id in markup_ids.csv (Appendix A), the Appendix A fallbacks (marker
    /// positions, trigger shapes) equal to the CSV, every GDD §7.12 objective id and text in ObjectiveTable. Pure file
    /// reads (no AssetDatabase), so it also runs from the command line.
    /// </summary>
    public static class MissionDataValidator
    {
        /// <summary>Runs every check; returns the problems (empty = valid).</summary>
        public static List<string> Validate(string projectRoot)
        {
            var problems = new List<string>();
            CheckLines(projectRoot, problems);
            CheckMarkup(projectRoot, problems);
            CheckObjectives(projectRoot, problems);
            return problems;
        }

        private static void CheckLines(string root, List<string> problems)
        {
            var path = Path.Combine(root, "Docs", "DIALOGUE.json");
            if (!File.Exists(path))
            {
                problems.Add("DIALOGUE.json not found");
                return;
            }
            var text = File.ReadAllText(path);
            var ids = new HashSet<string>();
            foreach (Match m in Regex.Matches(text, "\"id\"\\s*:\\s*\"([^\"]+)\"")) ids.Add(m.Groups[1].Value);
            foreach (var id in LineIds.All)
                if (!ids.Contains(id)) problems.Add("line " + id + " is not in DIALOGUE.json");
            foreach (var id in ids)
                if (!LineDurations.Contains(id)) problems.Add("LineDurations has no " + id + " (run Tools/mission/gen_line_durations.py)");
        }

        private static void CheckMarkup(string root, List<string> problems)
        {
            var path = Path.Combine(root, MarkupIdTable.AssetPath);
            if (!File.Exists(path))
            {
                problems.Add("markup_ids.csv not found");
                return;
            }
            var csv = MarkupIdTable.Parse(File.ReadAllText(path));
            foreach (var id in MissionMarkup.AllIds())
                if (!csv.Contains(id)) problems.Add("scene id " + id + " is not in markup_ids.csv");
            foreach (var id in MissionMarkers.FallbackIds)
            {
                if (!csv.TryGet(id, out var row) || !row.HasPosition) continue;
                MissionMarkers.TryGetFallback(id, out var p);
                if ((p - row.Position).sqrMagnitude > 0.0025f) problems.Add("fallback of " + id + " " + p + " differs from the CSV " + row.Position);
            }
            foreach (var id in MissionTriggers.Ids)
            {
                if (!csv.TryGet(id, out var row)) continue;
                MissionTriggers.TryGetShape(id, out var s);
                if (s.Kind == TriggerShapeKind.Sphere && (!row.TryGetRadius(out var r) || UnityEngine.Mathf.Abs(r - s.Radius) > 0.01f))
                    problems.Add("trigger " + id + " radius differs from the CSV");
                if (s.Kind == TriggerShapeKind.Box && (!row.TryGetBox(out var size) || (size - s.Size).sqrMagnitude > 0.0025f))
                    problems.Add("trigger " + id + " box differs from the CSV");
            }
        }

        private static void CheckObjectives(string root, List<string> problems)
        {
            var path = Path.Combine(root, "Docs", "GDD.md");
            if (!File.Exists(path))
            {
                problems.Add("GDD.md not found");
                return;
            }
            var gdd = File.ReadAllText(path);
            var start = gdd.IndexOf("### 7.12", System.StringComparison.Ordinal);
            var end = gdd.IndexOf("### 7.13", System.StringComparison.Ordinal);
            if (start < 0 || end < 0)
            {
                problems.Add("GDD §7.12 not found");
                return;
            }
            var seen = new HashSet<string>();
            foreach (var line in gdd.Substring(start, end - start).Split('\n'))
            {
                if (!line.StartsWith("| `")) continue;
                var cells = line.Trim().Trim('|').Split('|');
                var id = Regex.Match(cells[0], "`([^`]+)`").Groups[1].Value;
                seen.Add(id);
                var spec = ObjectiveTable.Get(id);
                if (spec == null)
                {
                    problems.Add("objective " + id + " (GDD §7.12) is missing from ObjectiveTable");
                    continue;
                }
                if (spec.Text != cells[1].Trim()) problems.Add("objective " + id + " text '" + spec.Text + "' != GDD '" + cells[1].Trim() + "'");
                if (spec.CompletionRule != cells[3].Trim()) problems.Add("objective " + id + " rule differs from the GDD");
            }
            foreach (var spec in ObjectiveTable.All)
                if (!seen.Contains(spec.Id)) problems.Add("objective " + spec.Id + " is not in GDD §7.12");
        }

        /// <summary>A text report of the problems.</summary>
        public static string Report(List<string> problems)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Mission data validation: " + (problems.Count == 0 ? "OK" : problems.Count + " problem(s)"));
            foreach (var p in problems) sb.AppendLine("  - " + p);
            return sb.ToString();
        }
    }
}
