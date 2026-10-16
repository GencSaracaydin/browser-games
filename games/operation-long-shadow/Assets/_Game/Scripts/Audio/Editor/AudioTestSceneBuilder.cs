using LongShadow.Audio.Ambience;
using LongShadow.Audio.Core;
using LongShadow.Audio.Dev;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LongShadow.Audio.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Scenes/Dev/Dev_AudioRange.unity</c> (never in Build Settings, D1): the audio systems
    /// (<c>PF_AudioSystems</c>), a main camera the AudioListener follows, one <see cref="ZoneTag"/> per acoustic zone with a
    /// listener point inside each, a squad stand-in for VO, a sodium lamp <see cref="LoopEmitter"/>, a muted-TV
    /// <see cref="CivilianEmitter"/> and the <see cref="AudioAuditionPanel"/>. Press Play and audition weapons at
    /// 10–600 m, VO dry/radio by distance, music layers and stingers, and the ambience across the LightStage.
    /// Run <b>LongShadow/Audio/Build All</b> first.
    /// </summary>
    public static class AudioTestSceneBuilder
    {
        /// <summary>Scene path.</summary>
        public const string ScenePath = "Assets/_Game/Scenes/Dev/Dev_AudioRange.unity";

        /// <summary>Builds and saves the scene (replaces it and the open scene).</summary>
        [MenuItem("LongShadow/Audio/Build Audio Test Scene", priority = 160)]
        public static string Build()
        {
            BuilderUtil.EnsureFolder("Assets/_Game/Scenes/Dev");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(20f, 78f, 0f);
            var ground = BuilderUtil.PlaceholderPrimitive("Ground", PrimitiveType.Cube, new Vector3(400f, 0.2f, 400f), new Color(0.79f, 0.66f, 0.48f), null, true);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.AddComponent<SurfaceTag>().Surface = SurfaceType.Sand;

            var camGo = new GameObject("Listener Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.transform.position = new Vector3(0f, 1.65f, 0f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSetupMenu.SystemsPrefabPath);
            var note = "PF_AudioSystems";
            if (prefab != null) PrefabUtility.InstantiatePrefab(prefab);
            else
            {
                AudioSetupMenu.BuildSystemsPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSetupMenu.SystemsPrefabPath);
                if (prefab != null) PrefabUtility.InstantiatePrefab(prefab);
                else note = "PF_AudioSystems missing";
            }

            // One zone per ZoneType, each with a listener point inside (OpenDesert = outside every tag).
            var zones = new GameObject("Zones").transform;
            var points = new Transform[7];
            points[(int)ZoneType.OpenDesert] = Point(zones, "P_OpenDesert", new Vector3(0f, 1.65f, 0f));
            Zone(zones, "ZONE.Street", ZoneType.Street, new Vector3(0f, 3f, 60f), new Vector3(8f, 8f, 40f), 0);
            points[(int)ZoneType.Street] = Point(zones, "P_Street", new Vector3(0f, 1.65f, 60f));
            Zone(zones, "ZONE.Square", ZoneType.Square, new Vector3(0f, 4f, -70f), new Vector3(40f, 10f, 40f), 0);
            points[(int)ZoneType.Square] = Point(zones, "P_Square", new Vector3(0f, 1.65f, -70f));
            Zone(zones, "ZONE.Interior", ZoneType.Interior, new Vector3(60f, 2f, 0f), new Vector3(10f, 4f, 10f), 1);
            points[(int)ZoneType.Interior] = Point(zones, "P_Interior", new Vector3(60f, 1.65f, 0f));
            Zone(zones, "ZONE.Basement", ZoneType.Basement, new Vector3(-60f, 2f, 0f), new Vector3(12f, 4f, 12f), 1);
            points[(int)ZoneType.Basement] = Point(zones, "P_Basement", new Vector3(-60f, 1.65f, 0f));
            Zone(zones, "ZONE.Roof", ZoneType.Roof, new Vector3(60f, 13f, 60f), new Vector3(20f, 6f, 20f), 1);
            points[(int)ZoneType.Roof] = Point(zones, "P_Roof", new Vector3(60f, 12.65f, 60f));
            Zone(zones, "ZONE.Cabin", ZoneType.Cabin, new Vector3(-60f, 2f, 60f), new Vector3(3f, 2.5f, 3f), 2);
            points[(int)ZoneType.Cabin] = Point(zones, "P_Cabin", new Vector3(-60f, 1.25f, 60f));

            var speaker = BuilderUtil.PlaceholderPrimitive("Speaker (squad stand-in)", PrimitiveType.Capsule, new Vector3(0.5f, 0.9f, 0.5f), new Color(0.4f, 0.5f, 0.3f));
            speaker.transform.position = new Vector3(2f, 0.9f, 4f);

            var lamp = new GameObject("Sodium Lamp (LoopEmitter)");
            lamp.transform.position = new Vector3(6f, 5f, 20f);
            lamp.AddComponent<LoopEmitter>().Configure("SFX_SODIUM_BALLAST", new Vector2(AudioConstants.CricketsStageS, 99f), -1, -1, 40f, true);
            var tv = new GameObject("Muted TV (CivilianEmitter)");
            tv.transform.position = new Vector3(-5f, 2f, 55f);
            tv.AddComponent<CivilianEmitter>().Configure(CivilianSound.MutedTv, -1, -1);

            var dev = new GameObject("Audio Audition (Dev)");
            dev.AddComponent<AudioAuditionPanel>().Configure(camGo.transform, speaker.transform, points);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var text = "[Audio] test scene saved at " + ScenePath + " (" + note + ")";
            LSLog.Info(LogCategory.Audio, text);
            return text;
        }

        private static Transform Point(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            return t;
        }

        private static void Zone(Transform parent, string id, ZoneType type, Vector3 centre, Vector3 size, int priority)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.layer = LayerIds.Trigger;
            var z = go.AddComponent<ZoneTag>();
            z.SetId(id);
            z.SetZone(type, priority);
            z.Configure(false, false, true);
        }
    }
}
