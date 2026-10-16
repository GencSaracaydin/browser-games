// LongShadow WP09 - full-screen glitch (TECH_PLAN §7.12 SG_Glitch; GDD §5.5, §8.4): scanline tear, RGB split and
// the one-frame white of the inserts. Material of the FullScreenPassRendererFeature "Glitch" on PC_Renderer,
// assigned at runtime by LongShadow.Cinematics.FX.GlitchPass (injection after post-processing, Blit.hlsl input).
Shader "LongShadow/SG_Glitch"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Split ("RGB Split", Range(0, 0.05)) = 0.008
        _Tear ("Tear", Range(0, 1)) = 0
        _White ("White Frame", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Glitch"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _Split;
                half _Tear;
                half _White;
                float _Seed;
            CBUFFER_END

            #include "SG_CRTCommon.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = LS_Time();
                float2 uv = input.texcoord;

                // Torn horizontal strips that jump sideways.
                float band = floor(uv.y * 22.0 + floor(t * 24.0) * 3.1);
                float h = LS_Hash11(band + _Seed);
                float tear = step(1.0 - 0.38 * _Tear, h) * (LS_Hash11(band * 1.7 + _Seed) - 0.5) * 0.14 * _Intensity;
                uv.x += tear;
                // Per-line sync jitter.
                uv.x += (LS_Hash11(floor(uv.y * 270.0) + floor(t * 60.0)) - 0.5) * 0.004 * _Intensity;
                uv = saturate(uv);

                float split = _Split * _Intensity;
                half4 c;
                c.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(split, 0.0)).r;
                c.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                c.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(split, 0.0)).b;
                c.a = 1.0;

                // Scanline darkening and snow in the torn strips.
                c.rgb *= 1.0 - 0.28 * _Intensity * (0.5 + 0.5 * sin(input.texcoord.y * 900.0));
                float snow = LS_Hash12(floor(input.texcoord * float2(480.0, 270.0)) + floor(t * 30.0) * float2(7.3, 1.9));
                c.rgb = lerp(c.rgb, (half3)snow, 0.18 * _Intensity * (abs(tear) > 0.0 ? 1.0 : 0.35));
                c.rgb = lerp(c.rgb, half3(1.0, 1.0, 1.0), _White);
                return c;
            }
            ENDHLSL
        }
    }
}
