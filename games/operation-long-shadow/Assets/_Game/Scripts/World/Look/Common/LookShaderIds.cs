using UnityEngine;

namespace LongShadow.World.Look.Common
{
    /// <summary>
    /// Shader property names written by the Look systems. The <c>_LS_*</c> globals are a contract with every shader
    /// that reacts to light or wind (Look's own shaders, and WP10 Level's cloth/awning materials, which cannot reference
    /// this assembly): read them as plain shader globals.
    /// </summary>
    public static class LookShaderIds
    {
        // ---- globals (Shader.SetGlobal*)
        /// <summary>float4: xyz = normalised world direction the wind blows toward (west → east), w = speed m/s.</summary>
        public static readonly int WindDirection = Shader.PropertyToID("_LS_WindDirection");
        /// <summary>float4: x = gust 0..1, y = turbulence 0..1, z = wind time (s, scaled by speed), w = flutter time.</summary>
        public static readonly int WindParams = Shader.PropertyToID("_LS_WindParams");
        /// <summary>float4: rgb = linear colour x intensity of the key light (sun or sky key), a = sun disc visibility.</summary>
        public static readonly int SunColor = Shader.PropertyToID("_LS_SunColor");
        /// <summary>float4: xyz = world direction toward the key light, w = 1 while the shadowed SUN is the key light.</summary>
        public static readonly int SunDirection = Shader.PropertyToID("_LS_SunDirection");
        /// <summary>float4: x = LightStage s, y = stage post exposure (EV), z = 1 after sunset, w = story clock minutes.</summary>
        public static readonly int StageParams = Shader.PropertyToID("_LS_StageParams");

        // ---- Sky/PanoBlend
        public static readonly int SkyTexA = Shader.PropertyToID("_TexA");
        public static readonly int SkyTexB = Shader.PropertyToID("_TexB");
        public static readonly int SkyBlend = Shader.PropertyToID("_Blend");
        public static readonly int SkyTint = Shader.PropertyToID("_Tint");
        public static readonly int SkyExposure = Shader.PropertyToID("_Exposure");
        public static readonly int SkyExposureA = Shader.PropertyToID("_ExposureA");
        public static readonly int SkyExposureB = Shader.PropertyToID("_ExposureB");
        public static readonly int SkyRotation = Shader.PropertyToID("_Rotation");
        public static readonly int SkyGlowBoost = Shader.PropertyToID("_GlowBoost");
        public static readonly int SkyHorizonFog = Shader.PropertyToID("_HorizonFog");
        public static readonly int SkyHorizonFogHeight = Shader.PropertyToID("_HorizonFogHeight");

        // ---- per-material / per-renderer (MaterialPropertyBlock)
        /// <summary>URP/Lit emission colour (HDR).</summary>
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        /// <summary>Base colour (URP/Lit, Unlit, particles, Look shaders).</summary>
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        /// <summary>Look additive shaders: overall intensity.</summary>
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
    }
}
