using LongShadow.Combat.Dev;
using LongShadow.Combat.Targets;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LongShadow.Combat.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/Dev/Dev_CombatRange.unity</c>, a weapons test range for the editor holder: sand floor,
    /// surface walls (plaster, concrete, wood, metal, an awning cloth to shoot through), Legion (100 HP) and Greycoat
    /// (150 HP, vest ×0.8) dummies at 10/25/45/60 m that log every hit, a resupply crate, AK and RPK pickups, a flood
    /// head (0.4 m hit radius), a transmitter rack (60 HP) and a truck (20 hits), an AI shooter that fires bursts at the
    /// player (near misses, cracks, tracers), <c>PF_CombatSystems</c> and WP02's <c>PF_Player</c> with the combat
    /// components (when it exists). Run <c>LongShadow/Combat/Build All</c> first.
    /// </summary>
    public static class CombatRangeBuilder
    {
        /// <summary>Scene path.</summary>
        public const string ScenePath = "Assets/_Game/Scenes/Dev/Dev_CombatRange.unity";

        /// <summary>Builds and saves the range scene (replaces it).</summary>
        [MenuItem("LongShadow/Combat/Build Combat Range (Dev Scene)", priority = 150)]
        public static string Build()
        {
            BuilderUtil.EnsureFolder("Assets/_Game/Scenes/Dev");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(18f, 78f, 0f); // TECH_PLAN §3.8 azimuth 258°, low sun

            var ground = BuilderUtil.PlaceholderPrimitive("Ground", PrimitiveType.Cube, new Vector3(60f, 0.2f, 120f), new Color(0.79f, 0.66f, 0.48f), null, true);
            ground.transform.position = new Vector3(0f, -0.1f, 50f);
            ground.AddComponent<SurfaceTag>().Surface = SurfaceType.Sand;

            Wall("Wall_Plaster", new Vector3(-8f, 1.5f, 15f), SurfaceType.Plaster, new Color(0.85f, 0.82f, 0.75f));
            Wall("Wall_Concrete", new Vector3(-4f, 1.5f, 15f), SurfaceType.Concrete, new Color(0.6f, 0.6f, 0.58f));
            Wall("Wall_Wood", new Vector3(4f, 1.5f, 15f), SurfaceType.Wood, new Color(0.45f, 0.32f, 0.2f));
            Wall("Wall_Metal", new Vector3(8f, 1.5f, 15f), SurfaceType.Metal, new Color(0.4f, 0.42f, 0.44f));
            var cloth = BuilderUtil.PlaceholderPrimitive("Awning_Fabric", PrimitiveType.Cube, new Vector3(3f, 2f, 0.02f), new Color(0.7f, 0.35f, 0.2f), null, true);
            cloth.transform.position = new Vector3(0f, 1.6f, 8f);
            cloth.AddComponent<SurfaceTag>().Surface = SurfaceType.Fabric;

            var distances = new[] { 10f, 25f, 45f, 60f };
            for (var i = 0; i < distances.Length; i++)
            {
                Dummy("Legion_" + distances[i] + "m", new Vector3(-3f, 0f, distances[i]), 100f, 1f, Team.Legion);    // GDD §14.1 rifleman
                Dummy("Greycoat_" + distances[i] + "m", new Vector3(3f, 0f, distances[i]), 150f, 0.8f, Team.Greycoat); // GDD §14.1 vest
            }

            var crate = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabBuilder.PrefabFolder + "/PF_AmmoCrate.prefab");
            if (crate != null) ((GameObject)PrefabUtility.InstantiatePrefab(crate)).transform.position = new Vector3(-2f, 0f, 3f);
            PlacePickup(WeaponIds.AK, new Vector3(1.5f, 0.05f, 4f));
            PlacePickup(WeaponIds.RPK, new Vector3(3f, 0.05f, 4f));

            BuildFlood(new Vector3(14f, 0f, 30f));
            BuildRack(new Vector3(-14f, 0f, 20f));
            BuildTruck(new Vector3(16f, 0f, 55f));

            var shooter = new GameObject("AI_Shooter (fires at the player)");
            shooter.transform.position = new Vector3(-12f, 0f, 35f);
            BuilderUtil.PlaceholderPrimitive("Body", PrimitiveType.Capsule, new Vector3(0.5f, 0.9f, 0.5f), Placeholders.Hostile, shooter.transform)
                .transform.localPosition = new Vector3(0f, 0.9f, 0f);
            shooter.AddComponent<RangeShooter>();

            var systems = AssetDatabase.LoadAssetAtPath<GameObject>(CombatSetupMenu.SystemsPrefabPath);
            if (systems != null) PrefabUtility.InstantiatePrefab(systems);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatSetupMenu.PlayerPrefabPath);
            string note;
            if (player != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(player);
                p.transform.position = new Vector3(0f, 0f, 0f);
                CombatSetupMenu.AddComponents(p);
                note = "player: PF_Player with combat components";
            }
            else note = "player: PF_Player not built yet (WP02); add it and run LongShadow/Combat/Add Player Combat To Selection";

            EditorSceneManager.SaveScene(scene, ScenePath);
            var text = "[Combat] range scene saved at " + ScenePath + " (" + note + ")" + (systems == null ? "; PF_CombatSystems missing: run Build All" : "");
            LSLog.Info(LogCategory.Combat, text);
            return text;
        }

        private static void Wall(string name, Vector3 pos, SurfaceType surface, Color c)
        {
            var w = BuilderUtil.PlaceholderPrimitive(name, PrimitiveType.Cube, new Vector3(3.5f, 3f, 0.3f), c, null, true);
            w.transform.position = pos;
            w.AddComponent<SurfaceTag>().Surface = surface;
        }

        private static void Dummy(string name, Vector3 pos, float hp, float torsoArmour, Team team)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var health = root.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("_maxHp").floatValue = hp;
            so.FindProperty("_team").enumValueIndex = (int)team;
            so.FindProperty("_torsoArmour").floatValue = torsoArmour;
            so.FindProperty("_actorId").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
            var tint = team == Team.Greycoat ? new Color(0.45f, 0.48f, 0.45f) : Placeholders.Hostile;
            Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.65f, 0f), new Vector3(0.24f, 0.26f, 0.24f), HitZone.Head, tint);
            Part(root.transform, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.22f, 0f), new Vector3(0.42f, 0.6f, 0.26f), HitZone.Torso, tint);
            Part(root.transform, "Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.86f, 0f), new Vector3(0.36f, 0.16f, 0.24f), HitZone.Torso, tint);
            Part(root.transform, "Arm_L", PrimitiveType.Cube, new Vector3(-0.3f, 1.2f, 0f), new Vector3(0.12f, 0.6f, 0.12f), HitZone.Limb, tint);
            Part(root.transform, "Arm_R", PrimitiveType.Cube, new Vector3(0.3f, 1.2f, 0f), new Vector3(0.12f, 0.6f, 0.12f), HitZone.Limb, tint);
            Part(root.transform, "Leg_L", PrimitiveType.Cube, new Vector3(-0.11f, 0.4f, 0f), new Vector3(0.15f, 0.8f, 0.15f), HitZone.Limb, tint);
            Part(root.transform, "Leg_R", PrimitiveType.Cube, new Vector3(0.11f, 0.4f, 0f), new Vector3(0.15f, 0.8f, 0.15f), HitZone.Limb, tint);
            root.AddComponent<RangeTarget>();
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 size, HitZone zone, Color c)
        {
            var go = BuilderUtil.PlaceholderPrimitive(name, type, size, c, parent, true);
            go.transform.localPosition = pos;
            go.layer = LayerIds.Hitbox;
            var hb = go.AddComponent<Hitbox>();
            hb.Configure(zone);
        }

        private static void PlacePickup(string weaponId, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabBuilder.PathFor("PF_Pickup_", weaponId));
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 30f, 90f));
        }

        private static void BuildFlood(Vector3 pos)
        {
            var root = new GameObject("Flood_F1");
            root.transform.position = pos;
            BuilderUtil.PlaceholderPrimitive("Mast", PrimitiveType.Cylinder, new Vector3(0.15f, 4f, 0.15f), new Color(0.35f, 0.35f, 0.35f), root.transform, true)
                .transform.localPosition = new Vector3(0f, 4f, 0f);
            var head = BuilderUtil.PlaceholderPrimitive("Head", PrimitiveType.Cube, new Vector3(0.6f, 0.4f, 0.3f), new Color(0.2f, 0.2f, 0.2f), root.transform, true);
            head.transform.localPosition = new Vector3(0f, 8.1f, 0f);
            head.transform.localRotation = Quaternion.Euler(30f, 180f, 0f);
            var light = new GameObject("Spot").AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 30f;
            light.spotAngle = 70f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.transform.SetParent(head.transform, false);
            light.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            var d = root.AddComponent<Destructible>();
            d.Configure("DEV.Flood", DestructibleKind.Flood, 1f, 0, 0.4f, head.transform); // TECH_PLAN §7.5: lamp head 0.4 m
            var so = new SerializedObject(d);
            var lights = so.FindProperty("_lights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRack(Vector3 pos)
        {
            var root = new GameObject("Rack_R1");
            root.transform.position = pos;
            var body = BuilderUtil.PlaceholderPrimitive("Cabinet", PrimitiveType.Cube, new Vector3(0.6f, 2f, 0.8f), new Color(0.3f, 0.32f, 0.3f), root.transform, true);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.AddComponent<SurfaceTag>().Surface = SurfaceType.Metal;
            var d = root.AddComponent<Destructible>();
            d.Configure("DEV.Rack", DestructibleKind.Rack, 60f, 0, 0f, body.transform); // GDD §7.9: 60 HP
        }

        private static void BuildTruck(Vector3 pos)
        {
            var root = new GameObject("Truck");
            root.transform.position = pos;
            var body = BuilderUtil.PlaceholderPrimitive("Body", PrimitiveType.Cube, new Vector3(2.3f, 2.4f, 6f), new Color(0.35f, 0.38f, 0.28f), root.transform, true);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            body.layer = LayerIds.Vehicle;
            body.AddComponent<SurfaceTag>().Surface = SurfaceType.Metal;
            var d = root.AddComponent<Destructible>();
            d.Configure("DEV.Truck", DestructibleKind.Vehicle, 1f, 20, 0f, body.transform); // GDD §7.11: 20 hits
        }
    }
}
