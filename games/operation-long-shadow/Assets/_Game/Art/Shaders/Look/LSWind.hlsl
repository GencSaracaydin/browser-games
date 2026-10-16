// OPERATION LONG SHADOW: global wind (WP10 Look, GDD §9.7). Written every frame by LongShadow.World.Look.Atmosphere.WindController:
//   _LS_WindDirection : xyz = normalised world direction the wind blows toward (west -> east), w = speed in m/s (3..5)
//   _LS_WindParams    : x = gust 0..1, y = turbulence 0..1, z = integrated wind phase (s), w = flutter phase (s)
// Any shader may include this file ("Assets/_Game/Art/Shaders/Look/LSWind.hlsl"): foliage, awnings, laundry, cloth.
// Without a WindController the globals are zero and nothing moves.
#ifndef LS_WIND_INCLUDED
#define LS_WIND_INCLUDED

float4 _LS_WindDirection;
float4 _LS_WindParams;

// Sway + flutter offset in world space for one vertex.
//   positionWS : vertex world position
//   pivotWS    : object origin in world space (per-object phase)
//   normalWS   : vertex normal (flutter axis)
//   sway       : 0 at the anchored base, 1 at the free tip (vertex colour R, or a height mask)
//   phase      : per-branch phase 0..1 (vertex colour G)
//   flutter    : leaf flutter weight 0..1 (vertex colour B)
//   strength   : material sway scale (1 = trees: ~6-10 cm at the crown in 4 m/s wind)
//   flutterAmp : leaf flutter amplitude in metres
float3 LSWindOffset(float3 positionWS, float3 pivotWS, float3 normalWS, float sway, float phase, float flutter,
                    float strength, float flutterAmp)
{
    float3 dir = _LS_WindDirection.xyz;
    float speed = _LS_WindDirection.w;
    float gust = _LS_WindParams.x;
    float t = _LS_WindParams.z;
    float tf = _LS_WindParams.w;
    float objPhase = frac(dot(pivotWS.xz, float2(0.0713, 0.1131)));

    float load = (speed * 0.25) * (0.55 + 0.45 * gust);                 // steady lean with the wind
    float osc = sin(t * 2.3 + (objPhase + phase) * 6.2832) * 0.6
              + sin(t * 0.97 + objPhase * 3.1416 + phase * 2.0) * 0.4; // slow sway
    float bend = strength * sway * (0.06 * load + 0.035 * osc * (0.6 + gust));
    float3 offset = dir * bend;
    // keep branch length roughly constant: drop slightly while bending
    offset.y -= bend * bend * 0.35;

    float f = sin(tf * 7.9 + phase * 12.57 + dot(positionWS, float3(1.7, 0.9, 2.3)))
            * cos(tf * 5.3 + phase * 6.28 + positionWS.y * 3.1);
    offset += normalWS * (f * flutterAmp * flutter * (0.4 + 0.8 * gust));
    return offset;
}

#endif
