using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Atmosphere;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Exposure;
using LongShadow.World.Look.Practicals;
using LongShadow.World.Look.Sky;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Builds <c>Assets/_Game/Prefabs/FX/Look/PF_LookRig.prefab</c>, everything the look needs in S_Persistent (TECH_PLAN
    /// §3.8, §5.1): <c>SUN</c> (directional, Realtime, soft shadows, rendering layer World, lens flare LF_Sun),
    /// <c>SKY_KEY</c> (directional, no shadows), the sun disc, the global volumes (VP_Base priority 0, the four stages
    /// 1–4), a WindZone, the aviation lights at the mast (378, 0, 362) and the components LightStageController,
    /// WindController, PracticalBudget, LampRipple, FloodGlare, EyeAdaptationDriver, DofDirector, FlareDirector and
    /// PracticalBinder (rules for LAMP.*, LAMPFAR.*, B6.F1–F4, B4/B11.FireProp, B9.Emergency1/2, B7.Fill1/2). The menu
    /// "Install Look Rig In Active Scene" drops it into the open scene (S_Persistent) and sets the skybox, fog and sun.
    /// </summary>
    public static class LookRigBuilder
    {
        [MenuItem("LongShadow/Look/Build Look Rig Prefab", priority = 114)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            Build(log);
            Debug.Log("[LS:Look] " + log);
        }

        [MenuItem("LongShadow/Look/Install Look Rig In Active Scene", priority = 115)]
        public static void InstallMenu()
        {
            var log = new StringBuilder();
            Install(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Builds the prefab.</summary>
        public static GameObject Build(StringBuilder log)
        {
            var root = new GameObject("PF_LookRig");
            var controller = root.AddComponent<LightStageController>();

            // Lights (TECH_PLAN §3.8).
            var sun = MakeDirectional("SUN", root.transform, LightShadows.Soft);
            var skyKey = MakeDirectional("SKY_KEY", root.transform, LightShadows.None);
            skyKey.enabled = false;
            var flareData = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(LookPaths.LensFlareSun);
            var flare = sun.gameObject.AddComponent<LensFlareComponentSRP>();
            flare.lensFlareData = flareData;
            flare.intensity = 0.6f;
            flare.useOcclusion = true;
            flare.occlusionRadius = 0.02f;
            flare.sampleCount = 16;
            flare.allowOffScreen = false;
            flare.attenuationByLightShape = false;
            if (flareData == null) log.Append("LF_Sun missing (run Build Sun Lens Flare). ");

            // Sun disc.
            var discRoot = new GameObject("SunDisc");
            discRoot.transform.SetParent(root.transform, false);
            var disc = discRoot.AddComponent<SunDisc>();
            var quad = LookBuildUtil.Quad("Disc", discRoot.transform, Vector3.zero, 1f, AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSunDisc));
            disc.EditorSetup(quad);

            // Global volumes (layer Default, TECH_PLAN §3.7).
            var vBase = MakeVolume("VOL_Base", root.transform, LookPaths.VPBase, 0f, 1f);
            var vGolden = MakeVolume("VOL_Golden", root.transform, LookPaths.VPGolden, 1f, 1f);
            var vSunset = MakeVolume("VOL_Sunset", root.transform, LookPaths.VPSunset, 2f, 0f);
            var vAfter = MakeVolume("VOL_Afterglow", root.transform, LookPaths.VPAfterglow, 3f, 0f);
            var vBlue = MakeVolume("VOL_BlueHour", root.transform, LookPaths.VPBlueHour, 4f, 0f);
            _ = vBase;

            // Wind.
            var windGo = new GameObject("WindZone");
            windGo.transform.SetParent(root.transform, false);
            var zone = windGo.AddComponent<WindZone>();
            zone.mode = WindZoneMode.Directional;
            var wind = root.AddComponent<WindController>();
            wind.EditorWire(zone);

            // Aviation lights at the mast (GDD §6.3 mast x 378, z 362).
            var avPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LookPaths.AviationLightsPrefab);
            if (avPrefab != null)
            {
                var av = (GameObject)PrefabUtility.InstantiatePrefab(avPrefab);
                av.transform.SetParent(root.transform, false);
                av.transform.localPosition = new Vector3(378f, 0f, 362f);
            }

            root.AddComponent<PracticalBudget>();
            root.AddComponent<LampRipple>();
            root.AddComponent<FloodGlare>();
            root.AddComponent<EyeAdaptationDriver>();
            root.AddComponent<DofDirector>();
            var flares = root.AddComponent<FlareDirector>();
            flares.EditorSetup(AssetDatabase.LoadAssetAtPath<GameObject>(LookPaths.ParachuteFlarePrefab));
            var binder = root.AddComponent<PracticalBinder>();
            binder.EditorSetRules(BindRules());

            controller.EditorWire(sun, skyKey, flare, disc, AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSky),
                vGolden, vSunset, vAfter, vBlue,
                AssetDatabase.LoadAssetAtPath<LightStageTuning>(LookPaths.TuningAsset),
                AssetDatabase.LoadAssetAtPath<AmbientSHSet>(LookPaths.AmbientSHAsset));

            var prefab = BuilderUtil.SavePrefab(root, LookPaths.LookRigPrefab);
            log.Append("PF_LookRig built. ");
            return prefab;
        }

        /// <summary>The PracticalBinder rules (marker id → prefab).</summary>
        public static PracticalBindRule[] BindRules()
        {
            GameObject P(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var rules = new System.Collections.Generic.List<PracticalBindRule>
            {
                new PracticalBindRule("LAMP.", P(LookPaths.SodiumLampHeadPrefab)),
                new PracticalBindRule("LAMPFAR.", P(LookPaths.SodiumLampSpritePrefab)),
            };
            foreach (var id in LookIds.Floods)
                rules.Add(new PracticalBindRule(id, P(FXPrefabBuilder.FloodPath(id)), true, LookIds.FloodAim));
            rules.Add(new PracticalBindRule(LookIds.B4FireProp, P(FXPrefabBuilder.FirePath(false))));
            rules.Add(new PracticalBindRule("B11.FireProp", P(FXPrefabBuilder.FirePath(true))));
            foreach (var id in LookIds.EmergencyMarkers) rules.Add(new PracticalBindRule(id, P(LookPaths.EmergencyLightPrefab)));
            rules.Add(new PracticalBindRule("B7.Fill1", P(LookPaths.TungstenFillPrefab)));
            rules.Add(new PracticalBindRule("B7.Fill2", P(LookPaths.TungstenFillPrefab)));
            return rules.ToArray();
        }

        /// <summary>Instantiates the rig in the active scene (once) and sets the scene's lighting environment.</summary>
        public static void Install(StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LookPaths.LookRigPrefab);
            if (prefab == null) prefab = Build(log);
            var scene = SceneManager_GetActive();
            var existing = Object.FindAnyObjectByType<LightStageController>();
            if (existing == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.name = "LookRig";
                existing = inst.GetComponent<LightStageController>();
                log.Append("LookRig instantiated in " + scene.name + ". ");
            }
            else log.Append("LookRig already in the scene. ");

            var sky = AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSky);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            if (existing != null && existing.SunLight != null) RenderSettings.sun = existing.SunLight;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && existing != null && l != existing.SunLight && l != existing.SkyKeyLight && l.lightmapBakeType != LightmapBakeType.Baked)
                    log.Append("Warning: extra directional light '" + l.name + "' in the scene (two suns). ");
            existing?.PreviewApply(0f);
            existing?.EditorReleasePreview();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static UnityEngine.SceneManagement.Scene SceneManager_GetActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        private static Light MakeDirectional(string name, Transform parent, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = LightStageTable.LightRotation(name == "SUN" ? 8f : LightStageTable.SkyKeyElevation);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.lightmapBakeType = LightmapBakeType.Realtime; // D6: one realtime sun; bakes use the EditorOnly BAKE_Sun
            l.shadows = shadows;
            l.shadowResolution = LightShadowResolution.FromQualitySettings;
            l.renderingLayerMask = (int)RenderingLayerIds.WorldMask;
            var data = l.GetUniversalAdditionalLightData();
            if (data != null)
            {
                data.usePipelineSettings = name != "SUN";
                data.renderingLayers = RenderingLayerIds.WorldMask;
            }
            return l;
        }

        private static Volume MakeVolume(string name, Transform parent, string profilePath, float priority, float weight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerIds.Default;
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.weight = weight;
            v.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            return v;
        }
    }
}
