// OPERATION LONG SHADOW (WP10 Look): TECH_PLAN §7.14 "SG_Backdrop" for the western ridge silhouette (2.5 km) and the far
// hills (GDD §9.3). Built-in exponential-squared fog would flatten anything at 2.5 km to the fog colour, so this shader
// skips it and applies its own aerial perspective toward the fog colour, capped (0.8 by default), so the ridge stays
// a darker band against the sky and hides the sun disc in MID-A. Simple diffuse from the key light (sun or sky key)
// plus the ambient probe; no shadows (beyond every shadow distance). Writes depth (lens-flare occlusion, sun disc).
Shader "LongShadow/World/Backdrop"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (0.42, 0.36, 0.30, 1)
        _AerialDensity("Aerial Density (per m)", Float) = 0.00055
        _AerialMax("Aerial Maximum", Range(0, 1)) = 0.8
        _AerialTint("Aerial Tint", Color) = (1, 1, 1, 1)
        _AmbientScale("Ambient Scale", Range(0, 2)) = 1
        _HeightDarken("Darken toward the base", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _AerialTint;
            float _AerialDensity;
            float _AerialMax;
            float _AmbientScale;
            float _HeightDarken;
            CBUFFER_END

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float height01 : TEXCOORD3;
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
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.height01 = saturate(p.positionWS.y / 60.0);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction));
                half3 lit = albedo * (mainLight.color * ndl + SampleSH(n) * _AmbientScale);
                lit *= lerp(1.0 - _HeightDarken, 1.0, input.height01);

                float dist = distance(input.positionWS, GetCameraPositionWS());
                half aerial = min(_AerialMax, 1.0 - exp(-dist * _AerialDensity));
                half3 c = lerp(lit, unity_FogColor.rgb * _AerialTint.rgb, aerial);
                return half4(c, 1.0);
            }
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
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _AerialTint;
            float _AerialDensity;
            float _AerialMax;
            float _AmbientScale;
            float _HeightDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
