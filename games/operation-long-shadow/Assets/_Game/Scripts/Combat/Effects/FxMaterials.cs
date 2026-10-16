using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// URP material setup for combat FX, shared by the runtime fallbacks and the editor FX builder (which saves the results
    /// as assets so the shaders ship in the build): transparent Particles/Unlit (alpha or additive) and the URP screen-space
    /// decal shader ("Shader Graphs/Decal", TECH_PLAN §3.5 Decal feature).
    /// </summary>
    public static class FxMaterials
    {
        /// <summary>URP particle shader.</summary>
        public const string ParticlesUnlitShader = "Universal Render Pipeline/Particles/Unlit";
        /// <summary>URP particle shader with simple lighting (smoke reads the sun and practicals).</summary>
        public const string ParticlesSimpleLitShader = "Universal Render Pipeline/Particles/Simple Lit";
        /// <summary>URP decal shader graph.</summary>
        public const string DecalShader = "Shader Graphs/Decal";

        /// <summary>
        /// A transparent particle material. <paramref name="additive"/> uses One/One blending (flashes, sparks, tracers, fire),
        /// otherwise alpha blending (dust, smoke, blood). Returns null when the shader is unavailable (logged by the caller).
        /// </summary>
        public static Material CreateParticle(string name, Texture texture, Color color, bool additive, bool lit = false)
        {
            var shader = Shader.Find(lit ? ParticlesSimpleLitShader : ParticlesUnlitShader);
            if (shader == null && lit) shader = Shader.Find(ParticlesUnlitShader);
            if (shader == null) return null;
            var m = new Material(shader) { name = name };
            Configure(m, texture, color, additive);
            return m;
        }

        /// <summary>Applies the transparent setup to an existing particle material.</summary>
        public static void Configure(Material m, Texture texture, Color color, bool additive)
        {
            if (m == null) return;
            if (texture != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            SetFloat(m, "_Surface", 1f);                 // transparent
            SetFloat(m, "_Blend", additive ? 2f : 0f);   // 0 alpha, 2 additive
            SetFloat(m, "_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
            SetFloat(m, "_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetFloat(m, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(m, "_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetFloat(m, "_ZWrite", 0f);
            SetFloat(m, "_Cull", (float)CullMode.Off);
            SetFloat(m, "_ColorMode", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>A screen-space decal material (base map with alpha, optional normal map), or null without the shader.</summary>
        public static Material CreateDecal(string name, Texture baseMap, Texture normalMap, float normalBlend = 0.6f)
        {
            var shader = Shader.Find(DecalShader);
            if (shader == null) return null;
            var m = new Material(shader) { name = name };
            ConfigureDecal(m, baseMap, normalMap, normalBlend);
            return m;
        }

        /// <summary>Applies textures to a decal material (property names of URP's default decal graph and common fallbacks).</summary>
        public static void ConfigureDecal(Material m, Texture baseMap, Texture normalMap, float normalBlend)
        {
            if (m == null) return;
            foreach (var p in new[] { "Base_Map", "_BaseMap", "_MainTex" })
                if (baseMap != null && m.HasProperty(p)) m.SetTexture(p, baseMap);
            var hasNormal = false;
            foreach (var p in new[] { "Normal_Map", "_NormalMap", "_BumpMap" })
                if (normalMap != null && m.HasProperty(p))
                {
                    m.SetTexture(p, normalMap);
                    hasNormal = true;
                }
            SetFloat(m, "Normal_Blend", normalBlend);
            SetFloat(m, "_NormalBlend", normalBlend);
            SetFloat(m, "_AffectAlbedo", 1f);
            SetFloat(m, "_AffectNormal", hasNormal ? 1f : 0f);
            if (hasNormal) m.EnableKeyword("_MATERIAL_AFFECTS_NORMAL");
            m.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
            m.enableInstancing = true;
        }

        /// <summary>Sets the base-map scale/offset (sub-rect of an atlas, e.g. the red half of the tracer texture).</summary>
        public static void SetBaseMapRect(Material m, Vector2 scale, Vector2 offset)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
            }
            else if (m.HasProperty("_MainTex"))
            {
                m.SetTextureScale("_MainTex", scale);
                m.SetTextureOffset("_MainTex", offset);
            }
        }

        private static void SetFloat(Material m, string name, float v)
        {
            if (m.HasProperty(name)) m.SetFloat(name, v);
        }
    }
}
