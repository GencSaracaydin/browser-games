using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LongShadow.Core;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Folder -> Addressables group mapping of TECH_PLAN §8.10 (WP01 owns the rules; group contents follow the content
    /// that exists). Only scenes and a few "main" asset kinds become explicit entries (scenes, prefabs, materials,
    /// library ScriptableObjects, audio clips, animation clips, images); everything else rides along as a dependency.
    /// Assets that two or more groups depend on without being placed are moved into <c>Shared_*</c> groups (split at
    /// about 10 MB of estimated download) so no bundle duplicates another's content. Section scenes are addressed by
    /// their scene name (<see cref="Sections.AddressKey"/>); a missing scene is stood in for by its dev placeholder
    /// (<see cref="SectionStreamer.PlaceholderPath"/>) when placeholders are enabled. Every managed group is local,
    /// LZ4, packed together, named "*.bundle" with an appended hash, included in the build.
    /// </summary>
    public static class AddressablesGroupRules
    {
        /// <summary>Groups of TECH_PLAN §8.10 (sections use their scene names).</summary>
        public static readonly string[] KnownGroups =
        {
            "Core_Shaders", "Core_Persistent", "Player_FP", "Weapons_A", "Weapons_B", "Chars_Squad", "Chars_Legion",
            "Chars_Greycoat", "Chars_Booth", "Anim_A", "Anim_B", "Audio_VO_A", "Audio_VO_B", "Audio_VO_C", "Audio_SFX_A",
            "Audio_SFX_B", "Audio_Music_A", "Audio_Music_B", "Audio_Amb", "Env_Tex_A", "Env_Tex_B", "Env_Tex_C", "Env_Tex_D",
            "Env_Tex_E", "Env_Tex_F", "Props_A", "Props_B", "Props_C", "Vehicles", "Images",
            "S0_Booth", "S1_Approach", "S2_Market", "S3_Square", "S4_Roofs", "S5_StationExt", "S6_StationInt", "S7_Pitch",
            "S8_Heli", "S9_TownFar",
        };

        /// <summary>Prefix of the automatic shared-dependency groups.</summary>
        public const string SharedPrefix = "Shared_";

        /// <summary>Estimated download per shared group before it is split (bytes; the hard bundle cap is 12 MB).</summary>
        public const long SharedGroupBudget = 10_000_000;

        private const string Game = "Assets/_Game/";

        private static readonly HashSet<string> s_explicitExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".mat", ".asset", ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".fbx", ".anim", ".controller",
            ".overrideController", ".png", ".jpg", ".jpeg", ".tga", ".psd", ".json", ".shader", ".shadergraph",
        };

        private static readonly string[] s_weaponsA = { "commando", "hushpuppy", "hush_puppy", "m67", "knife", "smoke" };
        private static readonly Regex s_voLine = new Regex(@"^VO_D(\d{3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex s_musicA = new Regex(@"^(MUS_0[1-4]|STG_)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Options for <see cref="Apply"/>.</summary>
        public sealed class Options
        {
            /// <summary>Stand in for missing S_Persistent/section scenes with their dev placeholders.</summary>
            public bool UsePlaceholders = true;
            /// <summary>Move dependencies shared by several groups into Shared_* groups.</summary>
            public bool IsolateSharedDependencies = true;
            /// <summary>Remove managed groups that end up empty.</summary>
            public bool RemoveEmptyGroups = true;
        }

        /// <summary>Summary of an <see cref="Apply"/> run.</summary>
        public sealed class Report
        {
            /// <summary>Entries created or moved.</summary>
            public int Placed;
            /// <summary>Entries removed (no rule any more, or a placeholder replaced by the real scene).</summary>
            public int Removed;
            /// <summary>Shared dependencies isolated.</summary>
            public int Shared;
            /// <summary>Estimated download per group (bytes).</summary>
            public readonly Dictionary<string, long> Estimates = new Dictionary<string, long>();
            /// <summary>Warnings (oversized groups, foreign groups).</summary>
            public readonly List<string> Warnings = new List<string>();
            /// <summary>Log text.</summary>
            public readonly StringBuilder Log = new StringBuilder();
        }

        /// <summary>True for groups these rules own (others are left alone).</summary>
        public static bool IsManagedGroup(string name) =>
            !string.IsNullOrEmpty(name) && (Array.IndexOf(KnownGroups, name) >= 0 || name.StartsWith(SharedPrefix, StringComparison.Ordinal));

        /// <summary>True for asset kinds that become explicit entries when a folder rule matches.</summary>
        public static bool IsExplicitKind(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (assetPath.IndexOf("/EditorOnly/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return s_explicitExtensions.Contains(Path.GetExtension(assetPath));
        }

        /// <summary>
        /// Group and address for an asset path (pure: no AssetDatabase), or null when no rule places it. Scenes get their
        /// scene name as address; other assets their project path.
        /// </summary>
        public static string GroupFor(string assetPath, out string address)
        {
            address = assetPath;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(Game, StringComparison.Ordinal) || !IsExplicitKind(assetPath)) return null;
            var rel = assetPath.Substring(Game.Length);
            var file = Path.GetFileNameWithoutExtension(assetPath);
            var lower = file.ToLowerInvariant();

            // Scenes (TECH_PLAN D1, §5.1).
            if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                if (rel == "Scenes/" + Sections.PersistentScene + ".unity")
                {
                    address = Sections.PersistentScene;
                    return "Core_Persistent";
                }
                if (rel.StartsWith("Scenes/Sections/", StringComparison.Ordinal) && Sections.TryParse(file, out var id) && Sections.SceneName(id) == file)
                {
                    address = file;
                    return file;
                }
                if (assetPath.StartsWith(SectionStreamer.PlaceholderFolder + "/PH_", StringComparison.Ordinal))
                {
                    var name = file.Substring(3);
                    address = name;
                    if (name == Sections.PersistentScene) return "Core_Persistent";
                    if (Sections.TryParse(name, out var pid) && Sections.SceneName(pid) == name) return name;
                }
                return null; // S_Boot (build settings) and dev scenes are never addressable
            }

            if (rel.StartsWith("Data/Libraries/", StringComparison.Ordinal)) return "Core_Persistent";
            if (rel.StartsWith("Art/Materials/Sky/", StringComparison.Ordinal) || rel.StartsWith("Art/HDRI/Runtime/", StringComparison.Ordinal)) return "Core_Persistent";
            if (rel.StartsWith("Art/Shaders/", StringComparison.Ordinal) || rel.StartsWith("Art/Materials/FX/", StringComparison.Ordinal) ||
                rel.StartsWith("Art/VFX/", StringComparison.Ordinal) || rel.StartsWith("Art/Textures/FX/", StringComparison.Ordinal) ||
                rel.StartsWith("Prefabs/FX/", StringComparison.Ordinal) || rel.StartsWith("UI/", StringComparison.Ordinal))
                return "Core_Shaders";

            // Player, weapons, characters.
            if (rel.StartsWith("Prefabs/Player/", StringComparison.Ordinal)) return "Player_FP";
            if (rel.StartsWith("Prefabs/Weapons/", StringComparison.Ordinal) || rel.StartsWith("Art/Materials/Weapons/", StringComparison.Ordinal))
                return s_weaponsA.Any(w => lower.Contains(w)) ? "Weapons_A" : "Weapons_B";
            if (rel.StartsWith("Prefabs/Bodies/", StringComparison.Ordinal))
            {
                if (lower.Contains("calder")) return "Player_FP";
                if (lower.Contains("hollis") || lower.Contains("sparrow") || lower.Contains("tinker")) return "Chars_Squad";
                if (lower.Contains("greycoat")) return "Chars_Greycoat";
                if (lower.Contains("legion")) return "Chars_Legion";
                return "Chars_Booth";
            }
            if (rel.StartsWith("Prefabs/Squad/", StringComparison.Ordinal)) return "Chars_Squad";
            if (rel.StartsWith("Prefabs/Enemies/", StringComparison.Ordinal)) return lower.Contains("greycoat") ? "Chars_Greycoat" : "Chars_Legion";
            if (rel.StartsWith("Art/Animation/", StringComparison.Ordinal))
            {
                if (rel.StartsWith("Art/Animation/Clips/", StringComparison.Ordinal))
                {
                    var source = rel.Substring("Art/Animation/Clips/".Length);
                    var slash = source.IndexOf('/');
                    source = slash > 0 ? source.Substring(0, slash).ToLowerInvariant() : string.Empty;
                    return source.StartsWith("meshy", StringComparison.Ordinal) || source.StartsWith("ual", StringComparison.Ordinal) ? "Anim_A" : "Anim_B";
                }
                return "Anim_A";
            }

            // Audio (TECH_PLAN §3.12 folders).
            if (rel.StartsWith("Audio/VO/", StringComparison.Ordinal))
            {
                var m = s_voLine.Match(file);
                if (!m.Success) return "Audio_VO_A"; // barks, death lines, numbers: used across the mission
                var n = int.Parse(m.Groups[1].Value);
                if (n <= 40 || n >= 200) return "Audio_VO_A"; // intro, insertion (D200+), beats 1-4
                return n <= 80 ? "Audio_VO_B" : "Audio_VO_C";
            }
            if (rel.StartsWith("Audio/Envelopes/", StringComparison.Ordinal)) return "Audio_VO_A";
            if (rel.StartsWith("Audio/SFX/", StringComparison.Ordinal))
            {
                var sub = rel.Substring("Audio/SFX/".Length);
                return sub.StartsWith("Weapons/", StringComparison.Ordinal) || sub.StartsWith("Impacts/", StringComparison.Ordinal) ||
                       sub.StartsWith("Foley/", StringComparison.Ordinal) || sub.StartsWith("Footsteps/", StringComparison.Ordinal) ||
                       sub.StartsWith("Whizz/", StringComparison.Ordinal) || sub.StartsWith("Explosions/", StringComparison.Ordinal)
                    ? "Audio_SFX_A"
                    : "Audio_SFX_B";
            }
            if (rel.StartsWith("Audio/Music/", StringComparison.Ordinal)) return s_musicA.IsMatch(file) ? "Audio_Music_A" : "Audio_Music_B";
            if (rel.StartsWith("Audio/Ambience/", StringComparison.Ordinal)) return "Audio_Amb";

            // World content shared by sections.
            if (rel.StartsWith("Art/Textures/Images/", StringComparison.Ordinal)) return "Images";
            if (rel.StartsWith("Prefabs/Vehicles/", StringComparison.Ordinal)) return "Vehicles";
            if (rel.StartsWith("Prefabs/Props/", StringComparison.Ordinal)) return "Props_" + Letter(file, 3);
            if (rel.StartsWith("Art/Materials/Env/", StringComparison.Ordinal)) return "Env_Tex_" + Letter(SetName(rel, "Art/Materials/Env/", file), 6);
            return null;
        }

        /// <summary>Menu: apply the rules (with placeholders) and log the result.</summary>
        [MenuItem("LongShadow/Platform/Apply Addressables Group Rules", priority = 211)]
        public static void ApplyMenu()
        {
            var settings = AddressablesSetup.Ensure(null, out _);
            var r = Apply(settings, new Options());
            Debug.Log("[LS:Platform] Addressables group rules:\n" + r.Log);
        }

        /// <summary>Applies the rules to the settings (creates groups, places/moves/removes entries, isolates shared dependencies).</summary>
        public static Report Apply(AddressableAssetSettings settings, Options options)
        {
            options = options ?? new Options();
            var report = new Report();
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            // 1. Desired explicit entries.
            var desired = new Dictionary<string, (string group, string address)>(StringComparer.Ordinal); // guid -> placement
            var realScenes = new HashSet<string>(StringComparer.Ordinal);
            var candidates = AssetDatabase.FindAssets(string.Empty, new[] { "Assets/_Game" })
                                          .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (var path in candidates)
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                var group = GroupFor(path, out var address);
                if (group == null) continue;
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !path.StartsWith(SectionStreamer.PlaceholderFolder, StringComparison.Ordinal))
                    realScenes.Add(address);
                desired[AssetDatabase.AssetPathToGUID(path)] = (group, address);
            }
            // Placeholders only stand in for scenes that do not exist (and only when enabled).
            foreach (var guid in desired.Keys.ToList())
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(SectionStreamer.PlaceholderFolder, StringComparison.Ordinal)) continue;
                if (!options.UsePlaceholders || realScenes.Contains(desired[guid].address)) desired.Remove(guid);
            }

            // 2. Remove entries of managed groups that are no longer desired.
            foreach (var g in settings.groups.Where(g => g != null && IsManagedGroup(g.Name)).ToList())
                foreach (var e in g.entries.ToList())
                {
                    if (desired.ContainsKey(e.guid) && !g.Name.StartsWith(SharedPrefix, StringComparison.Ordinal)) continue;
                    settings.RemoveAssetEntry(e.guid, false);
                    if (!g.Name.StartsWith(SharedPrefix, StringComparison.Ordinal)) report.Removed++;
                }

            // 3. Place desired entries.
            foreach (var kv in desired)
            {
                var group = GetOrCreateGroup(settings, kv.Value.group, report);
                var entry = settings.FindAssetEntry(kv.Key);
                if (entry != null && entry.parentGroup != null && !IsManagedGroup(entry.parentGroup.Name))
                {
                    report.Warnings.Add(AssetDatabase.GUIDToAssetPath(kv.Key) + " is in the unmanaged group '" + entry.parentGroup.Name + "'; left there");
                    continue;
                }
                if (entry == null || entry.parentGroup != group)
                {
                    entry = settings.CreateOrMoveEntry(kv.Key, group, false, false);
                    report.Placed++;
                }
                if (entry != null && entry.address != kv.Value.address) entry.SetAddress(kv.Value.address, false);
            }

            // 4. Shared dependencies.
            if (options.IsolateSharedDependencies) IsolateShared(settings, report);

            // 5. Schemas, estimates, empty groups.
            foreach (var g in settings.groups.Where(g => g != null && IsManagedGroup(g.Name)).ToList())
            {
                if (g.entries.Count == 0 && options.RemoveEmptyGroups)
                {
                    settings.RemoveGroup(g);
                    continue;
                }
                ConfigureSchema(settings, g);
                var est = EstimateGroup(g);
                report.Estimates[g.Name] = est;
                if (est > 12_000_000) report.Warnings.Add(g.Name + " is estimated at " + (est / 1e6).ToString("0.0") + " MB (> 12 MB): add a split rule (TECH_PLAN §8.10)");
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            report.Log.AppendLine("placed " + report.Placed + ", removed " + report.Removed + ", shared " + report.Shared);
            foreach (var kv in report.Estimates.OrderBy(k => k.Key, StringComparer.Ordinal))
                report.Log.AppendLine("  " + kv.Key.PadRight(18) + (kv.Value / 1e6).ToString("0.00").PadLeft(7) + " MB est.");
            foreach (var w in report.Warnings) report.Log.AppendLine("WARNING " + w);
            return report;
        }

        /// <summary>Configures a group for the web: local, LZ4, packed together, hashed *.bundle names. Returns true if changed.</summary>
        public static bool ConfigureSchema(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            var changed = false;
            var b = group.GetSchema<BundledAssetGroupSchema>();
            if (b == null)
            {
                b = group.AddSchema<BundledAssetGroupSchema>();
                changed = true;
            }
            if (group.GetSchema<ContentUpdateGroupSchema>() == null)
            {
                group.AddSchema<ContentUpdateGroupSchema>();
                changed = true;
            }
            changed |= Assign(b.Compression, BundledAssetGroupSchema.BundleCompressionMode.LZ4, v => b.Compression = v);
            changed |= Assign(b.BundleMode, BundledAssetGroupSchema.BundlePackingMode.PackTogether, v => b.BundleMode = v);
            changed |= Assign(b.BundleNaming, BundledAssetGroupSchema.BundleNamingStyle.AppendHash, v => b.BundleNaming = v);
            changed |= Assign(b.IncludeInBuild, true, v => b.IncludeInBuild = v);
            changed |= Assign(b.UseAssetBundleCache, false, v => b.UseAssetBundleCache = v);   // WebGL: the loader's IndexedDB cache
            changed |= Assign(b.UseAssetBundleCrc, false, v => b.UseAssetBundleCrc = v);       // saves main-thread time on WebGL
            changed |= Assign(b.UseUnityWebRequestForLocalBundles, true, v => b.UseUnityWebRequestForLocalBundles = v);
            changed |= Assign(b.IncludeAddressInCatalog, true, v => b.IncludeAddressInCatalog = v);
            changed |= Assign(b.IncludeGUIDInCatalog, false, v => b.IncludeGUIDInCatalog = v);
            changed |= Assign(b.IncludeLabelsInCatalog, true, v => b.IncludeLabelsInCatalog = v);
            changed |= Assign(b.RetryCount, 2, v => b.RetryCount = v);
            if (b.BuildPath.GetName(settings) != AddressableAssetSettings.kLocalBuildPath)
            {
                b.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                changed = true;
            }
            if (b.LoadPath.GetName(settings) != AddressableAssetSettings.kLocalLoadPath)
            {
                b.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                changed = true;
            }
            var cu = group.GetSchema<ContentUpdateGroupSchema>();
            if (cu != null) changed |= Assign(cu.StaticContent, false, v => cu.StaticContent = v);
            if (changed) EditorUtility.SetDirty(group);
            return changed;
        }

        /// <summary>Estimated download of an asset (bytes): crunched/compressed texture size, ~AAC audio, LZ4 meshes.</summary>
        public static long EstimateBytes(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            var fileBytes = new FileInfo(path).Length;
            var importer = AssetImporter.GetAtPath(path);
            if (importer is TextureImporter ti)
            {
                ti.GetSourceTextureWidthAndHeight(out var w, out var h);
                var ps = ti.GetPlatformTextureSettings("WebGL");
                var max = ps != null && ps.overridden ? ps.maxTextureSize : ti.maxTextureSize;
                var scale = Math.Max(1.0, Math.Max(w, h) / (double)Math.Max(1, max));
                var px = w / scale * (h / scale);
                var format = (ps != null && ps.overridden ? ps.format : ti.GetAutomaticFormat("WebGL")).ToString();
                double bpp = format.Contains("DXT1") || format.Contains("BC4") || format.Contains("ETC") ? 0.5
                    : format.Contains("DXT5") || format.Contains("BC5") || format.Contains("BC7") || format.Contains("Alpha8") ? 1.0
                    : format.Contains("RGBA32") || format.Contains("ARGB32") ? 4.0
                    : format.Contains("RGB24") ? 3.0
                    : PostBuildValidator.IsHdrFormat(format) ? 8.0 : 1.0;
                var mips = ti.mipmapEnabled ? 1.333 : 1.0;
                var crunch = format.Contains("Crunched") ? 0.33 : 0.8;
                return (long)(px * bpp * mips * crunch);
            }
            if (importer is AudioImporter) return (long)(fileBytes * 0.1);
            if (importer is ModelImporter) return (long)(fileBytes * 0.4);
            return fileBytes;
        }

        private static void IsolateShared(AddressableAssetSettings settings, Report report)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in settings.groups)
                if (g != null)
                    foreach (var e in g.entries) placed.Add(e.AssetPath);

            var users = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // dependency -> groups
            foreach (var g in settings.groups.Where(g => g != null && IsManagedGroup(g.Name) && !g.Name.StartsWith(SharedPrefix, StringComparison.Ordinal)))
            {
                var roots = g.entries.Select(e => e.AssetPath).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (roots.Length == 0) continue;
                foreach (var dep in AssetDatabase.GetDependencies(roots, true))
                {
                    if (placed.Contains(dep) || !IsShareable(dep)) continue;
                    if (!users.TryGetValue(dep, out var set)) users[dep] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add(g.Name);
                }
            }

            var budgets = new Dictionary<string, long>(StringComparer.Ordinal);
            var letters = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in users.Where(k => k.Value.Count >= 2).OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var kind = KindOf(kv.Key);
                if (!letters.ContainsKey(kind)) letters[kind] = 0;
                var name = SharedPrefix + kind + "_" + (char)('A' + letters[kind]);
                var est = EstimateBytes(kv.Key);
                budgets.TryGetValue(name, out var used);
                if (used > 0 && used + est > SharedGroupBudget)
                {
                    letters[kind]++;
                    name = SharedPrefix + kind + "_" + (char)('A' + letters[kind]);
                    used = 0;
                }
                budgets[name] = used + est;
                var group = GetOrCreateGroup(settings, name, report);
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(kv.Key), group, false, false);
                if (entry != null) report.Shared++;
            }
        }

        private static bool IsShareable(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".cs":
                case ".dll":
                case ".asmdef":
                case ".asmref":
                case ".hlsl":
                case ".cginc":
                case ".glslinc":
                case ".unity":
                    return false;
            }
            if (path.StartsWith(Sections.ScenesFolder + "/", StringComparison.Ordinal)) return false; // per-scene baked data
            return path.IndexOf("/Editor/", StringComparison.Ordinal) < 0 && path.IndexOf("/EditorOnly/", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string KindOf(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer is AudioImporter) return "Audio";
            if (importer is ModelImporter) return "Mesh";
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (importer is TextureImporter || ext == ".mat" || ext == ".shader" || ext == ".shadergraph") return "Env";
            if (ext == ".anim" || ext == ".controller") return "Anim";
            return "Misc";
        }

        private static long EstimateGroup(AddressableAssetGroup g)
        {
            var roots = g.entries.Select(e => e.AssetPath).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (roots.Length == 0) return 0;
            long total = 0;
            foreach (var p in AssetDatabase.GetDependencies(roots, true))
            {
                if (!IsShareable(p) && !p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                var owner = g.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(p));
                if (owner != null && owner.parentGroup != g) continue; // counted in its own group
                total += EstimateBytes(p);
            }
            return total;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name, Report report)
        {
            var g = settings.FindGroup(name);
            if (g != null) return g;
            g = settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            ConfigureSchema(settings, g);
            report.Log.AppendLine("created group " + name);
            return g;
        }

        private static bool Assign<T>(T current, T wanted, Action<T> set)
        {
            if (EqualityComparer<T>.Default.Equals(current, wanted)) return false;
            set(wanted);
            return true;
        }

        private static string Letter(string key, int count)
        {
            unchecked
            {
                var h = 2166136261u; // FNV-1a: stable across runs and machines
                foreach (var c in key.ToLowerInvariant()) h = (h ^ c) * 16777619u;
                return ((char)('A' + (int)(h % (uint)count))).ToString();
            }
        }

        private static string SetName(string rel, string prefix, string file)
        {
            var rest = rel.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            return slash > 0 ? rest.Substring(0, slash) : file;
        }
    }
}
