// ---- Created with 3Dmigoto v1.3.16 on Sun Sep 22 15:05:23 2024

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
    float4 LightDirectionAndbDirectional : packoffset(c39);
    float4 LightPositionAndInvRadius : packoffset(c40);
    float4 LightMapScale[2] : packoffset(c41);
    float3 ApproxFogColor : packoffset(c43);
    float3 FogVolumeBoxMin : packoffset(c44);
    float3 FogVolumeBoxMax : packoffset(c45);
}

cbuffer VSOffsetConstants : register(b1)
{
    float4x4 ViewProjectionMatrix : packoffset(c0);
    float4 CameraPosition : packoffset(c4);
    float4 PreViewTranslation : packoffset(c5);
}



// 3Dmigoto declarations
#define cmp -


void main(
  float4 v0 : POSITION0,
  float3 v1 : TANGENT0,
  float4 v2 : NORMAL0,
  float4 v3 : COLOR1,
  float2 v4 : TEXCOORD0,
  out float4 o0 : TEXCOORD10,
  out float4 o1 : TEXCOORD11,
  out float4 o2 : COLOR0,
  out float4 o3 : TEXCOORD0,
  out float4 o4 : TEXCOORD5,
  out float4 o5 : TEXCOORD6,
  out float3 o6 : TEXCOORD7,
  out float4 o7 : SV_Position0)
{
    float4 r0, r1, r2, r3, r4;
    uint4 bitmask, uiDest;
    float4 fDest;

    r0.xyz = v1.yzx * float3(2, 2, 2) + float3(-1, -1, -1);
    r1.xyzw = v2.xyzw * float4(2, 2, 2, 2) + float4(-1, -1, -1, -1);
    r2.xyz = r1.zxy * r0.xyz;
    r0.xyz = r1.yzx * r0.yzx + -r2.xyz;
    r0.xyz = r0.xyz * r1.www;
    r2.xyz = r0.zxy * r1.yzx;
    r2.xyz = r0.yzx * r1.zxy + -r2.xyz;
    r2.xyz = r2.xyz * r1.www;
    r3.xy = LocalToWorld._m01_m21 * r2.yy;
    r3.xy = LocalToWorld._m00_m20 * r2.xx + r3.xy;
    r3.xy = LocalToWorld._m02_m22 * r2.zz + r3.xy;
    r4.xy = LocalToWorld._m01_m21 * r0.yy;
    r4.xy = LocalToWorld._m00_m20 * r0.xx + r4.xy;
    r4.xy = LocalToWorld._m02_m22 * r0.zz + r4.xy;
    r3.z = r4.x;
    o1.y = r4.y;
    r4.xy = LocalToWorld._m01_m21 * r1.yy;
    r4.xy = LocalToWorld._m00_m20 * r1.xx + r4.xy;
    r4.xy = LocalToWorld._m02_m22 * r1.zz + r4.xy;
    r3.w = r4.x;
    o1.z = r4.y;
    o0.xyz = r3.xzw;
    o1.x = r3.y;
    o1.w = LocalToWorldRotDeterminantFlip * r1.w;
    o2.xyzw = float4(0, 0, 0, 0);
    o3.xy = v4.xy;
    o3.zw = float2(0, 0);
    r3.xyzw = LocalToWorld._m01_m11_m21_m31 * v0.yyyy;
    r3.xyzw = LocalToWorld._m00_m10_m20_m30 * v0.xxxx + r3.xyzw;
    r3.xyzw = LocalToWorld._m02_m12_m22_m32 * v0.zzzz + r3.xyzw;
    r3.xyzw = LocalToWorld._m03_m13_m23_m33 * v0.wwww + r3.xyzw;
    r4.xyzw = ViewProjectionMatrix._m01_m11_m21_m31 * r3.yyyy;
    r4.xyzw = ViewProjectionMatrix._m00_m10_m20_m30 * r3.xxxx + r4.xyzw;
    r4.xyzw = ViewProjectionMatrix._m02_m12_m22_m32 * r3.zzzz + r4.xyzw;
    r4.xyzw = ViewProjectionMatrix._m03_m13_m23_m33 * r3.wwww + r4.xyzw;
    r3.xyz = -r3.xyz * CameraPosition.www + CameraPosition.xyz;
    o4.xyzw = r4.xyzw;
    o7.xyzw = r4.xyzw;
    r4.xyz = WorldToLocal._m01_m11_m21 * r3.yyy;
    r3.xyw = WorldToLocal._m00_m10_m20 * r3.xxx + r4.xyz;
    r3.xyz = WorldToLocal._m02_m12_m22 * r3.zzz + r3.xyw;
    o5.z = dot(r1.xyz, r3.xyz);
    o6.z = dot(r1.xyz, WorldToLocal._m02_m12_m22);
    o5.y = dot(r0.xyz, r3.xyz);
    o6.y = dot(r0.xyz, WorldToLocal._m02_m12_m22);
    o5.x = dot(r2.xyz, r3.xyz);
    o6.x = dot(r2.xyz, WorldToLocal._m02_m12_m22);
    o5.w = 1;
    return;
}