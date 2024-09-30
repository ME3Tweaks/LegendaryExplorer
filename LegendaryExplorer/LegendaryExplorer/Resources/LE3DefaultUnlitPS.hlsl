// ---- Created with 3Dmigoto v1.3.16 on Fri Sep 20 17:39:59 2024

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
    float MotionBlurMask : packoffset(c39) = { 0 };
    float4 LightColorAndFalloffExponent : packoffset(c40);
    float3 SpotDirection : packoffset(c41);
    float2 SpotAngles : packoffset(c42);
    float3 DistanceFieldParameters : packoffset(c43);
    float3 UpperSkyColor : packoffset(c44);
    float3 LowerSkyColor : packoffset(c45);
    float CharacterMask : packoffset(c45.w) = { 0 };
    float4 AmbientColorAndSkyFactor : packoffset(c46);
}

SamplerState Texture2D_0Sampler_s : register(s0);
SamplerState Texture2D_1Sampler_s : register(s1);
Texture2D<float4> Texture2D_0 : register(t0);
Texture2D<float4> Texture2D_1 : register(t1);


// 3Dmigoto declarations
#define cmp -


void main(
  float4 v0 : TEXCOORD10,
  float4 v1 : TEXCOORD11,
  float4 v2 : COLOR0,
  float4 v3 : TEXCOORD0,
  float4 v4 : TEXCOORD5,
  float4 v5 : TEXCOORD6,
  float3 v6 : TEXCOORD7,
  uint v7 : SV_IsFrontFace0,
  out float4 o0 : SV_Target0,
  out float4 o2 : SV_Target2)
{
    float4 r0, r1, r2, r3;
    uint4 bitmask, uiDest;
    float4 fDest;

    r0.xyz = float3(1, 1, 1) + -UniformPixelVector_0.xyz;
    r1.xyz = Texture2D_1.Sample(Texture2D_1Sampler_s, v3.xy).xyz;
    r0.xyz = r1.xyz * r0.xyz;
    o0.xyz = r0.xyz * AmbientColorAndSkyFactor.xyz + UniformPixelVector_0.xyz;
    o0.w = 1;
    r0.xyz = Texture2D_0.Sample(Texture2D_0Sampler_s, v3.xy).xyz;
    r0.xyz = r0.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
    r0.w = dot(r0.xyz, r0.xyz);
    r0.w = rsqrt(r0.w);
    r0.xyz = r0.xyz * r0.www;
    r0.w = dot(v1.xyz, v1.xyz);
    r0.w = rsqrt(r0.w);
    r1.xyz = v1.xyz * r0.www;
    r0.w = dot(v0.xyz, v0.xyz);
    r0.w = rsqrt(r0.w);
    r2.xyz = v0.xyz * r0.www;
    r3.xyz = r2.yzx * r1.zxy;
    r3.xyz = r1.yzx * r2.zxy + -r3.xyz;
    r3.xyz = v1.www * r3.xyz;
    r0.w = dot(r3.xyz, r0.xyz);
    r3.xyz = WorldToViewMatrix._m01_m11_m21 * r3.zzz;
    r3.xyz = WorldToViewMatrix._m00_m10_m20 * r2.zzz + r3.xyz;
    r1.w = dot(r2.xyz, r0.xyz);
    r0.x = dot(r1.xyz, r0.xyz);
    r1.xyz = WorldToViewMatrix._m02_m12_m22 * r1.zzz + r3.xyz;
    r0.yzw = WorldToViewMatrix._m01_m11_m21 * r0.www;
    r0.yzw = WorldToViewMatrix._m00_m10_m20 * r1.www + r0.yzw;
    r0.xyz = WorldToViewMatrix._m02_m12_m22 * r0.xxx + r0.yzw;
    r0.z = dot(r0.xyz, r0.xyz);
    r0.z = rsqrt(r0.z);
    r0.xy = r0.xy * r0.zz;
    o2.xy = r0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    r0.x = dot(r1.xyz, r1.xyz);
    r0.x = rsqrt(r0.x);
    r0.xy = r1.xy * r0.xx;
    r0.xy = r0.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
    r0.z = cmp(CharacterMask < 1);
    o2.zw = r0.zz ? r0.xy : float2(1.#INF, 1.#INF);
    return;
}