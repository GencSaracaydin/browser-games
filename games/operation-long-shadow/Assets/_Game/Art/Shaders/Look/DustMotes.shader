// OPERATION LONG SHADOW (WP10 Look): TECH_PLAN §7.14 "SG_DustMotes": soft additive motes for particle systems
// (LongShadow.World.Look.Atmosphere.DustMotes / LightShaft set the particle colour from the key light). Procedural round
// sprite (no texture), soft-particle depth fade, near-camera fade, fogged toward black like any light.
Shader "LongShadow/World/DustMotes"
{
    Properties
    {
        [HDR] _BaseColor("Tint (HDR)", Color) = (1, 1, 1, 1)
        _Softness("Sprite Softness", Range(0.05, 1)) = 0.6
        _DepthFade("Soft Particle Distance (m)", Float) = 0.25
        _NearFade("Near-camera Fade (m)", Float) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "DustMotes"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Softness;
            float _DepthFade;
            float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.eyeDepth = -p.positionVS.z;
                o.color = input.color;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2.0;
                half sprite = pow(saturate(1.0 - r), 1.0 / max(0.05, _Softness));
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneEye - input.eyeDepth) / max(1e-3, _DepthFade));
                half nearFade = saturate((input.eyeDepth - 0.15) / max(1e-3, _NearFade));
                half3 c = input.color.rgb * input.color.a * _BaseColor.rgb * sprite * soft * nearFade;
                half fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                c = MixFogColor(c, half3(0.0, 0.0, 0.0), fogCoord);
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
