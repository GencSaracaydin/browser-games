// LongShadow WP09 - booth CRT screen (TECH_PLAN §7.12 SG_CRTScreen): curvature, scanlines, flicker, phosphor tint,
// static and degauss wobble over a 512x384 render texture, still or video; the glass reflects the booth probe.
// Driven per CRT by LongShadow.Cinematics.Booth.CRTScreen (material instance on the TV's *_Screen slot).
Shader "LongShadow/SG_CRTScreen"
{
    Properties
    {
        [MainTexture] _ScreenTex ("Screen Content", 2D) = "black" {}
        _UVRect ("Content UV Rect (xy offset, zw size)", Vector) = (0, 0, 1, 1)
        _Scroll ("Content Scroll (uv/s)", Vector) = (0, 0, 0, 0)
        [MainColor] _BaseColor ("Glass Colour (off)", Color) = (0.035, 0.045, 0.04, 1)
        _Smoothness ("Glass Smoothness", Range(0, 1)) = 0.92
        _Metallic ("Metallic (unused)", Range(0, 1)) = 0
        _Reflection ("Reflection Strength", Range(0, 2)) = 0.65
        _Power ("Power", Range(0, 1)) = 0
        _PowerOn ("Power-On Progress", Range(0, 1)) = 1
        _Static ("Static", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 6)) = 1.5
        _Degauss ("Degauss", Range(0, 1)) = 0
        _ScanlineBoost ("Scanline Boost", Range(0, 1)) = 0
        _ScanlineCount ("Scanline Count", Float) = 288
        _Tint ("Phosphor Tint", Color) = (0.86, 1, 0.92, 1)
        _Mono ("Monochrome", Range(0, 1)) = 0
        _Curvature ("Curvature", Range(0, 0.3)) = 0.11
        _Vignette ("Tube Vignette", Range(0, 2)) = 0.9
        _Flash ("White Flash", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ScreenTex_ST;
            float4 _UVRect;
            float4 _Scroll;
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            half _Reflection;
            half _Power;
            half _PowerOn;
            half _Static;
            half _Brightness;
            half _Degauss;
            half _ScanlineBoost;
            float _ScanlineCount;
            half4 _Tint;
            half _Mono;
            half _Curvature;
            half _Vignette;
            half _Flash;
            float _Seed;
        CBUFFER_END

        TEXTURE2D(_ScreenTex);
        SAMPLER(sampler_ScreenTex);

        #include "SG_CRTCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardCRT"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LSCrtVert
            #pragma fragment LSCrtFrag
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            half3 CrtScreenContent(float2 puv, float t)
            {
                float2 tuv = _UVRect.xy + puv * _UVRect.zw + _Scroll.xy * t;
                float scanline = floor(puv.y * _ScanlineCount);
                tuv.x += (LS_Hash11(scanline + floor(t * 24.0) * 7.0) - 0.5) * 0.0015;

                // Chroma bleed of a worn tube.
                half3 col;
                col.r = SAMPLE_TEXTURE2D(_ScreenTex, sampler_ScreenTex, tuv + float2(0.0013, 0.0)).r;
                col.g = SAMPLE_TEXTURE2D(_ScreenTex, sampler_ScreenTex, tuv).g;
                col.b = SAMPLE_TEXTURE2D(_ScreenTex, sampler_ScreenTex, tuv - float2(0.0013, 0.0)).b;
                col = lerp(col, (half3)LS_Luma(col), _Mono);
                col *= _Tint.rgb;

                // Snow with rolling bands.
                float n = LS_Hash12(floor(puv * float2(320.0, 240.0)) + floor(t * 30.0) * float2(17.13, 3.71) + _Seed);
                float roll = LS_Noise(float2(puv.y * 5.0 - t * 3.3, _Seed * 0.37));
                half3 snow = (half3)(n * (0.7 + 0.6 * roll)) * _Tint.rgb;
                return lerp(col, snow, _Static);
            }

            #define LS_CRT_PROCESS CrtScreenContent
            #include "SG_CRTPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings o = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
