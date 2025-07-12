// ReSharper disable InconsistentNaming

using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders
{
    [DebuggerDisplay("FShaderParameter BaseIndex: {BaseIndex} NumBytes: {NumBytes} BufferIndex: {BufferIndex}")]
    public struct FShaderParameter
    {
        //For LE1/LE2 TUniformParameters
        public const byte PixelScalarId = 15;
        public const byte PixelVectorId = 8;

        public ushort BaseIndex;
        public ushort NumBytes;
        public ushort BufferIndex;

        public readonly bool IsBound()
        {
            //if BufferIndex > 0, this param is in a shared constant buffer which is handled seperately
            return NumBytes > 0 && BufferIndex == 0;
        } 
    }

    [DebuggerDisplay("FShaderResourceParameter BaseIndex: {BaseIndex} NumResources: {NumResources} SamplerIndex: {SamplerIndex}")]
    public struct FShaderResourceParameter
    {
        //For LE1/LE2 TUniformParameters
        public const byte Pixel2DId = 16;
        public const byte PixelCubeId = 32;

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
        public FShaderParameter PrevViewProjMatrix;
        public FShaderParameter StaticVelocityParameters;
        public FShaderParameter DynamicVelocityParameters;
        public FShaderParameter RenderTargetClampParameter;
        public FShaderParameter MotionBlurMaskScaleAndBias;
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
            sc.SerializeUniformParameters(ref UniformVertexScalarShaderParameters, FShaderParameter.PixelScalarId,
                ref UniformVertexVectorShaderParameters, FShaderParameter.PixelVectorId);
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
        private int UniformPixelScalarShaderParameters_IsValid; //unk in LE1/LE2
        private int UniformPixelVectorShaderParameters_IsValid; //scalar and vector valid for LE1/LE2
        private int LE1LE2unkInt;

        // Appears to be BioWare specific as it is behind licensee check
        public FShaderParameter WrapLightingParameters;
        public void Serialize(SerializingContainer sc)
        {
            // MaterialShaderParameters
            sc.SerializeUnmanaged(ref CameraWorldPosition);
            sc.SerializeUnmanaged(ref ObjectWorldPositionAndRadius);
            sc.SerializeUnmanaged(ref ObjectOrientation);
            sc.SerializeUnmanaged(ref WindDirectionAndSpeed);
            sc.SerializeUnmanaged(ref FoliageImpulseDirection);
            sc.SerializeUnmanaged(ref FoliageNormalizedRotationAxisAndAngle);
            sc.SerializeUniformParameters(ref UniformPixelScalarShaderParameters, FShaderParameter.PixelScalarId,
                ref UniformPixelVectorShaderParameters, FShaderParameter.PixelVectorId);
            sc.SerializeUniformParameters(ref UniformPixel2DShaderResourceParameters, FShaderResourceParameter.Pixel2DId, 
                ref UniformPixelCubeShaderResourceParameters, FShaderResourceParameter.PixelCubeId);
        
            // PixelShaderParameters
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
            if (sc.Game is MEGame.LE1 or MEGame.LE2)
            {
                //Todo: figure out how to compute this if coming from LE3
                sc.Serialize(ref LE1LE2unkInt);
            }
            sc.SerializeUnmanaged(ref WrapLightingParameters);
        }
    }
}
namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class SerializingContainer
    {
        public void SerializeUniformParameters<TParam>(ref TUniformParameter<TParam>[] params1, byte param1TypeId, ref TUniformParameter<TParam>[] params2, byte param2TypeId) where TParam : unmanaged
        {
            if (Game is MEGame.LE3)
            {
                Serialize(ref params1, SerializeUnmanaged);
                Serialize(ref params2, SerializeUnmanaged);
                return;
            }

            if (IsSaving)
            {
                int combinedCount = params1.Length + params2.Length;
                Serialize(ref combinedCount);
                foreach (TUniformParameter<TParam> param in params1)
                {
                    var p = param;
                    Serialize(ref param1TypeId);
                    SerializeUnmanaged(ref p);
                }
                foreach (TUniformParameter<TParam> param in params2)
                {
                    var p = param;
                    Serialize(ref param2TypeId);
                    SerializeUnmanaged(ref p);
                }
            }
            else
            {
                int combinedCount = 0;
                Serialize(ref combinedCount);
                var params1List = new List<TUniformParameter<TParam>>();
                var params2List = new List<TUniformParameter<TParam>>();
                byte typeId = 0;
                for (int i = 0; i < combinedCount; i++)
                {
                    Serialize(ref typeId);
                    TUniformParameter<TParam> param = default;
                    SerializeUnmanaged(ref param);
                    if (typeId == param1TypeId)
                    {
                        params1List.Add(param);
                    }
                    else if (typeId == param2TypeId)
                    {
                        params2List.Add(param);
                    }
                    else
                    {
                        throw new System.Exception($"Unexpected TUniformParameter type Id: {typeId}");
                    }
                }
                params1 = params1List.ToArray();
                params2 = params2List.ToArray();
            }
        }
    }
}