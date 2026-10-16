using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// The textures of one material named per TECH_PLAN §4.2 (<c>T_&lt;Name&gt;_{BC,N,M,E,H,A}</c>) in one folder, plus
    /// loose maps (<c>_ARM</c>, <c>_MR</c>, <c>_Roughness</c>, <c>_AO</c>, <c>_Metallic</c>, <c>_Smoothness</c>) that
    /// <see cref="MaskPacker"/> can pack into the D4 mask when <c>_M</c> is missing.
    /// </summary>
    public sealed class TextureSet
    {
        private static readonly string[] s_textureExtensions = { ".png", ".tga", ".jpg", ".jpeg", ".psd", ".tif", ".tiff", ".exr" };
        private static readonly string[] s_looseTokens = { "ARM", "MR", "Roughness", "Rough", "AO", "Occlusion", "Metallic", "Metal", "Smoothness" };

        /// <summary>Set name ("Sand01", "WPN_AK_Mag").</summary>
        public string Name;
        /// <summary>Asset folder of the set.</summary>
        public string Folder;
        /// <summary>Folder below Art/Textures ("Env", "Props", ...), empty when outside.</summary>
        public string Kind = "";
        /// <summary>Members by suffix (asset paths).</summary>
        public readonly Dictionary<TextureSuffix, string> Maps = new Dictionary<TextureSuffix, string>();
        /// <summary>Loose maps by token ("ARM", "Roughness", ...).</summary>
        public readonly Dictionary<string, string> Loose = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Path of a member or null.</summary>
        public string Get(TextureSuffix s) => Maps.TryGetValue(s, out var p) ? p : null;

        /// <summary>Base colour path or null.</summary>
        public string BaseColor => Get(TextureSuffix.BC);

        /// <inheritdoc />
        public override string ToString() => Name + " (" + string.Join(",", Maps.Keys) + (Loose.Count > 0 ? " +loose " + string.Join(",", Loose.Keys) : "") + ") in " + Folder;

        /// <summary>The set that a texture belongs to (all members in its folder with the same set name).</summary>
        public static TextureSet FromMember(string textureAssetPath)
        {
            var folder = Path.GetDirectoryName(textureAssetPath)?.Replace('\\', '/');
            var name = SetNameOf(Path.GetFileNameWithoutExtension(textureAssetPath));
            if (folder == null || name == null) return null;
            foreach (var s in FindInFolder(folder, false))
                if (string.Equals(s.Name, name, StringComparison.Ordinal)) return s;
            return null;
        }

        /// <summary>All sets in a folder (optionally recursive). Sets without BC are returned too (callers decide).</summary>
        public static List<TextureSet> FindInFolder(string folder, bool recursive)
        {
            var result = new List<TextureSet>();
            folder = folder.Replace('\\', '/').TrimEnd('/');
            var full = Path.Combine(BuilderUtil.ProjectRoot, folder);
            if (!Directory.Exists(full)) return result;
            var byKey = new Dictionary<string, TextureSet>(StringComparer.Ordinal);
            foreach (var f in Directory.GetFiles(full, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (Array.IndexOf(s_textureExtensions, ext) < 0) continue;
                var assetPath = folder + f.Substring(full.Length).Replace('\\', '/');
                var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                var stem = Path.GetFileNameWithoutExtension(f);
                var suffix = TextureNaming.ParseSuffix(stem, out var setName);
                string looseToken = null;
                if (suffix == TextureSuffix.None)
                {
                    looseToken = LooseToken(stem, out setName);
                    if (looseToken == null) continue;
                }
                var key = dir + "|" + setName;
                if (!byKey.TryGetValue(key, out var set))
                {
                    set = new TextureSet { Name = setName, Folder = dir, Kind = KindOf(assetPath) };
                    byKey.Add(key, set);
                    result.Add(set);
                }
                if (looseToken != null) set.Loose[looseToken] = assetPath;
                else set.Maps[suffix] = assetPath;
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Folder + "/" + a.Name, b.Folder + "/" + b.Name));
            return result;
        }

        /// <summary>Folder below Art/Textures for an asset path ("Env"), or "".</summary>
        public static string KindOf(string assetPath)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            return ImportRulePaths.FolderBelow(rel, "Art/Textures/") ?? "";
        }

        private static string SetNameOf(string stem)
        {
            if (TextureNaming.ParseSuffix(stem, out var name) != TextureSuffix.None) return name;
            return LooseToken(stem, out name) != null ? name : null;
        }

        private static string LooseToken(string stem, out string setName)
        {
            setName = null;
            var us = stem.LastIndexOf('_');
            if (us <= 0) return null;
            var token = stem.Substring(us + 1);
            foreach (var t in s_looseTokens)
            {
                if (!string.Equals(t, token, StringComparison.OrdinalIgnoreCase)) continue;
                var head = stem.Substring(0, us);
                setName = head.StartsWith("T_") ? head.Substring(2) : head;
                return t;
            }
            return null;
        }
    }
}
