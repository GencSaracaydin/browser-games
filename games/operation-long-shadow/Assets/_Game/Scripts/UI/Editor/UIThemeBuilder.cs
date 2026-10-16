using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.UI.Common;
using UnityEditor;
using UnityEngine;

namespace LongShadow.UI.Editor
{
    /// <summary>
    /// Fills <see cref="UIAssetPaths.Theme"/> from the imported art (TECH_PLAN §7.13: "WP11 creates the theme asset"; this
    /// builder is what WP11's editor holder runs): TMP font assets per role (<see cref="FontAssetBuilder"/>), sprite
    /// overrides from the manifest target paths with the 9-slice borders the import rows ask for, and the TMP default font
    /// as Fallback. Missing files keep the procedural stand-ins; re-run after more art lands. Idempotent.
    /// </summary>
    public static class UIThemeBuilder
    {
        /// <summary>Builds or updates the theme asset and returns it; the report goes to <paramref name="log"/>.</summary>
        public static UITheme Build(bool rebuildFonts, StringBuilder log)
        {
            log ??= new StringBuilder();
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(UIAssetPaths.Theme)?.Replace('\\', '/'));
            var theme = BuilderUtil.LoadOrCreateAsset<UITheme>(UIAssetPaths.Theme);

            log.AppendLine("Fonts:");
            if (!FontAssetBuilder.TmpEssentialsPresent)
            {
                log.AppendLine("  TMP Essential Resources are not imported: run LongShadow/UI/1 Import TMP Essential Resources first. " +
                               "Font slots are left as they are; the HUD uses a runtime font until then.");
            }
            else
            {
                foreach (var spec in FontAssetBuilder.Specs)
                {
                    var fa = FontAssetBuilder.Build(spec, rebuildFonts, log, out var shadow, out var glow);
                    theme.SetFont(spec.Role, fa, shadow, glow);
                }
                theme.SetFont(FontRole.Fallback, FontAssetBuilder.DefaultFont, null, null);
            }

            log.AppendLine("Sprites:");
            var imported = 0;
            for (var i = 0; i < UISpriteIds.Count; i++)
            {
                var id = (UISpriteId)i;
                var path = UIAssetPaths.SpritePath(id, out var manifestId);
                if (path == null)
                {
                    theme.SetSprite(id, null);
                    continue;
                }
                var sprite = LoadSprite(path, id, log, manifestId);
                theme.SetSprite(id, sprite);
                if (sprite != null) imported++;
            }
            log.AppendLine("  " + imported + " imported sprites; the rest are procedural.");

            for (var i = 0; i < UISpriteIds.TextureCount; i++)
            {
                var id = (UITextureId)i;
                var path = UIAssetPaths.TexturePath(id, out _);
                theme.SetTexture(id, path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null);
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            UIFonts.ClearCache();
            log.AppendLine("Theme saved: " + UIAssetPaths.Theme);
            return theme;
        }

        private static Sprite LoadSprite(string path, UISpriteId id, StringBuilder log, string manifestId)
        {
            if (!File.Exists(Path.Combine(BuilderUtil.ProjectRoot, path)))
            {
                log.AppendLine("  " + id + ": missing " + path + " (" + manifestId + ") -> procedural");
                return null;
            }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                log.AppendLine("  " + id + ": " + path + " is not a texture");
                return null;
            }
            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            var border = UIAssetPaths.BorderOf(id, false);
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                log.AppendLine("  " + id + ": import settings updated (sprite, border " + border + ")");
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) LSLog.Warn(LogCategory.UI, "UI theme: no sprite at " + path);
            return sprite;
        }
    }
}
