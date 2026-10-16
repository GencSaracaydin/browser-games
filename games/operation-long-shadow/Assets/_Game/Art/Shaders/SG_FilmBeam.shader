// LongShadow WP09 - projector beam (CINEMATIC_DIRECTION §3: "a volumetric cone - an additive mesh with a noise
// texture - and dust motes"). Additive frustum from the lens to the screen (u around the side, v lens 0 -> screen 1):
// brighter near the lens, swirling dust density, soft where it meets geometry (depth texture) and soft at grazing
// angles. Built by LongShadow.Cinematics.Sets.ProjectionRig.
Shader "LongShadow/SG_FilmBeam"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.93, 0.8, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.22
        _NoiseScale ("Dust Scale", Float) = 1.6
        _Flicker ("Flicker", Range(0, 1)) = 0.35
        _SoftDepth ("Soft Intersection (m)", Range(0.01, 2)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BeamForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _NoiseScale;
                half _Flicker;
                float _SoftDepth;
            CBUFFER_END

            #include "SG_CRTCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
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
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = LS_Time();
                float along = saturate(input.uv.y);
                float side = input.uv.x;

                // Concentrated near the lens, spreading toward the screen.
                float falloff = lerp(1.0, 0.22, along);
                // Soft frustum edges and grazing-angle softness (the planar sides must not read as glass).
                float edge = smoothstep(0.0, 0.18, side) * smoothstep(0.0, 0.18, 1.0 - side);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float grazing = 1.0 - abs(dot(normalize(input.normalWS), v));
                float shape = edge * (0.35 + 0.65 * grazing);

                // Dust swirling in the light.
                float3 w = input.positionWS * _NoiseScale;
                float dust = LS_Noise(w.xz + float2(t * 0.08, t * 0.05)) * LS_Noise(w.yz * 1.3 - float2(t * 0.06, 0.0));
                dust = 0.45 + dust * 1.4;

                // Shutter flicker (24 fps).
                float flick = 1.0 - _Flicker * 0.15 * step(0.5, frac(t * 24.0));

                // Soft intersection with the room.
                float2 suv = GetNormalizedScreenSpaceUV(input.positionCS);
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float self = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float soft = saturate((scene - self) / _SoftDepth);

                float a = _Intensity * falloff * shape * dust * flick * soft * smoothstep(0.0, 0.04, along);
                return half4(_Color.rgb * a, 0.0);
            }
            ENDHLSL
        }
    }
}
