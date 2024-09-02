using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
public struct FShaderParameter
{
    public ushort BaseIndex;
    public ushort NumBytes;
    public ushort BufferIndex;
}

public struct FShaderResourceParameter
{
    public ushort BaseIndex;
    public ushort NumResources;
    public ushort SamplerIndex;
}

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

public abstract class FMaterialShaderParameters
{
    public FShaderParameter CameraWorldPosition;
    public FShaderParameter ObjectWorldPositionAndRadius;
    public FShaderParameter ObjectOrientation;
    public FShaderParameter WindDirectionAndSpeed;
    public FShaderParameter FoliageImpulseDirection;
    public FShaderParameter FoliageNormalizedRotationAxisAndAngle;
}

public class FMaterialVertexShaderParameters : FMaterialShaderParameters
{
    public TUniformParameter<FShaderParameter>[] UniformVertexScalarShaderParameters;
    public TUniformParameter<FShaderParameter>[] UniformVertexVectorShaderParameters;
}

public class FMaterialPixelShaderParameters : FMaterialShaderParameters
{
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
    //UBOOL UniformPixelScalarShaderParameters is well formed?
    //UBOOL UniformPixelVectorShaderParameters is well formed?
    public FShaderParameter WrapLightingParameters;
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