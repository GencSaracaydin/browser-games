using System.Text;
using LongShadow.Core;
using LongShadow.Core.Editor;
using LongShadow.World.Look.Sky;
using LongShadow.World.Look.Stage;
using UnityEditor;
using UnityEngine;

namespace LongShadow.World.Look.Editor
{
    /// <summary>
    /// Captures the four stage ambient probes into <c>AmbientSHSet</c> (GDD §9.1; TECH_PLAN §7.14 <c>AmbientSHCapture</c>).
    /// Per stage at its representative s: the stage's sky source — the EditorOnly sun-free EXR (cube or lat-long) when it
    /// has been imported, else the shipped LDR sky — is unwrapped to an equirect (Skybox/Panoramic convention), oriented so
    /// its horizon glow sits at azimuth 258° (measured, so Unity's cube import convention does not matter), scaled to the
    /// runtime sky calibration (LDR linear scale x base exposure x tint x 2^EV) and projected to L2 SH with Unity's own
    /// SphericalHarmonicsL2 (self-normalised). Without any sky texture a gradient from the GDD fog colours is stored.
    /// </summary>
    public static class AmbientSHCapture
    {
        private const int Width = 512;
        private const int Height = 256;

        [MenuItem("LongShadow/Look/Capture Ambient SH (4 stages)", priority = 120)]
        public static void CaptureMenu()
        {
            var log = new StringBuilder();
            CaptureAll(log);
            Debug.Log("[LS:Look] " + log);
        }

        /// <summary>Captures every stage into the AmbientSHSet asset.</summary>
        public static void CaptureAll(StringBuilder log)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<LightStageTuning>(LookPaths.TuningAsset);
            if (tuning == null) tuning = LookMaterialBuilder.BuildTuning(log);
            var set = BuilderUtil.LoadOrCreateAsset<AmbientSHSet>(LookPaths.AmbientSHAsset);
            for (var i = 0; i < 4; i++)
            {
                var stage = (StageId)i;
                var entry = CaptureStage(stage, tuning, log);
                set.SetStage(entry);
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Captures one stage.</summary>
        public static AmbientSHStage CaptureStage(StageId stage, LightStageTuning tuning, StringBuilder log)
        {
            var s = LightStageController.RepresentativeS(stage);
            var sample = LightStageTable.Sample(s);
            var useB = stage >= StageId.Afterglow;
            var skyId = useB ? "kiara_9_dusk" : "kiara_8_sunset";
            var manifestId = useB ? LookPaths.HdriDusk : LookPaths.HdriSunset;
            var cal = SkyCalibration.Load(skyId);
            var baseExposure = useB ? tuning.SkyExposureB : tuning.SkyExposureA;
            var tint = sample.SkyTint.linear;
            var ev = Mathf.Pow(2f, sample.SkyEv);
            var captureExposure = PanoBlendSky.LinearExposure(sample, tuning, useB);

            Color[] px = null;
            string source = null;
            var hdrScale = 1f;

            var cube = FindEditorOnly<Cubemap>(manifestId, skyId);
            if (cube != null)
            {
                px = UnwrapCube(cube);
                source = "EXR cube " + AssetDatabase.GetAssetPath(cube);
                hdrScale = cal.LinearScale; // HDR radiance → LDR texel units
            }
            else
            {
                var exr2D = FindEditorOnly<Texture2D>(manifestId, skyId);
                if (exr2D != null)
                {
                    px = Read2D(exr2D);
                    source = "EXR lat-long " + AssetDatabase.GetAssetPath(exr2D);
                    hdrScale = cal.LinearScale;
                }
                else
                {
                    var ldr = AssetManifest.Find<Texture2D>(manifestId, "T_Sky_" + skyId);
                    if (ldr != null)
                    {
                        px = Read2D(ldr);
                        source = "LDR " + AssetDatabase.GetAssetPath(ldr);
                        hdrScale = 1f;
                    }
                }
            }

            UnityEngine.Rendering.SphericalHarmonicsL2 sh;
            if (px != null)
            {
                if (LooksUpsideDown(px, Width, Height))
                {
                    FlipRows(px, Width, Height);
                    log.Append(stage + ": source was upside down, flipped. ");
                }
                var glowAz = MeasureGlowAzimuth(px, Width, Height);
                var rotation = Mathf.DeltaAngle(glowAz, LightStageTable.SunAzimuth);
                var k = hdrScale * baseExposure * ev;
                var scale = new Color(tint.r * k, tint.g * k, tint.b * k, 1f);
                sh = AmbientSHMath.ProjectEquirect(px, Width, Height, 2, scale, rotation);
                log.Append(stage + " SH from " + source + " (glow az " + glowAz.ToString("0") + "°, rotated " + rotation.ToString("0.0") + "°). ");
            }
            else
            {
                var horizon = sample.FogColor.linear * captureExposure * 2.5f;
                sh = AmbientSHMath.Gradient(Color.Lerp(horizon, tint * captureExposure, 0.5f), horizon, horizon * 0.25f);
                source = "gradient fallback (no sky texture)";
                log.Append(stage + ": no sky texture, gradient fallback. ");
            }
            return new AmbientSHStage(stage, s, captureExposure, AmbientSHMath.ToArray(sh), source);
        }

        /// <summary>Compass azimuth of the brightest horizon region (0–20° elevation), luminance²-weighted circular mean.</summary>
        public static float MeasureGlowAzimuth(Color[] px, int w, int h)
        {
            double sx = 0, sz = 0;
            for (var r = 0; r < h; r++)
            {
                var d0 = AmbientSHMath.EquirectDirection(0, r, w, h);
                var elevation = Mathf.Asin(Mathf.Clamp(d0.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (elevation < 0f || elevation > 20f) continue;
                for (var c = 0; c < w; c++)
                {
                    var p = px[r * w + c];
                    var lum = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
                    if (lum <= 0f || float.IsNaN(lum) || float.IsInfinity(lum)) continue;
                    var d = AmbientSHMath.EquirectDirection(c, r, w, h);
                    var az = MathX.BearingOf(d) * Mathf.Deg2Rad;
                    var weight = (double)lum * lum;
                    sx += weight * Mathf.Sin(az);
                    sz += weight * Mathf.Cos(az);
                }
            }
            if (sx * sx + sz * sz < 1e-12) return LightStageTable.SunAzimuth;
            return MathX.WrapAngle360((float)(System.Math.Atan2(sx, sz) * Mathf.Rad2Deg));
        }

        /// <summary>True when the lower half is much brighter than the upper half (a flipped read-back).</summary>
        public static bool LooksUpsideDown(Color[] px, int w, int h)
        {
            double top = 0, bottom = 0;
            for (var r = 0; r < h; r++)
                for (var c = 0; c < w; c += 4)
                {
                    var p = px[r * w + c];
                    var l = p.r + p.g + p.b;
                    if (float.IsNaN(l) || float.IsInfinity(l)) continue;
                    if (r >= h / 2) top += l;
                    else bottom += l;
                }
            return bottom > top * 1.5;
        }

        private static void FlipRows(Color[] px, int w, int h)
        {
            for (var r = 0; r < h / 2; r++)
                for (var c = 0; c < w; c++)
                {
                    var a = r * w + c;
                    var b = (h - 1 - r) * w + c;
                    (px[a], px[b]) = (px[b], px[a]);
                }
        }

        private static T FindEditorOnly<T>(string manifestId, string skyId) where T : Texture
        {
            foreach (var t in AssetManifest.FindAll<T>(manifestId, skyId + "_nosun"))
                if (AssetDatabase.GetAssetPath(t).Contains("/EditorOnly/")) return t;
            var guids = AssetDatabase.FindAssets(skyId + "_nosun t:" + typeof(T).Name, new[] { LookPaths.HdriEditorOnlyFolder });
            foreach (var g in guids)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null) return a;
            }
            return null;
        }

        private static Color[] UnwrapCube(Cubemap cube)
        {
            var shader = Shader.Find(LookPaths.ShaderCubeToEquirect);
            if (shader == null)
            {
                LSLog.Warn(LogCategory.Look, "Hidden/LongShadow/CubeToEquirect missing (land Staging/Assets shaders).");
                return null;
            }
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetTexture("_Cube", cube);
            mat.SetFloat("_Rotation", 0f);
            mat.SetFloat("_Scale", 1f);
            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(null, rt, mat);
            var px = ReadBack(rt);
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(mat);
            return px;
        }

        private static Color[] Read2D(Texture2D tex)
        {
            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            var px = ReadBack(rt);
            RenderTexture.ReleaseTemporary(rt);
            return px;
        }

        private static Color[] ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }
    }
}
