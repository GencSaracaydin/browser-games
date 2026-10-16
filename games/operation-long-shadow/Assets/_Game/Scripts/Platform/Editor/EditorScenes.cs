using System;
using System.IO;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Scene plumbing for the Platform builders: opens (or creates) a scene additively without touching the scenes the
    /// user has open, empties it, makes it active while the builder adds objects, then saves and closes it. Never shows
    /// a dialog (builders run inside Unity_RunCommand).
    /// </summary>
    internal sealed class EditorScenes : IDisposable
    {
        private readonly Scene _previousActive;
        private readonly bool _close;

        /// <summary>The scene being built.</summary>
        internal Scene Scene { get; }

        private EditorScenes(Scene scene, bool close, Scene previousActive)
        {
            Scene = scene;
            _close = close;
            _previousActive = previousActive;
        }

        /// <summary>Opens <paramref name="path"/> for rebuilding (created when missing; GUID kept when it exists) and clears it.</summary>
        internal static EditorScenes OpenEmpty(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave play mode before running a scene builder.");
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            var close = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (File.Exists(Path.Combine(BuilderUtil.ProjectRoot, path)))
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    close = true;
                }
                else
                {
                    scene = NewSceneAt(path, ref previous, out close);
                }
            }
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.SetActiveScene(scene);
            return new EditorScenes(scene, close, previous);
        }

        /// <summary>Saves the scene, restores the previously active scene and closes the builder's scene if it opened it.</summary>
        public void Dispose()
        {
            EditorSceneManager.MarkSceneDirty(Scene);
            if (!EditorSceneManager.SaveScene(Scene)) Debug.LogError("[LS:Platform] Saving " + Scene.path + " failed");
            if (_previousActive.IsValid() && _previousActive.isLoaded && _previousActive != Scene) SceneManager.SetActiveScene(_previousActive);
            if (_close && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(Scene, true);
        }

        private static Scene NewSceneAt(string path, ref Scene previous, out bool close)
        {
            // An untitled scene blocks additive creation; replace it when it holds nothing worth keeping.
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(s.path)) continue;
                if (s.isDirty) throw new InvalidOperationException("Save or discard the untitled scene before running a scene builder.");
                var single = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(single, path);
                previous = single;
                close = false;
                return single;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, path);
            close = true;
            return scene;
        }

        /// <summary>Lighting defaults for scenes that must not pull the built-in skybox into a build (RenderSettings of the active scene).</summary>
        internal static void NoSkybox(Color ambient)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;
            RenderSettings.sun = null;
        }
    }
}
