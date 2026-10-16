// LongShadow WP09: shared helpers of the cinematic shaders (CRT, gun camera, film, glitch, beam).
// Hand-written URP HLSL (DECISIONS.md [WP09]): Shader Graph assets cannot be authored offline. WebGL2-safe:
// no integer bit ops, no derivatives in branches, no texture arrays.
#ifndef LS_CRT_COMMON_INCLUDED
#define LS_CRT_COMMON_INCLUDED

// Unscaled time set every frame by CinematicCameras (inserts freeze _Time; the booth keeps moving).
float _LS_UnscaledTime;

float LS_Time()
{
    return _LS_UnscaledTime;
}

float LS_Hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float LS_Hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float LS_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = LS_Hash12(i);
    float b = LS_Hash12(i + float2(1.0, 0.0));
    float c = LS_Hash12(i + float2(0.0, 1.0));
    float d = LS_Hash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Barrel distortion of a 0..1 uv (CRT glass curvature).
float2 LS_Curve(float2 uv, float amount)
{
    float2 c = uv * 2.0 - 1.0;
    float2 off = c.yx * c.yx * amount;
    c += c * off;
    return c * 0.5 + 0.5;
}

// Scanline brightness for a vertical position; boost thickens the dark gaps (intro shot 8).
float LS_Scanlines(float y, float count, float boost)
{
    float s = sin(y * count * 3.14159265);
    float gap = lerp(0.18, 0.85, saturate(boost));
    return 1.0 - gap * (1.0 - s * s);
}

// Shadow-mask triads in screen space (subtle phosphor structure).
float3 LS_ApertureMask(float2 pixel, float strength)
{
    float m = frac(pixel.x / 3.0);
    float3 mask = float3(step(m, 0.333), step(0.333, m) * step(m, 0.666), step(0.666, m));
    return lerp(float3(1.0, 1.0, 1.0), mask * 1.6 + 0.2, strength);
}

float LS_Luma(float3 c)
{
    return dot(c, float3(0.299, 0.587, 0.114));
}

#endif
