// LongShadow WP09: forward pass shared by SG_CRTScreen and SG_GunCam.
// The including shader defines the UnityPerMaterial CBUFFER, _ScreenTex and LS_CRT_PROCESS(uv, t, puv) -> half3.
// The picture is emitted (CRT phosphor); the glass adds the room's reflection probe (box projected in the booth)
// and probe ambient, so a dark tube still reads as curved glass.
#ifndef LS_CRT_PASS_INCLUDED
#define LS_CRT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct LSCrtAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct LSCrtVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    half3  vertexSH   : TEXCOORD3;
    float  fogFactor  : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

LSCrtVaryings LSCrtVert(LSCrtAttributes input)
{
    LSCrtVaryings o = (LSCrtVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = nrm.normalWS;
    o.uv = input.uv;
    o.vertexSH = SampleSHVertex(nrm.normalWS);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
    return o;
}

half4 LSCrtFrag(LSCrtVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float t = LS_Time();

    // Snap-on: the picture grows from a bright horizontal line (CRTScreen.PowerOnSeconds).
    float on = saturate(_PowerOn);
    float squeeze = lerp(0.004, 1.0, on * on * (3.0 - 2.0 * on));

    // Degauss wobble and breathing.
    float2 cuv = input.uv - 0.5;
    cuv.x += sin(cuv.y * 38.0 + t * 31.0) * 0.007 * _Degauss;
    cuv *= 1.0 + 0.035 * _Degauss * sin(t * 19.0);

    // Glass curvature.
    float2 suv = LS_Curve(cuv + 0.5, _Curvature);
    float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);

    float2 puv = suv;
    puv.y = (suv.y - 0.5) / squeeze + 0.5;
    inside *= step(0.0, puv.y) * step(puv.y, 1.0);

    half3 col = LS_CRT_PROCESS(puv, t);

    // Scanlines, a soft vignette inside the tube, mains flicker.
    col *= LS_Scanlines(puv.y, _ScanlineCount, _ScanlineBoost);
    float2 v = puv - 0.5;
    col *= saturate(1.0 - dot(v, v) * _Vignette * 1.8);
    col *= 0.965 + 0.035 * sin(t * 119.0 + _Seed);
    col *= LS_ApertureMask(input.positionCS.xy, 0.18);

    // The collapsed line at power-on.
    float lineGlow = (1.0 - on) * exp(-abs(suv.y - 0.5) * 380.0) * step(0.001, on);
    col += lineGlow * 2.5;

    half3 emission = (col * _Brightness + _Flash * 1.6) * _Power * inside;

    // Glass: probe ambient + fresnel reflection of the room.
    float3 n = normalize(input.normalWS);
    float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float nv = saturate(dot(n, viewDir));
    float fresnel = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
    half3 reflectDir = reflect(-viewDir, n);
    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    half3 env = GlossyEnvironmentReflection(reflectDir, input.positionWS, half(1.0 - _Smoothness), half(1.0), screenUV);
    half3 ambient = SampleSHPixel(input.vertexSH, n) * _BaseColor.rgb;
    half3 color = emission + ambient + env * (half)(fresnel * _Reflection);
    color = MixFog(color, (half)input.fogFactor);
    return half4(color, 1.0);
}

#endif
