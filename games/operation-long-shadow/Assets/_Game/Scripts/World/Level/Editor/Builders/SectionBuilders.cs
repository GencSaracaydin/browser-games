using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Level.Editor.Common;
using LongShadow.World.Level.Editor.Markup;
using LongShadow.World.Level.Editor.Nav;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace LongShadow.World.Level.Editor.Builders
{
    /// <summary>
    /// Runs the section builders (TECH_PLAN §7.15): opens or creates <c>Assets/_Game/Scenes/Sections/&lt;S#&gt;.unity</c>,
    /// rebuilds its <c>LEVEL_Generated</c> root (everything else in the scene survives), places the markup, bakes the
    /// NavMesh, validates the markup and saves. Logs to <c>Logs/level_build_&lt;scene&gt;.txt</c>.
    /// </summary>
    public static class SectionBuilders
    {
        private static readonly Dictionary<SectionId, SectionBuilderBase> s_builders = new Dictionary<SectionId, SectionBuilderBase>
        {
            { SectionId.Approach, new S1ApproachBuilder() },
            { SectionId.Market, new S2MarketBuilder() },
            { SectionId.Square, new S3SquareBuilder() },
            { SectionId.Roofs, new S4RoofsBuilder() },
            { SectionId.StationExt, new S5StationExtBuilder() },
            { SectionId.StationInt, new S6StationIntBuilder() },
            { SectionId.Pitch, new S7PitchBuilder() },
            { SectionId.Heli, new S8HeliBuilder() },
            { SectionId.TownFar, new S9TownFarBuilder() },
        };

        /// <summary>The sections with builders, in build order.</summary>
        public static readonly SectionId[] Order =
        {
            SectionId.Approach, SectionId.Market, SectionId.Square, SectionId.Roofs, SectionId.StationExt, SectionId.StationInt,
            SectionId.Pitch, SectionId.Heli, SectionId.TownFar,
        };

        /// <summary>Builds one section scene; returns the one-line report.</summary>
        public static string Build(SectionId id)
        {
            if (!s_builders.TryGetValue(id, out var builder))
            {
                LSLog.Warn(LogCategory.Level, "No builder for " + id);
                return "no builder";
            }
            /* no modal prompt: modal dialogs block the editor and the MCP bridge */ { for (int __i = 0; __i < UnityEngine.SceneManagement.SceneManager.sceneCount; __i++) { var __s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(__i); if (__s.isDirty && !string.IsNullOrEmpty(__s.path)) EditorSceneManager.SaveScene(__s); } }
            var table = MarkupIds.Load();
            var scene = SectionScenes.OpenOrCreate(id);
            var root = SectionScenes.ResetGeneratedRoot(scene, id);
            var ctx = new SectionBuildContext(id, scene, root, table);
            try
            {
                EditorUtility.DisplayProgressBar("LongShadow Level", Sections.SceneName(id) + ": geometry", 0.1f);
                builder.Geometry(ctx);
                DecalPlacer.Place(ctx);
                ReflectionProbePlacer.Place(ctx);
                ctx.Flush();
                EditorUtility.DisplayProgressBar("LongShadow Level", Sections.SceneName(id) + ": markup", 0.5f);
                MarkupPlacer.Place(ctx);
                VolumePlacer.Place(ctx);
                builder.Wire(ctx);
                ctx.Flush();
                EditorUtility.DisplayProgressBar("LongShadow Level", Sections.SceneName(id) + ": NavMesh", 0.7f);
                var links = SectionNavBaker.Bake(ctx);
                builder.AfterNav(ctx, links);
                EditorUtility.DisplayProgressBar("LongShadow Level", Sections.SceneName(id) + ": validate", 0.9f);
                var report = MarkupValidator.Validate(scene, id, table);
                MarkupValidator.Write(id, report);
                ctx.Info("Validation: " + report.Summary);
                foreach (var e in report.Errors) ctx.Info("  ERROR " + e);
                var line = ctx.Finish();
                SectionScenes.Save(scene);
                LSLog.Info(LogCategory.Level, "Built " + Sections.SceneName(id) + ": " + line + "; markup " + report.Summary);
                return line;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Builds every section in order; returns a multi-line report.</summary>
        public static string BuildAll()
        {
            LevelMaterials.Reload();
            LevelProps.Reload();
            AssetManifest.Reload();
            var sb = new System.Text.StringBuilder();
            foreach (var id in Order) sb.AppendLine(Sections.SceneName(id) + ": " + Build(id));
            return sb.ToString();
        }
    }
}
