// OPERATION LONG SHADOW (WP10 Look): TECH_PLAN §7.14 "SG_FoliageWind" as a hand-written URP 17.6 shader.
// Lit (metallic workflow, D4 mask), alpha-clipped, two-sided, global wind from LSWind.hlsl in every pass, back-light
// translucency. Property names match URP/Lit, so LongShadow/Look/Convert Foliage Materials swaps the shader in place.
Shader "LongShadow/World/FoliageWind"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (A = opacity)", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 1.0

        [Toggle(_MASKMAP)] _UseMask("Use Mask Map (_MetallicGlossMap)", Float) = 0.0
        [NoScaleOffset] _MetallicGlossMap("Mask (R metal, G AO, A smooth)", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.25
        _OcclusionStrength("Occlusion", Range(0.0, 1.0)) = 1.0

        [Toggle(_NORMALMAP)] _UseNormal("Use Normal Map", Float) = 0.0
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        [Header(Wind (global _LS_Wind from WindController))]
        [Toggle(_WIND_VERTEXCOLOR)] _WindVertexColor("Vertex colour mask (R sway, G phase, B flutter)", Float) = 1.0
        _WindStrength("Sway Strength", Range(0.0, 4.0)) = 1.0
        _WindHeight("Height for full sway (no vertex colours), m", Float) = 4.0
        _FlutterAmplitude("Leaf Flutter (m)", Range(0.0, 0.2)) = 0.025

        [Header(Translucency)]
        _Translucency("Back-light Translucency", Range(0.0, 2.0)) = 0.6
        _TranslucencyPower("Back-light Focus", Range(1.0, 16.0)) = 4.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0.0
        [HideInInspector] _Surface("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageForwardVertex
            #pragma fragment FoliageForwardFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _MASKMAP
            #pragma shader_feature_local _WIND_VERTEXCOLOR
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #define LS_FOLIAGE_FORWARD 1
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindInput.hlsl"
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageShadowVertex
            #pragma fragment FoliageShadowFragment
            #pragma shader_feature_local _WIND_VERTEXCOLOR
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #define LS_FOLIAGE_SHADOW 1
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindInput.hlsl"
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageDepthVertex
            #pragma fragment FoliageDepthFragment
            #pragma shader_feature_local _WIND_VERTEXCOLOR
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #define LS_FOLIAGE_DEPTH 1
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindInput.hlsl"
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FoliageDepthVertex
            #pragma fragment FoliageDepthNormalsFragment
            #pragma shader_feature_local _WIND_VERTEXCOLOR
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #define LS_FOLIAGE_DEPTH 1
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindInput.hlsl"
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment FoliageMetaFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION

            #define LS_FOLIAGE_META 1
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindInput.hlsl"
            #include "Assets/_Game/Art/Shaders/Look/FoliageWindPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
