using System.IO;
using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Common;
using LongShadow.World.Look.Sky;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Builds the Look materials and data assets: <c>M_Sky_PanoBlend</c> (A = kiara_8_sunset, B = kiara_9_dusk LDR skies,
    /// procedural fallback until they land; base exposure A from the sky json, GDD §9.3), <c>M_SunDisc</c>, the backdrop
    /// materials (ridge silhouette, far hills), the light-shaft / projector-beam / dust-mote / lamp-glow FX materials, the
    /// practical lamp glass and lens materials (URP/Lit with <c>_EMISSION</c> on, emission driven per renderer at
    /// runtime), particle materials for dust, smoke and fire (URP Particles with the WP10_CC0 flipbooks), the generated
    /// shaft noise texture, the <c>LightStageTuning</c> asset and an empty <c>AmbientSHSet</c>. Idempotent.
    /// </summary>
    public static class LookMaterialBuilder
    {
        [MenuItem("LongShadow/Look/Build Materials and Data", priority = 111)]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            BuildAll(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Builds everything.</summary>
        public static void BuildAll(StringBuilder log)
        {
            var tuning = BuildTuning(log);
            BuilderUtil.LoadOrCreateAsset<AmbientSHSet>(LookPaths.AmbientSHAsset);
            BuildSky(tuning, log);
            BuildSunDisc(log);
            BuildBackdrops(log);
            BuildFx(log);
            BuildPracticalMaterials(log);
            BuildParticleMaterials(log);
            FoliageMaterialUpgrader.Convert(log);
            AssetDatabase.SaveAssets();
        }

        /// <summary>LST_Default with the sky calibration from the json files.</summary>
        public static LightStageTuning BuildTuning(StringBuilder log)
        {
            var t = BuilderUtil.LoadOrCreateAsset<LightStageTuning>(LookPaths.TuningAsset);
            var a = SkyCalibration.Load("kiara_8_sunset");
            var s0 = LightStageTable.Sample(0f);
            if (a.Loaded)
            {
                var expA = a.ExposurePerUnitSun * s0.SunIntensity;
                // B is art-directed (Poly Haven skies are not absolutely calibrated): keep the asset's value.
                t.SetSkyCalibration(expA, t.SkyExposureB);
                log.Append("Sky A exposure " + expA.ToString("0.###") + " from " + a.SourcePath + ". ");
            }
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>M_Sky_PanoBlend.</summary>
        public static Material BuildSky(LightStageTuning tuning, StringBuilder log)
        {
            var m = EnsureMaterial(LookPaths.MatSky, LookPaths.ShaderPanoBlend, log);
            if (m == null) return null;
            var a = AssetManifest.Find<Texture2D>(LookPaths.HdriSunset, "T_Sky_kiara_8");
            var b = AssetManifest.Find<Texture2D>(LookPaths.HdriDusk, "T_Sky_kiara_9");
            m.SetTexture(LookShaderIds.SkyTexA, a);
            m.SetTexture(LookShaderIds.SkyTexB, b != null ? b : a);
            var procedural = a == null;
            SetKeyword(m, "_PANO_PROCEDURAL", procedural);
            m.SetFloat("_Procedural", procedural ? 1f : 0f);
            var calA = SkyCalibration.Load("kiara_8_sunset");
            m.SetColor("_HorizonColor", calA.Horizon);
            m.SetColor("_ZenithColor", calA.Zenith);
            m.SetFloat("_Rotation", 0f);
            PanoBlendSky.Apply(m, LightStageTable.Sample(0f), tuning);
            EditorUtility.SetDirty(m);
            log.Append(procedural ? "Sky: procedural fallback (LDR skies not landed). " : "Sky: LDR skies wired. ");
            return m;
        }

        private static void BuildSunDisc(StringBuilder log)
        {
            var m = EnsureMaterial(LookPaths.MatSunDisc, LookPaths.ShaderSunDisc, log);
            if (m == null) return;
            m.SetFloat("_Overscan", Sky.SunDisc.QuadOverscan);
            m.renderQueue = (int)RenderQueue.Transparent - 400;
            EditorUtility.SetDirty(m);
        }

        private static void BuildBackdrops(StringBuilder log)
        {
            var ridge = EnsureMaterial(LookPaths.MatBackdropRidge, LookPaths.ShaderBackdrop, log);
            if (ridge != null)
            {
                ridge.SetColor("_BaseColor", new Color(0.36f, 0.30f, 0.25f, 1f));
                ridge.SetFloat("_AerialDensity", 0.00055f); // ≈ 0.75 at 2.5 km, capped 0.8 (GDD §9.3)
                ridge.SetFloat("_AerialMax", 0.8f);
                EditorUtility.SetDirty(ridge);
            }
            var hills = EnsureMaterial(LookPaths.MatBackdropHills, LookPaths.ShaderBackdrop, log);
            if (hills != null)
            {
                hills.SetColor("_BaseColor", new Color(0.46f, 0.39f, 0.31f, 1f));
                hills.SetFloat("_AerialDensity", 0.0007f);
                hills.SetFloat("_AerialMax", 0.85f);
                EditorUtility.SetDirty(hills);
            }
            var terrain = AssetManifest.FindAll<Texture2D>("TEX_ACG_TERRAIN003", "_BC");
            if (terrain.Count > 0 && ridge != null) ridge.SetTexture("_BaseMap", terrain[0]);
        }

        private static void BuildFx(StringBuilder log)
        {
            var noise = EnsureNoiseTexture(LookPaths.TexShaftNoise, 128, 7);
            var shaft = EnsureMaterial(LookPaths.MatLightShaft, LookPaths.ShaderLightShaft, log);
            if (shaft != null)
            {
                shaft.SetTexture("_NoiseTex", noise);
                EditorUtility.SetDirty(shaft);
            }
            var beam = EnsureMaterial(LookPaths.MatProjectorBeam, LookPaths.ShaderLightShaft, log);
            if (beam != null)
            {
                beam.SetTexture("_NoiseTex", noise);
                beam.SetFloat("_NoiseSpeed", 0.05f);
                beam.SetFloat("_NoiseAmount", 0.6f);
                beam.SetFloat("_EdgeSoftness", 0.35f);
                beam.SetFloat("_StartFade", 0.02f);
                EditorUtility.SetDirty(beam);
            }
            EnsureMaterial(LookPaths.MatDustMotes, LookPaths.ShaderDustMotes, log);
            var glow = EnsureMaterial(LookPaths.MatLampGlow, LookPaths.ShaderLampGlow, log);
            if (glow != null)
            {
                glow.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(glow);
            }
        }

        private static void BuildPracticalMaterials(StringBuilder log)
        {
            EmissiveLit(LookPaths.MatLampGlass, new Color(0.18f, 0.15f, 0.12f), 0.85f, log);
            EmissiveLit(LookPaths.MatAviationLens, new Color(0.25f, 0.04f, 0.03f), 0.8f, log);
            EmissiveLit(LookPaths.MatFloodLens, new Color(0.30f, 0.30f, 0.28f), 0.9f, log);
            EmissiveLit(LookPaths.MatFlareEmissive, new Color(0.8f, 0.78f, 0.7f), 0.2f, log);
            EmissiveLit(LookPaths.MatEmergencyLens, new Color(0.3f, 0.03f, 0.02f), 0.7f, log);
            EmissiveLit(LookPaths.MatTungstenBulb, new Color(0.6f, 0.55f, 0.45f), 0.8f, log);
            var housing = EnsureMaterial(LookPaths.MatLampHousing, LookPaths.ShaderLit, log);
            if (housing != null)
            {
                housing.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.16f));
                housing.SetFloat("_Metallic", 0.7f);
                housing.SetFloat("_Smoothness", 0.35f);
                MaterialFactory.ApplyUrpKeywords(housing);
                EditorUtility.SetDirty(housing);
            }
        }

        private static void BuildParticleMaterials(StringBuilder log)
        {
            var smoke = AssetManifest.Find<Texture2D>(LookPaths.VfxSmoke, "SmokePuff");
            var dust = AssetManifest.Find<Texture2D>(LookPaths.VfxDust, "DustImpact");
            var fire = AssetManifest.Find<Texture2D>(LookPaths.VfxFire, "Fire");
            Particle(LookPaths.MatDustParticles, LookPaths.ShaderParticlesSimpleLit, smoke != null ? smoke : dust, false, log);
            Particle(LookPaths.MatSmokeParticles, LookPaths.ShaderParticlesSimpleLit, smoke, false, log);
            Particle(LookPaths.MatFireParticles, LookPaths.ShaderParticlesUnlit, fire, true, log);
        }

        private static void Particle(string path, string shader, Texture tex, bool additive, StringBuilder log)
        {
            var m = EnsureMaterial(path, shader, log);
            if (m == null) return;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 1f);
            if (m.HasProperty("_SoftParticlesNearFadeDistance")) m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            if (m.HasProperty("_SoftParticlesFarFadeDistance")) m.SetFloat("_SoftParticlesFarFadeDistance", 0.8f);
            if (m.HasProperty("_CameraFadingEnabled")) m.SetFloat("_CameraFadingEnabled", 1f);
            if (m.HasProperty("_CameraNearFadeDistance")) m.SetFloat("_CameraNearFadeDistance", 0.3f);
            if (m.HasProperty("_CameraFarFadeDistance")) m.SetFloat("_CameraFarFadeDistance", 1.5f);
            if (m.HasProperty("_FlipbookBlending")) m.SetFloat("_FlipbookBlending", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            try
            {
                BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
            }
            catch (System.Exception e)
            {
                LSLog.Warn(LogCategory.Look, "Particle keywords for " + path + ": " + e.Message);
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        /// <summary>URP/Lit with emission enabled (black) for practical glass; runtime sets <c>_EmissionColor</c> per renderer.</summary>
        public static Material EmissiveLit(string path, Color baseColor, float smoothness, StringBuilder log)
        {
            var m = EnsureMaterial(path, LookPaths.ShaderLit, log);
            if (m == null) return null;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_EmissionColor", Color.black);
            m.EnableKeyword("_EMISSION");
            // Practicals are lit at runtime; baked GI must not see their emission (G4: no glowing lightmap after a shot-out lamp).
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            MaterialFactory.ApplyUrpKeywords(m);
            m.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Loads or creates a material asset with a shader (null + log when the shader has not landed).</summary>
        public static Material EnsureMaterial(string path, string shaderName, StringBuilder log)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                log?.Append("Shader '" + shaderName + "' missing (land Staging/Assets shaders): " + Path.GetFileName(path) + " skipped. ");
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
                m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        /// <summary>A tileable value-noise texture saved as PNG (R = noise), generated once.</summary>
        public static Texture2D EnsureNoiseTexture(string path, int size, int seed)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false, true);
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var v = 0f;
                    var amp = 0.55f;
                    var period = size / 4;
                    for (var o = 0; o < 4 && period >= 2; o++)
                    {
                        v += amp * TileNoise(x, y, period, seed + o * 31, size);
                        amp *= 0.5f;
                        period /= 2;
                    }
                    var b = (byte)Mathf.Clamp(Mathf.RoundToInt(v / 1.03f * 255f), 0, 255);
                    px[y * size + x] = new Color32(b, b, b, 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(BuilderUtil.ProjectRoot, path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.sRGBTexture = false;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 128;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float TileNoise(int x, int y, int period, int seed, int size)
        {
            var cell = Mathf.Max(1, period);
            var fx = (float)x / cell;
            var fy = (float)y / cell;
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var tx = fx - x0;
            var ty = fy - y0;
            var cells = Mathf.Max(1, size / cell);
            float H(int i, int j)
            {
                i = ((i % cells) + cells) % cells;
                j = ((j % cells) + cells) % cells;
                var s = Mathf.Sin(i * 127.1f + j * 311.7f + seed * 74.7f) * 43758.5453f;
                return s - Mathf.Floor(s);
            }
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), tx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), tx), ty);
        }

        /// <summary>Sets or clears a material keyword.</summary>
        public static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }
    }
}
