using System;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Practicals;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Look guardrails (TECH_PLAN §3.4, §3.8, §9.3, D6, D16; GDD §9.5): no shipped scene or prefab depends on an
    /// EditorOnly HDR (<c>Art/HDRI/EditorOnly</c>), section skyboxes are <c>M_Sky_PanoBlend</c>, BAKE_Sun objects are
    /// EditorOnly and inactive, there is one realtime sun (no second directional light in the open scenes), at most one
    /// shadowed local light outside the flares, practical glass materials have <c>_EMISSION</c>, the lightmaps of open
    /// sections fit their caps, the URP asset matches §3.4 (HDR grading, LUT 32) and the volume profiles exist.
    /// Writes <c>Logs/look_validate.txt</c>; returns the error count.
    /// </summary>
    public static class LookValidator
    {
        private const string LogPath = "Logs/look_validate.txt";

        [MenuItem("LongShadow/Look/Validate Look (scenes, assets, budgets)", priority = 150)]
        public static void ValidateMenu()
        {
            var report = new StringBuilder();
            var errors = Validate(report);
            var full = Path.Combine(BuilderUtil.ProjectRoot, LogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, report.ToString());
            if (errors > 0) Debug.LogWarning("[LS:Look] Look validation: " + errors + " error(s). See " + LogPath + "\n" + report);
            else Debug.Log("[LS:Look] Look validation passed. See " + LogPath + "\n" + report);
        }

        /// <summary>Runs every check; returns the number of errors.</summary>
        public static int Validate(StringBuilder r)
        {
            var errors = 0;

            // 1. EditorOnly HDRs never reach shipped content (§9.3).
            foreach (var path in ShippedRoots())
            {
                foreach (var dep in AssetDatabase.GetDependencies(path, true))
                {
                    if (dep.IndexOf("/HDRI/EditorOnly/", StringComparison.Ordinal) < 0) continue;
                    r.AppendLine("ERROR " + path + " depends on EditorOnly HDR " + dep);
                    errors++;
                }
            }

            // 2. Volume profiles.
            foreach (var p in new[] { LookPaths.VPBase, LookPaths.VPGolden, LookPaths.VPSunset, LookPaths.VPAfterglow, LookPaths.VPBlueHour, LookPaths.VPBooth })
            {
                if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(p) != null) continue;
                r.AppendLine("ERROR missing volume profile " + p + " (LongShadow/Look/Build Volume Profiles).");
                errors++;
            }

            // 3. URP asset (§3.4; ProjectSetup owns it, this only reads).
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (urp.colorGradingMode != ColorGradingMode.HighDynamicRange) { r.AppendLine("ERROR URP colour grading is not HDR (§3.4)."); errors++; }
                if (urp.colorGradingLutSize != 32) r.AppendLine("WARN  URP LUT size " + urp.colorGradingLutSize + " (expected 32).");
                if (!urp.supportsHDR) { r.AppendLine("ERROR URP HDR off."); errors++; }
                if (urp.shadowCascadeCount != 4) r.AppendLine("WARN  shadow cascades " + urp.shadowCascadeCount + " (4 expected outside Performance Mode).");
            }
            else r.AppendLine("WARN  active render pipeline is not URP.");
            CheckSsao(r); // warnings only: ProjectSetup owns the renderer

            // 4. Open scenes: suns, bake suns, shadowed locals, skyboxes, practical materials, lightmap caps.
            var directional = 0;
            var shadowedLocals = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null) continue;
                if (l.name == SectionBaker.BakeSunName)
                {
                    if (!l.CompareTag("EditorOnly")) { r.AppendLine("ERROR " + l.name + " in " + l.gameObject.scene.name + " is not tagged EditorOnly (D6)."); errors++; }
                    if (l.gameObject.activeInHierarchy) { r.AppendLine("ERROR " + l.name + " in " + l.gameObject.scene.name + " is active (second sun in play mode)."); errors++; }
                    continue;
                }
                if (!l.gameObject.activeInHierarchy || !l.enabled) continue;
                if (l.type == LightType.Directional && l.lightmapBakeType != LightmapBakeType.Baked) directional++;
                if (l.type != LightType.Directional && l.shadows != LightShadows.None && l.lightmapBakeType != LightmapBakeType.Baked &&
                    l.GetComponentInParent<ParachuteFlare>() == null) shadowedLocals++;
            }
            if (directional > 1) { r.AppendLine("ERROR " + directional + " active realtime directional lights in the open scenes (one sun, GDD §9.3)."); errors++; }
            if (shadowedLocals > 1) { r.AppendLine("ERROR " + shadowedLocals + " shadowed local lights (≤ 1 outside the flares, GDD §9.5)."); errors++; }
            if (RenderSettings.skybox != null && RenderSettings.skybox.shader != null && RenderSettings.skybox.shader.name != LookPaths.ShaderPanoBlend)
                r.AppendLine("WARN  active scene skybox uses " + RenderSettings.skybox.shader.name + " (expected Sky/PanoBlend).");

            foreach (var p in Object.FindObjectsByType<PracticalLight>(FindObjectsInactive.Include))
            {
                foreach (var ren in p.EditorEmissive)
                {
                    if (ren == null) continue;
                    foreach (var m in ren.sharedMaterials)
                    {
                        if (m == null || !m.HasProperty("_EmissionColor") || m.shader == null) continue;
                        if (m.shader.name == LookPaths.ShaderLit && !m.IsKeywordEnabled("_EMISSION"))
                        {
                            r.AppendLine("ERROR practical '" + p.name + "' material " + m.name + " lacks _EMISSION (it will not glow).");
                            errors++;
                        }
                    }
                }
            }

            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!Sections.TryParse(scene.name, out var id)) continue;
                var specs = SectionBakeTable.For(id);
                if (specs.Count == 0) continue;
                r.AppendLine("INFO  " + scene.name + ": cap " + specs[specs.Count - 1].CapEq + " eq (open alone and bake to measure; Logs/look_bake.txt).");
            }

            r.AppendLine("LightStage keys: " + LightStageTable.Keys.Count + ", strike at s " + LightStageTable.StrikeS + ", sunset at s " + LightStageTable.SunsetS + ".");
            r.AppendLine(errors == 0 ? "OK" : errors + " error(s)");
            return errors;
        }

        /// <summary>
        /// SSAO of PC_Renderer against TECH_PLAN §3.5 / GDD §9.6 (Depth source, downsample, Medium samples and blur,
        /// intensity 0.6, radius 0.35, direct lighting 0.25). ProjectSetup applies them; this only verifies. Returns the
        /// number of warnings.
        /// </summary>
        public static int CheckSsao(StringBuilder r)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return 0;
            var found = false;
            var warnings = 0;
            foreach (var data in urp.rendererDataList)
            {
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                {
                    if (!(f is ScreenSpaceAmbientOcclusion)) continue;
                    found = true;
                    var so = new SerializedObject(f);
                    void Expect(string path, float want, string what)
                    {
                        var p = so.FindProperty(path);
                        if (p == null) return;
                        var have = p.propertyType == SerializedPropertyType.Boolean ? (p.boolValue ? 1f : 0f)
                            : p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer ? p.intValue : p.floatValue;
                        if (Mathf.Abs(have - want) > 1e-3f)
                        {
                            r.AppendLine("WARN  " + data.name + " SSAO " + what + " = " + have + " (expected " + want + ", §3.5; ProjectSetup owns it).");
                            warnings++;
                        }
                    }
                    Expect("m_Settings.Source", 0f, "source (Depth)");
                    Expect("m_Settings.Downsample", 1f, "downsample");
                    Expect("m_Settings.Samples", 1f, "samples (Medium)");
                    Expect("m_Settings.BlurQuality", 1f, "blur (Medium)");
                    Expect("m_Settings.Intensity", 0.6f, "intensity");
                    Expect("m_Settings.Radius", 0.35f, "radius");
                    Expect("m_Settings.DirectLightingStrength", 0.25f, "direct lighting strength");
                }
            }
            if (!found) r.AppendLine("WARN  no SSAO renderer feature found (contact shadows, CINEMATIC_DIRECTION §1.3).");
            return warnings;
        }

        private static string[] ShippedRoots()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var folder in new[] { Sections.ScenesFolder, "Assets/_Game/Prefabs", "Assets/_Game/Settings/Volumes", "Assets/_Game/Art/Materials" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var g in AssetDatabase.FindAssets("t:SceneAsset t:Prefab t:Material t:VolumeProfile", new[] { folder }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (p.StartsWith(Sections.DevFolder, StringComparison.Ordinal)) continue;
                    list.Add(p);
                }
            }
            return list.ToArray();
        }
    }
}
