// OPERATION LONG SHADOW: LongShadow/World/FoliageWind inputs (WP10 Look). Property names follow URP/Lit so MaterialFactory
// specs (_BaseMap, _BumpMap, _MetallicGlossMap = D4 mask: R metal, G AO, A smoothness) apply unchanged.
#ifndef LS_FOLIAGE_WIND_INPUT_INCLUDED
#define LS_FOLIAGE_WIND_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Assets/_Game/Art/Shaders/Look/LSWind.hlsl"

// Every per-material property lives here (SRP Batcher).
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _Translucency;
half _TranslucencyPower;
half _WindStrength;
half _WindHeight;
half _FlutterAmplitude;
half _Surface;
CBUFFER_END

TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);

// Wind for one vertex in object space -> world-space position.
float3 FoliageWindPositionWS(float3 positionOS, float3 normalOS, float4 color)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float3 pivotWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
#if defined(_WIND_VERTEXCOLOR)
    // WP10_FAL trees and grass: R = sway (0 at the base), G = phase, B = leaf flutter.
    float sway = color.r;
    float phase = color.g;
    float flutter = color.b;
#else
    // No authored colours: bend by height above the pivot.
    float h = saturate(positionOS.y / max(0.05, _WindHeight));
    float sway = h * h;
    float phase = frac(dot(positionWS, float3(0.37, 0.11, 0.53)));
    float flutter = h;
#endif
    return positionWS + LSWindOffset(positionWS, pivotWS, normalWS, sway, phase, flutter, _WindStrength, _FlutterAmplitude);
}

half4 FoliageSampleMask(float2 uv)
{
#if defined(_MASKMAP)
    return half4(SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv));
#else
    return half4(_Metallic, 1.0, 0.0, 1.0);
#endif
}

void InitializeFoliageSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

    half4 mask = FoliageSampleMask(uv);
#if defined(_MASKMAP)
    outSurfaceData.metallic = mask.r * _Metallic;
    outSurfaceData.smoothness = mask.a * _Smoothness;
    outSurfaceData.occlusion = LerpWhiteTo(mask.g, _OcclusionStrength);
#else
    outSurfaceData.metallic = _Metallic;
    outSurfaceData.smoothness = _Smoothness;
    outSurfaceData.occlusion = 1.0;
#endif
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.emission = half3(0.0, 0.0, 0.0);
    outSurfaceData.clearCoatMask = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif
