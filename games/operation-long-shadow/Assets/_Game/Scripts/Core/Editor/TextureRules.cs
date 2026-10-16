using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LongShadow.Core.Editor
{
    /// <summary>
    /// The texture import table of TECH_PLAN §3.10, keyed by folder + suffix (pure function, no side effects; applied by
    /// <see cref="TextureImportRules"/>). Size caps come from <see cref="TextureBudgets"/> (manifest budgets, §8.9 lists in
    /// Tools/build/asset_budgets.json). Extensions beyond the §3.10 table, logged in Docs/DECISIONS.md: Vehicles,
    /// Vegetation, FX (emissive flipbooks keep the FX cap), Env/_Shared (detail maps, linear as URP/Lit requires),
    /// alpha-tested cards and impostors (clamp, coverage-preserving mips), editor-only reference images and 16-bit height
    /// sources (editor-only R16 data). Max Size never exceeds the source (rounded up to a power of two), so reports show
    /// the real in-game size. Common to all: Read/Write off, streaming mips off, crunched DXT on WebGL.
    /// </summary>
    public static class TextureRules
    {
        /// <summary>Crunch quality for BC maps of environment and props (§3.10).</summary>
        public const int QualityBC = 55;
        /// <summary>Crunch quality for N/M/E maps (§3.10).</summary>
        public const int QualityData = 60;
        /// <summary>Crunch quality for emissive FX sprites (additive, seen bright and close: muzzle flash, tracer, glint).</summary>
        public const int QualityFxEmissive = 70;

        /// <summary>
        /// Resolves the rule for a project path. Returns null for assets the rules do not manage (outside
        /// Assets/_Game/Art/{Textures,HDRI} and Assets/_Game/UI/Sprites, or under the test root's equivalents).
        /// </summary>
        public static TextureRule Resolve(string assetPath, TextureSourceInfo src)
        {
            var r = ResolveUnclamped(assetPath, src);
            if (r != null) ClampToSource(r, src);
            return r;
        }

        private static TextureRule ResolveUnclamped(string assetPath, TextureSourceInfo src)
        {
            var rel = ImportRulePaths.ToRuleRelative(assetPath);
            if (rel == null) return null;
            var file = Path.GetFileName(rel);
            var suffix = TextureNaming.ParseSuffix(file);

            if (ImportRulePaths.Under(rel, "Art/HDRI/EditorOnly/")) return HdriEditorOnly(src);
            if (ImportRulePaths.Under(rel, "Art/HDRI/Runtime/")) return Sky(src);
            if (ImportRulePaths.Under(rel, "UI/Sprites/")) return UiSprite(rel, src);
            if (!ImportRulePaths.Under(rel, "Art/Textures/")) return null;

            var kind = ImportRulePaths.FolderBelow(rel, "Art/Textures/") ?? "";
            var folder = ImportRulePaths.FolderBelow(rel, "Art/Textures/" + kind + "/");
            switch (kind.ToLowerInvariant())
            {
                case "env": return Env(folder, file, suffix, src);
                case "props": return Props(folder, file, suffix, src);
                case "weapons": return Weapons(folder, file, suffix, src);
                case "characters": return Characters(rel, folder, file, suffix, src);
                case "vehicles": return Vehicles(folder, file, suffix, src);
                case "vegetation": return Vegetation(folder, file, suffix, src);
                case "decals": return Decals(folder, suffix, src);
                case "images": return Images(file, src);
                case "fx": return Fx(file, suffix, src);
                default:
                    return Surface("Textures/" + (kind.Length > 0 ? kind : "?"), suffix, src, 1024, "class default (unknown kind)", QualityBC, QualityData, 2);
            }
        }

        /// <summary>
        /// Lowers Max Size to the source size rounded up to a power of two (Unity never upscales, so this changes no
        /// pixels; it makes the importer, the report and validate_assets.py show the real in-game size).
        /// </summary>
        private static void ClampToSource(TextureRule r, TextureSourceInfo src)
        {
            if (!src.Known || src.MaxSide <= 0 || r.Shape != TextureImporterShape.Texture2D || r.EditorOnly) return;
            var srcCap = TextureBudgets.ToMaxSize(src.MaxSide);
            if (srcCap >= r.MaxSize) return;
            r.MaxSize = srcCap;
            r.CapSource += ", source " + src.Width + "x" + src.Height;
        }

        // ------------------------------------------------------------------ surfaces (BC/N/M/E/H/A)

        /// <summary>
        /// Builds a surface-map rule: BC sRGB DXT1/DXT5 (by real alpha; alpha is opacity, so it also gets
        /// alpha-is-transparency), N normal map DXT5 (DXT5nm), M linear DXT5 (alpha = smoothness), E sRGB 512,
        /// H linear (16-bit sources: editor-only R16 data), A linear DXT1.
        /// </summary>
        private static TextureRule Surface(string cls, TextureSuffix suffix, TextureSourceInfo src, int cap, string capSource,
            int qBC, int qData, int aniso)
        {
            var r = new TextureRule { Name = cls + "/" + (suffix == TextureSuffix.None ? "Color" : suffix.ToString()), MaxSize = cap, CapSource = capSource, Aniso = aniso };
            switch (suffix)
            {
                case TextureSuffix.N:
                    r.Type = TextureImporterType.NormalMap;
                    r.SRGB = false;
                    r.WebGLFormat = TextureImporterFormat.DXT5Crunched; // DXT5nm layout for normal maps on WebGL
                    r.Quality = qData;
                    r.AlphaSource = TextureImporterAlphaSource.None;
                    break;
                case TextureSuffix.M:
                    r.SRGB = false;
                    r.WebGLFormat = TextureImporterFormat.DXT5Crunched; // A = smoothness
                    r.Quality = qData;
                    r.AlphaSource = TextureImporterAlphaSource.FromInput;
                    break;
                case TextureSuffix.E:
                    r.SRGB = true;
                    var emissiveCap = TextureBudgets.EmissiveCap;
                    if (cap > emissiveCap)
                    {
                        r.MaxSize = emissiveCap;
                        r.CapSource = "emissive " + emissiveCap;
                    }
                    r.WebGLFormat = src.AlphaUsed ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched;
                    r.Quality = qData;
                    r.Aniso = 1;
                    r.AlphaSource = src.AlphaUsed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                    break;
                case TextureSuffix.H:
                    r.SRGB = false;
                    if (src.BitsPerChannel >= 16 && !src.IsHdr)
                    {
                        // 16-bit height source (Terrain003): editor-only data read by generators, never shipped.
                        r.Name = cls + "/H16";
                        r.MaxSize = Math.Max(cap, TextureBudgets.ToMaxSize(Math.Max(src.MaxSide, 32)));
                        r.CapSource = "source size (R16 data)";
                        r.Compressed = false;
                        r.Crunched = false;
                        r.WebGLFormat = TextureImporterFormat.R16;
                        r.Mipmaps = false;
                        r.Readable = true;
                        r.Aniso = 1;
                        r.Filter = FilterMode.Bilinear;
                        r.WrapU = r.WrapV = TextureWrapMode.Clamp;
                        r.AlphaSource = TextureImporterAlphaSource.None;
                        r.EditorOnly = true;
                        return r;
                    }
                    if (r.MaxSize > 1024)
                    {
                        r.MaxSize = 1024;
                        r.CapSource = "height 1024";
                    }
                    r.WebGLFormat = TextureImporterFormat.DXT1Crunched;
                    r.Quality = QualityBC;
                    r.AlphaSource = TextureImporterAlphaSource.None;
                    break;
                case TextureSuffix.A:
                    r.SRGB = false;
                    r.WebGLFormat = TextureImporterFormat.DXT1Crunched;
                    r.Quality = QualityBC;
                    r.AlphaSource = TextureImporterAlphaSource.None;
                    break;
                default: // BC or no suffix
                    r.SRGB = true;
                    r.WebGLFormat = src.AlphaUsed ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched;
                    r.Quality = qBC;
                    r.AlphaSource = src.AlphaUsed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                    r.AlphaIsTransparency = src.AlphaUsed; // D4: BC alpha is opacity -> dilate colour under cut-out texels
                    break;
            }
            return r;
        }

        private static bool IsColour(TextureSuffix s) => s == TextureSuffix.BC || s == TextureSuffix.None;

        /// <summary>
        /// Alpha-tested card atlases (leaves, grass and foliage cards, far impostors). "Leaf" counts only as a whole name
        /// token ("T_Palm_Leaf_BC"), so door and gate leaves ("T_IronGateLeaf_BC") are not cards.
        /// </summary>
        private static bool IsAlphaTestedCard(string file) =>
            Contains(file, "Leaves") || ModelRules.HasToken(Path.GetFileNameWithoutExtension(file ?? string.Empty), "Leaf") ||
            Contains(file, "Grass") || Contains(file, "Foliage") || Contains(file, "Cards") || IsImpostor(file);

        private static bool IsImpostor(string file) => Contains(file, "Impostor");

        /// <summary>Cards and discs that must not wrap (atlas cells, impostors, rotor blur discs): every map of the set clamps.</summary>
        private static bool IsClampedCard(string file) => IsAlphaTestedCard(file) || Contains(file, "Blur");

        /// <summary>
        /// Card rules for Props/Vegetation/Vehicles sets (never Env, whose sets tile): clamp every map of a card set;
        /// alpha-tested colour maps keep their coverage in the mips (otherwise trees thin out with distance); impostors
        /// are capped (they are only seen far away).
        /// </summary>
        private static void ApplyCardRules(TextureRule r, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            if (!IsClampedCard(file)) return;
            r.WrapU = r.WrapV = TextureWrapMode.Clamp;
            if (IsColour(suffix) && src.AlphaUsed && IsAlphaTestedCard(file))
            {
                r.PreserveCoverage = true;
                r.AlphaTestReference = 0.5f;
                r.AlphaIsTransparency = true;
            }
            if (IsImpostor(file))
            {
                var cap = TextureBudgets.ImpostorCap;
                if (r.MaxSize > cap)
                {
                    r.MaxSize = cap;
                    r.CapSource = "impostor " + cap;
                }
            }
        }

        private static TextureRule Env(string set, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            if (string.Equals(set, "_Shared", StringComparison.OrdinalIgnoreCase))
            {
                // Shared tiling detail maps. URP/Lit samples _DetailAlbedoMap as linear data (x2, 0.5 = neutral) and warns
                // "This texture is not in linear space" for sRGB textures, so detail albedo is imported linear.
                var d = Surface("Env/_Shared", IsColour(suffix) ? TextureSuffix.A : suffix, src, 512, "detail maps 512", QualityBC, QualityData, 4);
                if (IsColour(suffix))
                {
                    d.SRGB = false;
                    d.Name = "Env/_Shared/DetailAlbedo";
                    d.WebGLFormat = TextureImporterFormat.DXT1Crunched;
                    d.AlphaSource = TextureImporterAlphaSource.None;
                }
                return d;
            }
            int cap;
            string capSource;
            var o = TextureBudgets.FolderCap("Env", set);
            var m = TextureBudgets.ManifestCap("Env", set);
            if (o.HasValue) { cap = TextureBudgets.ToMaxSize(o.Value); capSource = "override (asset_budgets.json)"; }
            else if (m.HasValue) { cap = TextureBudgets.ToMaxSize(m.Value); capSource = "manifest"; }
            else if (TextureBudgets.In(TextureBudgets.HeroEnvSets, set)) { cap = 2048; capSource = "hero list"; }
            else { cap = 1024; capSource = "class default"; }
            var aniso = TextureBudgets.In(TextureBudgets.GroundEnvSets, set) ? 8 : 4;
            var r = Surface("Env", suffix, src, cap, capSource, QualityBC, QualityData, aniso);
            if (IsColour(suffix) && src.AlphaUsed && IsAlphaTestedCard(file))
            {
                // Env sets tile, so they keep Repeat; alpha-tested ones (grass/foliage) still keep their coverage.
                r.PreserveCoverage = true;
                r.AlphaTestReference = 0.5f;
            }
            return r;
        }

        private static TextureRule Props(string folder, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            int cap;
            string capSource;
            var o = TextureBudgets.FolderCap("Props", folder);
            var m = TextureBudgets.ManifestCap("Props", folder);
            if (o.HasValue) { cap = TextureBudgets.ToMaxSize(o.Value); capSource = "override (asset_budgets.json)"; }
            else if (m.HasValue) { cap = TextureBudgets.ToMaxSize(m.Value); capSource = "manifest"; }
            else if (TextureBudgets.In(TextureBudgets.HeroProps, folder)) { cap = 1024; capSource = "hero list"; }
            else { cap = 512; capSource = "class default (small/medium prop)"; }
            var r = Surface("Props", suffix, src, cap, capSource, QualityBC, QualityData, 2);
            ApplyCardRules(r, file, suffix, src);
            return r;
        }

        private static TextureRule Weapons(string folder, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            const int q = 80; // §3.10: weapons crunch 80 for every map
            if (string.Equals(folder, "_Shared", StringComparison.OrdinalIgnoreCase))
            {
                var d = Surface("Weapons/_Shared", IsColour(suffix) ? TextureSuffix.A : suffix, src, 512, "detail maps 512", q, q, 4);
                if (IsColour(suffix)) { d.SRGB = false; d.Name = "Weapons/_Shared/DetailAlbedo"; }
                return d;
            }
            int cap;
            string capSource;
            var isMag = Contains(file, "_Mag");
            var isRocket = Contains(file, "_Rocket");
            var o = TextureBudgets.FolderCap("Weapons", folder);
            if (o.HasValue) { cap = TextureBudgets.ToMaxSize(o.Value); capSource = "override (asset_budgets.json)"; }
            else if (TextureBudgets.In(TextureBudgets.FirstPersonWeapons, folder))
            {
                if (isMag) { cap = 512; capSource = "FP magazine 512 (§8.9)"; }
                else if (suffix == TextureSuffix.M) { cap = 1024; capSource = "FP mask 1024 (§8.9)"; }
                else { cap = 2048; capSource = "FP weapon 2048 (§8.9)"; }
            }
            else if (TextureBudgets.In(TextureBudgets.SmallFirstPersonWeapons, folder)) { cap = 1024; capSource = "small FP item 1024"; }
            else if (TextureBudgets.In(TextureBudgets.TinyWeapons, folder) || isRocket) { cap = 512; capSource = "small item 512"; }
            else
            {
                var m = TextureBudgets.ManifestCap("Weapons", folder);
                cap = m.HasValue ? TextureBudgets.ToMaxSize(m.Value) : 1024;
                capSource = m.HasValue ? "manifest (world weapon)" : "world weapon 1024";
            }
            return Surface("Weapons", suffix, src, cap, capSource, q, q, 4);
        }

        private static TextureRule Characters(string rel, string folder, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            var isHead = Contains(file, "_Head");
            var q = isHead ? 80 : 70; // §3.10: characters 70, heads 80
            int cap;
            string capSource;
            var o = TextureBudgets.FolderCap("Characters", folder);
            if (o.HasValue) { cap = TextureBudgets.ToMaxSize(o.Value); capSource = "override (asset_budgets.json)"; }
            else if (string.Equals(folder, "Fabrics", StringComparison.OrdinalIgnoreCase)) { cap = 1024; capSource = "fabric tile 1024"; }
            else if (string.Equals(folder, "Props", StringComparison.OrdinalIgnoreCase))
            {
                var prop = ImportRulePaths.FolderBelow(rel, "Art/Textures/Characters/Props/");
                cap = Contains(prop ?? "", "PRC") ? 1024 : 512;
                capSource = "character prop (§8.9)";
            }
            else if (TextureBudgets.In(TextureBudgets.MinorCharacters, folder)) { cap = 512; capSource = "enemy/minor character 512 (§8.9)"; }
            else if (isHead && TextureBudgets.In(TextureBudgets.HeroHeads, folder)) { cap = 2048; capSource = "hero head 2048 (§8.9)"; }
            else { cap = 1024; capSource = TextureBudgets.In(TextureBudgets.SquadCharacters, folder) ? "squad 1024 (§8.9)" : "class default"; }
            return Surface("Characters", suffix, src, cap, capSource, q, q, 2); // BC alpha (hair, lashes) -> alpha-is-transparency in Surface
        }

        private static TextureRule Vehicles(string folder, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            var o = TextureBudgets.FolderCap("Vehicles", folder);
            var m = TextureBudgets.ManifestCap("Vehicles", folder);
            var cap = o.HasValue ? TextureBudgets.ToMaxSize(o.Value) : m.HasValue ? TextureBudgets.ToMaxSize(m.Value) : 1024;
            var capSource = o.HasValue ? "override (asset_budgets.json)" : m.HasValue ? "manifest" : "class default";
            if (!IsColour(suffix) && cap > 1024)
            {
                cap = 1024; // §8.9: technical/helicopter BC 2048 + N/M 1024
                capSource = "vehicle N/M 1024 (§8.9)";
            }
            var r = Surface("Vehicles", suffix, src, cap, capSource, QualityBC, QualityData, 4);
            ApplyCardRules(r, file, suffix, src);
            return r;
        }

        private static TextureRule Vegetation(string folder, string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            var o = TextureBudgets.FolderCap("Vegetation", folder);
            var m = TextureBudgets.ManifestCap("Vegetation", folder);
            var cap = o.HasValue ? TextureBudgets.ToMaxSize(o.Value) : m.HasValue ? TextureBudgets.ToMaxSize(m.Value) : 1024;
            var r = Surface("Vegetation", suffix, src, cap, o.HasValue ? "override (asset_budgets.json)" : m.HasValue ? "manifest" : "tree 1024 (§8.9)",
                QualityBC, QualityData, 2);
            ApplyCardRules(r, file, suffix, src);
            if (IsColour(suffix) && src.AlphaUsed && !r.PreserveCoverage)
            {
                // Any other cut-out vegetation colour map (fronds, bark cards) is alpha-tested too.
                r.PreserveCoverage = true;
                r.AlphaTestReference = 0.5f;
            }
            return r;
        }

        private static TextureRule Decals(string folder, TextureSuffix suffix, TextureSourceInfo src)
        {
            var o = TextureBudgets.FolderCap("Decals", folder);
            var m = TextureBudgets.ManifestCap("Decals", folder);
            var cap = o.HasValue ? TextureBudgets.ToMaxSize(o.Value) : m.HasValue ? TextureBudgets.ToMaxSize(m.Value) : 1024;
            var r = Surface("Decals", suffix, src, cap, o.HasValue ? "override (asset_budgets.json)" : m.HasValue ? "manifest" : "decal atlas 1024",
                QualityData, QualityData, 2);
            r.WrapU = r.WrapV = TextureWrapMode.Clamp;
            if (IsColour(suffix))
            {
                r.WebGLFormat = TextureImporterFormat.DXT5Crunched; // §3.10: decals DXT5 (alpha = opacity)
                r.AlphaSource = TextureImporterAlphaSource.FromInput;
                r.AlphaIsTransparency = true;
            }
            else if (suffix == TextureSuffix.M || suffix == TextureSuffix.A || suffix == TextureSuffix.H)
            {
                r.WebGLFormat = TextureImporterFormat.DXT5Crunched;
            }
            return r;
        }

        private static TextureRule Images(string file, TextureSourceInfo src)
        {
            if (IsEditorReference(file))
            {
                // Look-development references (IMG_CONCEPT_KEYFRAMES): judged against renders, so no crunch artefacts
                // and full size; editor-only, must never be referenced by shipped content.
                return new TextureRule
                {
                    Name = "Images/EditorReference",
                    MaxSize = 2048,
                    CapSource = "editor-only reference 2048",
                    SRGB = true,
                    Compressed = true,
                    Crunched = false,
                    WebGLFormat = TextureImporterFormat.DXT1,
                    Quality = 100,
                    Mipmaps = false,
                    Aniso = 1,
                    Filter = FilterMode.Bilinear,
                    WrapU = TextureWrapMode.Clamp,
                    WrapV = TextureWrapMode.Clamp,
                    AlphaSource = TextureImporterAlphaSource.None,
                    EditorOnly = true,
                };
            }
            var fullScreen = Contains(file, "_FS");
            var cap = fullScreen ? 2048 : 1024;
            var worldQuad = IsWorldImage(file);
            // Sprites cannot be rescaled to a power of two (NPOT None only), so a UI image that would not stay
            // block-aligned imports as a plain texture instead of falling back to uncompressed RGBA.
            var uiOnly = IsUiOnlyImage(file) && !worldQuad && (!src.Known || BlockAlignedAfterCap(src, cap));
            // CRT/cinematic plates are drawn into RT_CRT or the UI at a fixed size (WP09): no mips, and their 4:3 aspect is
            // kept (NPOT None) when the imported size stays block-aligned. NPOT None + mips must never be combined: Unity
            // then silently imports the texture uncompressed (RGB24) because the small mips are not multiples of 4.
            var keepAspect = !worldQuad && BlockAlignedAfterCap(src, cap);
            return new TextureRule
            {
                Name = uiOnly ? "Images/UI" : worldQuad ? "Images/World" : "Images/Plate",
                // UI-only images (intel card photos for IHud.IntelCard(Sprite), *_UI, *_Overlay) import as sprites.
                Type = uiOnly ? TextureImporterType.Sprite : TextureImporterType.Default,
                MaxSize = cap,
                CapSource = fullScreen ? "full-screen image 2048" : "image 1024",
                SRGB = true,
                WebGLFormat = src.AlphaUsed ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched,
                AlphaSource = src.AlphaUsed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None,
                AlphaIsTransparency = src.AlphaUsed,
                Quality = 60,
                Mipmaps = worldQuad, // §3.10: mips off for UI-only images; plates go through RT_CRT/UI
                Aniso = 1,
                Filter = FilterMode.Bilinear,
                WrapU = TextureWrapMode.Clamp,
                WrapV = TextureWrapMode.Clamp,
                NpotScale = uiOnly || keepAspect ? TextureImporterNPOTScale.None : TextureImporterNPOTScale.ToNearest,
            };
        }

        /// <summary>Images shown on 3D quads in the world (documents, pages): mipmapped, power-of-two.</summary>
        public static bool IsWorldImage(string file)
        {
            var stem = Path.GetFileNameWithoutExtension(file ?? string.Empty);
            return Contains(stem, "Page") || Contains(stem, "Document") || Contains(stem, "Dossier") || ModelRules.HasToken(stem, "3D");
        }

        /// <summary>
        /// True when a non-power-of-two source stays a multiple of 4 after Unity scales it down to fit <paramref name="cap"/>
        /// (proportionally, rounded to the nearest pixel: 2400x1792 at 1024 becomes 1024x765, which is not block-aligned and
        /// would import uncompressed). Unknown sources are treated as not aligned.
        /// </summary>
        public static bool BlockAlignedAfterCap(TextureSourceInfo src, int cap)
        {
            if (!src.Known || src.Width <= 0 || src.Height <= 0) return false;
            int w = src.Width, h = src.Height;
            var max = Math.Max(w, h);
            if (max > cap)
            {
                var scale = (double)cap / max;
                w = Math.Max(1, (int)Math.Floor(w * scale + 0.5));
                h = Math.Max(1, (int)Math.Floor(h * scale + 0.5));
            }
            return w % 4 == 0 && h % 4 == 0;
        }

        /// <summary>UI-only images: intel card photos ("T_Intel*", shown by IHud.IntelCard as a Sprite), "*_UI", "*_Overlay".</summary>
        public static bool IsUiOnlyImage(string file)
        {
            var stem = Path.GetFileNameWithoutExtension(file ?? string.Empty);
            return stem.StartsWith("T_Intel", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("intel_", StringComparison.OrdinalIgnoreCase) ||
                   Contains(stem, "_UI") || Contains(stem, "_Overlay");
        }

        /// <summary>Editor-only reference images ("concept_*", "*_Reference", "*_Ref").</summary>
        public static bool IsEditorReference(string file)
        {
            var stem = Path.GetFileNameWithoutExtension(file ?? string.Empty);
            return stem.StartsWith("concept", StringComparison.OrdinalIgnoreCase) || Contains(stem, "_Reference") ||
                   stem.EndsWith("_Ref", StringComparison.OrdinalIgnoreCase);
        }

        private static TextureRule Fx(string file, TextureSuffix suffix, TextureSourceInfo src)
        {
            var fxCap = TextureBudgets.FxCap;
            var r = Surface("FX", suffix, src, fxCap, "FX " + fxCap, QualityData, QualityData, 1);
            if (suffix == TextureSuffix.E)
            {
                // FX emissive maps are the whole sprite (fire flipbook, muzzle flash atlas, tracer, glint), not a surface
                // emission mask: they keep the FX cap and a higher crunch quality (bright additive blocks show artefacts).
                r.MaxSize = fxCap;
                r.CapSource = "FX " + fxCap;
                r.Quality = QualityFxEmissive;
            }
            r.Filter = FilterMode.Bilinear;
            r.Aniso = 1;
            var tiling = Contains(file, "Noise") || Contains(file, "Tile");
            r.WrapU = r.WrapV = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (Contains(file, "LensDirt") || Contains(file, "_Screen")) r.Mipmaps = false; // full-screen overlays
            return r;
        }

        private static TextureRule Sky(TextureSourceInfo src)
        {
            return new TextureRule
            {
                Name = "HDRI/Runtime/Sky",
                MaxSize = 2048,
                CapSource = "sky 2048x1024",
                SRGB = true,
                WebGLFormat = src.AlphaUsed ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched,
                AlphaSource = src.AlphaUsed ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None,
                Quality = 70,
                Mipmaps = true,
                MipFilter = TextureImporterMipFilter.KaiserFilter,
                Aniso = 1,
                Filter = FilterMode.Bilinear,
                WrapU = TextureWrapMode.Repeat,
                WrapV = TextureWrapMode.Clamp,
            };
        }

        private static TextureRule HdriEditorOnly(TextureSourceInfo src)
        {
            return new TextureRule
            {
                Name = "HDRI/EditorOnly",
                Shape = TextureImporterShape.TextureCube,
                CubeMapping = TextureImporterGenerateCubemap.Cylindrical, // latitude-longitude layout
                MaxSize = 2048,
                CapSource = "bake HDRI 2048",
                SRGB = false,
                Compressed = false,
                Crunched = false,
                WebGLFormat = TextureImporterFormat.RGBAHalf,
                Quality = 100,
                Mipmaps = true,
                Aniso = 0,
                Filter = FilterMode.Trilinear,
                WrapU = TextureWrapMode.Clamp,
                WrapV = TextureWrapMode.Clamp,
                AlphaSource = TextureImporterAlphaSource.None,
                NpotScale = TextureImporterNPOTScale.ToNearest,
                EditorOnly = true,
            };
        }

        private static TextureRule UiSprite(string rel, TextureSourceInfo src)
        {
            var folder = ImportRulePaths.FolderBelow(rel, "UI/Sprites/");
            var m = TextureBudgets.ManifestCap("UI/Sprites", folder);
            var cap = m.HasValue ? TextureBudgets.ToMaxSize(m.Value) : 512;
            // Sprites keep their size (NPOT None); block compression needs multiples of 4 after the Max Size scaling,
            // otherwise Unity would fall back to uncompressed anyway, so tiny or unaligned sprites are RGBA32 on purpose.
            var small = src.Known && (src.MaxSide <= 128 || !BlockAlignedAfterCap(src, cap));
            return new TextureRule
            {
                Name = small ? "UI/Sprite/Uncompressed" : "UI/Sprite",
                Type = TextureImporterType.Sprite,
                MaxSize = cap,
                CapSource = m.HasValue ? "manifest" : "UI 512",
                SRGB = true,
                Compressed = !small,
                Crunched = !small,
                WebGLFormat = small ? TextureImporterFormat.RGBA32 : TextureImporterFormat.DXT5Crunched,
                Quality = 70,
                Mipmaps = false,
                Aniso = 1,
                Filter = FilterMode.Bilinear,
                WrapU = TextureWrapMode.Clamp,
                WrapV = TextureWrapMode.Clamp,
                AlphaIsTransparency = true,
                AlphaSource = TextureImporterAlphaSource.FromInput,
                NpotScale = TextureImporterNPOTScale.None,
            };
        }

        private static bool Contains(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
