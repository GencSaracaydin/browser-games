using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core.Editor;
using LongShadow.UI.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LongShadow.UI.Editor
{
    /// <summary>
    /// Creates the TMP SDF font assets of the UI from the TTFs under <see cref="UIAssetPaths.Fonts"/> (manifest UI_FONT_*,
    /// downloaded by WP09_ART; Sardani from Tools/ui/glyph_font.py). Each atlas is prefilled with
    /// <see cref="UIFonts.RequiredCharacters"/> and then made static (the source TTF stays out of the build), with TMP's
    /// default LiberationSans SDF as fallback for anything else. Saves a shadow and a glow material per font so the
    /// UNDERLAY_ON shader variant ships in WebGL builds. Needs TMP Essential Resources (LongShadow/UI/1 Import TMP
    /// Essential Resources).
    /// </summary>
    public static class FontAssetBuilder
    {
        /// <summary>Atlas settings per role.</summary>
        public readonly struct Spec
        {
            public readonly FontRole Role;
            public readonly int Sampling;
            public readonly int Padding;
            public readonly int Atlas;

            public Spec(FontRole role, int sampling, int padding, int atlas)
            {
                Role = role;
                Sampling = sampling;
                Padding = padding;
                Atlas = atlas;
            }
        }

        /// <summary>Font roles built from TTFs (Fallback is TMP's own).</summary>
        public static readonly Spec[] Specs =
        {
            new Spec(FontRole.Typewriter, 72, 8, 1024),
            new Spec(FontRole.TypewriterClean, 64, 8, 1024),
            new Spec(FontRole.Condensed, 64, 7, 1024),
            new Spec(FontRole.CondensedMedium, 56, 7, 1024),
            new Spec(FontRole.Mono, 40, 6, 512),
            new Spec(FontRole.Sardani, 64, 8, 512),
        };

        /// <summary>True when TMP Essential Resources are imported (TMP shaders and settings exist).</summary>
        public static bool TmpEssentialsPresent =>
            Shader.Find("TextMeshPro/Mobile/Distance Field") != null && AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        /// <summary>TMP's default font asset (LiberationSans SDF) when Essentials are imported, else null.</summary>
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (!TmpEssentialsPresent) return null;
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_Settings"))
                {
                    var s = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guid));
                    if (s != null) return TMP_Settings.defaultFontAsset;
                }
                return null;
            }
        }

        /// <summary>Asset path of the SDF font asset built for a TTF file.</summary>
        public static string FontAssetPath(string ttfFile) => UIAssetPaths.FontAssets + Path.GetFileNameWithoutExtension(ttfFile) + " SDF.asset";

        /// <summary>
        /// Builds (or reuses) the font asset of a role. <paramref name="rebuild"/> recreates an existing asset (new atlas).
        /// Returns null when the TTF is missing (logged). Materials come back through the out parameters.
        /// </summary>
        public static TMP_FontAsset Build(Spec spec, bool rebuild, StringBuilder log, out Material shadow, out Material glow)
        {
            shadow = null;
            glow = null;
            var file = UIAssetPaths.FontFile(spec.Role);
            if (file == null) return null;
            var ttfPath = UIAssetPaths.Fonts + file;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                log?.AppendLine("  " + spec.Role + ": MISSING " + ttfPath + " (manifest " + UIFonts.ManifestIdOf(spec.Role) + "); the role falls back.");
                return null;
            }
            EnsureIncludeFontData(ttfPath);
            BuilderUtil.EnsureFolder(UIAssetPaths.FontAssets.TrimEnd('/'));
            var path = FontAssetPath(file);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa != null && rebuild)
            {
                AssetDatabase.DeleteAsset(path);
                fa = null;
            }
            if (fa == null)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, spec.Sampling, spec.Padding, GlyphRenderMode.SDFAA, spec.Atlas, spec.Atlas,
                    AtlasPopulationMode.Dynamic, true);
                if (fa == null)
                {
                    log?.AppendLine("  " + spec.Role + ": TMP could not load " + ttfPath);
                    return null;
                }
                fa.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(fa, path);
                fa.atlasTextures[0].name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);

                var chars = spec.Role == FontRole.Sardani ? UIFonts.SardaniCharacters : UIFonts.RequiredCharacters;
                if (!fa.TryAddCharacters(chars, out var missing) && !string.IsNullOrEmpty(missing))
                    log?.AppendLine("  " + spec.Role + ": glyphs not in the font: " + Escape(missing));
                PersistAtlases(fa);

                // Static atlas: the TTF is not needed at runtime (TMP drops the source reference in the editor).
                fa.atlasPopulationMode = AtlasPopulationMode.Static;
                var fallback = DefaultFont;
                if (fallback != null && fallback != fa) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                EditorUtility.SetDirty(fa);
                log?.AppendLine("  " + spec.Role + ": built " + path + " (" + spec.Atlas + " atlas, sampling " + spec.Sampling + ", " +
                                fa.atlasTextureCount + " page(s))");
            }
            else log?.AppendLine("  " + spec.Role + ": kept " + path);

            shadow = SaveMaterial(fa, " Shadow.mat", UIFonts.ConfigureShadow);
            glow = SaveMaterial(fa, " Glow.mat", UIFonts.ConfigureGlow);
            return fa;
        }

        private static void PersistAtlases(TMP_FontAsset fa)
        {
            var textures = fa.atlasTextures;
            if (textures == null) return;
            for (var i = 0; i < textures.Length; i++)
            {
                var t = textures[i];
                if (t == null || AssetDatabase.Contains(t)) continue;
                t.name = fa.name + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(t, fa);
            }
        }

        private static Material SaveMaterial(TMP_FontAsset fa, string suffix, System.Action<Material> configure)
        {
            var path = UIAssetPaths.FontAssets + fa.name + suffix;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(fa.material) { name = fa.name + suffix.Replace(".mat", string.Empty) };
                configure(m);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = fa.material.shader;
                m.CopyPropertiesFromMaterial(fa.material);
                configure(m);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        private static void EnsureIncludeFontData(string ttfPath)
        {
            var importer = AssetImporter.GetAtPath(ttfPath) as TrueTypeFontImporter;
            if (importer == null || importer.includeFontData) return;
            importer.includeFontData = true;
            importer.SaveAndReimport();
        }

        private static string Escape(string s)
        {
            var b = new StringBuilder();
            foreach (var c in s) b.Append(c < 128 ? c.ToString() : "U+" + ((int)c).ToString("X4")).Append(' ');
            return b.ToString();
        }
    }
}
