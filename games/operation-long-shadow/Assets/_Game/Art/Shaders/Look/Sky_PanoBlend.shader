// OPERATION LONG SHADOW (WP10 Look): the runtime sky, GDD §9.3 / TECH_PLAN §7.14 "Sky/PanoBlend".
// Two LDR equirect skies with the sun painted out (A = kiara_8_sunset, B = kiara_9_dusk; pre-rotated so the sun/glow
// sits at azimuth 258°, WP10_CC0 hdri_prep.py), blended by _Blend, tinted, exposed (base linear exposure per texture
// x 2^_Exposure EV), with a horizon haze toward the fog colour and the lower hemisphere fading to the fog colour.
// Mapping is Unity Skybox/Panoramic's: u = 0.5 - atan2(z, x) / 2π, v = 1 - acos(y) / π; _Rotation turns it around +Y.
// Driven by LightStageController through LongShadow.World.Look.Sky.PanoBlendSky. No HDR cubemap ships (§9.3).
Shader "Sky/PanoBlend"
{
    Properties
    {
        [NoScaleOffset] _TexA("Sky A (golden/sunset, LDR equirect)", 2D) = "grey" {}
        [NoScaleOffset] _TexB("Sky B (dusk, LDR equirect)", 2D) = "grey" {}
        _Blend("Blend A to B", Range(0, 1)) = 0
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure (EV)", Range(-8, 8)) = 0
        _ExposureA("Base Exposure A (linear)", Float) = 0.29
        _ExposureB("Base Exposure B (linear)", Float) = 0.075
        _Rotation("Rotation (degrees)", Range(0, 360)) = 0
        _GlowBoost("Glow Re-expansion", Range(0, 4)) = 1.5
        _HorizonFog("Horizon Haze", Range(0, 1)) = 0.35
        _HorizonFogHeight("Horizon Haze Height", Range(0.005, 0.5)) = 0.06
        _GroundFade("Below-horizon Fade", Range(0.005, 0.5)) = 0.04
        [Toggle(_PANO_PROCEDURAL)] _Procedural("Procedural Fallback (textures missing)", Float) = 0
        _ZenithColor("Fallback Zenith", Color) = (0.31, 0.38, 0.53, 1)
        _HorizonColor("Fallback Horizon", Color) = (0.76, 0.83, 0.82, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _PANO_PROCEDURAL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TexA);
            SAMPLER(sampler_TexA);
            TEXTURE2D(_TexB);
            SAMPLER(sampler_TexB);

            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half4 _ZenithColor;
            half4 _HorizonColor;
            float _Blend;
            float _Exposure;
            float _ExposureA;
            float _ExposureB;
            float _Rotation;
            float _GlowBoost;
            float _HorizonFog;
            float _HorizonFogHeight;
            float _GroundFade;
            CBUFFER_END

            // Written by LightStageController (LongShadow.World.Look.Common.LookShaderIds).
            float4 _LS_SunColor;     // rgb key light colour x intensity, a = sun disc visibility
            float4 _LS_SunDirection; // xyz toward the key light, w = 1 while the SUN is up

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundYInDegrees(float3 v, float degrees)
            {
                float alpha = degrees * PI / 180.0;
                float s, c;
                sincos(alpha, s, c);
                float2x2 m = float2x2(c, -s, s, c);
                return float3(mul(m, v.xz), v.y).xzy;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.direction = RotateAroundYInDegrees(input.positionOS.xyz, _Rotation);
                return o;
            }

            float2 ToRadialCoords(float3 d)
            {
                float latitude = acos(clamp(d.y, -1.0, 1.0));
                float longitude = atan2(d.z, d.x);
                return float2(0.5, 1.0) - float2(longitude, latitude) * float2(0.5 / PI, 1.0 / PI);
            }

            // Re-expands the hue-preserving log shoulder (texels above 0.6) so the glow around the painted-out sun keeps
            // some HDR range for bloom (the SunDisc restores the core).
            half3 ExpandGlow(half3 c)
            {
                half l = max(c.r, max(c.g, c.b));
                half k = 1.0 + _GlowBoost * saturate((l - 0.6) / 0.4);
                return c * k;
            }

            half3 SampleSky(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 dx, float2 dy)
            {
                return SAMPLE_TEXTURE2D_GRAD(tex, samp, uv, dx, dy).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d = normalize(input.direction);
                half3 fogColor = unity_FogColor.rgb;
                half3 sky;

            #if defined(_PANO_PROCEDURAL)
                half up = saturate(d.y);
                sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sqrt(up));
                float3 toKey = normalize(_LS_SunDirection.xyz + float3(0.0, 1e-5, 0.0));
                half mu = saturate(dot(d, toKey));
                half3 key = _LS_SunColor.rgb;
                sky += key * (0.12 * pow(mu, 8.0) + 0.5 * pow(mu, 64.0)) * _LS_SunColor.a;
                sky *= _ExposureA * 2.5;
            #else
                float2 uv = ToRadialCoords(d);
                // Seam-free mip selection across the atan2 wrap: take the smaller derivative of u and of frac(u + 0.5).
                float2 uvAlt = float2(frac(uv.x + 0.5), uv.y);
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                float2 dxAlt = ddx(uvAlt);
                float2 dyAlt = ddy(uvAlt);
                if (dot(dxAlt, dxAlt) < dot(dx, dx)) dx = dxAlt;
                if (dot(dyAlt, dyAlt) < dot(dy, dy)) dy = dyAlt;
                half3 a = ExpandGlow(SampleSky(TEXTURE2D_ARGS(_TexA, sampler_TexA), uv, dx, dy)) * _ExposureA;
                half3 b = ExpandGlow(SampleSky(TEXTURE2D_ARGS(_TexB, sampler_TexB), uv, dx, dy)) * _ExposureB;
                sky = lerp(a, b, saturate(_Blend));
            #endif

                sky *= _Tint.rgb * exp2(_Exposure);

                // Horizon haze toward the fog colour (matches distant fogged geometry), and the lower hemisphere fades
                // to the fog colour (the LDR skies have no land below 0°; the far hills are game geometry).
                half above = max(d.y, 0.0);
                half haze = _HorizonFog * exp(-above / max(1e-3, _HorizonFogHeight));
                sky = lerp(sky, fogColor, saturate(haze));
                half below = saturate(-d.y / max(1e-3, _GroundFade));
                sky = lerp(sky, fogColor, below);

                return half4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
