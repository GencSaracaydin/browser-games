// OPERATION LONG SHADOW (WP10 Look): the sun disc (GDD §9.3, TECH_PLAN §7.14 "SG_SunDisc").
// HDR emissive billboard placed 2.9 km out by LongShadow.World.Look.Sky.SunDisc; additive after the skybox, depth-tested
// (the 2.5 km ridge silhouette hides it in MID-A), never fogged. The quad is 1.6x the disc: limb-darkened core plus a
// thin aureole for bloom. Colour (linear HDR) comes from a MaterialPropertyBlock (_BaseColor).
Shader "LongShadow/World/SunDisc"
{
    Properties
    {
        [HDR] _BaseColor("Core Colour (HDR)", Color) = (40, 30, 18, 1)
        _Overscan("Quad / Disc Ratio", Float) = 1.6
        _LimbDarkening("Limb Darkening", Range(0, 1)) = 0.55
        _Aureole("Aureole", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-400" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "SunDisc"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Overscan;
            float _LimbDarkening;
            float _Aureole;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float r = length(input.uv - 0.5) * 2.0 * _Overscan; // 1 = disc edge
                float edge = fwidth(r) * 1.5 + 1e-4;
                float inside = 1.0 - smoothstep(1.0 - edge, 1.0 + edge, r);
                float mu = sqrt(saturate(1.0 - r * r));
                float limb = 1.0 - _LimbDarkening * (1.0 - mu);
                float aureole = _Aureole * exp(-max(r - 1.0, 0.0) * 7.0) * saturate((_Overscan - r) / max(1e-3, _Overscan - 1.0));
                half3 c = _BaseColor.rgb * (inside * limb + aureole);
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
