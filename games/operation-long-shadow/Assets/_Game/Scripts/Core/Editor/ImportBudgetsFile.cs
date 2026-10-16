using System;
using System.Collections.Generic;
using System.IO;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Reads <c>Tools/build/asset_budgets.json</c>, the budget data shared by the import rules and
    /// <c>Tools/build/validate_assets.py</c> (TECH_PLAN §3.10-3.12, §8.9, §8.10). Every accessor takes the built-in default
    /// that applies when the file, the section or the key is missing, so the rules keep working without the file.
    /// Cached per editor/import-worker process; <see cref="Reload"/> after editing the file.
    /// </summary>
    public static class ImportBudgetsFile
    {
        /// <summary>Path relative to the project root.</summary>
        public const string RelativePath = "Tools/build/asset_budgets.json";

        private static Dictionary<string, object> s_root;
        private static bool s_loaded;
        private static readonly Dictionary<string, string[]> s_lists = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Dictionary<string, int>> s_maps = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        /// <summary>Forgets the cached file.</summary>
        public static void Reload()
        {
            s_root = null;
            s_loaded = false;
            s_lists.Clear();
            s_maps.Clear();
        }

        /// <summary>True when the file was found and parsed.</summary>
        public static bool Present
        {
            get
            {
                Ensure();
                return s_root != null;
            }
        }

        /// <summary>A string list (<paramref name="section"/>.<paramref name="key"/>) or <paramref name="defaults"/>. Cached; do not modify the result.</summary>
        public static string[] List(string section, string key, string[] defaults)
        {
            var cacheKey = section + "." + key;
            if (s_lists.TryGetValue(cacheKey, out var cached)) return cached ?? defaults;
            var arr = MiniJson.Arr(Section(section), key);
            string[] result = null;
            if (arr != null)
            {
                var list = new List<string>(arr.Count);
                foreach (var v in arr)
                    if (v is string s && s.Length > 0) list.Add(s);
                result = list.ToArray();
            }
            s_lists[cacheKey] = result;
            return result ?? defaults;
        }

        /// <summary>An integer (<paramref name="section"/>.<paramref name="key"/>) or <paramref name="fallback"/>.</summary>
        public static int Int(string section, string key, int fallback)
        {
            var n = MiniJson.Num(Section(section), key);
            return n.HasValue ? (int)Math.Round(n.Value) : fallback;
        }

        /// <summary>A map of integers keyed case-insensitively (empty when missing; keys starting with "_" are skipped). Cached.</summary>
        public static IReadOnlyDictionary<string, int> IntMap(string section, string key)
        {
            var cacheKey = section + "." + key;
            if (s_maps.TryGetValue(cacheKey, out var cached)) return cached;
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var o = MiniJson.Obj(Section(section), key);
            if (o != null)
                foreach (var kv in o)
                    if (kv.Value is double d && !kv.Key.StartsWith("_", StringComparison.Ordinal)) result[kv.Key] = (int)Math.Round(d);
            s_maps[cacheKey] = result;
            return result;
        }

        private static Dictionary<string, object> Section(string name)
        {
            Ensure();
            return s_root == null ? null : MiniJson.Obj(s_root, name);
        }

        private static void Ensure()
        {
            if (s_loaded) return;
            s_loaded = true;
            var full = Path.Combine(BuilderUtil.ProjectRoot, RelativePath);
            if (!File.Exists(full))
            {
                LSLog.WarnOnce("budgets-file", LogCategory.Assets, "Import rules: " + RelativePath + " not found; using the built-in budget lists.");
                return;
            }
            try
            {
                s_root = MiniJson.Parse(File.ReadAllText(full)) as Dictionary<string, object>;
            }
            catch (Exception e)
            {
                LSLog.ErrorOnce("budgets-file-bad", LogCategory.Assets, "Import rules: cannot parse " + RelativePath + " (" + e.Message + "); using the built-in budget lists.");
                s_root = null;
            }
        }
    }
}
