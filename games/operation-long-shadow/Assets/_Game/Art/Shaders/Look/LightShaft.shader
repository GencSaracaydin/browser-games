// OPERATION LONG SHADOW (WP10 Look): TECH_PLAN §7.14 "SG_LightShaft": additive sun-shaft / projector-beam card.
// The card (1 x 1 quad: x = width, y = length from the aperture) is aligned to the light and turned toward the camera
// by LongShadow.World.Look.Atmosphere.LightShaft, which also sets its colour (_BaseColor, linear HDR, already scaled by
// the sun and the aperture visibility). Soft edges, slow scrolling dust density, depth fade where it meets geometry,
// near-camera fade, and the fog attenuates it like any distant light.
Shader "LongShadow/World/LightShaft"
{
    Properties
    {
        [HDR] _BaseColor("Colour (HDR, set per renderer)", Color) = (1, 0.8, 0.6, 1)
        _NoiseTex("Dust Noise (R)", 2D) = "white" {}
        _NoiseScale("Noise Scale (per m)", Float) = 0.35
        _NoiseSpeed("Noise Drift (m/s)", Float) = 0.12
        _NoiseAmount("Noise Amount", Range(0, 1)) = 0.45
        _EdgeSoftness("Edge Softness", Range(0.05, 1)) = 0.55
        _StartFade("Fade-in Length (fraction)", Range(0.001, 0.5)) = 0.06
        _EndFade("Fade-out Power", Range(0.2, 4)) = 1.3
        _DepthFade("Depth Fade (m)", Float) = 0.6
        _NearFade("Near-camera Fade (m)", Float) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LightShaft"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _NoiseTex_ST;
            half4 _BaseColor;
            float _NoiseScale;
            float _NoiseSpeed;
            float _NoiseAmount;
            float _EdgeSoftness;
            float _StartFade;
            float _EndFade;
            float _DepthFade;
            float _NearFade;
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
                float3 positionWS : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.eyeDepth = -p.positionVS.z;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.x - 0.5) * 2.0;
                half edge = pow(saturate(1.0 - across), 1.0 / max(0.05, _EdgeSoftness));
                half along = saturate(input.uv.y / _StartFade) * pow(saturate(1.0 - input.uv.y), _EndFade);

                float2 nuv = input.positionWS.xz * _NoiseScale + float2(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.37);
                float2 nuv2 = input.positionWS.zy * (_NoiseScale * 1.7) - float2(_Time.y * _NoiseSpeed * 0.5, 0.0);
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r * 0.6 + SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv2).r * 0.4;
                half density = lerp(1.0, n * 1.4, _NoiseAmount);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneEye - input.eyeDepth) / max(1e-3, _DepthFade));
                half nearFade = saturate((input.eyeDepth - 0.3) / max(1e-3, _NearFade));

                half3 c = _BaseColor.rgb * (edge * along * density * soft * nearFade);
                half fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                c = MixFogColor(c, half3(0.0, 0.0, 0.0), fogCoord);
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
