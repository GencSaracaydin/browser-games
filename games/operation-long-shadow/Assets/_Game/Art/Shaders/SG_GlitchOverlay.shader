// LongShadow WP09 - glitch/static overlay for the cinematic overlay canvas: CRT snow with rolling bands, torn
// scanline strips and the one-frame white (GDD §5.5 inserts). Used for the death insert's static and as the fallback
// when the "Glitch" renderer feature is missing (no RGB split possible on an overlay).
Shader "LongShadow/SG_GlitchOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.86, 0.95, 0.88, 1)
        _Intensity ("Static", Range(0, 1)) = 0
        _Tear ("Tear", Range(0, 1)) = 0
        _White ("White Frame", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GlitchOverlay"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _Tear;
                half _White;
                float _Seed;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #include "SG_CRTCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color * _Color;
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = LS_Time();
                float2 uv = input.uv;
                float band = floor(uv.y * 26.0 + floor(t * 22.0) * 3.1);
                float tornBand = step(1.0 - 0.4 * _Tear, LS_Hash11(band + _Seed));
                float2 px = floor(uv * float2(_ScreenParams.x, _ScreenParams.y) * 0.5);
                float snow = LS_Hash12(px + floor(t * 30.0) * float2(17.13, 3.71) + _Seed);
                float roll = LS_Noise(float2(uv.y * 4.0 - t * 2.7, _Seed));
                float scan = 0.75 + 0.25 * sin(uv.y * _ScreenParams.y * 1.6);
                half3 col = (half3)(snow * (0.65 + 0.55 * roll) * scan) * input.color.rgb;
                half alpha = saturate(_Intensity * (0.75 + 0.25 * roll) + tornBand * _Tear * 0.6);
                col = lerp(col, half3(1.0, 1.0, 1.0), _White);
                alpha = max(alpha, _White);
                return half4(col, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
