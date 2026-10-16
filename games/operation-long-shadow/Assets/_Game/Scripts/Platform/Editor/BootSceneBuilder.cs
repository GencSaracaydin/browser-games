using System;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Platform.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/S_Boot.unity</c> from code (TECH_PLAN §5.1, D1): the only Build Settings scene,
    /// kept tiny because everything it references lands in the main .data file. Contents:
    /// <list type="bullet">
    /// <item><c>BootRoot</c>: SettingsService, DisplayInfo, TimeScaleService, InputLockService, PointerLockController,
    /// PauseService, QualityApplier, SectionStreamer, PoolService, HtmlClickGate, DevStart and Bootstrap, wired together;</item>
    /// <item><c>WebBridge</c> (root object with that exact name: the jslib sends messages to it);</item>
    /// <item><c>Input</c> with <c>LongShadow.Input.InputReader</c> (added by type name; Platform cannot reference Input);</item>
    /// <item><c>BootCamera</c>: black, culls nothing, renderer 1 (no SSAO), depth -100, no AudioListener (WP06 owns it);</item>
    /// <item>WP08's click gate prefab <c>Assets/_Game/Prefabs/Systems/PF_ClickGate.prefab</c> when it exists.</item>
    /// </list>
    /// No skybox, no lights, no libraries (those live in S_Persistent so their content stays in bundles). Re-running
    /// rebuilds the scene in place (same GUID).
    /// </summary>
    public static class BootSceneBuilder
    {
        /// <summary>Project path of S_Boot.</summary>
        public static readonly string ScenePath = Sections.ScenesFolder + "/" + Sections.BootScene + ".unity";

        /// <summary>Optional in-engine click gate from WP08 (instantiated into S_Boot when present).</summary>
        public const string ClickGatePrefabPath = "Assets/_Game/Prefabs/Systems/PF_ClickGate.prefab";

        /// <summary>Full type name of WP00's input reader.</summary>
        public const string InputReaderType = "LongShadow.Input.InputReader";

        /// <summary>Menu: (re)build S_Boot.</summary>
        [MenuItem("LongShadow/Platform/Build Boot Scene (S_Boot)", priority = 200)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            Build(log);
            Debug.Log("[LS:Platform] " + log);
        }

        /// <summary>Builds the scene; returns its path.</summary>
        public static string Build(StringBuilder log = null)
        {
            log = log ?? new StringBuilder();
            using (var scope = EditorScenes.OpenEmpty(ScenePath))
            {
                EditorScenes.NoSkybox(Color.black);

                var bridge = new GameObject(WebBridge.ObjectName);
                bridge.AddComponent<WebBridge>();

                var root = new GameObject("BootRoot");
                var settings = root.AddComponent<SettingsService>();
                var pointer = root.AddComponent<PointerLockController>();
                var display = root.AddComponent<DisplayInfo>();
                root.AddComponent<TimeScaleService>();
                root.AddComponent<InputLockService>();
                var pause = root.AddComponent<PauseService>();
                root.AddComponent<QualityApplier>();
                var streamer = root.AddComponent<SectionStreamer>();
                root.AddComponent<PoolService>();
                var gate = root.AddComponent<HtmlClickGate>();
                var devStart = root.AddComponent<DevStart>();
                var boot = root.AddComponent<Bootstrap>();
                _ = settings;

                var camera = CreateBootCamera();

                Wire(boot, "_streamer", streamer);
                Wire(boot, "_pointerLock", pointer);
                Wire(boot, "_pause", pause);
                Wire(boot, "_fallbackGate", gate);
                Wire(boot, "_devStart", devStart);
                Wire(boot, "_bootCamera", camera);
                Wire(pause, "_pointerLock", pointer);
                Wire(display, "_pointerLock", pointer);

                var input = new GameObject("Input");
                var readerType = FindType(InputReaderType, "Assets/_Game/Scripts/Input");
                if (readerType != null)
                {
                    input.AddComponent(readerType);
                    log.AppendLine("Input: " + InputReaderType);
                }
                else
                {
                    log.AppendLine("WARNING: " + InputReaderType + " not found; S_Boot has no input reader.");
                    LSLog.Warn(LogCategory.Platform, InputReaderType + " not found; S_Boot built without an input reader.");
                }

                var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClickGatePrefabPath);
                if (gatePrefab != null)
                {
                    PrefabUtility.InstantiatePrefab(gatePrefab, scope.Scene);
                    log.AppendLine("Click gate: " + ClickGatePrefabPath);
                }
                else
                {
                    log.AppendLine("Click gate: HTML fallback (" + ClickGatePrefabPath + " not found; WP08 provides it)");
                }
                log.AppendLine("Built " + ScenePath);
            }
            return ScenePath;
        }

        private static Camera CreateBootCamera()
        {
            var go = new GameObject("BootCamera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.depth = -100f;
            cam.useOcclusionCulling = false;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.SetRenderer(1); // PC_Renderer_RT: no SSAO, no decals (TECH_PLAN §3.5)
            return cam;
        }

        private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[LS:Platform] " + target.GetType().Name + " has no serialized field " + field);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Finds a MonoBehaviour type by full name through its MonoScript (editor only; no runtime reflection).</summary>
        internal static Type FindType(string fullName, string folder)
        {
            var shortName = fullName.Substring(fullName.LastIndexOf('.') + 1);
            var folders = string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ? null : new[] { folder };
            foreach (var guid in AssetDatabase.FindAssets(shortName + " t:MonoScript", folders))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                var type = script != null ? script.GetClass() : null;
                if (type != null && type.FullName == fullName) return type;
            }
            return null;
        }

        /// <summary>True when the scene file exists.</summary>
        public static bool Exists => File.Exists(Path.Combine(BuilderUtil.ProjectRoot, ScenePath));
    }
}
