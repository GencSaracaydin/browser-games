using System.Collections.Generic;
using LongShadow.Cinematics.Booth;
using LongShadow.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// WP09 menu (LongShadow/Cinematics/...). Every item is also callable from Unity_RunCommand, e.g.
    /// <c>LongShadow.Cinematics.Editor.CinematicsMenu.BuildAll();</c>. Order for a fresh project: Build All (materials and
    /// profiles → timelines → library → systems prefab → booth scene → validate), then Bake Booth Lighting once the
    /// booth content has landed.
    /// </summary>
    public static class CinematicsMenu
    {
        private const string Root = "LongShadow/Cinematics/";

        /// <summary>Runs every builder in dependency order and validates. Returns the validation error count.</summary>
        [MenuItem(Root + "Build All", priority = 0)]
        public static int BuildAll()
        {
            BuildMaterials();
            BuildTimelines();
            BuildLibrary();
            BuildSystemsPrefab();
            BuildBoothScene();
            return Validate();
        }

        /// <summary>Materials for the WP09 shaders, FX volume profiles, the smoke material.</summary>
        [MenuItem(Root + "Build Materials And Profiles", priority = 20)]
        public static void BuildMaterials() => CinematicReport.Section("Materials and profiles", CinematicAssetsBuilder.Build());

        /// <summary>TL_Intro, TL_TitleCard, TL_MidB_Reel, TL_Climax, TL_Outro from the shot tables.</summary>
        [MenuItem(Root + "Build Timelines", priority = 21)]
        public static void BuildTimelines() => CinematicReport.Section("Timelines", CinematicTimelinesBuilder.Build());

        /// <summary>LIB_Cinematics.</summary>
        [MenuItem(Root + "Build Library", priority = 22)]
        public static int BuildLibrary()
        {
            var log = new List<string>();
            var missing = CinematicLibraryBuilder.Build(log);
            CinematicReport.Section("Library", log);
            return missing;
        }

        /// <summary>PF_CinematicSystems for S_Persistent.</summary>
        [MenuItem(Root + "Build Systems Prefab", priority = 23)]
        public static void BuildSystemsPrefab()
        {
            var path = CinematicSystemsPrefabBuilder.Build();
            CinematicReport.Section("Systems prefab", new[] { path + " (drop one instance into S_Persistent; WP11)" });
        }

        /// <summary>S0_Booth.</summary>
        [MenuItem(Root + "Build Booth Scene", priority = 24)]
        public static int BuildBoothScene() => BoothSceneBuilder.Build();

        /// <summary>Report of every check.</summary>
        [MenuItem(Root + "Validate", priority = 40)]
        public static int Validate() => CinematicsValidator.Validate();

        /// <summary>
        /// Bakes S0_Booth's lightmap, probes and reflection probe with only the booth open (TECH_PLAN D6/§3.8), then
        /// marks the booth as baked. Refuses when other open scenes have unsaved changes.
        /// </summary>
        [MenuItem(Root + "Bake Booth Lighting", priority = 60)]
        public static bool BakeBoothLighting()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty)
                {
                    LSLog.Error(LogCategory.Cinematics, "Bake Booth Lighting: save or discard the changes in '" + s.name + "' first.");
                    return false;
                }
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(CinematicPaths.BoothScene, OpenSceneMode.Single);
            var ok = Lightmapping.Bake();
            foreach (var root in scene.GetRootGameObjects())
            {
                var booth = root.GetComponent<BoothRoot>();
                if (booth == null) continue;
                booth.SetLightingBaked(ok);
                EditorUtility.SetDirty(booth);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            CinematicReport.Section("Booth bake", new[] { ok ? "baked" : "FAILED (see console)" });
            LSLog.Info(LogCategory.Cinematics, "Booth lighting bake " + (ok ? "done" : "failed") + ".");
            return ok;
        }

        /// <summary>Plays a sequence in play mode (testing aid).</summary>
        public static void PlayInPlayMode(string sequenceId)
        {
            if (!Application.isPlaying)
            {
                LSLog.Warn(LogCategory.Cinematics, "Enter play mode first.");
                return;
            }
            GameServices.Get<ICinematicService>()?.Play(sequenceId, () => LSLog.Info(LogCategory.Cinematics, sequenceId + " finished."));
        }

        [MenuItem(Root + "Play/Intro (play mode)", priority = 80)]
        private static void PlayIntro() => PlayInPlayMode(SequenceIds.Intro);

        [MenuItem(Root + "Play/MID-B (play mode)", priority = 81)]
        private static void PlayMidB() => PlayInPlayMode(SequenceIds.MidB);

        [MenuItem(Root + "Play/Climax (play mode)", priority = 82)]
        private static void PlayClimax() => PlayInPlayMode(SequenceIds.Climax);

        [MenuItem(Root + "Play/Outro (play mode)", priority = 83)]
        private static void PlayOutro() => PlayInPlayMode(SequenceIds.Outro);

        [MenuItem(Root + "Play/Insert I1 (play mode)", priority = 90)]
        private static void PlayI1() => GameServices.Get<IInsertService>()?.Play(SequenceIds.InsertI1);

        [MenuItem(Root + "Play/Insert I4 (play mode)", priority = 91)]
        private static void PlayI4() => GameServices.Get<IInsertService>()?.Play(SequenceIds.InsertI4);

        [MenuItem(Root + "Play/Death Insert (play mode)", priority = 92)]
        private static void PlayDeath() => GameServices.Get<IInsertService>()?.PlayDeath(() => LSLog.Info(LogCategory.Cinematics, "Death insert: reload now."));
    }
}
