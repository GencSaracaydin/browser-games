using System.IO;
using LongShadow.AI.Dev;
using LongShadow.Core;
using LongShadow.Core.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LongShadow.AI.Editor
{
    /// <summary>
    /// Generates the AI dev arena scene (TECH_PLAN §7.6 AIDevArena, §11.4 WP04 DoD) at
    /// <see cref="AIDevArenaLayout.ScenePath"/>: an 80 × 80 m yard with full/half cover blocks and their cover nodes,
    /// a perched gunner platform, two spawn closets behind walls (out of sight of the yard), two flank routes, a
    /// suppressor mark, a push trigger, a flood cone (IlluminationZone with the B6 flood bonus), two squad node sets, a
    /// baked NavMesh, PF_AIServices, PF_Squad, a patrolling player dummy (<see cref="AIDevTarget"/>), an overview camera
    /// and <see cref="AIDevArenaDirector"/> with its overlay. Menu: LongShadow/AI/Build AI Dev Arena.
    /// </summary>
    public static class AIDevArenaBuilder
    {
        private const string NavMeshAssetPath = "Assets/_Game/Scenes/Dev/AIDevArena/NavMesh_AIDevArena.asset";

        /// <summary>Menu entry: builds (or rebuilds) and saves the arena scene.</summary>
        [MenuItem("LongShadow/AI/Build AI Dev Arena", priority = 110)]
        public static void BuildMenu()
        {
            /* no modal prompt: modal dialogs block the editor and the MCP bridge */ { for (int __i = 0; __i < UnityEngine.SceneManagement.SceneManager.sceneCount; __i++) { var __s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(__i); if (__s.isDirty && !string.IsNullOrEmpty(__s.path)) EditorSceneManager.SaveScene(__s); } }
            Build();
        }

        /// <summary>Opens the arena scene.</summary>
        [MenuItem("LongShadow/AI/Open AI Dev Arena", priority = 111)]
        public static void Open()
        {
            if (!File.Exists(Path.Combine(BuilderUtil.ProjectRoot, AIDevArenaLayout.ScenePath))) Build();
            else EditorSceneManager.OpenScene(AIDevArenaLayout.ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Builds and saves the scene; returns it.</summary>
        public static Scene Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabBuilder.ServicesPrefabPath) == null) EnemyPrefabBuilder.BuildMenu();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SquadPrefabBuilder.SquadPrefabPath) == null) SquadPrefabBuilder.Build();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = BuilderUtil.PlaceholderMaterial(new Color(0.62f, 0.55f, 0.43f));
            var concrete = BuilderUtil.PlaceholderMaterial(new Color(0.58f, 0.57f, 0.54f));
            var wall = BuilderUtil.PlaceholderMaterial(new Color(0.48f, 0.44f, 0.38f));

            // Light and camera.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.85f, 0.68f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(18f, 78f, 0f);                                 // low WSW sun, az 258° (TECH_PLAN §3.8)
            var cam = new GameObject("OverviewCamera").AddComponent<Camera>();
            cam.tag = Tags.MainCamera;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 38f, -58f), Quaternion.Euler(38f, 0f, 0f));
            cam.farClipPlane = 400f;

            // Geometry under one root (the NavMesh surface collects its children).
            var geometry = new GameObject("Geometry");
            Box("Ground", geometry.transform, new Vector3(0f, -0.1f, 0f), new Vector3(AIDevArenaLayout.HalfSize * 2f, 0.2f, AIDevArenaLayout.HalfSize * 2f), ground);
            var markup = new GameObject("Markup").transform;
            var blocks = AIDevArenaLayout.Blocks;
            for (var i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                var height = b.y;
                Box("Block_" + i, geometry.transform, new Vector3(b.x, height * 0.5f, b.z), new Vector3(b.w, height, 0.4f), height >= 1.6f ? wall : concrete);
                var north = i < AIDevArenaLayout.PlayerRowStart;                                   // enemy and middle rows: north side
                var south = i >= AIDevArenaLayout.MiddleRowStart;                                  // middle and player rows: south side
                if (north) CoverNodes(markup, b, true);
                if (south) CoverNodes(markup, b, false);
            }

            // Perch platform for the gunner.
            var perch = AIDevArenaLayout.PerchPoint;
            Box("PerchPlatform", geometry.transform, new Vector3(perch.x, perch.y * 0.5f, perch.z), new Vector3(3.5f, perch.y, 3.5f), wall);
            var pn = Marker<PerchNode>(markup, AIDevArenaLayout.Perch, perch, Quaternion.Euler(0f, 180f, 0f));
            pn.SetTag(PerchTag.Rooftop);

            // Closets: rooms behind 3 m walls, opening away from the yard.
            Closet(markup, geometry.transform, wall, AIDevArenaLayout.ClosetNorth, AIDevArenaLayout.ClosetNorthPos, AIDevArenaLayout.ClosetNorthDoor);
            Closet(markup, geometry.transform, wall, AIDevArenaLayout.ClosetEast, AIDevArenaLayout.ClosetEastPos, AIDevArenaLayout.ClosetEastDoor);

            // Routes, suppress mark, push trigger.
            Marker<FlankRoute>(markup, AIDevArenaLayout.RouteWest, AIDevArenaLayout.RouteWestPoints[0], Quaternion.identity).SetPoints(AIDevArenaLayout.RouteWestPoints);
            Marker<FlankRoute>(markup, AIDevArenaLayout.RouteEast, AIDevArenaLayout.RouteEastPoints[0], Quaternion.identity).SetPoints(AIDevArenaLayout.RouteEastPoints);
            Marker<SceneMarker>(markup, AIDevArenaLayout.SuppressPos, AIDevArenaLayout.SuppressPoint, Quaternion.Euler(0f, 220f, 0f));
            var push = Marker<TriggerVolume>(markup, AIDevArenaLayout.PushTrigger, AIDevArenaLayout.PushCentre, Quaternion.identity);
            var box = push.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = AIDevArenaLayout.PushSize;
            push.gameObject.layer = LayerIds.Trigger;
            push.Configure(true, true, true);

            // Flood cone (accuracy ×1.25 and sight ×2 inside, GDD §7.6, §14.2).
            var floodDir = (AIDevArenaLayout.FloodAim - AIDevArenaLayout.FloodLamp).normalized;
            var flood = Marker<IlluminationZone>(markup, null, AIDevArenaLayout.FloodLamp, Quaternion.LookRotation(floodDir));
            flood.Configure(IlluminationShape.Cone, 45f, 40f, AIDevArenaLayout.FloodOwner);
            var spot = flood.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 40f;
            spot.range = 45f;
            spot.intensity = 6f;
            spot.shadows = LightShadows.None;

            // Squad node sets.
            NodeSet(markup, AIDevArenaLayout.NodeSetA, AIDevArenaLayout.NodeSetASlots);
            NodeSet(markup, AIDevArenaLayout.NodeSetB, AIDevArenaLayout.NodeSetBSlots);

            // NavMesh.
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << LayerIds.Default;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(NavMeshAssetPath));
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath) != null) AssetDatabase.DeleteAsset(NavMeshAssetPath);
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
                EditorUtility.SetDirty(surface);
            }

            // Services, squad, player dummy, director.
            var services = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabBuilder.ServicesPrefabPath);
            if (services != null) PrefabUtility.InstantiatePrefab(services, scene);
            var squadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquadPrefabBuilder.SquadPrefabPath);
            if (squadPrefab != null)
            {
                var squad = (GameObject)PrefabUtility.InstantiatePrefab(squadPrefab, scene);
                var slots = AIDevArenaLayout.NodeSetASlots;
                for (var i = 0; i < squad.transform.childCount && i < slots.Length; i++) squad.transform.GetChild(i).position = slots[i];
            }
            BuildDummy();
            new GameObject("AIDevArena").AddComponent<AIDevArenaDirector>();

            BuilderUtil.EnsureFolder(Path.GetDirectoryName(AIDevArenaLayout.ScenePath));
            EditorSceneManager.SaveScene(scene, AIDevArenaLayout.ScenePath);
            LSLog.Info(LogCategory.AI, "Built " + AIDevArenaLayout.ScenePath + " (press Play; overlay top-left).");
            return scene;
        }

        private static void BuildDummy()
        {
            var go = new GameObject("DevPlayer");
            go.transform.position = AIDevArenaLayout.PlayerStart;
            go.layer = LayerIds.Player;
            go.tag = Tags.Player;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.35f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var visual = BuilderUtil.PlaceholderPrimitive("DevPlayer", PrimitiveType.Capsule, new Vector3(0.6f, 0.9f, 0.6f), new Color(0.2f, 0.45f, 0.85f), go.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<Health>();
            go.AddComponent<AIDevTarget>().Configure(AIDevArenaLayout.PlayerPatrol, true);
        }

        private static void CoverNodes(Transform parent, Vector4 b, bool northSide)
        {
            var standing = b.y >= 1.6f;
            var z = northSide ? b.z + 0.75f : b.z - 0.75f;
            var facing = northSide ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;        // forward faces the threat side
            var ends = b.w >= 3f ? 2 : 1;
            for (var k = 0; k < ends; k++)
            {
                var x = ends == 1 ? b.x : b.x + (k == 0 ? -b.w * 0.3f : b.w * 0.3f);
                var node = Marker<CoverNode>(parent, null, new Vector3(x, 0f, z), facing);
                // Full cover leans out at its ends; half cover pops up over the top (Legion "half cover", GDD §14.1).
                // A south-facing node's right is west: the west end (k 0) leans right on the north side, left on the south.
                var left = standing && (ends == 1 || (k == 0) != northSide);
                var right = standing && (ends == 1 || (k == 1) != northSide);
                node.Configure(standing, left, right, 120f, "DEV.Cover");
            }
        }

        private static void Closet(Transform markup, Transform geometry, Material wall, string id, Vector3 pos, Vector3 door)
        {
            // Three walls of a 6 × 6 m room around pos; the open side faces the door.
            var toDoor = door - pos;
            toDoor.y = 0f;
            var open = Mathf.Abs(toDoor.x) > Mathf.Abs(toDoor.z) ? new Vector3(Mathf.Sign(toDoor.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toDoor.z));
            var side = new Vector3(open.z, 0f, -open.x);
            Box(id + "_Back", geometry, pos - open * 3f + Vector3.up * 1.5f, Size(side, 6.4f, 3f), wall);
            Box(id + "_SideA", geometry, pos + side * 3f + Vector3.up * 1.5f, Size(open, 6.4f, 3f), wall);
            Box(id + "_SideB", geometry, pos - side * 3f + Vector3.up * 1.5f, Size(open, 6.4f, 3f), wall);
            // A screen wall in front of the opening hides the room from the yard; the way out is around its end.
            Box(id + "_Screen", geometry, pos + open * 4.5f + side * 1f + Vector3.up * 1.5f, Size(side, 6.4f, 3f), wall);
            var closet = Marker<SpawnCloset>(markup, id, pos, Quaternion.LookRotation(open));
            var doorT = new GameObject("Door").transform;
            doorT.SetParent(closet.transform, false);
            doorT.position = door;
            var p1 = new GameObject("Spawn1").transform;
            p1.SetParent(closet.transform, false);
            p1.localPosition = new Vector3(-1f, 0f, 0f);
            var p2 = new GameObject("Spawn2").transform;
            p2.SetParent(closet.transform, false);
            p2.localPosition = new Vector3(1f, 0f, -1f);
            closet.Configure(doorT, new[] { p1, p2 }, 15f);
        }

        private static Vector3 Size(Vector3 along, float length, float height)
        {
            return new Vector3(Mathf.Abs(along.x) > 0.5f ? length : 0.4f, height, Mathf.Abs(along.z) > 0.5f ? length : 0.4f);
        }

        private static void NodeSet(Transform parent, string id, Vector3[] slots)
        {
            var set = Marker<SquadNodeSet>(parent, id, slots[0], Quaternion.identity);
            var nodes = new SquadNode[3];
            for (var i = 0; i < 3; i++)
            {
                var t = new GameObject(SquadIds.All[i]).transform;
                t.SetParent(set.transform, false);
                t.position = slots[i];
                t.rotation = Quaternion.identity;                                                   // facing north (the enemy side)
                nodes[i] = new SquadNode { Point = t, Stance = Stance.Crouch, CoverPose = 0 };
            }
            set.Configure(nodes[0], nodes[1], nodes[2]);
        }

        private static T Marker<T>(Transform parent, string id, Vector3 position, Quaternion rotation) where T : SceneMarker
        {
            var go = new GameObject(string.IsNullOrEmpty(id) ? typeof(T).Name : id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var m = go.AddComponent<T>();
            if (!string.IsNullOrEmpty(id)) m.SetId(id);
            return m;
        }

        private static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.layer = LayerIds.Default;
            go.isStatic = true;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
