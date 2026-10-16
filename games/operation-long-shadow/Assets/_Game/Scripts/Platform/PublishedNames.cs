using System;
using System.Collections.Generic;

namespace LongShadow.Platform
{
    /// <summary>
    /// The artifact host's file-name rule (TECH_PLAN §9.2, R3; measured by the orchestrator 2026-10-16): the host serves
    /// only a fixed list of extensions, so <c>Tools/build/publish_plan.py</c> appends a suffix (".wasm") to every file
    /// whose extension is not served (<c>.unityweb</c>, <c>.bundle</c>, <c>.hash</c>, extensionless files). This class is
    /// the runtime mirror of that rule, used by <see cref="AddressablesRemap"/> so bundle URLs in the catalog resolve
    /// to the renamed files. Keep <see cref="DefaultServedExtensions"/> in sync with
    /// <c>Tools/build/build_limits.json</c> "served_extensions" (the page passes the live list in, see
    /// <c>window.LS_SERVED_EXT</c>).
    /// </summary>
    public static class PublishedNames
    {
        /// <summary>Suffix appended by publish_plan.py to files the host would refuse.</summary>
        public const string DefaultSuffix = ".wasm";

        /// <summary>Extensions the artifact host serves (lower case, no dot).</summary>
        public static readonly string[] DefaultServedExtensions =
        {
            "html", "css", "js", "mjs", "wasm", "json", "map", "geojson", "webmanifest", "txt", "md", "csv", "xml", "svg",
            "png", "jpg", "jpeg", "gif", "webp", "avif", "ico", "woff", "woff2", "ttf", "otf", "mp3", "wav", "ogg", "mp4",
            "webm", "pdf",
        };

        /// <summary>A case-insensitive set built from <see cref="DefaultServedExtensions"/>.</summary>
        public static HashSet<string> DefaultServedSet() => new HashSet<string>(DefaultServedExtensions, StringComparer.OrdinalIgnoreCase);

        /// <summary>Parses a comma/space separated extension list ("html, .css js"); returns null when empty.</summary>
        public static HashSet<string> ParseServedList(string list)
        {
            if (string.IsNullOrEmpty(list)) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in list.Split(new[] { ',', ' ', ';', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var e = raw.Trim().TrimStart('.');
                if (e.Length > 0) set.Add(e);
            }
            return set.Count > 0 ? set : null;
        }

        /// <summary>
        /// Extension (lower case, no dot) of the last path segment of a URL or path, ignoring "?query" and "#fragment";
        /// empty when the segment has no dot.
        /// </summary>
        public static string ExtensionOf(string urlOrPath)
        {
            if (string.IsNullOrEmpty(urlOrPath)) return string.Empty;
            var path = urlOrPath.Substring(0, PathEnd(urlOrPath));
            var start = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\')) + 1;
            var dot = path.LastIndexOf('.');
            if (dot < start || dot >= path.Length - 1) return string.Empty;
            return path.Substring(dot + 1).ToLowerInvariant();
        }

        /// <summary>True if the host serves the file's extension (extensionless files are never served).</summary>
        public static bool IsServed(string urlOrPath, ISet<string> served)
        {
            var ext = ExtensionOf(urlOrPath);
            return ext.Length > 0 && (served ?? DefaultServedSet()).Contains(ext);
        }

        /// <summary>
        /// Published name of a URL/path: unchanged when served (or already suffixed), otherwise the path with
        /// <paramref name="suffix"/> appended before any "?query"/"#fragment". Idempotent.
        /// </summary>
        public static string Map(string urlOrPath, string suffix, ISet<string> served)
        {
            if (string.IsNullOrEmpty(urlOrPath) || string.IsNullOrEmpty(suffix)) return urlOrPath;
            if (IsServed(urlOrPath, served)) return urlOrPath;
            var end = PathEnd(urlOrPath);
            if (end >= suffix.Length && string.Compare(urlOrPath, end - suffix.Length, suffix, 0, suffix.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return urlOrPath;
            return urlOrPath.Substring(0, end) + suffix + urlOrPath.Substring(end);
        }

        /// <summary>True for ids that are fetched over the network (absolute URLs or Addressables runtime paths).</summary>
        public static bool IsNetworkPath(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id.IndexOf("://", StringComparison.Ordinal) > 0 ||
                   id.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", StringComparison.Ordinal);
        }

        private static int PathEnd(string s)
        {
            var q = s.IndexOf('?');
            var h = s.IndexOf('#');
            var end = s.Length;
            if (q >= 0) end = Math.Min(end, q);
            if (h >= 0) end = Math.Min(end, h);
            return end;
        }
    }
}
