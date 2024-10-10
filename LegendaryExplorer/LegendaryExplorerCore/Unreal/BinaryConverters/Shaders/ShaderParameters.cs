// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
public struct FShaderParameter
{
    public ushort BaseIndex;
    public ushort NumBytes;
    public ushort BufferIndex;

    public readonly bool IsBound()
    {
        //if BufferIndex > 0, this param is in a shared constant buffer which is handled seperately
        return NumBytes > 0 && BufferIndex == 0;
    } 
}

public struct FShaderResourceParameter
{
    public ushort BaseIndex;
    public ushort NumResources;
    public ushort SamplerIndex;

    public readonly bool IsBound()
    {
        return NumResources > 0;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct TUniformParameter<TParam> where TParam : unmanaged
{
    public int Index;
    public TParam Param;
}

public struct FSceneTextureShaderParameters
{
    public FShaderResourceParameter SceneColorTexture;
    public FShaderResourceParameter SceneDepthTexture;
    public FShaderParameter MinZ_MaxZRatio;
    public FShaderParameter ScreenPositionScaleBias;
}

public struct FMotionBlurShaderParameters
{
    public FShaderResourceParameter LowResSceneBuffer;
    public FShaderResourceParameter VelocityBuffer;
    public FShaderParameter ScreenToWorld;
    public FShaderParameter PrevViewProj;
    public FShaderParameter StaticVelocityParameters;
    public FShaderParameter DynamicVelocityParameters;
    public FShaderParameter RenderTargetClamp;
    public FShaderParameter MotionBlurMaskScale;
    public FShaderParameter StepOffsetsOpaque;
    public FShaderParameter StepWeightsOpaque;
    public FShaderParameter StepOffsetsTranslucent;
    public FShaderParameter StepWeightsTranslucent;
}

public struct FColorRemapShaderParameters
{
    public FShaderParameter SceneShadowsAndDesaturation;
    public FShaderParameter SceneInverseHighLights;
    public FShaderParameter SceneMidTones;
    public FShaderParameter SceneScaledLuminanceWeights;
}

public struct FGammaShaderParameters
{
    public FShaderParameter GammaColorScaleAndInverse;
    public FShaderParameter GammaOverlayColor;
    public FShaderResourceParameter ColorGradingLUT;
    public FShaderParameter RenderTargetExtent;
}

public struct FAmbientOcclusionParams
{
    public FShaderResourceParameter AmbientOcclusionTexture;
    public FShaderResourceParameter AOHistoryTexture;
    public FShaderParameter AOScreenPositionScaleBias;
    public FShaderParameter ScreenEdgeLimits;
}

public struct FLightShaftPixelShaderParameters
{
    public FShaderParameter TextureSpaceBlurOrigin;
    public FShaderParameter WorldSpaceBlurOriginAndRadius;
    public FShaderParameter SpotAngles;
    public FShaderParameter WorldSpaceSpotDirection;
    public FShaderParameter WorldSpaceCameraPosition;
    public FShaderParameter UVMinMax;
    public FShaderParameter AspectRatioAndInvAspectRatio;
    public FShaderParameter LightShaftParameters;
    public FShaderParameter BloomTintAndThreshold;
    public FShaderParameter BloomScreenBlendThreshold;
    public FShaderParameter DistanceFade;
    public FShaderResourceParameter SourceTexture;
    public FShaderParameter OcclusionValueLimit;
}

public struct FHeightFogVertexShaderParameters
{
    public FShaderParameter FogDistanceScale;
    public FShaderParameter FogExtinctionDistance;
    public FShaderParameter FogMinHeight;
    public FShaderParameter FogMaxHeight;
    public FShaderParameter FogInScattering;
    public FShaderParameter FogStartDistance;
}

public struct FDOFShaderParameters
{
    public FShaderParameter PackedParameters;
    public FShaderParameter MinMaxBlurClamp;
    public FShaderResourceParameter DOFTexture;
}

public struct FHBAOShaderParameters
{
    public FShaderParameter RadiusToScreen;
    public FShaderParameter NegInvR2;
    public FShaderParameter NDotVBias;
    public FShaderParameter AOMultiplier;
    public FShaderParameter PowExponent;
    public FShaderParameter ProjInfo;
    public FShaderParameter BlurSharpness;
    public FShaderParameter InvFullResolution;
    public FShaderParameter InvQuarterResolution;
    public FShaderParameter FullResOffset;
    public FShaderParameter QuarterResOffset;
}

public struct FMaterialVertexShaderParameters
{
    public FShaderParameter CameraWorldPosition;
    public FShaderParameter ObjectWorldPositionAndRadius;
    public FShaderParameter ObjectOrientation;
    public FShaderParameter WindDirectionAndSpeed;
    public FShaderParameter FoliageImpulseDirection;
    public FShaderParameter FoliageNormalizedRotationAxisAndAngle;
    public TUniformParameter<FShaderParameter>[] UniformVertexScalarShaderParameters;
    public TUniformParameter<FShaderParameter>[] UniformVertexVectorShaderParameters;

    public void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref CameraWorldPosition);
        sc.SerializeUnmanaged(ref ObjectWorldPositionAndRadius);
        sc.SerializeUnmanaged(ref ObjectOrientation);
        sc.SerializeUnmanaged(ref WindDirectionAndSpeed);
        sc.SerializeUnmanaged(ref FoliageImpulseDirection);
        sc.SerializeUnmanaged(ref FoliageNormalizedRotationAxisAndAngle);
        sc.Serialize(ref UniformVertexScalarShaderParameters, sc.SerializeUnmanaged);
        sc.Serialize(ref UniformVertexVectorShaderParameters, sc.SerializeUnmanaged);
    }
}

public struct FMaterialPixelShaderParameters
{
    public FShaderParameter CameraWorldPosition;
    public FShaderParameter ObjectWorldPositionAndRadius;
    public FShaderParameter ObjectOrientation;
    public FShaderParameter WindDirectionAndSpeed;
    public FShaderParameter FoliageImpulseDirection;
    public FShaderParameter FoliageNormalizedRotationAxisAndAngle;
    public TUniformParameter<FShaderParameter>[] UniformPixelScalarShaderParameters;
    public TUniformParameter<FShaderParameter>[] UniformPixelVectorShaderParameters;
    public TUniformParameter<FShaderResourceParameter>[] UniformPixel2DShaderResourceParameters;
    public TUniformParameter<FShaderResourceParameter>[] UniformPixelCubeShaderResourceParameters;
    public FShaderParameter LocalToWorld;
    public FShaderParameter WorldToLocal;
    public FShaderParameter WorldToView;
    public FShaderParameter InvViewProjection;
    public FShaderParameter ViewProjection;
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter TwoSidedSign;
    public FShaderParameter InvGamma;
    public FShaderParameter DecalFarPlaneDistance;
    public FShaderParameter ObjectPostProjectionPosition;
    public FShaderParameter ObjectMacroUVScales;
    public FShaderParameter ObjectNDCPosition;
    public FShaderParameter OcclusionPercentage;
    public FShaderParameter EnableScreenDoorFade;
    public FShaderParameter ScreenDoorFadeSettings;
    public FShaderParameter ScreenDoorFadeSettings2;
    public FShaderResourceParameter ScreenDoorNoiseTexture;
    //should these be calculated instead of stored?
    private int UniformPixelScalarShaderParameters_IsValid;
    private int UniformPixelVectorShaderParameters_IsValid;
    public FShaderParameter WrapLightingParameters;
    public void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref CameraWorldPosition);
        sc.SerializeUnmanaged(ref ObjectWorldPositionAndRadius);
        sc.SerializeUnmanaged(ref ObjectOrientation);
        sc.SerializeUnmanaged(ref WindDirectionAndSpeed);
        sc.SerializeUnmanaged(ref FoliageImpulseDirection);
        sc.SerializeUnmanaged(ref FoliageNormalizedRotationAxisAndAngle);
        sc.Serialize(ref UniformPixelScalarShaderParameters, sc.SerializeUnmanaged);
        sc.Serialize(ref UniformPixelVectorShaderParameters, sc.SerializeUnmanaged);
        sc.Serialize(ref UniformPixel2DShaderResourceParameters, sc.SerializeUnmanaged);
        sc.Serialize(ref UniformPixelCubeShaderResourceParameters, sc.SerializeUnmanaged);
        sc.SerializeUnmanaged(ref LocalToWorld);
        sc.SerializeUnmanaged(ref WorldToLocal);
        sc.SerializeUnmanaged(ref WorldToView);
        sc.SerializeUnmanaged(ref InvViewProjection);
        sc.SerializeUnmanaged(ref ViewProjection);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref TwoSidedSign);
        sc.SerializeUnmanaged(ref InvGamma);
        sc.SerializeUnmanaged(ref DecalFarPlaneDistance);
        sc.SerializeUnmanaged(ref ObjectPostProjectionPosition);
        sc.SerializeUnmanaged(ref ObjectMacroUVScales);
        sc.SerializeUnmanaged(ref ObjectNDCPosition);
        sc.SerializeUnmanaged(ref OcclusionPercentage);
        sc.SerializeUnmanaged(ref EnableScreenDoorFade);
        sc.SerializeUnmanaged(ref ScreenDoorFadeSettings);
        sc.SerializeUnmanaged(ref ScreenDoorFadeSettings2);
        sc.SerializeUnmanaged(ref ScreenDoorNoiseTexture);
        sc.Serialize(ref UniformPixelScalarShaderParameters_IsValid);
        sc.Serialize(ref UniformPixelVectorShaderParameters_IsValid);
        sc.SerializeUnmanaged(ref WrapLightingParameters);
    }
}