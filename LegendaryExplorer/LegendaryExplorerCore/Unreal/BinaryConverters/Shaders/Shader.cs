using System;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

// ReSharper disable InconsistentNaming

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

public abstract class Shader
{
    public enum ShaderFrequency : byte
    {
        Vertex = 0,
        Pixel = 1,
        PixelUDK = 3 // This is a hack
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
    public ushort[] UDKSerializations; // UDK only
    public byte[] UDKSourceSHA; // UDK only - SHA of source code for shader (HLSL?)
    public byte[] UDKShaderSHA; // UDK only - SHA of shader (bytecode?) for shader. Maybe previous data or something.

    public override Shader Clone()
    {
        var newShader = (UnparsedShader)SharedClone();
        newShader.unkBytesPreName = unkBytesPreName?.ArrayClone();
        newShader.unkBytes = unkBytes?.ArrayClone();
        return newShader;
    }

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        if (sc.Game == MEGame.UDK)
        {
            sc.Serialize(ref UDKSourceSHA, 0x14);
        }

        bool preSerializesParams = ShaderType.Name is "FBinkYCrCbToRGBNoPixelAlphaPixelShader" or "FBinkYCrCbAToRGBAPixelShader";
        var defferedFileOffsetWriter = sc.SerializeDefferedFileOffset();
        if (sc.Game == MEGame.UDK)
        {
            sc.Serialize(ref UDKSerializations);
        }
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
        if (sc.Game == MEGame.UDK)
        {
            sc.Serialize(ref UDKShaderSHA, 0x14);
        }
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
                unkBytes = sc.ms.ReadToBuffer(defferedFileOffsetWriter.SerializedOffset - sc.FileOffset);
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
    public FShaderParameter HDRBrightnessScale; // LE2
    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);

        sc.SerializeUnmanaged(ref TextureParams); // TextureImage /2 /3 /4
        sc.SerializeUnmanaged(ref ConstantColor);
        sc.SerializeUnmanaged(ref ColorScale);
        sc.SerializeUnmanaged(ref ColorBias);
        sc.SerializeUnmanaged(ref InverseGamma);

        if (sc.Game == MEGame.LE2)
        {
            sc.SerializeUnmanaged(ref HDRBrightnessScale);
        }

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

public class THeightFogPixelShader : Shader
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

public class FBranchingPCFProjectionPixelShader : Shader
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

public class TBranchingPCFModProjectionPixelShader<LightMapPolicy> : FBranchingPCFProjectionPixelShader
    where LightMapPolicy : struct, IModShadowPixelParamsType
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

public class FGFxVertexShader : Shader
{
    public FShaderParameter Transform;
    public Fixed2<FShaderParameter> TextureMatrixParams;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);

        sc.SerializeUnmanaged(ref Transform);
        sc.SerializeUnmanaged(ref TextureMatrixParams);

        return defferedOffsetWriter;
    }
}

// Verified: LE2 - Uses FShader::Serialize
public class FResolveVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FReconstructHDRVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FLDRExtractVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FMotionBlurVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FBinkVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FOneColorVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FGammaCorrectionVertexShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FNULLPixelShader : Shader;

// Verified: LE2 - Uses FShader::Serialize
public class FHorizonBasedAOVertexShader : Shader;

public class FModShadowVolumeVertexShader : Shader;

public class FOcclusionQueryVertexShader : Shader;

public class FModShadowProjectionVertexShader : Shader;

public class FLUTBlenderVertexShader : Shader;

public class FPostProcessAAVertexShader : Shader;

public class FShadowProjectionVertexShader : Shader;

public class FScreenVertexShader : Shader;

public class FFluidVertexShader : Shader;

public class FEdgePreservingFilterVertexShader : Shader;

public class FLightFunctionVertexShader : Shader;

public class FResolveSingleSamplePixelShader : Shader
{
    public FShaderResourceParameter UnresolvedSurface;
    public FShaderParameter SingleSampleIndex;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);

        sc.SerializeUnmanaged(ref UnresolvedSurface);
        sc.SerializeUnmanaged(ref SingleSampleIndex);

        return defferedOffsetWriter;
    }
}

public class FResolveDepthPixelShader : Shader
{
    public FShaderResourceParameter UnresolvedSurface;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref UnresolvedSurface);
        return defferedOffsetWriter;
    }
}

public class TModShadowVolumePixelShader<LightMapPolicy> : Shader
    where LightMapPolicy : struct, IModShadowPixelParamsType
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter ShadowModulateColor;
    public FShaderParameter ScreenToWorld;
    public LightMapPolicy ModShadowPixelParams;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref ShadowModulateColor);
        sc.SerializeUnmanaged(ref ScreenToWorld);
        ModShadowPixelParams.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FGammaCorrectionPixelShader : Shader
{
    public FShaderResourceParameter SceneTexture;
    public FShaderParameter InverseGamma;
    public FShaderParameter ColorScale;
    public FShaderParameter OverlayColor;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTexture);
        sc.SerializeUnmanaged(ref InverseGamma);
        sc.SerializeUnmanaged(ref ColorScale);
        sc.SerializeUnmanaged(ref OverlayColor);
        return defferedOffsetWriter;
    }
}

public class FDownsampleSceneDepthPixelShader : Shader
{
    public FShaderParameter ProjectionScaleBias;
    public FShaderParameter SourceTexelOffsets01;
    public FShaderParameter SourceTexelOffsets23;
    public FSceneTextureShaderParameters SceneTextureParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ProjectionScaleBias);
        sc.SerializeUnmanaged(ref SourceTexelOffsets01);
        sc.SerializeUnmanaged(ref SourceTexelOffsets23);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        return defferedOffsetWriter;
    }
}

public class FFXAA3BlendPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter rcpFrame;
    public FShaderParameter rcpFrameOpt;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref rcpFrame);
        sc.SerializeUnmanaged(ref rcpFrameOpt);
        return defferedOffsetWriter;
    }
}

public class FTexturedCalibrationBoxHDRPixelShader : Shader
{
    public FShaderParameter CalibrationParameters;
    public FShaderResourceParameter SourceTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref CalibrationParameters);
        sc.SerializeUnmanaged(ref SourceTexture);
        return defferedOffsetWriter;
    }
}

public abstract class SingleTextureShader : Shader
{
    public FShaderResourceParameter Texture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Texture);
        return defferedOffsetWriter;
    }
}

public class FScreenPixelShader : SingleTextureShader;

public class FHBAOApplyPixelShader : SingleTextureShader;

public class FCopyVariancePixelShader : SingleTextureShader;

public class FSimpleElementHitProxyPixelShader : SingleTextureShader;

public class FMotionBlurPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FMotionBlurShaderParameters MotionBlurParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref MotionBlurParameters);
        return defferedOffsetWriter;
    }
}

public class FDownsampleDepthVertexShader : Shader
{
    public FShaderParameter HalfSceneColorTexelSize;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HalfSceneColorTexelSize);
        return defferedOffsetWriter;
    }
}

public class FAmbientOcclusionVertexShader : Shader
{
    public FShaderParameter ScreenToView;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ScreenToView);
        return defferedOffsetWriter;
    }
}

public class FCalibrationBoxHDRPixelShader : Shader
{
    public FShaderParameter CalibrationParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref CalibrationParameters);
        return defferedOffsetWriter;
    }
}

public class TFilterVertexShader : Shader
{
    public FShaderParameter SampleOffsets;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SampleOffsets);
        return defferedOffsetWriter;
    }
}

public class TDOFAndBloomGatherVertexShader : Shader
{
    public FShaderParameter SampleOffsets;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SampleOffsets);
        return defferedOffsetWriter;
    }
}

public class FShaderComplexityAccumulatePixelShader : Shader
{
    public FShaderParameter NormalizedComplexity;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref NormalizedComplexity);
        return defferedOffsetWriter;
    }
}

public class FDistortionApplyScreenVertexShader : Shader
{
    public FShaderParameter Transform;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Transform);
        return defferedOffsetWriter;
    }
}

public class FSimpleElementVertexShader : Shader
{
    public FShaderParameter Transform;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Transform);
        return defferedOffsetWriter;
    }
}

public class FDownsampleLightShaftsVertexShader : Shader
{
    public FShaderParameter ScreenToWorld;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ScreenToWorld);
        return defferedOffsetWriter;
    }
}

public class FRadialBlurVertexShader : Shader
{
    public FShaderParameter WorldCenterPos;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref WorldCenterPos);
        return defferedOffsetWriter;
    }
}

public class FOneColorPixelShader : Shader
{
    public FShaderParameter Color;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Color);
        return defferedOffsetWriter;
    }
}

public class FDOFAndBloomBlendVertexShader : Shader
{
    public FShaderParameter SceneCoordinateScaleBias;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneCoordinateScaleBias);
        return defferedOffsetWriter;
    }
}

public class FHistoryUpdateVertexShader : Shader
{
    public FShaderParameter ScreenToWorldOffset;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ScreenToWorldOffset);
        return defferedOffsetWriter;
    }
}

public class FReconstructHDRPixelShader : Shader
{
    public FShaderResourceParameter SourceTexture;
    public FShaderParameter HDRParameters;
    public FShaderParameter CalibrationParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SourceTexture);
        sc.SerializeUnmanaged(ref HDRParameters);
        sc.SerializeUnmanaged(ref CalibrationParameters);
        return defferedOffsetWriter;
    }
}

public class FSimpleElementPixelShader : Shader
{
    public FShaderResourceParameter Texture;
    public FShaderParameter TextureComponentReplicate;
    public FShaderParameter TextureComponentReplicateAlpha;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Texture);
        sc.SerializeUnmanaged(ref TextureComponentReplicate);
        sc.SerializeUnmanaged(ref TextureComponentReplicateAlpha);
        return defferedOffsetWriter;
    }
}

public class TFilterPixelShader : Shader
{
    public FShaderResourceParameter FilterTexture;
    public FShaderParameter SampleWeights;
    public FShaderParameter SampleMaskRect;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref FilterTexture);
        sc.SerializeUnmanaged(ref SampleWeights);
        sc.SerializeUnmanaged(ref SampleMaskRect);
        return defferedOffsetWriter;
    }
}

public class FShadowVolumeVertexShader : Shader
{
    public FShaderParameter LightPosition;
    public FShaderParameter BaseExtrusion;
    public FShaderParameter LocalToWorld;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightPosition);
        sc.SerializeUnmanaged(ref BaseExtrusion);
        sc.SerializeUnmanaged(ref LocalToWorld);
        return defferedOffsetWriter;
    }
}

public class FSFXUberPostProcessBlendPixelShader : FUberPostProcessBlendPixelShader
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
        sc.SerializeUnmanaged(ref NoiseTexture);
        sc.SerializeUnmanaged(ref NoiseTextureOffset);
        sc.SerializeUnmanaged(ref FilmGrain_Scale);
        sc.SerializeUnmanaged(ref smpFilmicLUT);
        sc.SerializeUnmanaged(ref ScreenUVScaleBias);
        sc.SerializeUnmanaged(ref HighPrecisionGamma);
        return defferedOffsetWriter;
    }
}

public class FUberPostProcessVertexShader : Shader
{
    public FShaderParameter SceneCoordinate1ScaleBias;
    public FShaderParameter SceneCoordinate2ScaleBias;
    public FShaderParameter SceneCoordinate3ScaleBias;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneCoordinate1ScaleBias);
        sc.SerializeUnmanaged(ref SceneCoordinate2ScaleBias);
        sc.SerializeUnmanaged(ref SceneCoordinate3ScaleBias);
        return defferedOffsetWriter;
    }
}

public class FUberHalfResPixelShader : FDOFAndBloomBlendPixelShader
{
    public FMotionBlurShaderParameters MotionBlurParameters;
    public FShaderResourceParameter LowResSceneBufferPoint;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref MotionBlurParameters);
        sc.SerializeUnmanaged(ref LowResSceneBufferPoint);
        return defferedOffsetWriter;
    }
}

public class FUberPostProcessBlendPixelShader : FDOFAndBloomBlendPixelShader
{
    public FColorRemapShaderParameters MaterialParameters;
    public FGammaShaderParameters GammaParameters;
    public FShaderResourceParameter LowResSceneBuffer;
    public FShaderParameter HalfResMaskRec;
    public FMotionBlurShaderParameters MotionBlurParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref MaterialParameters);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref LowResSceneBuffer);
        sc.SerializeUnmanaged(ref HalfResMaskRec);
        sc.SerializeUnmanaged(ref MotionBlurParameters);
        return defferedOffsetWriter;
    }
}

public class TAOApplyPixelShader : Shader
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
        sc.SerializeUnmanaged(ref AOParams);
        sc.SerializeUnmanaged(ref OcclusionColor);
        sc.SerializeUnmanaged(ref FogColor);
        sc.SerializeUnmanaged(ref TargetSize);
        sc.SerializeUnmanaged(ref InvEncodePower);
        sc.SerializeUnmanaged(ref FogTexture);
        return defferedOffsetWriter;
    }
}

public class TModShadowProjectionPixelShader<LightMapPolicy> : Shader
    where LightMapPolicy : struct, IModShadowPixelParamsType
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
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref ScreenToShadowMatrix);
        sc.SerializeUnmanaged(ref ShadowDepthTexture);
        sc.SerializeUnmanaged(ref ShadowDepthTextureComparisonSampler);
        sc.SerializeUnmanaged(ref SampleOffsets);
        sc.SerializeUnmanaged(ref ShadowBufferSize);
        sc.SerializeUnmanaged(ref ShadowFadeFraction);
        sc.SerializeUnmanaged(ref ShadowModulateColor);
        sc.SerializeUnmanaged(ref ScreenToWorld);
        sc.SerializeUnmanaged(ref bEmissiveAlphaMaskScale);
        sc.SerializeUnmanaged(ref bApplyEmissiveToShadowCoverage);
        ModShadowPixelParams.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FFluidApplyPixelShader : Shader
{
    public FShaderResourceParameter FluidHeightTexture;
    public FShaderResourceParameter FluidNormalTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref FluidHeightTexture);
        sc.SerializeUnmanaged(ref FluidNormalTexture);
        return defferedOffsetWriter;
    }
}

public class FXAAFilterComputeShader : Shader
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
        sc.SerializeUnmanaged(ref WorkQueue);
        sc.SerializeUnmanaged(ref Color);
        sc.SerializeUnmanaged(ref InColor);
        sc.SerializeUnmanaged(ref Luma);
        sc.SerializeUnmanaged(ref LinearSampler);
        sc.SerializeUnmanaged(ref RcpTextureSize);
        return defferedOffsetWriter;
    }
}

public class TShadowProjectionPixelShader : Shader
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
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref ScreenToShadowMatrix);
        sc.SerializeUnmanaged(ref ShadowDepthTexture);
        sc.SerializeUnmanaged(ref ShadowDepthTextureComparisonSampler);
        sc.SerializeUnmanaged(ref SampleOffsets);
        sc.SerializeUnmanaged(ref ShadowBufferSize);
        sc.SerializeUnmanaged(ref ShadowFadeFraction);
        return defferedOffsetWriter;
    }
}

public class FBlurLightShaftsPixelShader : Shader
{
    public FLightShaftPixelShaderParameters LightShaftParameters;
    public FShaderParameter BlurPassIndex;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightShaftParameters);
        sc.SerializeUnmanaged(ref BlurPassIndex);
        return defferedOffsetWriter;
    }
}

public class FFilterVSMComputeShader : Shader
{
    public FShaderResourceParameter DepthTexture;
    public FShaderResourceParameter VarianceTexture;
    public FShaderParameter SubRect;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref DepthTexture);
        sc.SerializeUnmanaged(ref VarianceTexture);
        sc.SerializeUnmanaged(ref SubRect);
        return defferedOffsetWriter;
    }
}

public class FDistortionApplyScreenPixelShader : Shader
{
    public FShaderResourceParameter AccumulatedDistortionTextureParam;
    public FShaderResourceParameter SceneColorTextureParam;
    public FShaderParameter SceneColorRect;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref AccumulatedDistortionTextureParam);
        sc.SerializeUnmanaged(ref SceneColorTextureParam);
        sc.SerializeUnmanaged(ref SceneColorRect);
        return defferedOffsetWriter;
    }
}

public class FSRGBMLAABlendPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderResourceParameter EdgeCountTexture;
    public FShaderParameter gRTSize;
    public FShaderParameter gLuminanceEquation;
    public FShaderParameter gInverseDisplayGamma;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref EdgeCountTexture);
        sc.SerializeUnmanaged(ref gRTSize);
        sc.SerializeUnmanaged(ref gLuminanceEquation);
        sc.SerializeUnmanaged(ref gInverseDisplayGamma);
        return defferedOffsetWriter;
    }
}

public class TBasePassVertexShader<LightMapPolicy, DensityPolicy> : Shader
    where LightMapPolicy : struct, IVertexParametersType
    where DensityPolicy : struct, IVertexShaderParametersType
{
    public LightMapPolicy LightMapVertexParams;
    public FVertexFactoryParameterRef VertexFactoryParameters;
    public FHeightFogVertexShaderParameters HeightFogParameters;
    public FMaterialVertexShaderParameters MaterialParameters;
    public DensityPolicy DensityVertexShaderParams;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        LightMapVertexParams.Serialize(sc);
        VertexFactoryParameters.Serialize(sc);
        VertexFactoryType = VertexFactoryParameters.VertexFactoryType;
        sc.SerializeUnmanaged(ref HeightFogParameters);
        MaterialParameters.Serialize(sc);
        DensityVertexShaderParams.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FShadowProjectionMaskPixelShader : Shader
{
    public FShaderParameter LightDirection;
    public FShaderParameter ScreenPositionScaleBias;
    public FShaderResourceParameter SceneNormalTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightDirection);
        sc.SerializeUnmanaged(ref ScreenPositionScaleBias);
        sc.SerializeUnmanaged(ref SceneNormalTexture);
        return defferedOffsetWriter;
    }
}

public class FShaderComplexityApplyPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter ShaderComplexityColors;
    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref ShaderComplexityColors);
        return defferedOffsetWriter;
    }
}

public class FCopyTranslucencyDepthPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        return defferedOffsetWriter;
    }
}

public class TDownsampleDepthPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        return defferedOffsetWriter;
    }
}

public class FApplyLightShaftsPixelShader : Shader
{
    public FLightShaftPixelShaderParameters LightShaftParameters;
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderResourceParameter SmallSceneColorTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightShaftParameters);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref SmallSceneColorTexture);
        return defferedOffsetWriter;
    }
}

public class FFXAAResolveComputeShader : Shader
{
    public FShaderResourceParameter WorkQueueH;
    public FShaderResourceParameter WorkQueueV;
    public FShaderResourceParameter IndirectParams;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref WorkQueueH);
        sc.SerializeUnmanaged(ref WorkQueueV);
        sc.SerializeUnmanaged(ref IndirectParams);
        return defferedOffsetWriter;
    }
}

public class FDownsampleSceneDepthAndNormalsPixelShader : Shader
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
        sc.SerializeUnmanaged(ref ProjectionScaleBias);
        sc.SerializeUnmanaged(ref SourceTexelOffsets01);
        sc.SerializeUnmanaged(ref SourceTexelOffsets23);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref FullSizedNormalsTexture);
        sc.SerializeUnmanaged(ref OffsetIndex);
        return defferedOffsetWriter;
    }
}

public class FFXAAPrepComputeShader : Shader
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
        sc.SerializeUnmanaged(ref HWork);
        sc.SerializeUnmanaged(ref VWork);
        sc.SerializeUnmanaged(ref Color);
        sc.SerializeUnmanaged(ref Luma);
        sc.SerializeUnmanaged(ref LinearSampler);
        sc.SerializeUnmanaged(ref RcpTextureSize);
        sc.SerializeUnmanaged(ref TextureOffset);
        sc.SerializeUnmanaged(ref ContrastThreshold);
        sc.SerializeUnmanaged(ref SubpixelRemoval);
        return defferedOffsetWriter;
    }
}

public class FSRGBMLAAEdgeDetectionPixelShader : Shader
{
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter gRTSize;
    public FShaderParameter gLuminanceEquation;
    public FShaderParameter gInverseDisplayGamma;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref gRTSize);
        sc.SerializeUnmanaged(ref gLuminanceEquation);
        sc.SerializeUnmanaged(ref gInverseDisplayGamma);
        return defferedOffsetWriter;
    }
}

public class THeightFogVertexShader : Shader
{
    public FShaderParameter ScreenPositionScaleBias;
    public FShaderParameter FogMinHeight;
    public FShaderParameter FogMaxHeight;
    public FShaderParameter ScreenToWorld;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ScreenPositionScaleBias);
        sc.SerializeUnmanaged(ref FogMinHeight);
        sc.SerializeUnmanaged(ref FogMaxHeight);
        sc.SerializeUnmanaged(ref ScreenToWorld);
        return defferedOffsetWriter;
    }
}

public class FApplyLightShaftsVertexShader : Shader
{
    public FShaderParameter SourceTextureScaleBias;
    public FShaderParameter SceneColorScaleBias;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref SourceTextureScaleBias);
        sc.SerializeUnmanaged(ref SceneColorScaleBias);
        return defferedOffsetWriter;
    }
}

public class TBasePassPixelShader<LightMapPolicy> : Shader
    where LightMapPolicy : struct, IPixelParametersType
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
        PixelParams.Serialize(sc);
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref AmbientColorAndSkyFactor);
        sc.SerializeUnmanaged(ref UpperSkyColor);
        sc.SerializeUnmanaged(ref LowerSkyColor);
        sc.SerializeUnmanaged(ref MotionBlurMask);
        sc.SerializeUnmanaged(ref CharacterMask);
        sc.SerializeUnmanaged(ref TranslucencyDepth);
        return defferedOffsetWriter;
    }
}

public class FLUTBlenderPixelShader_1 : Shader
{
    public Fixed1<FShaderResourceParameter> TextureParameter;
    public FShaderParameter Weights;
    public FGammaShaderParameters GammaParameters;
    public FColorRemapShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureParameter);
        sc.SerializeUnmanaged(ref Weights);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref MaterialParameters);
        return defferedOffsetWriter;
    }
}

public class FLUTBlenderPixelShader_2 : Shader
{
    public Fixed2<FShaderResourceParameter> TextureParameter;
    public FShaderParameter Weights;
    public FGammaShaderParameters GammaParameters;
    public FColorRemapShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureParameter);
        sc.SerializeUnmanaged(ref Weights);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref MaterialParameters);
        return defferedOffsetWriter;
    }
}

public class FLUTBlenderPixelShader_3 : Shader
{
    public Fixed3<FShaderResourceParameter> TextureParameter;
    public FShaderParameter Weights;
    public FGammaShaderParameters GammaParameters;
    public FColorRemapShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureParameter);
        sc.SerializeUnmanaged(ref Weights);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref MaterialParameters);
        return defferedOffsetWriter;
    }
}

public class FLUTBlenderPixelShader_4 : Shader
{
    public Fixed4<FShaderResourceParameter> TextureParameter;
    public FShaderParameter Weights;
    public FGammaShaderParameters GammaParameters;
    public FColorRemapShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureParameter);
        sc.SerializeUnmanaged(ref Weights);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref MaterialParameters);
        return defferedOffsetWriter;
    }
}

public class FLUTBlenderPixelShader_5 : Shader
{
    public Fixed5<FShaderResourceParameter> TextureParameter;
    public FShaderParameter Weights;
    public FGammaShaderParameters GammaParameters;
    public FColorRemapShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureParameter);
        sc.SerializeUnmanaged(ref Weights);
        sc.SerializeUnmanaged(ref GammaParameters);
        sc.SerializeUnmanaged(ref MaterialParameters);
        return defferedOffsetWriter;
    }
}

public class FFluidNormalPixelShader : Shader
{
    public FShaderParameter CellSize;
    public FShaderParameter HeightScale;
    public FShaderResourceParameter HeightTexture;
    public FShaderParameter SplineMargin;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref CellSize);
        sc.SerializeUnmanaged(ref HeightScale);
        sc.SerializeUnmanaged(ref HeightTexture);
        sc.SerializeUnmanaged(ref SplineMargin);
        return defferedOffsetWriter;
    }
}

public class TDownsampleLightShaftsPixelShader : Shader
{
    public FLightShaftPixelShaderParameters LightShaftParameters;
    public FShaderParameter SampleOffsets;
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderResourceParameter SmallSceneColorTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightShaftParameters);
        sc.SerializeUnmanaged(ref SampleOffsets);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref SmallSceneColorTexture);
        return defferedOffsetWriter;
    }
}

public class FModShadowMeshPixelShader : Shader
{
    public FMaterialPixelShaderParameters MaterialParameters;
    public FShaderParameter AttenAllowed;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref AttenAllowed);
        return defferedOffsetWriter;
    }
}

public class FFluidSimulatePixelShader : Shader
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
        sc.SerializeUnmanaged(ref CellSize);
        sc.SerializeUnmanaged(ref DampFactor);
        sc.SerializeUnmanaged(ref TravelSpeed);
        sc.SerializeUnmanaged(ref PreviousOffset1);
        sc.SerializeUnmanaged(ref PreviousOffset2);
        sc.SerializeUnmanaged(ref PreviousHeights1);
        sc.SerializeUnmanaged(ref PreviousHeights2);
        return defferedOffsetWriter;
    }
}

public class FApplyForcePixelShader : Shader
{
    public FShaderParameter ForcePosition;
    public FShaderParameter ForceRadius;
    public FShaderParameter ForceMagnitude;
    public FShaderResourceParameter PreviousHeights1;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ForcePosition);
        sc.SerializeUnmanaged(ref ForceRadius);
        sc.SerializeUnmanaged(ref ForceMagnitude);
        sc.SerializeUnmanaged(ref PreviousHeights1);
        return defferedOffsetWriter;
    }
}

public class FDOFAndBloomBlendPixelShader : Shader
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
        sc.SerializeUnmanaged(ref DOFParameters);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref BlurredImage);
        sc.SerializeUnmanaged(ref DOFBlurredNear);
        sc.SerializeUnmanaged(ref DOFBlurredFar);
        sc.SerializeUnmanaged(ref BlurredImageSeperateBloom);
        sc.SerializeUnmanaged(ref BloomTintAndScreenBlendThreshold);
        sc.SerializeUnmanaged(ref InputTextureSize);
        sc.SerializeUnmanaged(ref DOFKernelParams);
        return defferedOffsetWriter;
    }
}

public class TDOFBlurPixelShader : Shader
{
    public FDOFShaderParameters DOFParameters;
    public FShaderResourceParameter DOFTempTexture;
    public FShaderResourceParameter DOFTempTexture2;
    public FShaderParameter DOFKernelParams;
    public FShaderParameter BlurDirections;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref DOFParameters);
        sc.SerializeUnmanaged(ref DOFTempTexture);
        sc.SerializeUnmanaged(ref DOFTempTexture2);
        sc.SerializeUnmanaged(ref DOFKernelParams);
        sc.SerializeUnmanaged(ref BlurDirections);
        return defferedOffsetWriter;
    }
}

public class TDOFAndBloomGatherPixelShader : Shader
{
    public FDOFShaderParameters DOFParameters;
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FShaderParameter BloomScaleAndThreshold;
    public FShaderParameter SceneMultiplier;
    public FShaderResourceParameter SmallSceneColorTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref DOFParameters);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        sc.SerializeUnmanaged(ref BloomScaleAndThreshold);
        sc.SerializeUnmanaged(ref SceneMultiplier);
        sc.SerializeUnmanaged(ref SmallSceneColorTexture);
        return defferedOffsetWriter;
    }
}

public class TLightMapDensityPixelShader<LightMapTexturePolicy> : Shader
    where LightMapTexturePolicy : struct, IPixelParametersType
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
        PixelParams.Serialize(sc);
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref LightMapDensityParameters);
        sc.SerializeUnmanaged(ref BuiltLightingAndSelectedFlags);
        sc.SerializeUnmanaged(ref DensitySelectedColor);
        sc.SerializeUnmanaged(ref LightMapResolutionScale);
        sc.SerializeUnmanaged(ref LightMapDensityDisplayOptions);
        sc.SerializeUnmanaged(ref VertexMappedColor);
        sc.SerializeUnmanaged(ref GridTexture);
        return defferedOffsetWriter;
    }
}

public class TDOFGatherPixelShader : TDOFAndBloomGatherPixelShader
{
    public FShaderParameter InputTextureSize;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref InputTextureSize);
        return defferedOffsetWriter;
    }
}

public class FHBAOBlurComputeShader : Shader
{
    public FHBAOShaderParameters HBAOParameters;
    public FShaderResourceParameter AOTexture;
    public FShaderResourceParameter BlurOut;
    public FShaderParameter AOTexDimensions;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HBAOParameters);
        sc.SerializeUnmanaged(ref AOTexture);
        sc.SerializeUnmanaged(ref BlurOut);
        sc.SerializeUnmanaged(ref AOTexDimensions);
        return defferedOffsetWriter;
    }
}

public class FHBAODeinterleaveComputeShader : Shader
{
    public FHBAOShaderParameters HBAOParameters;
    public FShaderResourceParameter SceneDepthTexture;
    public FShaderResourceParameter DeinterleaveOut;
    public FShaderParameter ArrayOffset;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HBAOParameters);
        sc.SerializeUnmanaged(ref SceneDepthTexture);
        sc.SerializeUnmanaged(ref DeinterleaveOut);
        sc.SerializeUnmanaged(ref ArrayOffset);
        return defferedOffsetWriter;
    }
}

public class FFXAA3VertexShader : Shader
{
    public FShaderParameter rcpFrame;
    public FShaderParameter rcpFrameOpt;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref rcpFrame);
        sc.SerializeUnmanaged(ref rcpFrameOpt);
        return defferedOffsetWriter;
    }
}

public class FSimpleElementDistanceFieldGammaPixelShader : Shader
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
        sc.SerializeUnmanaged(ref SmoothWidth);
        sc.SerializeUnmanaged(ref EnableShadow);
        sc.SerializeUnmanaged(ref ShadowDirection);
        sc.SerializeUnmanaged(ref ShadowColor);
        sc.SerializeUnmanaged(ref ShadowSmoothWidth);
        sc.SerializeUnmanaged(ref EnableGlow);
        sc.SerializeUnmanaged(ref GlowColor);
        sc.SerializeUnmanaged(ref GlowOuterRadius);
        sc.SerializeUnmanaged(ref GlowInnerRadius);
        return defferedOffsetWriter;
    }
}

public class TShadowDepthVertexShader : Shader
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
        VertexFactoryParameters.Serialize(sc);
        VertexFactoryType = VertexFactoryParameters.VertexFactoryType;
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref ProjectionMatrix);
        sc.SerializeUnmanaged(ref InvMaxSubjectDepth);
        sc.SerializeUnmanaged(ref DepthBias);
        sc.SerializeUnmanaged(ref bClampToNearPlane);
        return defferedOffsetWriter;
    }
}

public class FSimpleElementMaskedGammaPixelShader : FSimpleElementGammaPixelShader
{
    public FShaderParameter ClipRef;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref ClipRef);
        return defferedOffsetWriter;
    }
}

public class FSimpleElementGammaPixelShader : FSimpleElementPixelShader
{
    public FShaderParameter Gamma;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref Gamma);
        return defferedOffsetWriter;
    }
}

public class FGenerateDeinterleavedHBAOComputeShader : Shader
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
        sc.SerializeUnmanaged(ref HBAOParameters);
        sc.SerializeUnmanaged(ref OutAO);
        sc.SerializeUnmanaged(ref QuarterResDepthCS);
        sc.SerializeUnmanaged(ref ViewNormalTex);
        sc.SerializeUnmanaged(ref JitterCS);
        sc.SerializeUnmanaged(ref ArrayOffset);
        return defferedOffsetWriter;
    }
}

public class FHBAOReconstructNormalsComputeShader : Shader
{
    public FHBAOShaderParameters HBAOParameters;
    public FShaderResourceParameter SceneDepthTexture;
    public FShaderResourceParameter ReconstructNormalOut;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HBAOParameters);
        sc.SerializeUnmanaged(ref SceneDepthTexture);
        sc.SerializeUnmanaged(ref ReconstructNormalOut);
        return defferedOffsetWriter;
    }
}

public class TAOMaskPixelShader : Shader
{
    public FAmbientOcclusionParams AOParams;
    public FShaderParameter HistoryConvergenceRates;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref AOParams);
        sc.SerializeUnmanaged(ref HistoryConvergenceRates);
        return defferedOffsetWriter;
    }
}

public class FStaticHistoryUpdatePixelShader : Shader
{
    public FAmbientOcclusionParams AOParams;
    public FShaderParameter PrevViewProjMatrix;
    public FShaderParameter HistoryConvergenceRates;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref AOParams);
        sc.SerializeUnmanaged(ref PrevViewProjMatrix);
        sc.SerializeUnmanaged(ref HistoryConvergenceRates);
        return defferedOffsetWriter;
    }
}

public class TEdgePreservingFilterPixelShader : Shader
{
    public FAmbientOcclusionParams AOParams;
    public FShaderParameter FilterSampleOffsets;
    public FShaderParameter FilterParameters;
    public FShaderParameter CustomParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref AOParams);
        sc.SerializeUnmanaged(ref FilterSampleOffsets);
        sc.SerializeUnmanaged(ref FilterParameters);
        sc.SerializeUnmanaged(ref CustomParameters);
        return defferedOffsetWriter;
    }
}

public class TAmbientOcclusionPixelShader : Shader
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
        sc.SerializeUnmanaged(ref OcclusionSampleOffsets);
        sc.SerializeUnmanaged(ref RandomNormalTexture);
        sc.SerializeUnmanaged(ref ProjectionScale);
        sc.SerializeUnmanaged(ref ProjectionMatrix);
        sc.SerializeUnmanaged(ref NoiseScale);
        sc.SerializeUnmanaged(ref AOParams);
        sc.SerializeUnmanaged(ref OcclusionCalcParameters);
        sc.SerializeUnmanaged(ref HaloDistanceScale);
        sc.SerializeUnmanaged(ref OcclusionRemapParameters);
        sc.SerializeUnmanaged(ref OcclusionFadeoutParameters);
        sc.SerializeUnmanaged(ref MaxRadiusTransform);
        return defferedOffsetWriter;
    }
}

public class FBinkGpuShaderYCrCbToRGB : Shader
{
    public FShaderResourceParameter YTex;
    public FShaderResourceParameter CrCbTex;
    public FShaderResourceParameter ATex;
    public FShaderParameter cmatrix;
    public FShaderParameter alpha_mult;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref YTex);
        sc.SerializeUnmanaged(ref CrCbTex);
        sc.SerializeUnmanaged(ref ATex);
        sc.SerializeUnmanaged(ref cmatrix);
        sc.SerializeUnmanaged(ref alpha_mult);
        return defferedOffsetWriter;
    }
}

public class FBinkGpuShaderHDR : Shader
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
        sc.SerializeUnmanaged(ref YTex);
        sc.SerializeUnmanaged(ref CrCbTex);
        sc.SerializeUnmanaged(ref ATex);
        sc.SerializeUnmanaged(ref HTex);
        sc.SerializeUnmanaged(ref alpha_mult);
        sc.SerializeUnmanaged(ref hdr);
        sc.SerializeUnmanaged(ref ctcp);
        return defferedOffsetWriter;
    }
}

public class FBinkYCrCbAToRGBAPixelShader : Shader
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

public class FBinkYCrCbToRGBNoPixelAlphaPixelShader : Shader
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

public class TLightPixelShader<LightTypePolicy, ShadowingTypePolicy> : Shader
    where LightTypePolicy : struct, IPixelParametersType
    where ShadowingTypePolicy : struct, IPixelParametersType
{
    public LightTypePolicy LightTypePixelParams;
    public ShadowingTypePolicy ShadowingPixelParams;
    public FMaterialPixelShaderParameters MaterialParameters;
    public FShaderResourceParameter LightAttenuationTexture;
    public FShaderParameter bReceiveDynamicShadows;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        LightTypePixelParams.Serialize(sc);
        ShadowingPixelParams.Serialize(sc);
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref LightAttenuationTexture);
        sc.SerializeUnmanaged(ref bReceiveDynamicShadows);
        return defferedOffsetWriter;
    }
}

public class TLightMapDensityVertexShader<LightMapTexturePolicy> : TLightVertexShader<LightMapTexturePolicy, FNullPolicy>
    where LightMapTexturePolicy : struct, IVertexParametersType;

public class TLightVertexShader<LightTypePolicy, ShadowingTypePolicy> : Shader
    where LightTypePolicy : struct, IVertexParametersType
    where ShadowingTypePolicy : struct, IVertexParametersType
{
    public LightTypePolicy LightTypeVertexParams;
    public ShadowingTypePolicy ShadowingVertexParams;
    public FVertexFactoryParameterRef VertexFactoryParameters;
    public FMaterialVertexShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        LightTypeVertexParams.Serialize(sc);
        ShadowingVertexParams.Serialize(sc);
        VertexFactoryParameters.Serialize(sc);
        VertexFactoryType = VertexFactoryParameters.VertexFactoryType;
        MaterialParameters.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FModShadowMeshVertexShader : MaterialVertexShader
{
    public FShaderParameter LightPosition;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref LightPosition);
        return defferedOffsetWriter;
    }
}

public abstract class MaterialPixelShader : Shader
{
    public FMaterialPixelShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        MaterialParameters.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FSFXWorldNormalPixelShader : MaterialPixelShader;

public class TDepthOnlyScreenDoorPixelShader : MaterialPixelShader;

public class FTranslucencyPostRenderDepthPixelShader : MaterialPixelShader;

public class TDistortionMeshPixelShader : MaterialPixelShader;

public class TDepthOnlySolidPixelShader : MaterialPixelShader;

public abstract class MaterialVertexShader : Shader
{
    public FVertexFactoryParameterRef VertexFactoryParameters;
    public FMaterialVertexShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        VertexFactoryParameters.Serialize(sc);
        VertexFactoryType = VertexFactoryParameters.VertexFactoryType;
        MaterialParameters.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FSFXWorldNormalVertexShader : MaterialVertexShader;

public class FTextureDensityVertexShader : MaterialVertexShader;

public class TDepthOnlyVertexShader : MaterialVertexShader;

public class FHitProxyVertexShader : MaterialVertexShader;

public class TDistortionMeshVertexShader : MaterialVertexShader;

public class FFogVolumeApplyVertexShader : MaterialVertexShader;

public class FVelocityPixelShader : MaterialPixelShader
{
    public FShaderParameter VelocityScaleOffset;
    public FShaderParameter IndividualVelocityScale;
    public FShaderParameter ObjectVelocity;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref VelocityScaleOffset);
        sc.SerializeUnmanaged(ref IndividualVelocityScale);
        sc.SerializeUnmanaged(ref ObjectVelocity);
        return defferedOffsetWriter;
    }
}

public class FVelocityVertexShader : MaterialVertexShader
{
    public FShaderParameter PrevViewProjectionMatrix;
    public FShaderParameter PreviousLocalToWorld;
    public FShaderParameter StretchTimeScale;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref PrevViewProjectionMatrix);
        sc.SerializeUnmanaged(ref PreviousLocalToWorld);
        sc.SerializeUnmanaged(ref StretchTimeScale);
        return defferedOffsetWriter;
    }
}

public class FTextureDensityPixelShader : MaterialPixelShader
{
    public FShaderParameter TextureDensityParameters;
    public FShaderParameter TextureLookupInfo;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref TextureDensityParameters);
        sc.SerializeUnmanaged(ref TextureLookupInfo);
        return defferedOffsetWriter;
    }
}

public class TShadowDepthPixelShader : MaterialPixelShader
{
    public FShaderParameter InvMaxSubjectDepth;
    public FShaderParameter DepthBias;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref InvMaxSubjectDepth);
        sc.SerializeUnmanaged(ref DepthBias);
        return defferedOffsetWriter;
    }
}

public class FHitProxyPixelShader : MaterialPixelShader
{
    public FShaderParameter HitProxyId;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HitProxyId);
        return defferedOffsetWriter;
    }
}

public class FFogVolumeApplyPixelShader : Shader
{
    public FShaderParameter MaxIntegral;
    public FMaterialPixelShaderParameters MaterialParameters;
    public FShaderResourceParameter AccumulatedFrontfacesLineIntegralTexture;
    public FShaderResourceParameter AccumulatedBackfacesLineIntegralTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref MaxIntegral);
        MaterialParameters.Serialize(sc);
        sc.SerializeUnmanaged(ref AccumulatedFrontfacesLineIntegralTexture);
        sc.SerializeUnmanaged(ref AccumulatedBackfacesLineIntegralTexture);
        return defferedOffsetWriter;
    }
}

public class TFogIntegralPixelShader : MaterialPixelShader
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
        sc.SerializeUnmanaged(ref DepthFilterSampleOffsets);
        sc.SerializeUnmanaged(ref ScreenToWorld);
        sc.SerializeUnmanaged(ref FogCameraPosition);
        sc.SerializeUnmanaged(ref FaceScale);
        sc.SerializeUnmanaged(ref FirstDensityFunctionParameters);
        sc.SerializeUnmanaged(ref SecondDensityFunctionParameters);
        sc.SerializeUnmanaged(ref StartDistance);
        sc.SerializeUnmanaged(ref InvMaxIntegral);
        return defferedOffsetWriter;
    }
}

public class TFogIntegralVertexShader : MaterialVertexShader
{
    public FHeightFogVertexShaderParameters HeightFogParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HeightFogParameters);
        return defferedOffsetWriter;
    }
}

public class FRadialBlurPixelShader : Shader
{
    public FShaderParameter RadialBlurScale;
    public FShaderParameter RadialBlurFalloffExp;
    public FShaderParameter RadialBlurOpacity;
    public FSceneTextureShaderParameters SceneTextureParameters;
    public FMaterialPixelShaderParameters MaterialParameters;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref RadialBlurScale);
        sc.SerializeUnmanaged(ref RadialBlurFalloffExp);
        sc.SerializeUnmanaged(ref RadialBlurOpacity);
        sc.SerializeUnmanaged(ref SceneTextureParameters);
        MaterialParameters.Serialize(sc);
        return defferedOffsetWriter;
    }
}

public class FHitMaskPixelShader : MaterialPixelShader
{
    public FShaderParameter HitStartLocation;
    public FShaderParameter HitLocation;
    public FShaderParameter HitRadius;
    public FShaderParameter HitCullDistance;
    public FShaderResourceParameter CurrentMaskTexture;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref HitStartLocation);
        sc.SerializeUnmanaged(ref HitLocation);
        sc.SerializeUnmanaged(ref HitRadius);
        sc.SerializeUnmanaged(ref HitCullDistance);
        sc.SerializeUnmanaged(ref CurrentMaskTexture);
        return defferedOffsetWriter;
    }
}

public class FHitMaskVertexShader : MaterialVertexShader
{
    public FShaderParameter PixelCenterOffset;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        sc.SerializeUnmanaged(ref PixelCenterOffset);
        return defferedOffsetWriter;
    }
}

public class TAOMeshVertexShader : Shader
{
    public FVertexFactoryParameterRef VertexFactoryParameters;
    public FShaderParameter PrevViewProjectionMatrix;
    public FShaderParameter PreviousLocalToWorld;

    internal override DefferedFileOffsetWriter Serialize(SerializingContainer sc)
    {
        var defferedOffsetWriter = base.Serialize(sc);
        VertexFactoryParameters.Serialize(sc);
        VertexFactoryType = VertexFactoryParameters.VertexFactoryType;
        sc.SerializeUnmanaged(ref PrevViewProjectionMatrix);
        sc.SerializeUnmanaged(ref PreviousLocalToWorld);
        return defferedOffsetWriter;
    }
}

public class FLightFunctionPixelShader : Shader
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
        sc.SerializeUnmanaged(ref SceneColorTexture);
        sc.SerializeUnmanaged(ref SceneDepthTexture);
        sc.SerializeUnmanaged(ref MinZ_MaxZRatio);
        sc.SerializeUnmanaged(ref ScreenPositionScaleBias);
        sc.SerializeUnmanaged(ref ScreenToLight);
        MaterialParameters.Serialize(sc);
        return defferedOffsetWriter;
    }
}