using UnityEngine;

namespace LongShadow.Cinematics.FX
{
    /// <summary>
    /// Shader names and property ids of the WP09 shaders (Assets/_Game/Art/Shaders/SG_*.shader, hand-written URP HLSL,
    /// DECISIONS.md [WP09]). The shaders declare exactly these properties; materials are built by the cinematic
    /// materials builder and listed in LIB_Cinematics so the shaders are included in the build.
    /// </summary>
    public static class ShaderIds
    {
        // ---- shader names
        public const string CrtShader = "LongShadow/SG_CRTScreen";
        public const string GunCamShader = "LongShadow/SG_GunCam";
        public const string FilmShader = "LongShadow/SG_Film16mm";
        public const string FilmBurnShader = "LongShadow/SG_FilmBurn";
        public const string GlitchShader = "LongShadow/SG_Glitch";
        public const string GlitchOverlayShader = "LongShadow/SG_GlitchOverlay";
        public const string FilmBeamShader = "LongShadow/SG_FilmBeam";

        /// <summary>Global unscaled time in seconds (inserts freeze <c>_Time</c>; the booth must keep moving).</summary>
        public static readonly int UnscaledTime = Shader.PropertyToID("_LS_UnscaledTime");

        // ---- CRT and gun camera
        public static readonly int ScreenTex = Shader.PropertyToID("_ScreenTex");
        public static readonly int UVRect = Shader.PropertyToID("_UVRect");
        public static readonly int Scroll = Shader.PropertyToID("_Scroll");
        public static readonly int Power = Shader.PropertyToID("_Power");
        public static readonly int PowerOn = Shader.PropertyToID("_PowerOn");
        public static readonly int Static = Shader.PropertyToID("_Static");
        public static readonly int Brightness = Shader.PropertyToID("_Brightness");
        public static readonly int Degauss = Shader.PropertyToID("_Degauss");
        public static readonly int ScanlineBoost = Shader.PropertyToID("_ScanlineBoost");
        public static readonly int Tint = Shader.PropertyToID("_Tint");
        public static readonly int Mono = Shader.PropertyToID("_Mono");
        public static readonly int Seed = Shader.PropertyToID("_Seed");
        public static readonly int Flash = Shader.PropertyToID("_Flash");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        // ---- film
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Burn = Shader.PropertyToID("_Burn");
        public static readonly int Fps = Shader.PropertyToID("_Fps");
        public static readonly int Exposure = Shader.PropertyToID("_Exposure");
        public static readonly int Weave = Shader.PropertyToID("_Weave");
        public static readonly int Scratches = Shader.PropertyToID("_Scratches");
        public static readonly int LeaderSweep = Shader.PropertyToID("_LeaderSweep");

        // ---- glitch
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int White = Shader.PropertyToID("_White");
        public static readonly int Split = Shader.PropertyToID("_Split");
        public static readonly int Tear = Shader.PropertyToID("_Tear");
        public static readonly int Color = Shader.PropertyToID("_Color");
    }
}
