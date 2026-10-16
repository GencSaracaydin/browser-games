using System;
using System.Collections.Generic;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.Player.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LongShadow.Player.Editor
{
    /// <summary>
    /// Builds the WP02 movement test course <c>Assets/_Game/Scenes/Dev/Dev_PlayerCourse.unity</c> (TECH_PLAN §11.4 WP02 DoD)
    /// from code, greybox only:
    /// <list type="bullet">
    /// <item>Lane A (x 0): 60 m speed lane over gravel / stone / wood / metal strips with timed gates
    /// <c>DEV.Gate.A</c> → <c>DEV.Gate.B</c> (30 m) for walk / ADS / crouch / sprint speeds.</item>
    /// <item>Lane B (x 10): ledges 0.6 / 1.0 / 1.4 (auto-vault), 1.8 (climb on Space), 2.2 m (too high), and a 1.0 m
    /// thin wall (vault over).</item>
    /// <item>Lane C (x 20): stairs to a 2 m platform, a 2 m gap, another 2 m platform, then a 2 m gap with a 2 m rise
    /// (air mantle), then a 4 m drop (no damage).</item>
    /// <item>Lane D (x 32): a 6 m tower with a ladder (LadderVolume), a 4.5 m step-down (no damage) and a 6 m drop
    /// (45 HP, GDD §12).</item>
    /// <item>Lane E (x 44): ramps of 20°, 35°, 45° (walkable) and 50° (too steep), and 0.3 m stairs.</item>
    /// <item>Lane F (x 54): a 1.3 m crouch tunnel (un-crouch clearance) and a 1.2 m slide bar.</item>
    /// <item>Pads (row at z -8, west of the spawn): damage, difficulty cycle, binoculars, kill, forced walk, mount;
    /// two interaction crates (press / hold) east of the spawn; an out-of-bounds volume in front of a PlayerClip wall
    /// (x -44); three glassing POIs on pillars far north.</item>
    /// </list>
    /// The player is a PF_Player instance (built first if missing) with <see cref="MovementTelemetry"/> and
    /// <see cref="PlayerDevHarness"/>; an InputReader (WP00 Input) is added by type lookup because Player may not
    /// reference Input. A low sun (25°, azimuth 258°) makes the shadow proxy readable. The scene is never added to
    /// Build Settings (D1).
    /// </summary>
    public static class PlayerCourseBuilder
    {
        /// <summary>Scene path.</summary>
        public const string ScenePath = "Assets/_Game/Scenes/Dev/Dev_PlayerCourse.unity";

        private static readonly Color Sand = new Color(0.76f, 0.66f, 0.48f);
        private static readonly Color Gravel = new Color(0.55f, 0.53f, 0.50f);
        private static readonly Color Stone = new Color(0.64f, 0.61f, 0.56f);
        private static readonly Color Wood = new Color(0.45f, 0.32f, 0.20f);
        private static readonly Color Metal = new Color(0.36f, 0.39f, 0.41f);
        private static readonly Color Tile = new Color(0.72f, 0.70f, 0.64f);
        private static readonly Color Block = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color Warn = new Color(0.80f, 0.30f, 0.20f);
        private static readonly Color PadBlue = new Color(0.25f, 0.45f, 0.80f);
        private static readonly Color PadGreen = new Color(0.30f, 0.70f, 0.35f);
        private static readonly Color PadAmber = new Color(0.90f, 0.65f, 0.20f);

        [MenuItem("LongShadow/Player/Build Movement Test Course", priority = 210)]
        private static void BuildMenu() => Build(false);

        [MenuItem("LongShadow/Player/Open Movement Test Course", priority = 211)]
        private static void OpenMenu() => Open();

        /// <summary>
        /// Builds and saves the course. The scene is created additively and closed again unless <paramref name="keepOpen"/>
        /// (when the only open scene is untitled and clean, it is replaced instead). Refuses to run when an untitled scene
        /// has unsaved changes (never shows a dialog). Returns the scene path or null.
        /// </summary>
        public static string Build(bool keepOpen)
        {
            var hasUntitled = false;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(s.path))
                {
                    if (s.isDirty)
                    {
                        LSLog.Error(LogCategory.Player, "Build Movement Test Course: save or discard the untitled scene first (no dialogs are shown).");
                        return null;
                    }
                    hasUntitled = true;
                }
                if (s.path == ScenePath)
                {
                    if (s.isDirty)
                    {
                        LSLog.Error(LogCategory.Player, "Build Movement Test Course: " + ScenePath + " is open with unsaved changes; save or close it first.");
                        return null;
                    }
                    if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(s, true);
                    else hasUntitled = true; // it is the only scene: replace it
                }
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath) == null) PlayerPrefabBuilder.Build();

            var previous = SceneManager.GetActiveScene();
            var mode = hasUntitled ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                Populate(scene);
                BuilderUtil.EnsureFolder("Assets/_Game/Scenes/Dev");
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    LSLog.Error(LogCategory.Player, "Saving " + ScenePath + " failed.");
                    return null;
                }
                LSLog.Info(LogCategory.Player, "Movement test course saved: " + ScenePath);
                return ScenePath;
            }
            catch (Exception e)
            {
                LSLog.Exception(LogCategory.Player, e);
                return null;
            }
            finally
            {
                if (mode == NewSceneMode.Additive && !keepOpen && scene.IsValid())
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>Opens the course as the only scene (refuses when an open scene has unsaved changes). Builds it first if missing.</summary>
        public static void Open()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                LSLog.Error(LogCategory.Player, "Open Movement Test Course: save the open scenes first (no dialogs are shown).");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && Build(false) == null) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------ content

        private static void Populate(Scene scene)
        {
            var course = Group("Course");
            var pads = Group("Pads");

            // Sun: elevation 25°, azimuth 258° (TECH_PLAN §3.8 convention: rotation (elevation, 78°, 0)).
            var sunGo = new GameObject("SUN");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 2.2f;
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.renderingLayerMask = (int)RenderingLayerIds.WorldMask | (int)RenderingLayerIds.ViewmodelMask | (int)RenderingLayerIds.CabinMask;
            sunGo.transform.rotation = Quaternion.Euler(25f, 78f, 0f);
            RenderSettings.sun = sun;

            // Ground (sand) and the systems the rig expects.
            Box(course, "Ground", new Vector3(10f, -0.5f, 55f), new Vector3(150f, 1f, 190f), Sand, SurfaceType.Sand);
            var systems = new GameObject("Dev_Systems");
            AddInputReader(systems);
            // The game's only AudioListener belongs to WP06's AudioService (S_Persistent); this dev scene has none, so a
            // fixed listener keeps Unity quiet. It is not on the rig.
            systems.AddComponent<AudioListener>();

            BuildSpeedLane(course);
            BuildLedgeLane(course);
            BuildGapLane(course);
            BuildLadderLane(course);
            BuildRampLane(course);
            BuildCrouchLane(course);
            BuildBounds(course);
            BuildPois(course);
            BuildPads(pads);
            BuildInteractions(pads);

            // Spawn and player.
            var spawn = new GameObject("DEV.Spawn");
            spawn.transform.SetPositionAndRotation(new Vector3(0f, 0f, -8f), Quaternion.identity);
            spawn.AddComponent<SceneMarker>().SetId(PlayerDevHarness.SpawnId);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
            GameObject player;
            if (prefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            }
            else
            {
                LSLog.Warn(LogCategory.Player, "PF_Player is missing; the course uses an unsaved factory rig.");
                player = PlayerRigFactory.Build(spawn.transform.position, 0f).gameObject;
            }
            if (player.GetComponent<MovementTelemetry>() == null) player.AddComponent<MovementTelemetry>();
            if (player.GetComponent<PlayerDevHarness>() == null) player.AddComponent<PlayerDevHarness>();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildSpeedLane(Transform parent)
        {
            var lane = Group("A_SpeedLane", parent);
            var surfaces = new[] { SurfaceType.Gravel, SurfaceType.Stone, SurfaceType.Wood, SurfaceType.Metal };
            var colors = new[] { Gravel, Stone, Wood, Metal };
            for (var i = 0; i < 4; i++)
                Box(lane, "Strip_" + surfaces[i], new Vector3(0f, 0.01f, 7.5f + 15f * i), new Vector3(4f, 0.02f, 15f), colors[i], surfaces[i]);
            Trigger<TriggerVolume>(lane, PlayerDevHarness.GateA, new Vector3(0f, 1f, 12f), new Vector3(6f, 2.5f, 0.4f));
            Trigger<TriggerVolume>(lane, PlayerDevHarness.GateB, new Vector3(0f, 1f, 42f), new Vector3(6f, 2.5f, 0.4f));
            foreach (var z in new[] { 12f, 42f })
            {
                Visual(lane, "GatePost_L", new Vector3(-3.2f, 1.25f, z), new Vector3(0.15f, 2.5f, 0.15f), PadAmber);
                Visual(lane, "GatePost_R", new Vector3(3.2f, 1.25f, z), new Vector3(0.15f, 2.5f, 0.15f), PadAmber);
            }
        }

        private static void BuildLedgeLane(Transform parent)
        {
            var lane = Group("B_Ledges", parent);
            var heights = new[] { 0.6f, 1.0f, 1.4f, 1.8f, 2.2f };
            for (var i = 0; i < heights.Length; i++)
            {
                var h = heights[i];
                Box(lane, "Ledge_" + h.ToString("0.0") + "m", new Vector3(10f, h * 0.5f, 6f + 10f * i), new Vector3(3f, h, 2f), h > 2.1f ? Warn : Block, SurfaceType.Concrete);
            }
            Box(lane, "ThinWall_1.0m", new Vector3(10f, 0.5f, 56f), new Vector3(3f, 1f, 0.25f), Block, SurfaceType.Plaster);
        }

        private static void BuildGapLane(Transform parent)
        {
            var lane = Group("C_Gaps", parent);
            const float x = 20f;
            for (var i = 0; i < 8; i++)
            {
                var h = 0.25f * (i + 1);
                Box(lane, "Step_" + i, new Vector3(x, h * 0.5f, 0.2f + 0.4f * i), new Vector3(4f, h, 0.4f), Stone, SurfaceType.Stone);
            }
            Box(lane, "P1_2m", new Vector3(x, 1f, 7.6f), new Vector3(4f, 2f, 8.8f), Block, SurfaceType.Plaster);         // z 3.2..12
            Box(lane, "P2_2m_after_2m_gap", new Vector3(x, 1f, 18f), new Vector3(4f, 2f, 8f), Block, SurfaceType.Plaster); // z 14..22
            Box(lane, "P3_4m_after_2m_gap_2m_rise", new Vector3(x, 2f, 28f), new Vector3(4f, 4f, 8f), Block, SurfaceType.Plaster); // z 24..32, air mantle
        }

        private static void BuildLadderLane(Transform parent)
        {
            var lane = Group("D_LadderAndDrops", parent);
            const float x = 32f;
            Box(lane, "Tower_6m", new Vector3(x, 3f, 6f), new Vector3(4f, 6f, 4f), Block, SurfaceType.Concrete);                 // z 4..8
            Box(lane, "StepDown_1.5m (4.5 m drop)", new Vector3(x, 0.75f, 10f), new Vector3(4f, 1.5f, 4f), Stone, SurfaceType.Stone); // z 8..12

            // Ladder on the south face (z = 4): its forward axis points away from the wall, toward the climber.
            var ladder = Trigger<LadderVolume>(lane, "DEV.Ladder", new Vector3(x, 3.1f, 3.55f), new Vector3(1.2f, 6.4f, 0.9f));
            ladder.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var bottom = new GameObject("Bottom").transform;
            bottom.SetParent(ladder.transform, false);
            bottom.position = new Vector3(x, 0f, 3.45f);
            var top = new GameObject("Top").transform;
            top.SetParent(ladder.transform, false);
            top.position = new Vector3(x, 6f, 4.7f);
            ladder.Configure(bottom, top, 2.2f); // TECH_PLAN §7.3: 2.2 m/s
            for (var side = -1; side <= 1; side += 2)
                Visual(lane, "LadderRail", new Vector3(x + 0.25f * side, 3f, 3.93f), new Vector3(0.05f, 6f, 0.05f), Wood);
            for (var i = 0; i < 18; i++)
                Visual(lane, "Rung_" + i, new Vector3(x, 0.3f + 0.33f * i, 3.93f), new Vector3(0.5f, 0.04f, 0.04f), Wood);
        }

        private static void BuildRampLane(Transform parent)
        {
            var lane = Group("E_Ramps", parent);
            const float x = 44f;
            var angles = new[] { 20f, 35f, 45f, 50f };
            var z0 = 0f;
            foreach (var a in angles)
            {
                var run = 2f / Mathf.Tan(a * Mathf.Deg2Rad);
                var length = 2f / Mathf.Sin(a * Mathf.Deg2Rad);
                var rad = a * Mathf.Deg2Rad;
                var normal = new Vector3(0f, Mathf.Cos(rad), -Mathf.Sin(rad));
                var mid = new Vector3(x, 1f, z0 + run * 0.5f) - normal * 0.15f;
                var ramp = Box(lane, "Ramp_" + a.ToString("0") + "deg" + (a > 45f ? "_TooSteep" : ""), mid, new Vector3(3f, 0.3f, length + 0.1f), a > 45f ? Warn : Tile, SurfaceType.Tile);
                ramp.transform.rotation = Quaternion.Euler(-a, 0f, 0f);
                Box(lane, "RampTop_" + a.ToString("0"), new Vector3(x, 1f, z0 + run + 1.5f), new Vector3(3f, 2f, 3f), Block, SurfaceType.Concrete);
                z0 += run + 3f + 6f;
            }
            for (var i = 0; i < 6; i++)
            {
                var h = 0.3f * (i + 1);
                Box(lane, "Stair_" + i, new Vector3(x, h * 0.5f, z0 + 0.175f + 0.35f * i), new Vector3(3f, h, 0.35f), Stone, SurfaceType.Stone);
            }
        }

        private static void BuildCrouchLane(Transform parent)
        {
            var lane = Group("F_CrouchAndSlide", parent);
            const float x = 54f;
            Box(lane, "Tunnel_WallL", new Vector3(x - 1.6f, 0.9f, 14f), new Vector3(0.2f, 1.8f, 8f), Block, SurfaceType.Concrete);
            Box(lane, "Tunnel_WallR", new Vector3(x + 1.6f, 0.9f, 14f), new Vector3(0.2f, 1.8f, 8f), Block, SurfaceType.Concrete);
            Box(lane, "Tunnel_Roof_1.3m", new Vector3(x, 1.55f, 14f), new Vector3(3.4f, 0.5f, 8f), Block, SurfaceType.Concrete); // underside at 1.3 m
            Box(lane, "SlideBar_1.2m", new Vector3(x, 1.35f, 34f), new Vector3(3.4f, 0.3f, 0.3f), Warn, SurfaceType.Metal);       // underside at 1.2 m
            Box(lane, "SlideBar_PostL", new Vector3(x - 1.8f, 0.75f, 34f), new Vector3(0.2f, 1.5f, 0.3f), Metal, SurfaceType.Metal);
            Box(lane, "SlideBar_PostR", new Vector3(x + 1.8f, 0.75f, 34f), new Vector3(0.2f, 1.5f, 0.3f), Metal, SurfaceType.Metal);
        }

        private static void BuildBounds(Transform parent)
        {
            var g = Group("G_OutOfBounds", parent);
            Trigger<OutOfBoundsVolume>(g, "DEV.OOB", new Vector3(-44f, 2f, 30f), new Vector3(8f, 4f, 40f));
            Visual(g, "OOB_Floor", new Vector3(-44f, 0.03f, 30f), new Vector3(8f, 0.02f, 40f), Warn);
            var clip = new GameObject("PlayerClip_Wall");
            clip.transform.SetParent(g, false);
            clip.transform.position = new Vector3(-48.25f, 3f, 30f);
            clip.layer = LayerIds.PlayerClip;
            clip.AddComponent<BoxCollider>().size = new Vector3(0.5f, 6f, 40f);
        }

        private static void BuildPois(Transform parent)
        {
            var g = Group("POIs", parent);
            var pois = new[] { new Vector3(-30f, 12f, 110f), new Vector3(25f, 18f, 140f), new Vector3(70f, 8f, 100f) };
            for (var i = 0; i < pois.Length; i++)
            {
                var p = pois[i];
                Box(g, "Pillar_" + (i + 1), new Vector3(p.x, p.y * 0.5f, p.z), new Vector3(1.5f, p.y, 1.5f), Stone, SurfaceType.Stone);
                var marker = new GameObject("DEV.POI." + (i + 1));
                marker.transform.SetParent(g, false);
                marker.transform.position = p + Vector3.up * 0.8f;
                marker.AddComponent<GlassingPOI>().SetId("DEV.POI." + (i + 1));
                Visual(g, "Beacon_" + (i + 1), p + Vector3.up * 0.8f, new Vector3(0.8f, 0.8f, 0.8f), PadAmber);
            }
        }

        private static void BuildPads(Transform parent)
        {
            Pad(parent, "DEV.Damage.1", new Vector3(-6f, 0f, -8f), Warn).transform.rotation = Quaternion.Euler(0f, 180f, 0f); // shots from the north
            Pad(parent, "DEV.Difficulty.1", new Vector3(-10f, 0f, -8f), PadBlue);
            Pad(parent, "DEV.Binoculars.1", new Vector3(-14f, 0f, -8f), PadGreen);
            Pad(parent, "DEV.Kill.1", new Vector3(-18f, 0f, -8f), Warn);
            var walk = Trigger<TriggerVolume>(parent, "DEV.ForcedWalk.1", new Vector3(-26f, 1.5f, 2f), new Vector3(6f, 3f, 16f));
            Visual(walk.transform, "ForcedWalk_Floor", new Vector3(-26f, 0.03f, 2f), new Vector3(6f, 0.02f, 16f), PadAmber);
            var mount = Pad(parent, "DEV.Mount.1", new Vector3(-34f, 0f, -8f), PadBlue);
            Box(parent, "MountPlatform", new Vector3(-34f, 0.6f, -4f), new Vector3(2f, 1.2f, 2f), Metal, SurfaceType.Metal);
            var seat = new GameObject("Seat").transform;
            seat.SetParent(mount.transform, false);
            seat.position = new Vector3(-34f, 1.2f, -4f);
            seat.rotation = Quaternion.identity;
        }

        private static void BuildInteractions(Transform parent)
        {
            Crate(parent, "DEV.Interact.Press", new Vector3(4f, 0f, -8f), "[E] Test interaction", false, 0f);
            Crate(parent, "DEV.Interact.Hold", new Vector3(7f, 0f, -8f), "HOLD [E] Take the ledgers", true, 1.5f);
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Group(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color, SurfaceType surface)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.layer = LayerIds.Default;
            go.GetComponent<Renderer>().sharedMaterial = BuilderUtil.PlaceholderMaterial(color);
            go.AddComponent<SurfaceTag>().Surface = surface;
            go.isStatic = true;
            return go;
        }

        private static void Visual(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var go = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Cube, size, color, parent);
            go.transform.position = center;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static T Trigger<T>(Transform parent, string id, Vector3 center, Vector3 size) where T : TriggerVolume
        {
            var go = new GameObject(id) { layer = LayerIds.Trigger };
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var t = go.AddComponent<T>();
            t.Configure(true, false, true);
            t.SetId(id);
            return t;
        }

        private static TriggerVolume Pad(Transform parent, string id, Vector3 floor, Color color)
        {
            var t = Trigger<TriggerVolume>(parent, id, floor + Vector3.up * 1f, new Vector3(2f, 2f, 2f));
            Visual(t.transform, id + "_Floor", floor + Vector3.up * 0.03f, new Vector3(2f, 0.02f, 2f), color);
            return t;
        }

        private static void Crate(Transform parent, string id, Vector3 floor, string prompt, bool hold, float holdSeconds)
        {
            var body = Box(parent, id + "_Crate", floor + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.9f), Wood, SurfaceType.Wood);
            var use = new GameObject(id) { layer = LayerIds.Interactable };
            use.transform.SetParent(body.transform, false);
            var c = use.AddComponent<BoxCollider>();
            c.size = new Vector3(1.05f, 1.05f, 1.05f);
            var ip = use.AddComponent<InteractionPoint>();
            ip.Configure(prompt, hold, holdSeconds, true, false);
            ip.SetId(id);
        }

        private static void AddInputReader(GameObject host)
        {
            Type best = null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<IInputReader>())
            {
                if (t.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                if (best == null || t.Namespace == "LongShadow.Input") best = t;
            }
            if (best == null)
            {
                LSLog.Warn(LogCategory.Player, "No IInputReader MonoBehaviour found (LongShadow.Input.InputReader); the course player will not move.");
                return;
            }
            host.AddComponent(best);
        }

        /// <summary>Every course pad id (tests, docs).</summary>
        public static readonly IReadOnlyList<string> PadIds = new[]
        {
            "DEV.Damage.1", "DEV.Difficulty.1", "DEV.Binoculars.1", "DEV.Kill.1", "DEV.ForcedWalk.1", "DEV.Mount.1",
            PlayerDevHarness.GateA, PlayerDevHarness.GateB, "DEV.Ladder", "DEV.OOB", PlayerDevHarness.SpawnId,
        };
    }
}
