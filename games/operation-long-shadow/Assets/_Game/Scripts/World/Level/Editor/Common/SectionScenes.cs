using System.IO;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.World.Level.Editor.Common
{
    /// <summary>
    /// Opens or creates a section scene (<see cref="Sections.ScenePath"/>) and manages its generated root: the builders own
    /// the <see cref="SectionRoot.GeneratedRootName"/> object and rebuild it from code; every other root object in the scene
    /// (WP09's insertion cameras, WP10 Look's bake rig and probes) survives a rebuild.
    /// </summary>
    public static class SectionScenes
    {
        /// <summary>Opens the section scene alone (creating it if missing).</summary>
        public static Scene OpenOrCreate(SectionId id)
        {
            var path = Sections.ScenePath(id);
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            if (File.Exists(Path.Combine(BuilderUtil.ProjectRoot, path)))
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        /// <summary>Destroys and recreates the generated root of the scene; returns it.</summary>
        public static SectionRoot ResetGeneratedRoot(Scene scene, SectionId id)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == SectionRoot.GeneratedRootName) Object.DestroyImmediate(go);
            var root = new GameObject(SectionRoot.GeneratedRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var sr = root.AddComponent<SectionRoot>();
            sr.Stamp(id, LevelBuildInfo.GeneratorVersion, System.DateTime.UtcNow.ToString("u"), string.Empty);
            return sr;
        }

        /// <summary>The generated root of an open scene, or null.</summary>
        public static SectionRoot FindRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var sr = go.GetComponent<SectionRoot>();
                if (sr != null) return sr;
            }
            return null;
        }

        /// <summary>Saves the scene.</summary>
        public static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
