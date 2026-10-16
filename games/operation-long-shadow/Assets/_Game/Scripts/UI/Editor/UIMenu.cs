using System;
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
    /// Menu items of the UI package (LongShadow/UI/...). Each writes a report to <c>Logs/ui_build.txt</c>. The integrator
    /// runs them in order: 1 Import TMP Essential Resources (once), 2 Build Theme, 3 Build Prefabs, 4 Build UI Test Scene;
    /// "Build All" runs 2-4. All are callable from <c>Unity_RunCommand</c> through the public static methods.
    /// </summary>
    public static class UIMenu
    {
        private const string Root = "LongShadow/UI/";
        /// <summary>Report file.</summary>
        public const string ReportPath = "Logs/ui_build.txt";

        /// <summary>Imports TMP Essential Resources (shaders, LiberationSans SDF, TMP Settings) without a dialog.</summary>
        [MenuItem(Root + "1 Import TMP Essential Resources", priority = 1)]
        public static void ImportTmpEssentials()
        {
            if (FontAssetBuilder.TmpEssentialsPresent)
            {
                Write("TMP Essential Resources already imported.");
                return;
            }
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            Write("TMP Essential Resources import started; wait for the import to finish, then run 2 Build Theme.");
        }

        /// <summary>Builds or updates the theme (fonts + sprites).</summary>
        [MenuItem(Root + "2 Build Theme (fonts + sprites)", priority = 2)]
        public static void BuildTheme() => Run("Build Theme", log => UIThemeBuilder.Build(false, log));

        /// <summary>Recreates every TMP font asset (new atlases), then the theme.</summary>
        [MenuItem(Root + "2b Rebuild Font Assets", priority = 3)]
        public static void RebuildFonts() => Run("Rebuild Fonts", log => UIThemeBuilder.Build(true, log));

        /// <summary>Builds PF_HUD and PF_ClickGate.</summary>
        [MenuItem(Root + "3 Build Prefabs (HUD + Click Gate)", priority = 4)]
        public static void BuildPrefabs() => Run("Build Prefabs", HudBuilder.BuildAll);

        /// <summary>Builds the UI test bed scene.</summary>
        [MenuItem(Root + "4 Build UI Test Scene", priority = 5)]
        public static void BuildTestScene() => Run("Build UI Test Scene", UITestSceneBuilder.Build);

        /// <summary>Theme, prefabs and test scene in one go.</summary>
        [MenuItem(Root + "Build All", priority = 20)]
        public static void BuildAll() => Run("Build All", log =>
        {
            UIThemeBuilder.Build(false, log);
            HudBuilder.BuildAll(log);
            UITestSceneBuilder.Build(log);
            var problems = HudLayoutAudit.Run(AssetDatabase.LoadAssetAtPath<UITheme>(UIAssetPaths.Theme), log);
            if (problems.Count > 0) LSLog.Warn(LogCategory.UI, "HUD layout audit: " + problems.Count + " problem(s), see " + ReportPath);
        });

        /// <summary>Checks the HUD for overlaps at four screen sizes.</summary>
        [MenuItem(Root + "Run Layout Audit", priority = 30)]
        public static void LayoutAudit() => Run("Layout Audit", log =>
        {
            var problems = HudLayoutAudit.Run(AssetDatabase.LoadAssetAtPath<UITheme>(UIAssetPaths.Theme), log);
            if (problems.Count > 0) LSLog.Warn(LogCategory.UI, "HUD layout audit: " + problems.Count + " problem(s), see " + ReportPath);
            else LSLog.Info(LogCategory.UI, "HUD layout audit: PASS");
        });

        /// <summary>Writes every procedural sprite as a PNG to Logs/ui_procedural/ for review.</summary>
        [MenuItem(Root + "Export Procedural Sprites (review)", priority = 31)]
        public static void ExportProcedural() => Run("Export Procedural Sprites", log =>
        {
            var dir = Path.Combine(BuilderUtil.ProjectRoot, "Logs/ui_procedural");
            Directory.CreateDirectory(dir);
            for (var i = 0; i < UISpriteIds.Count; i++)
            {
                var id = (UISpriteId)i;
                if (UISpriteIds.IsOptional(id)) continue;
                var tex = UISprites.PaintSprite(id);
                File.WriteAllBytes(Path.Combine(dir, "UI_" + id + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            for (var i = 0; i < UISpriteIds.TextureCount; i++)
            {
                var tex = UISprites.PaintTexture((UITextureId)i);
                File.WriteAllBytes(Path.Combine(dir, "UI_" + (UITextureId)i + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            log.AppendLine("Wrote " + dir);
        });

        private static void Run(string title, Action<StringBuilder> body)
        {
            var log = new StringBuilder();
            log.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + title);
            try
            {
                body(log);
            }
            catch (Exception e)
            {
                log.AppendLine("EXCEPTION: " + e);
                LSLog.Exception(LogCategory.UI, e);
            }
            Write(log.ToString());
        }

        private static void Write(string text)
        {
            var path = Path.Combine(BuilderUtil.ProjectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.AppendAllText(path, text + Environment.NewLine);
            LSLog.Info(LogCategory.UI, text);
        }
    }
}
