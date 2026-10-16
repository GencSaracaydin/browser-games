// LongShadow WP09 - 16 mm projection (TECH_PLAN §7.12 SG_Film16mm; GDD §8.6 shots 3-8): the film image on the
// screening-room screen - mono Tri-X response, scratches, dust, gate weave and flicker stepped at 18 fps, the
// projector hot spot, film-gate edges and a burn-out from the centre. Unlit: the image is the projector's light.
// Driven by LongShadow.Cinematics.Sets.ProjectionRig.
Shader "LongShadow/SG_Film16mm"
{
    Properties
    {
        [MainTexture] _MainTex ("Film Frame", 2D) = "black" {}
        _Brightness ("Brightness", Range(0, 6)) = 1.8
        _Exposure ("Exposure", Range(0, 3)) = 1
        _Mono ("Monochrome", Range(0, 1)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1.25
        _Scratches ("Scratches", Range(0, 1)) = 1
        _Dust ("Dust", Range(0, 1)) = 1
        _Weave ("Gate Weave", Range(0, 2)) = 1
        _Flicker ("Flicker", Range(0, 1)) = 1
        _Fps ("Film Frames/s", Float) = 18
        _Burn ("Burn-out", Range(0, 1)) = 0
        _Vignette ("Hot Spot Falloff", Range(0, 2)) = 0.8
        _Seed ("Seed", Float) = 0
        _LeaderSweep ("Leader Sweep (-1 off)", Float) = -1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half _Brightness;
            half _Exposure;
            half _Mono;
            half _Contrast;
            half _Scratches;
            half _Dust;
            half _Weave;
            half _Flicker;
            float _Fps;
            half _Burn;
            half _Vignette;
            float _Seed;
            float _LeaderSweep;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        #include "SG_CRTCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardFilm"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
                float t = LS_Time();
                float frame = floor(t * max(1.0, _Fps));
                float2 weave = (float2(LS_Hash11(frame * 1.7 + _Seed), LS_Hash11(frame * 2.3 + _Seed)) - 0.5) * float2(0.004, 0.008) * _Weave;
                float2 uv = input.uv + weave;

                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                float l = LS_Luma(c);
                c = lerp(c, (half3)l, _Mono);
                c = (half3)saturate((c - 0.5) * _Contrast + 0.5) * _Exposure;

                // Frame flicker of the shutter and lamp.
                c *= 1.0 + (LS_Hash11(frame + 13.0) - 0.5) * 0.14 * _Flicker;

                // Scratches: two thin vertical lines that wander slowly and jump on splices.
                float s1 = LS_Hash11(floor(frame / 9.0) + 3.0);
                float s2 = LS_Hash11(floor(frame / 14.0) + 7.0);
                float x1 = s1 + sin(t * 0.9) * 0.01;
                float x2 = s2 + sin(t * 1.3 + 2.0) * 0.008;
                float scratch = exp(-abs(uv.x - x1) * 1800.0) * step(0.35, LS_Hash11(frame * 0.37));
                scratch += exp(-abs(uv.x - x2) * 2400.0) * 0.6 * step(0.55, LS_Hash11(frame * 0.53));
                c += scratch * 0.35 * _Scratches;

                // Dust specks (dark) and hairs, new every frame.
                float2 cell = floor(uv * float2(48.0, 36.0));
                float speck = step(0.9965, LS_Hash12(cell + frame * float2(3.1, 7.7)));
                float2 inCell = frac(uv * float2(48.0, 36.0)) - 0.5;
                speck *= step(dot(inCell, inCell), 0.07);
                c *= 1.0 - speck * 0.85 * _Dust;

                // Countdown leader sweep (darken the swept sector).
                if (_LeaderSweep >= 0.0)
                {
                    float2 d = (uv - 0.5) * float2(1.333, 1.0);
                    float a = atan2(d.x, d.y) / 6.2831853 + 0.5;
                    c *= lerp(1.0, 0.55, step(a, frac(_LeaderSweep)));
                }

                // Projector hot spot and the film gate's soft edges.
                float2 v = uv - 0.5;
                c *= saturate(1.15 - dot(v, v) * _Vignette * 2.2);
                float2 gate = smoothstep(0.0, 0.012, uv) * smoothstep(0.0, 0.012, 1.0 - uv);
                c *= gate.x * gate.y;

                // Burn-out from the centre: overexposed white core with a charred amber rim.
                if (_Burn > 0.0)
                {
                    float2 bd = (uv - 0.5) * float2(1.333, 1.0);
                    float r = length(bd) + (LS_Noise(uv * 9.0 + t * 0.7) - 0.5) * 0.12;
                    float radius = _Burn * 1.05;
                    float core = 1.0 - smoothstep(radius - 0.05, radius, r);
                    float rim = (1.0 - smoothstep(radius, radius + 0.06, r)) - core;
                    c = lerp(c, half3(1.0, 0.55, 0.18) * 3.0, saturate(rim));
                    c = lerp(c, half3(6.0, 5.8, 5.4), core);
                }

                return half4(c * _Brightness, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
