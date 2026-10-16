// LongShadow WP09 - film burn-out overlay (TECH_PLAN §7.12 SG_FilmBurn; GDD §8.6 shot 8: "the film jams and the
// frame burns out from the centre to white"). A UI shader for the cinematic overlay's RawImage: an organic hole
// grows from the centre with a charred amber rim and a white core; outside it the picture shows through.
Shader "LongShadow/SG_FilmBurn"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Burn ("Burn", Range(0, 1)) = 0
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
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FilmBurn"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Burn;
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
                float2 d = (input.uv - 0.5) * float2(_ScreenParams.x / max(1.0, _ScreenParams.y), 1.0);
                float n = LS_Noise(input.uv * 7.0 + t * 0.9 + _Seed) * 0.6 + LS_Noise(input.uv * 21.0 - t * 1.7) * 0.4;
                float r = length(d) + (n - 0.5) * 0.18;
                float radius = _Burn * 1.25;
                float core = 1.0 - smoothstep(radius - 0.06, radius, r);
                float rim = saturate((1.0 - smoothstep(radius, radius + 0.08, r)) - core);
                half3 rimColor = lerp(half3(0.25, 0.08, 0.02), half3(1.0, 0.62, 0.22), rim);
                half3 col = lerp(rimColor, half3(1.0, 0.98, 0.94), core);
                half alpha = saturate(core + rim * 0.95) * step(0.001, _Burn);
                return half4(col, alpha) * input.color;
            }
            ENDHLSL
        }
    }
}
