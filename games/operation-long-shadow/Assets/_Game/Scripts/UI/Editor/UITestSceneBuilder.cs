using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.UI.Common;
using LongShadow.UI.Dev;
using LongShadow.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LongShadow.UI.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/Dev/UI_TestBed.unity</c>: a greybox yard with three squad stand-ins, two sentries,
    /// marker targets (one behind a wall for the through-walls check, one 3 m away for the 4 m hide rule), a free camera
    /// (<see cref="FakePlayerRig"/>), every fake service (<see cref="UiFakeServices"/>), the test-bed driver, PF_HUD and
    /// PF_ClickGate (or bare components when the prefabs do not exist yet). Press Play: the click gate fills and waits for
    /// a click, then F1 lists the keys and F2 runs the full showcase. Uses only Core and UI.
    /// </summary>
    public static class UITestSceneBuilder
    {
        /// <summary>Scene path.</summary>
        public const string ScenePath = "Assets/_Game/Scenes/Dev/UI_TestBed.unity";

        /// <summary>Builds and saves the scene (replacing an existing one).</summary>
        public static void Build(StringBuilder log)
        {
            // Build in a new additive scene so open (possibly unsaved) scenes are untouched and no dialog appears
            // (Unity_RunCommand cannot answer one). An open copy of the test bed itself is replaced.
            var previous = SceneManager.GetActiveScene();
            var existing = SceneManager.GetSceneByPath(ScenePath);
            Scene scene;
            if (existing.IsValid() && existing.isLoaded && SceneManager.sceneCount == 1)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                previous = default;
            }
            else
            {
                if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
            }
            try
            {
                Populate(scene, log);
                BuilderUtil.EnsureFolder("Assets/_Game/Scenes/Dev");
                EditorSceneManager.SaveScene(scene, ScenePath);
                log?.AppendLine("Saved " + ScenePath + " (Play: click the gate, F1 keys, F2 showcase).");
            }
            finally
            {
                if (previous.IsValid() && previous != scene)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void Populate(Scene scene, StringBuilder log)
        {

            // Light and ground.
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.82f, 0.62f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(12f, 258f, 0f); // GDD §9: low golden sun from the west
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.38f, 0.33f);

            var world = new GameObject("World").transform;
            BuilderUtil.PlaceholderPrimitive("Ground", PrimitiveType.Cube, new Vector3(240f, 0.2f, 240f), new Color(0.72f, 0.62f, 0.46f), world, true)
                .transform.position = new Vector3(0f, -0.1f, 60f);
            Block(world, "Wall_A", new Vector3(18f, 3f, 30f), new Vector3(10f, 6f, 1f));
            Block(world, "House_B", new Vector3(-20f, 4f, 50f), new Vector3(10f, 8f, 10f));
            Block(world, "House_C", new Vector3(30f, 5f, 70f), new Vector3(12f, 10f, 14f));
            Block(world, "Tower", new Vector3(-40f, 15f, 120f), new Vector3(5f, 30f, 5f));
            Block(world, "Arch", new Vector3(0f, 4f, 62f), new Vector3(8f, 8f, 2f));

            // Player rig with a camera.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, 0f);
            var rig = player.AddComponent<FakePlayerRig>();
            var camGo = new GameObject("Camera");
            camGo.tag = Tags.MainCamera;
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = MathX.HorizontalToVerticalFov(90f, 16f / 9f); // GDD §11 default FOV 90 horizontal
            cam.backgroundColor = new Color(0.86f, 0.72f, 0.52f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            rig.SetCamera(cam);

            // Squad and sentries.
            var actors = new GameObject("Actors").transform;
            var hollis = Actor("Hollis", Placeholders.Friendly, actors);
            hollis.transform.position = new Vector3(-3f, 0f, 9f);
            var sparrow = Actor("Sparrow", Placeholders.Friendly, actors);
            sparrow.transform.position = new Vector3(3.5f, 0f, 12f);
            var tinker = Actor("Tinker", Placeholders.Friendly, actors);
            tinker.transform.position = new Vector3(0.5f, 0f, 17f);
            var sentryA = Actor("Sentry_E2-01", Placeholders.Hostile, actors);
            sentryA.transform.position = new Vector3(-10f, 0f, 38f);
            var sentryB = Actor("Sentry_E2-02", Placeholders.Hostile, actors);
            sentryB.transform.position = new Vector3(8f, 0f, 44f);

            // Marker targets.
            var targets = new GameObject("MarkerTargets").transform;
            var arch = Target(targets, "Arch", new Vector3(0f, 1f, 62f));
            var technical = Target(targets, "Technical (behind Wall_A)", new Vector3(18f, 1f, 34f));
            var poi = Target(targets, "POI (tower)", new Vector3(-40f, 25f, 120f));
            var intel = Target(targets, "Intel (3 m)", new Vector3(1.5f, 1f, 2.5f));
            var ring = Target(targets, "DrillRing", new Vector3(7f, 0f, 22f));

            // Services, driver, HUD and gate.
            var bed = new GameObject("TestBed");
            var fakes = bed.AddComponent<UiFakeServices>();
            fakes.Configure(hollis.transform, sparrow.transform, tinker.transform, sentryA.transform, sentryB.transform);
            var driver = bed.AddComponent<UiTestBedDriver>();
            driver.Configure(fakes, new[] { arch, technical, poi, intel }, ring, player.transform);

            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.HudPrefabPath);
            if (hudPrefab != null) PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            else
            {
                var hud = new GameObject("HUD (no prefab)").AddComponent<HudRoot>();
                var so = new SerializedObject(hud);
                so.FindProperty("_theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UITheme>(UIAssetPaths.Theme);
                so.ApplyModifiedPropertiesWithoutUndo();
                log?.AppendLine("PF_HUD not built yet: the scene uses a bare HudRoot.");
            }
            var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.GatePrefabPath);
            if (gatePrefab != null) PrefabUtility.InstantiatePrefab(gatePrefab, scene);
            else
            {
                new GameObject("ClickGate (no prefab)").AddComponent<Menus.ClickGateView>();
                log?.AppendLine("PF_ClickGate not built yet: the scene uses a bare ClickGateView.");
            }

        }

        private static void Block(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Cube, size, new Color(0.78f, 0.7f, 0.58f), parent, true);
            go.transform.position = center;
            go.layer = LayerIds.Default;
        }

        private static GameObject Actor(string name, Color color, Transform parent)
        {
            // Placeholders.Humanoid uses runtime materials; swap them for saved placeholder materials so the scene keeps them.
            var go = Placeholders.Humanoid(name, color, parent);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = BuilderUtil.PlaceholderMaterial(r.name.EndsWith("_Visor", System.StringComparison.Ordinal) ? Color.black : color);
            return go;
        }

        private static Transform Target(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            return t;
        }

        /// <summary>True when the active scene is the test bed.</summary>
        public static bool IsOpen => SceneManager.GetActiveScene().path == ScenePath;
    }
}
