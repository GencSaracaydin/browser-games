// OPERATION LONG SHADOW (WP10 Look, editor only): unwraps a cubemap (the EditorOnly *_nosun_2k.exr imported as a
// Lat-Long cube) into an equirect render texture in Unity Skybox/Panoramic's convention (u = 0.5 - atan2(z, x) / 2π,
// v = 1 - acos(y) / π, row 0 = bottom), rotated by _Rotation degrees around +Y like Skybox/Cubemap. Used by
// AmbientSHCapture (Graphics.Blit). Never referenced by shipped content.
Shader "Hidden/LongShadow/CubeToEquirect"
{
    Properties
    {
        _Cube("Cubemap", Cube) = "" {}
        _Rotation("Rotation", Float) = 0
        _Scale("Scale", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Cube;
            float _Rotation;
            float _Scale;

            float4 frag(v2f_img i) : SV_Target
            {
                float u = i.uv.x;
                float v = i.uv.y;
                float theta = (1.0 - v) * UNITY_PI;
                float phi = (0.5 - u) * 2.0 * UNITY_PI;
                float3 d = float3(sin(theta) * cos(phi), cos(theta), sin(theta) * sin(phi));
                float a = _Rotation * UNITY_PI / 180.0;
                float s = sin(a);
                float c = cos(a);
                float2 xz = float2(c * d.x - s * d.z, s * d.x + c * d.z);
                float3 r = float3(xz.x, d.y, xz.y);
                float3 col = texCUBElod(_Cube, float4(r, 0)).rgb * _Scale;
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
