using System.Collections.Generic;
using System.IO;
using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using LongShadow.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LongShadow.Cinematics.Editor
{
    /// <summary>
    /// Builds the WP09 materials (one per hand-written shader, Art/Materials/FX/Cinematics), the FX volume profiles owned
    /// by WP09 (TECH_PLAN §3.7: VP_Death, VP_LowHealth, VP_Suppression, VP_Flash), a
    /// fallback VP_Booth when WP10's is missing, and the booth smoke material. Profile assets keep their effects in the
    /// build's post-processing variant set. Idempotent: assets are updated in place.
    /// </summary>
    public static class CinematicAssetsBuilder
    {
        /// <summary>Fallback booth grade when WP10 has not made Settings/Volumes/VP_Booth.asset.</summary>
        public const string BoothFallbackProfile = CinematicPaths.BoothPrefabs + "/Profiles/VP_Booth_WP09.asset";
        /// <summary>WP10's booth profile (TECH_PLAN §3.7).</summary>
        public const string BoothProfile = CinematicPaths.FxProfiles + "/VP_Booth.asset";

        /// <summary>Builds materials and profiles; returns lines for the report.</summary>
        public static List<string> Build()
        {
            var log = new List<string>();
            BuilderUtil.EnsureFolder(CinematicPaths.FxMaterials);
            Mat(log, CinematicAssetIds.MatCrt, ShaderIds.CrtShader, null);
            Mat(log, CinematicAssetIds.MatGunCam, ShaderIds.GunCamShader, null);
            Mat(log, CinematicAssetIds.MatFilm, ShaderIds.FilmShader, null);
            Mat(log, CinematicAssetIds.MatFilmBurn, ShaderIds.FilmBurnShader, null);
            Mat(log, CinematicAssetIds.MatGlitch, ShaderIds.GlitchShader, null);
            Mat(log, CinematicAssetIds.MatGlitchOverlay, ShaderIds.GlitchOverlayShader, null);
            Mat(log, CinematicAssetIds.MatFilmBeam, ShaderIds.FilmBeamShader, null);
            SmokeMaterial(log);

            Profile(log, CinematicAssetIds.ProfileDeath, ScreenFXService.BuildDeath);
            Profile(log, CinematicAssetIds.ProfileLowHealth, ScreenFXService.BuildLowHealth);
            Profile(log, CinematicAssetIds.ProfileSuppression, ScreenFXService.BuildSuppression);
            Profile(log, CinematicAssetIds.ProfileFlash, ScreenFXService.BuildFlash);
            // VP_Cinematic and VP_Booth are WP10_LOOK's grades (Settings/Volumes); the lens volume clones VP_Cinematic.
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(BoothProfile) == null)
            {
                ProfileAt(log, BoothFallbackProfile, BoothLook.BuildFallback);
                log.Add("VP_Booth: WP10's " + BoothProfile + " missing; using " + BoothFallbackProfile);
            }
            AssetDatabase.SaveAssets();
            return log;
        }

        private static void Mat(List<string> log, string id, string shaderName, System.Action<Material> setup)
        {
            var path = CinematicPaths.FxMaterials + "/" + id + ".mat";
            var shader = Shader.Find(shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (shader == null)
            {
                log.Add("ERROR shader '" + shaderName + "' not found (land Staging/Assets/_Game/Art/Shaders with land.py --assets and check the console)");
                if (m == null) return;
            }
            if (m == null)
            {
                m = new Material(shader) { name = id };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null && m.shader != shader) m.shader = shader;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            log.Add(id + " (" + shaderName + ")");
        }

        private static void SmokeMaterial(List<string> log)
        {
            var path = CinematicPaths.FxMaterials + "/" + CinematicAssetIds.MatSmoke + ".mat";
            var texPath = CinematicPaths.FxMaterials + "/T_FX_SmokePuff.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                tex = BoothSmoke.CreatePuff(false);
                tex.name = "T_FX_SmokePuff";
                AssetDatabase.CreateAsset(tex, texPath);
            }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader == null)
            {
                log.Add("ERROR URP Particles/Simple Lit not found");
                return;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = CinematicAssetIds.MatSmoke };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.85f, 0.87f, 0.9f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SoftParticlesEnabled", 1f);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            m.SetFloat("_SoftParticlesFarFadeDistance", 0.25f);
            m.SetFloat("_ReceiveShadows", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_SOFTPARTICLES_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            log.Add(CinematicAssetIds.MatSmoke + " (URP Particles/Simple Lit, soft, generated puff)");
        }

        private static void Profile(List<string> log, string id, System.Action<VolumeProfile> build) =>
            ProfileAt(log, CinematicPaths.FxProfiles + "/" + id + ".asset", build);

        private static void ProfileAt(List<string> log, string path, System.Action<VolumeProfile> build)
        {
            BuilderUtil.EnsureFolder(Path.GetDirectoryName(path));
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            foreach (var c in new List<VolumeComponent>(p.components))
            {
                if (c == null) continue;
                AssetDatabase.RemoveObjectFromAsset(c);
                Object.DestroyImmediate(c, true);
            }
            p.components.Clear();
            build(p);
            foreach (var c in p.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            log.Add(Path.GetFileNameWithoutExtension(path) + ": " + p.components.Count + " overrides");
        }
    }
}
