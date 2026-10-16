using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Lightmaps;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Per-section lightmap bakes (TECH_PLAN §3.8, D6, D7, D16; GDD §9.4). For each <see cref="SectionBakeSpec"/> of a
    /// section it opens the section scene alone, enables an EditorOnly <c>BAKE_Sun</c> (Mixed/Baked Indirect, or Baked for
    /// S9_TownFar) at the spec's elevation, colour and bounce multiplier, sets the environment to the stage's sun-free
    /// sky (EditorOnly EXR cube rotated so its glow sits at 258°, scaled to the runtime sky calibration; LDR or
    /// procedural fallback), applies <c>LS_&lt;Section&gt;.lighting</c> (Progressive GPU, non-directional, 2048 max, padding 4,
    /// Normal Quality, baked AO, 2 bounces, Baked Indirect), places light probes along the Appendix A critical path when
    /// the section has none, bakes synchronously (lightmaps + reflection probes), lowers the texel density and re-bakes
    /// until the D16 cap holds, copies alternate sets (TownFar SUNSET/DUSK, S6 EMERGENCY) into <c>Data/Lighting</c> as
    /// <see cref="LightmapSet"/>s at reduced texture size, installs the <see cref="LightmapSwapper"/>, disables BAKE_Sun
    /// (no second sun in play mode), restores the skybox to <c>M_Sky_PanoBlend</c> (no HDR cubemap in bundles) and saves.
    /// Report: <c>Logs/look_bake.txt</c>.
    /// </summary>
    public static class SectionBaker
    {
        /// <summary>Name of the EditorOnly bake sun.</summary>
        public const string BakeSunName = "BAKE_Sun";
        private const string EditorOnlyTag = "EditorOnly";
        private const string LogPath = "Logs/look_bake.txt";

        [MenuItem("LongShadow/Look/Bake/S1 Approach", priority = 200)] private static void B1() => BakeMenu(SectionId.Approach);
        [MenuItem("LongShadow/Look/Bake/S2 Market", priority = 201)] private static void B2() => BakeMenu(SectionId.Market);
        [MenuItem("LongShadow/Look/Bake/S3 Square", priority = 202)] private static void B3() => BakeMenu(SectionId.Square);
        [MenuItem("LongShadow/Look/Bake/S4 Roofs", priority = 203)] private static void B4() => BakeMenu(SectionId.Roofs);
        [MenuItem("LongShadow/Look/Bake/S5 Station Exterior", priority = 204)] private static void B5() => BakeMenu(SectionId.StationExt);
        [MenuItem("LongShadow/Look/Bake/S6 Station Interior (+ emergency set)", priority = 205)] private static void B6() => BakeMenu(SectionId.StationInt);
        [MenuItem("LongShadow/Look/Bake/S7 Pitch", priority = 206)] private static void B7() => BakeMenu(SectionId.Pitch);
        [MenuItem("LongShadow/Look/Bake/S8 Heli (probes)", priority = 207)] private static void B8() => BakeMenu(SectionId.Heli);
        [MenuItem("LongShadow/Look/Bake/S9 Town Far (GOLDEN + SUNSET + DUSK)", priority = 208)] private static void B9() => BakeMenu(SectionId.TownFar);
        [MenuItem("LongShadow/Look/Bake/S0 Booth", priority = 209)] private static void B0() => BakeMenu(SectionId.Booth);

        [MenuItem("LongShadow/Look/Bake/All Sections", priority = 220)]
        private static void BakeAllMenu()
        {
            var log = new StringBuilder();
            foreach (SectionId id in Enum.GetValues(typeof(SectionId))) Bake(id, log);
            Finish(log);
        }

        private static void BakeMenu(SectionId id)
        {
            var log = new StringBuilder();
            Bake(id, log);
            Finish(log);
        }

        private static void Finish(StringBuilder log)
        {
            var full = Path.Combine(BuilderUtil.ProjectRoot, LogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, log.ToString());
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Bakes every set of a section. Returns false when the scene is missing or a bake failed.</summary>
        public static bool Bake(SectionId section, StringBuilder log)
        {
            var specs = SectionBakeTable.For(section);
            var path = Sections.ScenePath(section);
            if (specs.Count == 0) return true;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                log.AppendLine(section + ": scene " + path + " not found (WP10 Level builds it). Skipped.");
                return false;
            }
            EditorSceneManager.SaveOpenScenes();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var final = specs[specs.Count - 1];
            var settings = EnsureLightingSettings(section, final.TexelsPerMeter);
            Lightmapping.lightingSettings = settings;
            EnsureProbes(final, log);

            var alternates = new List<LightmapSet>();
            var density = final.TexelsPerMeter;
            var ok = true;
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var fit = i == 0; // the first bake fits the density to the cap; later sets reuse it (same atlas layout)
                if (!BakeOne(spec, settings, ref density, fit, final.CapEq, log))
                {
                    ok = false;
                    break;
                }
                if (spec != final) alternates.Add(CopyAlternate(section, spec, log));
            }
            if (ok && specs.Count > 1) InstallSwapper(section, final, alternates, log);

            RestoreSkybox();
            var sun = GameObject.Find(BakeSunName);
            if (sun != null) sun.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return ok;
        }

        private static bool BakeOne(SectionBakeSpec spec, LightingSettings settings, ref float density, bool fit, float capEq, StringBuilder log)
        {
            var sample = LightStageTable.Sample(spec.StageS);
            var sun = EnsureBakeSun();
            var el = float.IsNaN(spec.SunElevation) ? sample.SunElevation : spec.SunElevation;
            sun.gameObject.SetActive(true);
            sun.enabled = spec.SunOn;
            sun.transform.rotation = LightStageTable.LightRotation(el);
            sun.color = Mathf.CorrelatedColorTemperatureToRGB(sample.SunKelvin);
            sun.intensity = Mathf.Max(0.05f, sample.SunIntensity);
            sun.bounceIntensity = spec.IndirectMultiplier;
            sun.lightmapBakeType = spec.SunBakeType;
            sun.shadows = LightShadows.Soft;
            sun.shadowAngle = 0.53f; // the sun's real angular diameter: realistic baked penumbrae

            var toggled = ApplyLightFilters(spec);
            var temp = spec.BakeLampMarkers ? AddLampBakeLights() : new List<GameObject>();
            SetEnvironment(spec, sample, log);

            var attempts = fit ? 4 : 1;
            var success = false;
            for (var a = 0; a < attempts; a++)
            {
                settings.lightmapResolution = density;
                var t0 = DateTime.Now;
                if (!Lightmapping.Bake())
                {
                    log.AppendLine(spec.Section + " " + spec.SetName + ": Lightmapping.Bake failed.");
                    break;
                }
                var eq = CurrentEquivalents(out var count);
                log.AppendLine(spec.Section + " " + spec.SetName + ": " + count + " lightmap(s), " + eq.ToString("0.00") + " eq at " +
                               density.ToString("0.##") + " texels/m (cap " + capEq + "), " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s. " + spec.Notes);
                success = true;
                if (!fit || capEq <= 0f || eq <= capEq * 1.001f) break;
                density *= Mathf.Sqrt(capEq / eq) * 0.95f;
                log.AppendLine("  over the D16 cap: re-baking at " + density.ToString("0.##") + " texels/m.");
            }

            foreach (var go in temp) Object.DestroyImmediate(go);
            foreach (var l in toggled) l.enabled = true;
            return success;
        }

        /// <summary>Lightmap count and 1024² equivalents of the open scene's bake.</summary>
        public static float CurrentEquivalents(out int count)
        {
            var maps = LightmapSettings.lightmaps;
            count = maps != null ? maps.Length : 0;
            var eq = 0f;
            if (maps == null) return 0f;
            foreach (var m in maps)
                if (m != null && m.lightmapColor != null)
                    eq += m.lightmapColor.width * (float)m.lightmapColor.height / (1024f * 1024f);
            return eq;
        }

        private static Light EnsureBakeSun()
        {
            var go = GameObject.Find(BakeSunName);
            if (go == null)
            {
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == BakeSunName) go = root;
            }
            if (go == null) go = new GameObject(BakeSunName);
            go.tag = EditorOnlyTag;
            var l = go.GetComponent<Light>() ?? go.AddComponent<Light>();
            l.type = LightType.Directional;
            return l;
        }

        private static List<Light> ApplyLightFilters(SectionBakeSpec spec)
        {
            var off = new List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l == null || !l.enabled || l.name == BakeSunName) continue;
                var disable = false;
                if (!string.IsNullOrEmpty(spec.DisableLightsContaining) && l.name.IndexOf(spec.DisableLightsContaining, StringComparison.OrdinalIgnoreCase) >= 0) disable = true;
                if (!string.IsNullOrEmpty(spec.DisableLightsUnder))
                    for (var t = l.transform; t != null && !disable; t = t.parent)
                        if (t.name.IndexOf(spec.DisableLightsUnder, StringComparison.OrdinalIgnoreCase) >= 0) disable = true;
                if (!disable) continue;
                l.enabled = false;
                off.Add(l);
            }
            return off;
        }

        private static List<GameObject> AddLampBakeLights()
        {
            var list = new List<GameObject>();
            foreach (var m in Object.FindObjectsByType<SceneMarker>(FindObjectsInactive.Exclude))
            {
                if (m == null || string.IsNullOrEmpty(m.Id) || !m.Id.StartsWith("LAMP.", StringComparison.Ordinal)) continue;
                var go = new GameObject("BAKE_Lamp_" + m.Id) { tag = EditorOnlyTag };
                go.transform.position = m.transform.position + Vector3.down * 0.25f;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.lightmapBakeType = LightmapBakeType.Baked;
                l.color = new Color(1f, 0.56f, 0.19f);
                l.intensity = 8f;
                l.range = 18f;
                l.shadows = LightShadows.Soft;
                l.shapeRadius = 0.15f;
                list.Add(go);
            }
            return list;
        }

        private static void SetEnvironment(SectionBakeSpec spec, LightStageSample sample, StringBuilder log)
        {
            if (spec.EnvironmentOff)
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                return;
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            var mat = BakeSkyMaterial(spec, sample, log);
            RenderSettings.skybox = mat;
        }

        /// <summary>The EditorOnly bake environment for a stage (never referenced after the bake).</summary>
        public static Material BakeSkyMaterial(SectionBakeSpec spec, LightStageSample sample, StringBuilder log)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<LightStageTuning>(LookPaths.TuningAsset) ?? LightStageTuning.CreateDefault();
            var useB = sample.S >= LightStageTable.SunsetS;
            var skyId = useB ? "kiara_9_dusk" : "kiara_8_sunset";
            var manifestId = useB ? LookPaths.HdriDusk : LookPaths.HdriSunset;
            var cal = SkyCalibration.Load(skyId);
            var baseExp = useB ? tuning.SkyExposureB : tuning.SkyExposureA;
            var k = baseExp * Mathf.Pow(2f, sample.SkyEv);
            var tintLin = sample.SkyTint.linear;

            Material mat = null;
            var cube = FindEditorOnlyCube(skyId);
            if (cube != null)
            {
                var shader = Shader.Find("Skybox/Cubemap");
                mat = BakeMaterial(shader);
                mat.SetTexture("_Tex", cube);
                mat.SetFloat("_Exposure", k * cal.LinearScale);
                mat.SetFloat("_Rotation", MathX.WrapAngle360(RotationFor(cube)));
            }
            else
            {
                var ldr = AssetManifest.Find<Texture2D>(manifestId, "T_Sky_" + skyId);
                if (ldr != null)
                {
                    var shader = Shader.Find("Skybox/Panoramic");
                    mat = BakeMaterial(shader);
                    mat.SetTexture("_MainTex", ldr);
                    mat.SetFloat("_Exposure", k);
                    mat.SetFloat("_Rotation", 0f);
                    log.AppendLine("  bake environment: LDR " + skyId + " (EditorOnly EXR not imported).");
                }
            }
            if (mat == null)
            {
                log.AppendLine("  bake environment: procedural M_Sky_PanoBlend (no sky textures).");
                return AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSky);
            }
            // Skybox/Cubemap and /Panoramic multiply by _Tint x unity_ColorSpaceDouble (4.59 in linear): encode the stage tint.
            var tint = new Color(tintLin.r / 4.59f, tintLin.g / 4.59f, tintLin.b / 4.59f, 1f).gamma;
            mat.SetColor("_Tint", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BakeMaterial(Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSkyBake);
            if (mat == null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(LookPaths.MatSkyBake));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, LookPaths.MatSkyBake);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        private static Cubemap FindEditorOnlyCube(string skyId)
        {
            foreach (var g in AssetDatabase.FindAssets(skyId + "_nosun t:Cubemap", new[] { LookPaths.HdriEditorOnlyFolder }))
            {
                var c = AssetDatabase.LoadAssetAtPath<Cubemap>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null) return c;
            }
            return null;
        }

        private static readonly Dictionary<string, float> s_rotationCache = new Dictionary<string, float>();

        /// <summary>Skybox/Cubemap rotation that puts the cube's horizon glow at azimuth 258° (measured once per cube).</summary>
        public static float RotationFor(Cubemap cube)
        {
            var key = AssetDatabase.GetAssetPath(cube);
            if (s_rotationCache.TryGetValue(key, out var r)) return r;
            var shader = Shader.Find(LookPaths.ShaderCubeToEquirect);
            if (shader == null) return 0f;
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetTexture("_Cube", cube);
            const int w = 256, h = 128;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(null, rt, mat);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(mat);
            var az = AmbientSHCapture.MeasureGlowAzimuth(px, w, h);
            r = Mathf.DeltaAngle(az, LightStageTable.SunAzimuth);
            s_rotationCache[key] = r;
            return r;
        }

        private static void RestoreSkybox()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(LookPaths.MatSky);
            RenderSettings.skybox = sky; // never leave an HDR cubemap referenced by a shipped scene (§9.3)
        }

        /// <summary>Creates or updates <c>LS_&lt;Section&gt;.lighting</c> with the TECH_PLAN §3.8 values.</summary>
        public static LightingSettings EnsureLightingSettings(SectionId section, float texelsPerMeter)
        {
            var path = LookPaths.LightingSettingsPath(section);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
                ls = new LightingSettings { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(ls, path);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.directionalityMode = LightmapsMode.NonDirectional;
            ls.lightmapMaxSize = 2048;
            ls.lightmapPadding = 4;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            ls.ao = true;
            ls.aoMaxDistance = 1f;
            ls.aoExponentIndirect = 1f;
            ls.aoExponentDirect = 0f;
            ls.maxBounces = 2;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapResolution = texelsPerMeter;
            EditorUtility.SetDirty(ls);
            return ls;
        }

        private static void EnsureProbes(SectionBakeSpec spec, StringBuilder log)
        {
            if (spec.ProbeBeats == null || spec.ProbeBeats.Length == 0) return;
            if (Object.FindAnyObjectByType<LightProbeGroup>(FindObjectsInactive.Include) != null) return;
            var positions = ProbePlacer.AlongBeats(spec.ProbeBeats);
            if (positions.Count == 0) return;
            var go = new GameObject("LightProbes_CriticalPath");
            var group = go.AddComponent<LightProbeGroup>();
            group.probePositions = positions.ToArray();
            log.AppendLine("  placed " + positions.Count + " light probes along beats " + string.Join(",", spec.ProbeBeats) + " (none in the scene).");
        }

        private static LightmapSet CopyAlternate(SectionId section, SectionBakeSpec spec, StringBuilder log)
        {
            var folder = LookPaths.LightmapSetFolder(section) + "/" + spec.SetName;
            BuilderUtil.EnsureFolder(folder);
            var maps = LightmapSettings.lightmaps;
            var copies = new Texture2D[maps.Length];
            for (var i = 0; i < maps.Length; i++)
            {
                var src = maps[i] != null ? maps[i].lightmapColor : null;
                if (src == null) continue;
                var srcPath = AssetDatabase.GetAssetPath(src);
                var dst = folder + "/Lightmap-" + i + "_" + spec.SetName + Path.GetExtension(srcPath);
                AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(srcPath, dst))
                {
                    log.AppendLine("  copy failed: " + srcPath + " → " + dst);
                    continue;
                }
                if (AssetImporter.GetAtPath(dst) is TextureImporter ti)
                {
                    ti.maxTextureSize = Mathf.Min(ti.maxTextureSize, spec.AlternateMaxSize);
                    ti.SaveAndReimport();
                }
                copies[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            }
            var set = BuilderUtil.LoadOrCreateAsset<LightmapSet>(LookPaths.LightmapSetFolder(section) + "/LMS_" + Sections.SceneName(section) + "_" + spec.SetName + ".asset");
            set.EditorSet(spec.SetName, Sections.SceneName(section), copies);
            log.AppendLine("  " + spec.SetName + " set copied (" + copies.Length + " maps, max " + spec.AlternateMaxSize + " px).");
            return set;
        }

        private static void InstallSwapper(SectionId section, SectionBakeSpec final, List<LightmapSet> alternates, StringBuilder log)
        {
            var maps = LightmapSettings.lightmaps;
            var baseTex = new Texture2D[maps.Length];
            for (var i = 0; i < maps.Length; i++) baseTex[i] = maps[i]?.lightmapColor;
            var baseSet = BuilderUtil.LoadOrCreateAsset<LightmapSet>(LookPaths.LightmapSetFolder(section) + "/LMS_" + Sections.SceneName(section) + "_" + final.SetName + ".asset");
            baseSet.EditorSet(final.SetName, Sections.SceneName(section), baseTex);
            foreach (var alt in alternates)
                if (alt != null && alt.Count != baseSet.Count)
                    log.AppendLine("  WARNING: set " + alt.SetName + " has " + alt.Count + " maps, base " + baseSet.Count + " (layouts differ; re-bake the section).");

            var go = GameObject.Find("LightmapSwapper") ?? new GameObject("LightmapSwapper");
            var swapper = go.GetComponent<LightmapSwapper>() ?? go.AddComponent<LightmapSwapper>();
            if (section == SectionId.TownFar)
            {
                // Alternates in threshold order: SUNSET at 1.35, DUSK at 1.97 (D7).
                var ordered = new LightmapSet[2];
                foreach (var a in alternates)
                {
                    if (a == null) continue;
                    if (a.SetName == "SUNSET") ordered[0] = a;
                    else if (a.SetName == "DUSK") ordered[1] = a;
                }
                swapper.EditorSetup("S9.TownFar", baseSet, ordered, LightmapSwapRule.StageThresholds, SectionBakeTable.TownFarThresholds, null);
            }
            else
                swapper.EditorSetup(Sections.SceneName(section) + ".Lightmaps", baseSet, alternates.ToArray(), LightmapSwapRule.MissionEvent,
                    null, Common.LookIds.TransmitterDead);
            AssetDatabase.SaveAssets();
            log.AppendLine("  LightmapSwapper installed (" + (alternates.Count + 1) + " sets).");
        }
    }
}
