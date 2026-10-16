// OPERATION LONG SHADOW (WP10 Look): TECH_PLAN §7.14 "SG_SodiumLamp" glow part / GDD §9.5 "distant lamps are emissive
// sprites with bloom and cast no light". A camera-facing additive glow quad at the lamp head; its colour is
// _EmissionColor so PracticalLight / SodiumLamp / AviationLights drive it through the same MaterialPropertyBlock as the
// lamp glass (sodium warm-up hue included). The glow keeps a minimum screen size so distant lamps still read.
Shader "LongShadow/World/LampGlow"
{
    Properties
    {
        [HDR] _EmissionColor("Emission (HDR, set per renderer)", Color) = (0, 0, 0, 1)
        _Size("World Size (m)", Float) = 0.9
        _MinPixels("Minimum Size (pixels)", Float) = 6
        _Core("Core Sharpness", Range(1, 32)) = 10
        _Halo("Halo Amount", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LampGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            float _Size;
            float _MinPixels;
            float _Core;
            float _Halo;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 centerVS = TransformWorldToView(centerWS);
                float dist = max(0.1, -centerVS.z);
                // world size, but never smaller than _MinPixels on screen
                float pixelWorld = 2.0 * dist / max(1.0, _ScreenParams.y * UNITY_MATRIX_P[1][1]);
                float size = max(_Size, _MinPixels * pixelWorld);
                float2 corner = (input.uv - 0.5) * size;
                float3 positionVS = centerVS + float3(corner, 0.0);
                o.positionCS = TransformWViewToHClip(positionVS);
                o.positionWS = centerWS;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2.0;
                half core = pow(saturate(1.0 - r), _Core);
                half halo = _Halo * pow(saturate(1.0 - r), 2.0);
                half3 c = _EmissionColor.rgb * (core + halo);
                half fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                c = MixFogColor(c, half3(0.0, 0.0, 0.0), fogCoord * 0.6); // bright sources punch through haze
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
