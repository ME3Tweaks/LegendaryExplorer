using System;
using System.IO;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders
{
    public abstract class Shader
    {
        public enum ShaderFrequency : byte
        {
            Vertex = 0,
            Pixel = 1,
        }

        public NameReference ShaderType;
        public Guid Guid;
        public ShaderFrequency Frequency;
        public byte[] ShaderByteCode;
        public uint ParameterMapCRC;
        public int InstructionCount;
        public byte Platform; // LE is 5, OT is 0. However, LE also has a few 2 and 3 for SM2 and SM3 shaders. So we must store this info.
        public NameReference? VertexFactoryType; //only exists in Shaders with a FVertexFactoryParameterRef

        public virtual Shader Clone() => SharedClone();

        protected Shader SharedClone()
        {
            var newShader = (Shader)MemberwiseClone();
            newShader.ShaderByteCode = ShaderByteCode.ArrayClone();
            return newShader;
        }

        public void ReplaceByteCode(byte[] newShaderByteCode)
        {
            ShaderByteCode = newShaderByteCode;
        }

        internal virtual DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedWriter = sc.SerializeDefferedFileOffset();
            sc.Serialize(ref Platform);
            sc.Serialize(ref Frequency);
            sc.Serialize(ref ShaderByteCode);
            sc.Serialize(ref ParameterMapCRC);
            sc.Serialize(ref Guid);
            sc.Serialize(ref ShaderType);
            sc.Serialize(ref InstructionCount);
            return defferedWriter;
        }
    }

    public class UnparsedShader : Shader
    {
        private byte[] unkBytesPreName; //only exists in some Shaders with a FVertexFactoryParameterRef
        private byte[] unkBytes;

        public override Shader Clone()
        {
            var newShader = (UnparsedShader)SharedClone();
            newShader.unkBytesPreName = unkBytesPreName?.ArrayClone();
            newShader.unkBytes = unkBytes?.ArrayClone();
            return newShader;
        }

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            bool preSerializesParams = ShaderType.Name is "FBinkYCrCbToRGBNoPixelAlphaPixelShader" or "FBinkYCrCbAToRGBAPixelShader";
            int endOffset = 0;
            var defferedFileOffsetWriter = new DefferedFileOffsetWriter(sc.ms.Position);
            sc.Serialize(ref endOffset);
            sc.Serialize(ref Platform);
            sc.Serialize(ref Frequency);
            if (preSerializesParams)
            {
                if (sc.IsLoading)
                {
                    unkBytes = sc.ms.ReadBytes(ShaderType.Name is "FBinkYCrCbAToRGBAPixelShader" ? 6 : 0x30);
                }
                else
                {
                    sc.ms.Writer.Write(unkBytes);
                }
            }

            sc.Serialize(ref ShaderByteCode);
            sc.Serialize(ref ParameterMapCRC);
            sc.Serialize(ref Guid);
            sc.Serialize(ref ShaderType);
            sc.Serialize(ref InstructionCount);
            if (!preSerializesParams)
            {
                if (sc.IsLoading)
                {
                    switch (ShaderType.Name)
                    {
                        case "FFogVolumeApplyVertexShader":
                        case "FHitMaskVertexShader":
                        case "FHitProxyVertexShader":
                        case "FModShadowMeshVertexShader":
                        case "FSFXWorldNormalVertexShader":
                        case "FTextureDensityVertexShader":
                        case "TDepthOnlyVertexShader<0>":
                        case "TDepthOnlyVertexShader<1>":
                        case "FVelocityVertexShader":
                        case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                        case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                        case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                        case "FShadowDepthVertexShader":
                        case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                        case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                        case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                        case "TAOMeshVertexShader<0>":
                        case "TAOMeshVertexShader<1>":
                        case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                        case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                        case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                        case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                        case "FShadowDepthNoPSVertexShader":
                            unkBytesPreName = null;
                            VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
                            break;
                        case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                        case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                        case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                        case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                        case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                        case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                        case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                        case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                        case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                        case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFSHLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFPointLightLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                        case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy":
                            unkBytesPreName = sc.ms.ReadBytes(6);
                            VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
                            break;
                        case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                        case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                        case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                        case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                        case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                            unkBytesPreName = sc.ms.ReadBytes(12);
                            VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
                            break;
                        default:
                            unkBytesPreName = null;
                            VertexFactoryType = null;
                            break;
                    }
                    unkBytes = sc.ms.ReadToBuffer(endOffset - sc.FileOffset);
                }
                else
                {
                    if (VertexFactoryType is NameReference vertexFactoryType)
                    {
                        if (unkBytesPreName is not null)
                        {
                            sc.ms.Writer.WriteFromBuffer(unkBytesPreName);
                        }
                        sc.ms.Writer.WriteNameReference(vertexFactoryType, sc.Pcc);
                    }
                    sc.ms.Writer.WriteFromBuffer(unkBytes);
                }
            }
            return defferedFileOffsetWriter;
        }
    }

    public class FGFxPixelShader : Shader
    {
        public Fixed4<FShaderResourceParameter> TextureParams;
        public FShaderParameter ConstantColor;
        public FShaderParameter ColorScale;
        public FShaderParameter ColorBias;
        public FShaderParameter InverseGamma;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            sc.SerializeUnmanaged(ref TextureParams);
            sc.SerializeUnmanaged(ref ConstantColor);
            sc.SerializeUnmanaged(ref ColorScale);
            sc.SerializeUnmanaged(ref ColorBias);
            sc.SerializeUnmanaged(ref InverseGamma);

            return defferedOffsetWriter;
        }
    }

    public class FGFxPixelShaderHDR : FGFxPixelShader
    {
        public FShaderParameter HDRBrightnessScale;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            sc.SerializeUnmanaged(ref HDRBrightnessScale);

            return defferedOffsetWriter;
        }
    }

    class THeightFogPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter FogDistanceScale;
        public FShaderParameter FogExtinctionDistance;
        public FShaderParameter FogInScattering;
        public FShaderParameter FogStartDistance;
        public FShaderParameter FogMinStartDistance;
        public FShaderParameter EncodePower;
        public FShaderParameter FalloffStrength;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            sc.SerializeUnmanaged(ref SceneTextureParameters);
            sc.SerializeUnmanaged(ref FogDistanceScale);
            sc.SerializeUnmanaged(ref FogExtinctionDistance);
            sc.SerializeUnmanaged(ref FogInScattering);
            sc.SerializeUnmanaged(ref FogStartDistance);
            sc.SerializeUnmanaged(ref FogMinStartDistance);
            sc.SerializeUnmanaged(ref EncodePower);
            sc.SerializeUnmanaged(ref FalloffStrength);

            return defferedOffsetWriter;
        }
    }

    class FBranchingPCFProjectionPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter ScreenToShadowMatrix;
        public FShaderParameter InvRandomAngleTextureSize;
        public FShaderResourceParameter ShadowDepthTexture;
        public FShaderResourceParameter RandomAngleTexture;
        public FShaderParameter RefiningSampleOffsets;
        public FShaderParameter EdgeSampleOffsets;
        public FShaderParameter ShadowBufferSize;
        public FShaderParameter ShadowFadeFraction;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            sc.SerializeUnmanaged(ref SceneTextureParameters);
            sc.SerializeUnmanaged(ref ScreenToShadowMatrix);
            sc.SerializeUnmanaged(ref InvRandomAngleTextureSize);
            sc.SerializeUnmanaged(ref ShadowDepthTexture);
            sc.SerializeUnmanaged(ref RandomAngleTexture);
            sc.SerializeUnmanaged(ref RefiningSampleOffsets);
            sc.SerializeUnmanaged(ref EdgeSampleOffsets);
            sc.SerializeUnmanaged(ref ShadowBufferSize);
            sc.SerializeUnmanaged(ref ShadowFadeFraction);

            return defferedOffsetWriter;
        }
    }

    class TBranchingPCFModProjectionPixelShader<LightMapPolicy> : FBranchingPCFProjectionPixelShader
        where LightMapPolicy : unmanaged, IModShadowPixelParamsType
    {
        public FShaderParameter ShadowModulateColorParam;
        public FShaderParameter ScreenToWorldParam;
        public FShaderParameter EmissiveAlphaMaskScale;
        public FShaderParameter UseEmissiveMask;
        public LightMapPolicy ModShadowPixelParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            sc.SerializeUnmanaged(ref ShadowModulateColorParam);
            sc.SerializeUnmanaged(ref ScreenToWorldParam);
            sc.SerializeUnmanaged(ref EmissiveAlphaMaskScale);
            sc.SerializeUnmanaged(ref UseEmissiveMask);
            ModShadowPixelParams.Serialize(sc);

            return defferedOffsetWriter;
        }
    }

    class FGFxVertexShader : Shader
    {
        public FShaderParameter Transform;
        public Fixed2<FShaderParameter> TextureMatrixParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            throw new NotImplementedException();

            return defferedOffsetWriter;
        }
    }

    class FResolveVertexShader : Shader
    {
    }

    class FReconstructHDRVertexShader : Shader
    {
    }

    class FLDRExtractVertexShader : Shader
    {
    }

    class FMotionBlurVertexShader : Shader
    {
    }

    class FBinkVertexShader : Shader
    {
    }

    class FOneColorVertexShader : Shader
    {
    }

    class FGammaCorrectionVertexShader : Shader
    {
    }

    class FNULLPixelShader : Shader
    {
    }

    class FHorizonBasedAOVertexShader : Shader
    {
    }

    class FModShadowVolumeVertexShader : Shader
    {
    }

    class FOcclusionQueryVertexShader : Shader
    {
    }

    class FModShadowProjectionVertexShader : Shader
    {
    }

    class FLUTBlenderVertexShader : Shader
    {
    }

    class FPostProcessAAVertexShader : Shader
    {
    }

    class FShadowProjectionVertexShader : Shader
    {
    }

    class FScreenVertexShader : Shader
    {
    }

    class FFluidVertexShader : Shader
    {
    }

    class FEdgePreservingFilterVertexShader : Shader
    {
    }

    class FLightFunctionVertexShader : Shader
    {
    }

    class FResolveSingleSamplePixelShader : Shader
    {
        public FShaderResourceParameter UnresolvedSurface;
        public FShaderParameter SingleSampleIndex;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);

            throw new NotImplementedException();

            return defferedOffsetWriter;
        }
    }

    class FResolveDepthPixelShader : Shader
    {
        public FShaderResourceParameter UnresolvedSurface;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TModShadowVolumePixelShader<LightMapPolicy> : Shader
        where LightMapPolicy : unmanaged, IModShadowPixelParamsType
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter ShadowModulateColor;
        public FShaderParameter ScreenToWorld;
        public LightMapPolicy ModShadowPixelParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FGammaCorrectionPixelShader : Shader
    {
        public FShaderResourceParameter SceneTexture;
        public FShaderParameter InverseGamma;
        public FShaderParameter ColorScale;
        public FShaderParameter OverlayColor;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDownsampleSceneDepthPixelShader : Shader
    {
        public FShaderParameter ProjectionScaleBias;
        public FShaderParameter SourceTexelOffsets01;
        public FShaderParameter SourceTexelOffsets23;
        public FSceneTextureShaderParameters SceneTextureParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFXAA3BlendPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter rcpFrame;
        public FShaderParameter rcpFrameOpt;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FTexturedCalibrationBoxHDRPixelShader : Shader
    {
        public FShaderParameter CalibrationParameters;
        public FShaderResourceParameter SourceTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    abstract class SingleTextureShader : Shader
    {
        public FShaderResourceParameter Texture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FScreenPixelShader : SingleTextureShader
    {
    }

    class FHBAOApplyPixelShader : SingleTextureShader
    {
    }

    class FCopyVariancePixelShader : SingleTextureShader
    {
    }

    class FSimpleElementHitProxyPixelShader : SingleTextureShader
    {
    }

    class FMotionBlurPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FMotionBlurShaderParameters MotionBlurParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDownsampleDepthVertexShader : Shader
    {
        public FShaderParameter HalfSceneColorTexelSize;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FAmbientOcclusionVertexShader : Shader
    {
        public FShaderParameter ScreenToView;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FCalibrationBoxHDRPixelShader : Shader
    {
        public FShaderParameter CalibrationParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TFilterVertexShader : Shader
    {
        public FShaderParameter SampleOffsets;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDOFAndBloomGatherVertexShader : Shader
    {
        public FShaderParameter SampleOffsets;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FShaderComplexityAccumulatePixelShader : Shader
    {
        public FShaderParameter NormalizedComplexity;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDistortionApplyScreenVertexShader : Shader
    {
        public FShaderParameter Transform;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSimpleElementVertexShader : Shader
    {
        public FShaderParameter Transform;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDownsampleLightShaftsVertexShader : Shader
    {
        public FShaderParameter ScreenToWorld;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FRadialBlurVertexShader : Shader
    {
        public FShaderParameter WorldCenterPos;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FOneColorPixelShader : Shader
    {
        public FShaderParameter Color;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDOFAndBloomBlendVertexShader : Shader
    {
        public FShaderParameter SceneCoordinateScaleBias;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHistoryUpdateVertexShader : Shader
    {
        public FShaderParameter ScreenToWorldOffset;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FReconstructHDRPixelShader : Shader
    {
        public FShaderResourceParameter SourceTexture;
        public FShaderParameter HDRParameters;
        public FShaderParameter CalibrationParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSimpleElementPixelShader : Shader
    {
        public FShaderResourceParameter Texture;
        public FShaderParameter TextureComponentReplicate;
        public FShaderParameter TextureComponentReplicateAlpha;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TFilterPixelShader : Shader
    {
        public FShaderResourceParameter FilterTexture;
        public FShaderParameter SampleWeights;
        public FShaderParameter SampleMaskRect;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FShadowVolumeVertexShader : Shader
    {
        public FShaderParameter LightPosition;
        public FShaderParameter BaseExtrusion;
        public FShaderParameter LocalToWorld;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSFXUberPostProcessBlendPixelShader : FUberPostProcessBlendPixelShader
    {
        public FShaderResourceParameter NoiseTexture;
        public FShaderParameter NoiseTextureOffset;
        public FShaderParameter FilmGrain_Scale;
        public FShaderResourceParameter smpFilmicLUT;
        public FShaderParameter ScreenUVScaleBias;
        public FShaderParameter HighPrecisionGamma;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FUberPostProcessVertexShader : Shader
    {
        public FShaderParameter SceneCoordinate1ScaleBias;
        public FShaderParameter SceneCoordinate2ScaleBias;
        public FShaderParameter SceneCoordinate3ScaleBias;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FUberHalfResPixelShader : FDOFAndBloomBlendPixelShader
    {
        public FMotionBlurShaderParameters MotionBlurParameters;
        public FShaderResourceParameter LowResSceneBufferPoint;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FUberPostProcessBlendPixelShader : FDOFAndBloomBlendPixelShader
    {
        public FColorRemapShaderParameters MaterialParameters;
        public FGammaShaderParameters GammaParameters;
        public FShaderResourceParameter LowResSceneBuffer;
        public FShaderParameter HalfResMaskRec;
        public FMotionBlurShaderParameters MotionBlurParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TAOApplyPixelShader : Shader
    {
        public FAmbientOcclusionParams AOParams;
        public FShaderParameter OcclusionColor;
        public FShaderParameter FogColor;
        public FShaderParameter TargetSize;
        public FShaderParameter InvEncodePower;
        public FShaderResourceParameter FogTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TModShadowProjectionPixelShader<LightMapPolicy> : Shader
        where LightMapPolicy : unmanaged, IModShadowPixelParamsType
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter ScreenToShadowMatrix;
        public FShaderResourceParameter ShadowDepthTexture;
        public FShaderResourceParameter ShadowDepthTextureComparisonSampler;
        public FShaderParameter SampleOffsets;
        public FShaderParameter ShadowBufferSize;
        public FShaderParameter ShadowFadeFraction;
        public FShaderParameter ShadowModulateColor;
        public FShaderParameter ScreenToWorld;
        public FShaderParameter bEmissiveAlphaMaskScale;
        public FShaderParameter bApplyEmissiveToShadowCoverage;
        public LightMapPolicy ModShadowPixelParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFluidApplyPixelShader : Shader
    {
        public FShaderResourceParameter FluidHeightTexture;
        public FShaderResourceParameter FluidNormalTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FXAAFilterComputeShader : Shader
    {
        public FShaderResourceParameter WorkQueue;
        public FShaderResourceParameter Color;
        public FShaderResourceParameter InColor;
        public FShaderResourceParameter Luma;
        public FShaderResourceParameter LinearSampler;
        public FShaderParameter RcpTextureSize;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TShadowProjectionPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter ScreenToShadowMatrix;
        public FShaderResourceParameter ShadowDepthTexture;
        public FShaderResourceParameter ShadowDepthTextureComparisonSampler;
        public FShaderParameter SampleOffsets;
        public FShaderParameter ShadowBufferSize;
        public FShaderParameter ShadowFadeFraction;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FBlurLightShaftsPixelShader : Shader
    {
        public FLightShaftPixelShaderParameters LightShaftParameters;
        public FShaderParameter BlurPassIndex;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFilterVSMComputeShader : Shader
    {
        public FShaderResourceParameter DepthTexture;
        public FShaderResourceParameter VarianceTexture;
        public FShaderParameter SubRect;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDistortionApplyScreenPixelShader : Shader
    {
        public FShaderResourceParameter AccumulatedDistortionTextureParam;
        public FShaderResourceParameter SceneColorTextureParam;
        public FShaderParameter SceneColorRect;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSRGBMLAABlendPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderResourceParameter EdgeCountTexture;
        public FShaderParameter gRTSize;
        public FShaderParameter gLuminanceEquation;
        public FShaderParameter gInverseDisplayGamma;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TBasePassVertexShader<LightMapPolicy, DensityPolicy> : Shader
        where LightMapPolicy : unmanaged, IVertexParametersType
        where DensityPolicy : unmanaged, IVertexShaderParametersType
    {
        public LightMapPolicy LightMapVertexParams;
        public FVertexFactoryParameterRef VertexFactoryParameters;
        public FHeightFogVertexShaderParameters HeightFogParameters;
        public FMaterialVertexShaderParameters MaterialParameters;
        public DensityPolicy DensityVertexShaderParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FShadowProjectionMaskPixelShader : Shader
    {
        public FShaderParameter LightDirection;
        public FShaderParameter ScreenPositionScaleBias;
        public FShaderResourceParameter SceneNormalTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FShaderComplexityApplyPixelShader : Shader
    {
        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FCopyTranslucencyDepthPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDownsampleDepthPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FApplyLightShaftsPixelShader : Shader
    {
        public FLightShaftPixelShaderParameters LightShaftParameters;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderResourceParameter SmallSceneColorTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFXAAResolveComputeShader : Shader
    {
        public FShaderResourceParameter WorkQueueH;
        public FShaderResourceParameter WorkQueueV;
        public FShaderResourceParameter IndirectParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDownsampleSceneDepthAndNormalsPixelShader : Shader
    {
        public FShaderParameter ProjectionScaleBias;
        public FShaderParameter SourceTexelOffsets01;
        public FShaderParameter SourceTexelOffsets23;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderResourceParameter FullSizedNormalsTexture;
        public FShaderParameter OffsetIndex;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFXAAPrepComputeShader : Shader
    {
        public FShaderResourceParameter HWork;
        public FShaderResourceParameter VWork;
        public FShaderResourceParameter Color;
        public FShaderResourceParameter Luma;
        public FShaderResourceParameter LinearSampler;
        public FShaderParameter RcpTextureSize;
        public FShaderParameter TextureOffset;
        public FShaderParameter ContrastThreshold;
        public FShaderParameter SubpixelRemoval;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSRGBMLAAEdgeDetectionPixelShader : Shader
    {
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter gRTSize;
        public FShaderParameter gLuminanceEquation;
        public FShaderParameter gInverseDisplayGamma;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class THeightFogVertexShader : Shader
    {
        public FShaderParameter ScreenPositionScaleBias;
        public FShaderParameter FogMinHeight;
        public FShaderParameter FogMaxHeight;
        public FShaderParameter ScreenToWorld;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FApplyLightShaftsVertexShader : Shader
    {
        public FShaderParameter SourceTextureScaleBias;
        public FShaderParameter SceneColorScaleBias;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TBasePassPixelShader<LightMapPolicy> : Shader
        where LightMapPolicy : unmanaged, IPixelParametersType
    {
        public LightMapPolicy PixelParams;
        public FMaterialPixelShaderParameters MaterialParameters;
        public FShaderParameter AmbientColorAndSkyFactor;
        public FShaderParameter UpperSkyColor;
        public FShaderParameter LowerSkyColor;
        public FShaderParameter MotionBlurMask;
        public FShaderParameter CharacterMask;
        public FShaderParameter TranslucencyDepth;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLUTBlenderPixelShader_1 : Shader
    {
        public Fixed1<FShaderResourceParameter> TextureParameter;
        public FShaderParameter Weights;
        public FGammaShaderParameters GammaParameters;
        public FColorRemapShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLUTBlenderPixelShader_2 : Shader
    {
        public Fixed2<FShaderResourceParameter> TextureParameter;
        public FShaderParameter Weights;
        public FGammaShaderParameters GammaParameters;
        public FColorRemapShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLUTBlenderPixelShader_3 : Shader
    {
        public Fixed3<FShaderResourceParameter> TextureParameter;
        public FShaderParameter Weights;
        public FGammaShaderParameters GammaParameters;
        public FColorRemapShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLUTBlenderPixelShader_4 : Shader
    {
        public Fixed4<FShaderResourceParameter> TextureParameter;
        public FShaderParameter Weights;
        public FGammaShaderParameters GammaParameters;
        public FColorRemapShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLUTBlenderPixelShader_5 : Shader
    {
        public Fixed5<FShaderResourceParameter> TextureParameter;
        public FShaderParameter Weights;
        public FGammaShaderParameters GammaParameters;
        public FColorRemapShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFluidNormalPixelShader : Shader
    {
        public FShaderParameter CellSize;
        public FShaderParameter HeightScale;
        public FShaderResourceParameter HeightTexture;
        public FShaderParameter SplineMargin;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDownsampleLightShaftsPixelShader : Shader
    {
        public FLightShaftPixelShaderParameters LightShaftParameters;
        public FShaderParameter SampleOffsets;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderResourceParameter SmallSceneColorTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FModShadowMeshPixelShader : Shader
    {
        public FMaterialPixelShaderParameters MaterialParameters;
        public FShaderParameter AttenAllowed;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFluidSimulatePixelShader : Shader
    {
        public FShaderParameter CellSize;
        public FShaderParameter DampFactor;
        public FShaderParameter TravelSpeed;
        public FShaderParameter PreviousOffset1;
        public FShaderParameter PreviousOffset2;
        public FShaderResourceParameter PreviousHeights1;
        public FShaderResourceParameter PreviousHeights2;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FApplyForcePixelShader : Shader
    {
        public FShaderParameter ForcePosition;
        public FShaderParameter ForceRadius;
        public FShaderParameter ForceMagnitude;
        public FShaderResourceParameter PreviousHeights1;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FDOFAndBloomBlendPixelShader : Shader
    {
        public FDOFShaderParameters DOFParameters;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderResourceParameter BlurredImage;
        public FShaderResourceParameter DOFBlurredNear;
        public FShaderResourceParameter DOFBlurredFar;
        public FShaderResourceParameter BlurredImageSeperateBloom;
        public FShaderParameter BloomTintAndScreenBlendThreshold;
        public FShaderParameter InputTextureSize;
        public FShaderParameter DOFKernelParams;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDOFBlurPixelShader : Shader
    {
        public FDOFShaderParameters DOFParameters;
        public FShaderResourceParameter DOFTempTexture;
        public FShaderResourceParameter DOFTempTexture2;
        public FShaderParameter DOFKernelParams;
        public FShaderParameter BlurDirections;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDOFAndBloomGatherPixelShader : Shader
    {
        public FDOFShaderParameters DOFParameters;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FShaderParameter BloomScaleAndThreshold;
        public FShaderParameter SceneMultiplier;
        public FShaderResourceParameter SmallSceneColorTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TLightMapDensityPixelShader<LightMapTexturePolicy> : Shader
        where LightMapTexturePolicy : unmanaged, IPixelParametersType
    {
        public LightMapTexturePolicy PixelParams;
        public FMaterialPixelShaderParameters MaterialParameters;
        public FShaderParameter LightMapDensityParameters;
        public FShaderParameter BuiltLightingAndSelectedFlags;
        public FShaderParameter DensitySelectedColor;
        public FShaderParameter LightMapResolutionScale;
        public FShaderParameter LightMapDensityDisplayOptions;
        public FShaderParameter VertexMappedColor;
        public FShaderResourceParameter GridTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TDOFGatherPixelShader : TDOFAndBloomGatherPixelShader
    {
        public FShaderParameter InputTextureSize;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHBAOBlurComputeShader : Shader
    {
        public FHBAOShaderParameters HBAOParameters;
        public FShaderResourceParameter AOTexture;
        public FShaderResourceParameter BlurOut;
        public FShaderParameter AOTexDimensions;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHBAODeinterleaveComputeShader : Shader
    {
        public FHBAOShaderParameters HBAOParameters;
        public FShaderResourceParameter SceneDepthTexture;
        public FShaderResourceParameter DeinterleaveOut;
        public FShaderParameter ArrayOffset;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFXAA3VertexShader : Shader
    {
        public FShaderParameter rcpFrame;
        public FShaderParameter rcpFrameOpt;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSimpleElementDistanceFieldGammaPixelShader : Shader
    {
        public FShaderParameter SmoothWidth;
        public FShaderParameter EnableShadow;
        public FShaderParameter ShadowDirection;
        public FShaderParameter ShadowColor;
        public FShaderParameter ShadowSmoothWidth;
        public FShaderParameter EnableGlow;
        public FShaderParameter GlowColor;
        public FShaderParameter GlowOuterRadius;
        public FShaderParameter GlowInnerRadius;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TShadowDepthVertexShader : Shader
    {
        public FVertexFactoryParameterRef VertexFactoryParameters;
        public FMaterialVertexShaderParameters MaterialParameters;
        public FShaderParameter ProjectionMatrix;
        public FShaderParameter InvMaxSubjectDepth;
        public FShaderParameter DepthBias;
        public FShaderParameter bClampToNearPlane;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSimpleElementMaskedGammaPixelShader : FSimpleElementGammaPixelShader
    {
        public FShaderParameter ClipRef;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSimpleElementGammaPixelShader : FSimpleElementPixelShader
    {
        public FShaderParameter Gamma;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FGenerateDeinterleavedHBAOComputeShader : Shader
    {
        public FHBAOShaderParameters HBAOParameters;
        public FShaderResourceParameter OutAO;
        public FShaderResourceParameter QuarterResDepthCS;
        public FShaderResourceParameter ViewNormalTex;
        public FShaderParameter JitterCS;
        public FShaderParameter ArrayOffset;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHBAOReconstructNormalsComputeShader : Shader
    {
        public FHBAOShaderParameters HBAOParameters;
        public FShaderResourceParameter SceneDepthTexture;
        public FShaderResourceParameter ReconstructNormalOut;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TAOMaskPixelShader : Shader
    {
        public FAmbientOcclusionParams AOParams;
        public FShaderParameter HistoryConvergenceRates;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FStaticHistoryUpdatePixelShader : Shader
    {
        public FAmbientOcclusionParams AOParams;
        public FShaderParameter PrevViewProjMatrix;
        public FShaderParameter HistoryConvergenceRates;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TEdgePreservingFilterPixelShader : Shader
    {
        public FAmbientOcclusionParams AOParams;
        public FShaderParameter FilterSampleOffsets;
        public FShaderParameter FilterParameters;
        public FShaderParameter CustomParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TAmbientOcclusionPixelShader : Shader
    {
        public FShaderParameter OcclusionSampleOffsets;
        public FShaderResourceParameter RandomNormalTexture;
        public FShaderParameter ProjectionScale;
        public FShaderParameter ProjectionMatrix;
        public FShaderParameter NoiseScale;
        public FAmbientOcclusionParams AOParams;
        public FShaderParameter OcclusionCalcParameters;
        public FShaderParameter HaloDistanceScale;
        public FShaderParameter OcclusionRemapParameters;
        public FShaderParameter OcclusionFadeoutParameters;
        public FShaderParameter MaxRadiusTransform;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FBinkGpuShaderYCrCbToRGB : Shader
    {
        public FShaderResourceParameter YTex;
        public FShaderResourceParameter CrCbTex;
        public FShaderResourceParameter ATex;
        public FShaderParameter cmatrix;
        public FShaderParameter alpha_mult;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FBinkGpuShaderHDR : Shader
    {
        public FShaderResourceParameter YTex;
        public FShaderResourceParameter CrCbTex;
        public FShaderResourceParameter ATex;
        public FShaderResourceParameter HTex;
        public FShaderParameter alpha_mult;
        public FShaderParameter hdr;
        public FShaderParameter ctcp;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FBinkYCrCbAToRGBAPixelShader : Shader
    {
        public FShaderResourceParameter tex3;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            //weird serialization where the param comes before the bytecode
            var defferedWriter = sc.SerializeDefferedFileOffset();
            sc.Serialize(ref Platform);
            sc.Serialize(ref Frequency);
            sc.SerializeUnmanaged(ref tex3);
            sc.Serialize(ref ShaderByteCode);
            sc.Serialize(ref ParameterMapCRC);
            sc.Serialize(ref Guid);
            sc.Serialize(ref ShaderType);
            sc.Serialize(ref InstructionCount);
            return defferedWriter;
        }
    }

    class FBinkYCrCbToRGBNoPixelAlphaPixelShader : Shader
    {
        public FShaderResourceParameter tex0;
        public FShaderResourceParameter tex1;
        public FShaderResourceParameter tex2;
        public FShaderParameter crc;
        public FShaderParameter cbc;
        public FShaderParameter adj;
        public FShaderParameter yscale;
        public FShaderParameter consts;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            //weird serialization where the params come before the bytecode
            var defferedWriter = sc.SerializeDefferedFileOffset();
            sc.Serialize(ref Platform);
            sc.Serialize(ref Frequency);
            sc.SerializeUnmanaged(ref tex0);
            sc.SerializeUnmanaged(ref tex1);
            sc.SerializeUnmanaged(ref tex2);
            sc.SerializeUnmanaged(ref crc);
            sc.SerializeUnmanaged(ref cbc);
            sc.SerializeUnmanaged(ref adj);
            sc.SerializeUnmanaged(ref yscale);
            sc.SerializeUnmanaged(ref consts);
            sc.Serialize(ref ShaderByteCode);
            sc.Serialize(ref ParameterMapCRC);
            sc.Serialize(ref Guid);
            sc.Serialize(ref ShaderType);
            sc.Serialize(ref InstructionCount);
            return defferedWriter;
        }
    }

    class TLightPixelShader<LightTypePolicy, ShadowingTypePolicy> : Shader
        where LightTypePolicy : unmanaged, IPixelParametersType
        where ShadowingTypePolicy : unmanaged, IPixelParametersType
    {
        public LightTypePolicy LightTypePixelParams;
        public ShadowingTypePolicy ShadowingPixelParams;
        public FMaterialPixelShaderParameters MaterialParameters;
        public FShaderResourceParameter LightAttenuationTexture;
        public FShaderParameter bReceiveDynamicShadows;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TLightMapDensityVertexShader<LightMapTexturePolicy> : TLightVertexShader<LightMapTexturePolicy, FNullPolicy>
        where LightMapTexturePolicy : unmanaged, IVertexParametersType
    {
    }

    class TLightVertexShader<LightTypePolicy, ShadowingTypePolicy> : Shader
        where LightTypePolicy : unmanaged, IVertexParametersType
        where ShadowingTypePolicy : unmanaged, IVertexParametersType
    {
        public LightTypePolicy LightTypeVertexParams;
        public ShadowingTypePolicy ShadowingVertexParams;
        public FVertexFactoryParameterRef VertexFactoryParameters;
        public FMaterialVertexShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FModShadowMeshVertexShader : MaterialVertexShader
    {
        public FShaderParameter LightPosition;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    abstract class MaterialPixelShader : Shader
    {
        public FMaterialPixelShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSFXWorldNormalPixelShader : MaterialPixelShader
    {
    }

    class TDepthOnlyScreenDoorPixelShader : MaterialPixelShader
    {
    }

    class FTranslucencyPostRenderDepthPixelShader : MaterialPixelShader
    {
    }

    class TDistortionMeshPixelShader : MaterialPixelShader
    {
    }

    class TDepthOnlySolidPixelShader : MaterialPixelShader
    {
    }

    abstract class MaterialVertexShader : Shader
    {
        public FVertexFactoryParameterRef VertexFactoryParameters;
        public FMaterialVertexShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FSFXWorldNormalVertexShader : MaterialVertexShader
    {
    }

    class FTextureDensityVertexShader : MaterialVertexShader
    {
    }

    class TDepthOnlyVertexShader : MaterialVertexShader
    {
    }

    class FHitProxyVertexShader : MaterialVertexShader
    {
    }

    class TDistortionMeshVertexShader : MaterialVertexShader
    {
    }

    class FFogVolumeApplyVertexShader : MaterialVertexShader
    {
    }

    class FVelocityPixelShader : MaterialPixelShader
    {
        public FShaderParameter VelocityScaleOffset;
        public FShaderParameter IndividualVelocityScale;
        public FShaderParameter ObjectVelocity;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FVelocityVertexShader : MaterialVertexShader
    {
        public FShaderParameter PrevViewProjectionMatrix;
        public FShaderParameter PreviousLocalToWorld;
        public FShaderParameter StretchTimeScale;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FTextureDensityPixelShader : MaterialPixelShader
    {
        public FShaderParameter TextureDensityParameters;
        public FShaderParameter TextureLookupInfo;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TShadowDepthPixelShader : MaterialPixelShader
    {
        public FShaderParameter InvMaxSubjectDepth;
        public FShaderParameter DepthBias;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHitProxyPixelShader : MaterialPixelShader
    {
        public FShaderParameter HitProxyId;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FFogVolumeApplyPixelShader : Shader
    {
        public FShaderParameter MaxIntegral;
        public FMaterialPixelShaderParameters MaterialParameters;
        public FShaderResourceParameter AccumulatedFrontfacesLineIntegralTexture;
        public FShaderResourceParameter AccumulatedBackfacesLineIntegralTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TFogIntegralPixelShader : MaterialPixelShader
    {
        public FShaderParameter DepthFilterSampleOffsets;
        public FShaderParameter ScreenToWorld;
        public FShaderParameter FogCameraPosition;
        public FShaderParameter FaceScale;
        public FShaderParameter FirstDensityFunctionParameters;
        public FShaderParameter SecondDensityFunctionParameters;
        public FShaderParameter StartDistance;
        public FShaderParameter InvMaxIntegral;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TFogIntegralVertexShader : MaterialVertexShader
    {
        public FHeightFogVertexShaderParameters HeightFogParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FRadialBlurPixelShader : Shader
    {
        public FShaderParameter RadialBlurScale;
        public FShaderParameter RadialBlurFalloffExp;
        public FShaderParameter RadialBlurOpacity;
        public FSceneTextureShaderParameters SceneTextureParameters;
        public FMaterialPixelShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHitMaskPixelShader : MaterialPixelShader
    {
        public FShaderParameter HitStartLocation;
        public FShaderParameter HitLocation;
        public FShaderParameter HitRadius;
        public FShaderParameter HitCullDistance;
        public FShaderResourceParameter CurrentMaskTexture;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FHitMaskVertexShader : MaterialVertexShader
    {
        public FShaderParameter PixelCenterOffset;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class TAOMeshVertexShader : Shader
    {
        public FVertexFactoryParameterRef VertexFactoryParameters;
        public FShaderParameter PrevViewProjectionMatrix;
        public FShaderParameter PreviousLocalToWorld;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }

    class FLightFunctionPixelShader : Shader
    {
        public FShaderResourceParameter SceneColorTexture;
        public FShaderResourceParameter SceneDepthTexture;
        public FShaderParameter MinZ_MaxZRatio;
        public FShaderParameter ScreenPositionScaleBias;
        public FShaderParameter ScreenToLight;
        public FMaterialPixelShaderParameters MaterialParameters;

        internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
        {
            var defferedOffsetWriter = base.Serialize(sc);
            throw new NotImplementedException();
            return defferedOffsetWriter;
        }
    }
}