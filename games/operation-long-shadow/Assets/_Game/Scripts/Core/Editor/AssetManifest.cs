using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Editor lookup of assets by manifest id (Docs/ASSET_MANIFEST.json target_path + TECH_PLAN §4.2 naming). Assets are
    /// produced in parallel and land later: every lookup returns null and logs a missing-asset warning once when the file
    /// is not there yet, so builders can fall back to placeholders and be re-run when the asset lands.
    /// <code>var mesh = AssetManifest.Find&lt;GameObject&gt;("WPN_COMMANDO", "SM_WPN_Commando_FP");</code>
    /// </summary>
    public static class AssetManifest
    {
        /// <summary>Manifest path relative to the project root.</summary>
        public const string RelativePath = "Docs/ASSET_MANIFEST.json";

        [Serializable]
        private class Wrapper
        {
            public List<ManifestEntry> items = new List<ManifestEntry>();
        }

        private static List<ManifestEntry> s_entries;
        private static Dictionary<string, ManifestEntry> s_byId;

        /// <summary>Every entry (loaded on first use).</summary>
        public static IReadOnlyList<ManifestEntry> Entries
        {
            get
            {
                Ensure();
                return s_entries;
            }
        }

        /// <summary>Forgets the cache (after the manifest changes).</summary>
        public static void Reload()
        {
            s_entries = null;
            s_byId = null;
        }

        /// <summary>Finds an entry.</summary>
        public static bool TryGet(string id, out ManifestEntry entry)
        {
            Ensure();
            return s_byId.TryGetValue(id ?? string.Empty, out entry);
        }

        /// <summary>Target path with {CHAIN} replaced (VO), or null if the id is unknown.</summary>
        public static string ResolvePath(string id, string chain = null)
        {
            if (!TryGet(id, out var e) || string.IsNullOrEmpty(e.target_path)) return null;
            var p = e.target_path;
            if (chain != null) p = p.Replace("{CHAIN}", chain);
            return p;
        }

        /// <summary>
        /// Loads an asset for a manifest id. For folder targets the first asset of type T whose file name contains
        /// <paramref name="nameContains"/> (or any, if null) is returned. Logs once and returns null when missing.
        /// </summary>
        public static T Find<T>(string id, string nameContains = null, string chain = null) where T : UnityEngine.Object
        {
            var all = FindAll<T>(id, nameContains, chain);
            if (all.Count > 0) return all[0];
            LSLog.MissingAsset(LogCategory.Assets, id + (nameContains != null ? "/" + nameContains : ""), ResolvePath(id, chain));
            return null;
        }

        /// <summary>All assets of type T for an id (empty list when missing; no logging).</summary>
        public static List<T> FindAll<T>(string id, string nameContains = null, string chain = null) where T : UnityEngine.Object
        {
            var result = new List<T>();
            var path = ResolvePath(id, chain);
            if (string.IsNullOrEmpty(path)) return result;
            if (path.EndsWith("/"))
            {
                var folder = path.TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(folder)) return result;
                foreach (var g in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (nameContains != null && Path.GetFileName(p).IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var a = AssetDatabase.LoadAssetAtPath<T>(p);
                    if (a != null) result.Add(a);
                }
                result.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            }
            else
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(path);
                if (a != null) result.Add(a);
            }
            return result;
        }

        /// <summary>True if at least one asset exists for the id.</summary>
        public static bool Exists(string id, string chain = null) => FindAll<UnityEngine.Object>(id, null, chain).Count > 0;

        private static void Ensure()
        {
            if (s_entries != null) return;
            s_entries = new List<ManifestEntry>();
            s_byId = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
            var file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RelativePath);
            if (!File.Exists(file))
            {
                LSLog.Error(LogCategory.Assets, "Asset manifest not found at " + file);
                return;
            }
            try
            {
                var w = JsonUtility.FromJson<Wrapper>("{\"items\":" + File.ReadAllText(file) + "}");
                if (w?.items != null) s_entries = w.items;
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Assets, e);
            }
            foreach (var e in s_entries)
                if (e != null && !string.IsNullOrEmpty(e.id) && !s_byId.ContainsKey(e.id)) s_byId.Add(e.id, e);
        }
    }
}
