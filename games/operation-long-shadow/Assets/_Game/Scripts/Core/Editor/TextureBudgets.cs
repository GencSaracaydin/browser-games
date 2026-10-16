using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// Texture size caps per asset folder (TECH_PLAN §3.10 "hero list", §8.9 budgets). The primary source is the
    /// <c>budget</c> text of <c>Docs/ASSET_MANIFEST.json</c> ("512 maps in game", "hero prop: 1024 in game", "2048 (hero)",
    /// "1024 atlas"); the lists below are the fallback when an entry is missing or its text has no size, and they encode
    /// the §8.9 rows that a single number cannot express (FP weapons, squad heads, vehicle N/M maps). The lists live in
    /// <c>Tools/build/asset_budgets.json</c> (shared with validate_assets.py, see <see cref="ImportBudgetsFile"/>); the
    /// arrays in this file are the built-in defaults used when that file is missing.
    /// </summary>
    public static class TextureBudgets
    {
        private static readonly string[] s_heroEnvSets = { "sand_01", "painted_plaster_wall", "PaintedPlaster012", "concrete_floor_worn_001" };
        private static readonly string[] s_heroProps =
        {
            "Television_01", "television_02", "binoculars", "filmstrip_projector_8mm", "scrambler_box", "interrogation_chair",
            "reel_to_reel", "tube_rack", "wall_safe", "prc77", "teletype", "large_iron_gate", "rock_face_01",
        };
        private static readonly string[] s_groundEnvSets =
        {
            "sand_01", "aerial_sand", "dry_ground_rocks", "rocky_trail_02", "gravelly_sand", "Ground097", "road_damaged",
            "mud_cracked_dry_03", "saltcrust", "stone_paving", "Rocks017", "concrete_floor_worn_001", "floor_tiles_08",
            "terrazzo_tiles", "Terrazzo003", "old_linoleum_flooring_01", "cinema_carpet", "brown_planks_03", "Concrete042B",
        };
        private static readonly string[] s_fpWeapons = { "Commando", "HushPuppy", "AK", "RPK", "M60D" };
        private static readonly string[] s_smallFpWeapons = { "M67", "Knife" };
        private static readonly string[] s_tinyWeapons = { "Smoke", "Casings" };
        private static readonly string[] s_squadCharacters = { "Calder", "CalderBooth", "CalderReel", "Hollis", "Sparrow", "Tinker", "Harrou", "Fallback" };
        private static readonly string[] s_heroHeads = { "Hollis" };
        private static readonly string[] s_minorCharacters = { "LegionA", "LegionB", "LegionC", "GreycoatA", "GreycoatB", "Interrogator", "Magpie" };
        private static readonly string[] s_lightmappedProps = { "covered_car", "namaqualand_boulder_04", "rock_face_01" };

        /// <summary>Env sets imported at 2048 (TECH_PLAN §8.3; also derived from the manifest).</summary>
        public static string[] HeroEnvSets => ImportBudgetsFile.List("texture", "hero_env_sets", s_heroEnvSets);

        /// <summary>The 13 hero props of TECH_PLAN §8.9 at 1024 (folder names under Art/Textures/Props/).</summary>
        public static string[] HeroProps => ImportBudgetsFile.List("texture", "hero_props", s_heroProps);

        /// <summary>Env sets seen at grazing angles (anisotropy 8 instead of 4, TECH_PLAN §3.10 "ground 8").</summary>
        public static string[] GroundEnvSets => ImportBudgetsFile.List("texture", "ground_env_sets", s_groundEnvSets);

        /// <summary>Weapons with a first-person model: BC/N 2048, M 1024, magazine 512 (TECH_PLAN §8.9).</summary>
        public static string[] FirstPersonWeapons => ImportBudgetsFile.List("texture", "fp_weapons", s_fpWeapons);
        /// <summary>Small first-person items at 1024 (M67, knife).</summary>
        public static string[] SmallFirstPersonWeapons => ImportBudgetsFile.List("texture", "small_fp_weapons", s_smallFpWeapons);
        /// <summary>Tiny items at 512 (smoke grenade, casings).</summary>
        public static string[] TinyWeapons => ImportBudgetsFile.List("texture", "tiny_weapons", s_tinyWeapons);

        /// <summary>Squad and Calder folders (1024 sets, TECH_PLAN §8.9).</summary>
        public static string[] SquadCharacters => ImportBudgetsFile.List("texture", "squad_characters", s_squadCharacters);
        /// <summary>Characters whose head set is 2048 (Hollis, heli climax close-up; TECH_PLAN §8.9 / manifest CHR_HOLLIS).</summary>
        public static string[] HeroHeads => ImportBudgetsFile.List("texture", "hero_heads", s_heroHeads);
        /// <summary>Enemy and minor characters at 512 (shared fabrics, seen at 10–35 m).</summary>
        public static string[] MinorCharacters => ImportBudgetsFile.List("texture", "minor_characters", s_minorCharacters);

        /// <summary>Static props (folder names) that are lightmapped and get generated lightmap UVs (D8 "big props").</summary>
        public static string[] LightmappedProps => ImportBudgetsFile.List("model", "lightmapped_props", s_lightmappedProps);

        /// <summary>Cap for far-impostor cards (<c>*_Impostor_*</c>).</summary>
        public static int ImpostorCap => ImportBudgetsFile.Int("texture", "impostor_cap", 256);
        /// <summary>Cap for FX textures (flipbooks, atlases, lens dirt), emissive ones included.</summary>
        public static int FxCap => ImportBudgetsFile.Int("texture", "fx_cap", 1024);
        /// <summary>Cap for surface emission maps (§3.10 "*_E 512").</summary>
        public static int EmissiveCap => ImportBudgetsFile.Int("texture", "emissive_cap", 512);

        /// <summary>
        /// Explicit per-folder cap from asset_budgets.json <c>texture.folder_caps</c> ("Decals/poster_art": 2048), which
        /// wins over the manifest (decisions logged in Docs/DECISIONS.md). Null when none.
        /// </summary>
        public static int? FolderCap(string kind, string folder)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(folder)) return null;
            return ImportBudgetsFile.IntMap("texture", "folder_caps").TryGetValue(kind + "/" + folder, out var v) ? v : (int?)null;
        }

        private static Dictionary<string, int> s_caps;

        /// <summary>Forgets the parsed manifest (after it changed).</summary>
        public static void Reload()
        {
            s_caps = null;
            AssetManifest.Reload();
            ImportBudgetsFile.Reload();
        }

        /// <summary>
        /// Cap from the manifest for a texture or model folder: <paramref name="kind"/> is the folder below
        /// Art/Textures or Art/Models ("Props", "Env", "Vehicles", "Vegetation", "Decals", "Weapons") or "UI/Sprites",
        /// <paramref name="folder"/> the asset folder ("teletype"). Null when the manifest says nothing.
        /// </summary>
        public static int? ManifestCap(string kind, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            Ensure();
            return s_caps.TryGetValue(Key(kind, folder), out var v) ? v : (int?)null;
        }

        /// <summary>True when <paramref name="name"/> is in <paramref name="list"/> (case-insensitive).</summary>
        public static bool In(string[] list, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var s in list)
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static readonly Regex s_inGame = new Regex(@"(\d{3,4})\s*(?:maps\s*)?in game", RegexOptions.IgnoreCase);
        private static readonly Regex s_dims = new Regex(@"(\d{3,4})\s*x\s*(\d{3,4})", RegexOptions.IgnoreCase);
        private static readonly Regex s_maps = new Regex(@"(\d{3,4})\s*(?:maps|atlas|tile)\b", RegexOptions.IgnoreCase);
        private static readonly Regex s_pxRange = new Regex(@"\d{2,4}\s*-\s*(\d{2,4})\s*px", RegexOptions.IgnoreCase);
        private static readonly Regex s_pxMax = new Regex(@"<=\s*(\d{2,4})\s*px", RegexOptions.IgnoreCase);
        private static readonly Regex s_lead = new Regex(@"^\s*(\d{3,4})\b");

        /// <summary>
        /// The in-game texture size stated by a manifest budget text, or null. Order: "N in game", "WxH", "N maps|atlas|tile",
        /// "a-b px", "&lt;= N px", a leading number.
        /// </summary>
        public static int? ParseCap(string budget)
        {
            if (string.IsNullOrEmpty(budget)) return null;
            Match m;
            if ((m = s_inGame.Match(budget)).Success) return int.Parse(m.Groups[1].Value);
            if ((m = s_dims.Match(budget)).Success) return Math.Max(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
            if ((m = s_maps.Match(budget)).Success) return int.Parse(m.Groups[1].Value);
            if ((m = s_pxRange.Match(budget)).Success) return int.Parse(m.Groups[1].Value);
            if ((m = s_pxMax.Match(budget)).Success) return int.Parse(m.Groups[1].Value);
            if ((m = s_lead.Match(budget)).Success) return int.Parse(m.Groups[1].Value);
            return null;
        }

        /// <summary>Rounds up to a power of two in [32, 8192] (Unity's Max Size values).</summary>
        public static int ToMaxSize(int pixels)
        {
            var s = 32;
            while (s < pixels && s < 8192) s <<= 1;
            return s;
        }

        private static string Key(string kind, string folder) => (kind + "/" + folder).ToLowerInvariant();

        private static void Ensure()
        {
            if (s_caps != null) return;
            s_caps = new Dictionary<string, int>();
            foreach (var e in AssetManifest.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.target_path)) continue;
                var p = e.target_path.Replace('\\', '/');
                string kind = null, rest = null;
                foreach (var root in new[] { "Assets/_Game/Art/Textures/", "Assets/_Game/Art/Models/" })
                {
                    if (!p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    var r = p.Substring(root.Length);
                    var slash = r.IndexOf('/');
                    if (slash <= 0) break;
                    kind = r.Substring(0, slash);
                    rest = r.Substring(slash + 1);
                    break;
                }
                if (kind == null && p.StartsWith("Assets/_Game/UI/Sprites/", StringComparison.OrdinalIgnoreCase))
                {
                    kind = "UI/Sprites";
                    rest = p.Substring("Assets/_Game/UI/Sprites/".Length);
                }
                if (kind == null || string.IsNullOrEmpty(rest)) continue;
                var folderEnd = rest.IndexOf('/');
                if (folderEnd <= 0) continue; // a file directly in the kind folder (images): no folder cap
                var folder = rest.Substring(0, folderEnd);
                if (string.Equals(kind, "Characters", StringComparison.OrdinalIgnoreCase)) continue; // code table (§8.9)
                var cap = ParseCap(e.budget);
                if (cap == null) continue;
                var key = Key(kind, folder);
                // Several entries can share a folder (gun + magazine): keep the largest.
                if (!s_caps.TryGetValue(key, out var old) || cap.Value > old) s_caps[key] = cap.Value;
            }
        }
    }
}
