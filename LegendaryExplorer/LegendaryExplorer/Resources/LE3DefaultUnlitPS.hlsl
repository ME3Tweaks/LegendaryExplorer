// ---- Created with 3Dmigoto v1.3.16 on Tue Oct  1 17:10:39 2024

cbuffer _Globals : register(b0)
{
    float4x4 InvViewProjectionMatrix : packoffset(c0);
    float3 CameraWorldPos : packoffset(c4);
    float4 ObjectWorldPositionAndRadius : packoffset(c5);
    float3 ObjectOrientation : packoffset(c6);
    float3 ObjectPostProjectionPosition : packoffset(c7);
    float3 ObjectNDCPosition : packoffset(c8);
    float4 ObjectMacroUVScales : packoffset(c9);
    float3 FoliageImpulseDirection : packoffset(c10);
    float4 FoliageNormalizedRotationAxisAndAngle : packoffset(c11);
    float4 WindDirectionAndSpeed : packoffset(c12);
    float4 WrapLightingParameters : packoffset(c13);
    bool bEnableDistanceShadowFading : packoffset(c14);
    float2 DistanceFadeParameters : packoffset(c14.y);
    float3x3 LocalToWorldMatrix : packoffset(c15);
    float3x3 WorldToLocalMatrix : packoffset(c18);
    float3x3 WorldToViewMatrix : packoffset(c21);
    float4 UniformPixelVector_0 : packoffset(c24);
    float4x4 LocalToWorld : packoffset(c25);
    float4x4 PreviousLocalToWorld : packoffset(c29);
    float LocalToWorldRotDeterminantFlip : packoffset(c33);
    float3x3 WorldToLocal : packoffset(c34);
    float4 LightmapCoordinateScaleBias : packoffset(c37);
    float4 ShadowmapCoordinateScaleBias : packoffset(c38);
    float3 LightColor : packoffset(c39);
    float3 DistanceFieldParameters : packoffset(c40);
    bool bReceiveDynamicShadows : packoffset(c40.w);
}

cbuffer VSOffsetConstants : register(b1)
{
    float4x4 ViewProjectionMatrix : packoffset(c0);
    float4 CameraPosition : packoffset(c4);
    float4 PreViewTranslation : packoffset(c5);
}

cbuffer PSOffsetConstants : register(b2)
{
    float4 ScreenPositionScaleBias : packoffset(c0);
    float4 MinZ_MaxZRatio : packoffset(c1);
    float4 DynamicScale : packoffset(c2);
}

SamplerState LightAttenuationTextureSampler_s : register(s0);
SamplerState Texture2D_0Sampler_s : register(s1);
SamplerState Texture2D_1Sampler_s : register(s2);
Texture2D<float4> LightAttenuationTexture : register(t0);
Texture2D<float4> Texture2D_0 : register(t1);
Texture2D<float4> Texture2D_1 : register(t2);


// 3Dmigoto declarations
#define cmp -


void main(
  float4 v0 : TEXCOORD10,
  float4 v1 : TEXCOORD11,
  float4 v2 : COLOR0,
  float4 v3 : TEXCOORD0,
  float4 v4 : TEXCOORD4,
  float4 v5 : TEXCOORD5,
  float4 v6 : TEXCOORD6,
  float4 v7 : TEXCOORD7,
  uint v8 : SV_IsFrontFace0,
  out float4 o0 : SV_Target0)
{
    float4 r0, r1, r2, r3, r4, r5;
    uint4 bitmask, uiDest;
    float4 fDest;

    r0.x = dot(v6.xyz, v6.xyz);
    r0.x = rsqrt(r0.x);
    r0.xyz = v6.xyz * r0.xxx;
    r0.w = dot(v4.xyz, v4.xyz);
    r0.w = rsqrt(r0.w);
    r1.xyz = v4.xyz * r0.www;
    r2.xyz = Texture2D_0.Sample(Texture2D_0Sampler_s, v3.xy).xyz;
    r2.xyz = r2.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
    r0.w = dot(r2.xyz, r2.xyz);
    r0.w = rsqrt(r0.w);
    r2.xyz = r2.xyz * r0.www;
    r0.w = dot(r2.xyz, r0.xyz);
    r3.xyz = r2.xyz * r0.www;
    r0.xyz = r3.xyz * float3(2, 2, 2) + -r0.xyz;
    if (bReceiveDynamicShadows != 0)
    {
        r3.xyz = ViewProjectionMatrix._m01_m11_m31 * v7.yyy;
        r3.xyz = ViewProjectionMatrix._m00_m10_m30 * v7.xxx + r3.xyz;
        r3.xyz = ViewProjectionMatrix._m02_m12_m32 * v7.zzz + r3.xyz;
        r3.xyz = ViewProjectionMatrix._m03_m13_m33 * v7.www + r3.xyz;
        r3.xy = r3.xy / r3.zz;
        r3.xy = r3.xy * ScreenPositionScaleBias.xy + ScreenPositionScaleBias.wz;
        r3.xyzw = LightAttenuationTexture.Sample(LightAttenuationTextureSampler_s, r3.xy).xyzw;
        if (bEnableDistanceShadowFading != 0)
        {
            r0.w = dot(v7.xyz, v7.xyz);
            r0.w = sqrt(r0.w);
            r0.w = DistanceFadeParameters.x + -r0.w;
            r0.w = saturate(DistanceFadeParameters.y * r0.w);
            r0.w = r0.w * r0.w;
            r1.w = -1 + r3.w;
            r4.xyz = r0.www * r1.www + float3(1, 1, 1);
        }
        else
        {
            r4.xyz = float3(1, 1, 1);
        }
        r3.xyz = r4.xyz * r3.xyz;
    }
    else
    {
        r3.xyz = float3(1, 1, 1);
    }
    r4.xyz = float3(1, 1, 1) + -UniformPixelVector_0.xyz;
    r5.xyz = Texture2D_1.Sample(Texture2D_1Sampler_s, v3.xy).xyz;
    r4.xyz = r5.xyz * r4.xyz;
    r0.w = saturate(dot(r2.xyz, r1.xyz));
    r0.x = saturate(dot(r0.xyz, r1.xyz));
    r0.xw = max(float2(9.99999975e-05, 9.99999975e-05), r0.xw);
    r0.x = log2(r0.x);
    r0.x = 15 * r0.x;
    r0.x = exp2(r0.x);
    r0.xyz = r0.xxx * r5.xyz;
    r0.xyz = r4.xyz * r0.www + r0.xyz;
    r0.xyz = r0.xyz * r3.xyz;
    o0.xyz = LightColor.xyz * r0.xyz;
    o0.w = 1;
    return;
}