using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Editor.Builders;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Kit;
using LongShadow.World.Level.Editor.Markup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace LongShadow.World.Level.Editor.Menu
{
    /// <summary>
    /// The LongShadow/Level menu (TECH_PLAN §7.15): build everything, one section at a time, the kit and review scenes,
    /// markup validation (open scenes or every section scene) and the missing-asset report. Every builder is re-runnable
    /// and rebuilds only the <c>LEVEL_Generated</c> root of its scene.
    /// </summary>
    public static class LevelBuildMenu
    {
        private const string Root = "LongShadow/Level/";

        /// <summary>Kit pieces, then S1–S9 in load order.</summary>
        [MenuItem(Root + "Build All (Kit + Sections S1-S9)", false, 1)]
        public static void BuildAll()
        {
            KitGenerator.BuildAll();
            var report = SectionBuilders.BuildAll();
            LSLog.Info(LogCategory.Level, "Level build:\n" + report);
        }

        /// <summary>All section scenes (no kit prefabs).</summary>
        [MenuItem(Root + "Build Sections S1-S9", false, 2)]
        public static void BuildSections() => LSLog.Info(LogCategory.Level, "Level build:\n" + SectionBuilders.BuildAll());

        [MenuItem(Root + "Sections/S1 Approach", false, 20)] private static void S1() => SectionBuilders.Build(SectionId.Approach);
        [MenuItem(Root + "Sections/S2 Market", false, 21)] private static void S2() => SectionBuilders.Build(SectionId.Market);
        [MenuItem(Root + "Sections/S3 Square", false, 22)] private static void S3() => SectionBuilders.Build(SectionId.Square);
        [MenuItem(Root + "Sections/S4 Roofs", false, 23)] private static void S4() => SectionBuilders.Build(SectionId.Roofs);
        [MenuItem(Root + "Sections/S5 Station Exterior", false, 24)] private static void S5() => SectionBuilders.Build(SectionId.StationExt);
        [MenuItem(Root + "Sections/S6 Station Interior", false, 25)] private static void S6() => SectionBuilders.Build(SectionId.StationInt);
        [MenuItem(Root + "Sections/S7 Pitch", false, 26)] private static void S7() => SectionBuilders.Build(SectionId.Pitch);
        [MenuItem(Root + "Sections/S8 Heli", false, 27)] private static void S8() => SectionBuilders.Build(SectionId.Heli);
        [MenuItem(Root + "Sections/S9 Town Far", false, 28)] private static void S9() => SectionBuilders.Build(SectionId.TownFar);

        [MenuItem(Root + "Kit/Build Kit Pieces", false, 100)] private static void Kit() => KitGenerator.BuildAll();
        [MenuItem(Root + "Kit/Build Kit Review Scene", false, 101)] private static void KitScene() => KitGenerator.BuildReviewScene();

        /// <summary>Validates the markup of every open section scene.</summary>
        [MenuItem(Root + "Markup/Validate Open Section Scenes", false, 200)]
        public static void ValidateOpen()
        {
            var table = MarkupIds.Load();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                var root = SectionScenes.FindRoot(scene);
                if (root == null) continue;
                MarkupValidator.Write(root.Section, MarkupValidator.Validate(scene, root.Section, table));
            }
        }

        /// <summary>Opens every section scene in turn and validates its markup; logs a summary.</summary>
        [MenuItem(Root + "Markup/Validate All Section Scenes", false, 201)]
        public static void ValidateAll()
        {
            /* no modal prompt: modal dialogs block the editor and the MCP bridge */ { for (int __i = 0; __i < UnityEngine.SceneManagement.SceneManager.sceneCount; __i++) { var __s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(__i); if (__s.isDirty && !string.IsNullOrEmpty(__s.path)) EditorSceneManager.SaveScene(__s); } }
            var table = MarkupIds.Load();
            var sb = new StringBuilder();
            var errors = 0;
            foreach (var id in SectionBuilders.Order)
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(BuilderUtil.ProjectRoot, Sections.ScenePath(id))))
                {
                    sb.AppendLine(Sections.SceneName(id) + ": scene missing (build it first)");
                    errors++;
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(Sections.ScenePath(id), OpenSceneMode.Single);
                var report = MarkupValidator.Validate(scene, id, table);
                MarkupValidator.Write(id, report);
                errors += report.Errors.Count;
                sb.AppendLine(Sections.SceneName(id) + ": " + report.Summary);
            }
            if (errors > 0) LSLog.Error(LogCategory.Level, "Markup validation:\n" + sb);
            else LSLog.Info(LogCategory.Level, "Markup validation:\n" + sb);
        }

        /// <summary>Opens the Markup Tools window.</summary>
        [MenuItem(Root + "Markup/Markup Tools", false, 202)]
        public static void OpenTools() => EditorWindow.GetWindow<MarkupToolsWindow>("Markup Tools");

        /// <summary>Logs the prop keys whose models were missing in this session (greybox stand-ins).</summary>
        [MenuItem(Root + "Report Missing Props", false, 300)]
        public static void MissingProps()
        {
            var sb = new StringBuilder();
            foreach (var k in LevelProps.MissingKeys) sb.Append(k).Append(' ');
            LSLog.Info(LogCategory.Level, sb.Length == 0 ? "No missing prop models this session." : "Greybox props (models missing): " + sb);
        }
    }
}
