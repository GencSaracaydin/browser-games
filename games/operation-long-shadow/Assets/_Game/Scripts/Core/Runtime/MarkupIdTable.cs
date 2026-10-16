using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LongShadow.Core
{
    /// <summary>
    /// Parser for <c>Assets/_Game/Data/markup_ids.csv</c>, the CSV copy of TECH_PLAN Appendix A that the level builders
    /// (WP10 MarkupTools) and the consumers (WP04, WP07 tests) check against. Pure C#: load the text yourself
    /// (editor: <c>LongShadow.Core.Editor.MarkupIds.Load()</c>). Columns are found by header name:
    /// id, kind, component, beat, x, y, z, extents, heading, points, notes. Points are "label:x y z|x y z|...".
    /// </summary>
    public sealed class MarkupIdTable
    {
        /// <summary>Project path of the CSV.</summary>
        public const string AssetPath = "Assets/_Game/Data/markup_ids.csv";

        private readonly List<MarkupIdRow> _rows = new List<MarkupIdRow>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Rows in file order.</summary>
        public IReadOnlyList<MarkupIdRow> Rows => _rows;
        /// <summary>Parse problems (duplicate ids, bad numbers).</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>True if the id is listed.</summary>
        public bool Contains(string id) => id != null && _index.ContainsKey(id);

        /// <summary>Finds a row.</summary>
        public bool TryGet(string id, out MarkupIdRow row)
        {
            if (id != null && _index.TryGetValue(id, out var i))
            {
                row = _rows[i];
                return true;
            }
            row = default;
            return false;
        }

        /// <summary>Parses CSV text.</summary>
        public static MarkupIdTable Parse(string csv)
        {
            var t = new MarkupIdTable();
            if (string.IsNullOrEmpty(csv)) return t;
            var lines = SplitRecords(csv);
            if (lines.Count == 0) return t;
            var header = lines[0];
            int Col(string name) => header.FindIndex(h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));
            int cId = Col("id"), cKind = Col("kind"), cComp = Col("component"), cBeat = Col("beat"), cX = Col("x"), cY = Col("y"),
                cZ = Col("z"), cExt = Col("extents"), cHead = Col("heading"), cPts = Col("points"), cNotes = Col("notes");
            if (cId < 0)
            {
                t.Problems.Add("missing 'id' column");
                return t;
            }
            string F(List<string> r, int c) => c >= 0 && c < r.Count ? r[c].Trim() : string.Empty;
            for (var li = 1; li < lines.Count; li++)
            {
                var r = lines[li];
                var id = F(r, cId);
                if (string.IsNullOrEmpty(id) || id.StartsWith("#")) continue;
                var row = new MarkupIdRow
                {
                    Id = id,
                    Kind = F(r, cKind),
                    Component = F(r, cComp),
                    Beat = F(r, cBeat),
                    Extents = F(r, cExt),
                    Notes = F(r, cNotes),
                    Points = new List<Vector3>(),
                    PointLabels = new List<string>(),
                };
                string sx = F(r, cX), sy = F(r, cY), sz = F(r, cZ);
                if (sx.Length > 0 && sy.Length > 0 && sz.Length > 0)
                {
                    if (TryFloat(sx, out var x) && TryFloat(sy, out var y) && TryFloat(sz, out var z))
                    {
                        row.HasPosition = true;
                        row.Position = new Vector3(x, y, z);
                    }
                    else t.Problems.Add(id + ": bad position");
                }
                var sh = F(r, cHead);
                if (sh.Length > 0)
                {
                    if (TryFloat(sh, out var h))
                    {
                        row.HasHeading = true;
                        row.Heading = h;
                    }
                    else t.Problems.Add(id + ": bad heading");
                }
                var pts = F(r, cPts);
                if (pts.Length > 0)
                {
                    foreach (var part in pts.Split('|'))
                    {
                        var p = part.Trim();
                        if (p.Length == 0) continue;
                        var label = string.Empty;
                        var colon = p.IndexOf(':');
                        if (colon >= 0)
                        {
                            label = p.Substring(0, colon).Trim();
                            p = p.Substring(colon + 1).Trim();
                        }
                        var n = p.Split(' ');
                        if (n.Length == 3 && TryFloat(n[0], out var px) && TryFloat(n[1], out var py) && TryFloat(n[2], out var pz))
                        {
                            row.Points.Add(new Vector3(px, py, pz));
                            row.PointLabels.Add(label);
                        }
                        else t.Problems.Add(id + ": bad point '" + part + "'");
                    }
                }
                if (t._index.ContainsKey(id))
                {
                    t.Problems.Add("duplicate id " + id);
                    continue;
                }
                t._index[id] = t._rows.Count;
                t._rows.Add(row);
            }
            return t;
        }

        /// <summary>Invariant-culture float parse.</summary>
        public static bool TryFloat(string s, out float v) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static List<List<string>> SplitRecords(string csv)
        {
            var records = new List<List<string>>();
            var field = new StringBuilder();
            var record = new List<string>();
            var inQuotes = false;
            for (var i = 0; i < csv.Length; i++)
            {
                var c = csv[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        record.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }
}
