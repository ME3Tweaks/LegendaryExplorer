using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Shaders;

/// <summary>
/// Binds D3D-reflected allocations to LE3's serialized shader parameter structures without a game
/// process. This is the shared material-binding layer, not a complete shader-type constructor.
/// Policy-specific constructors must bind their own parameters and check UnboundParameters.
/// </summary>
public sealed class LE3ShaderParameterBinder
{
    private readonly ShaderParameterBinding[] allocations;
    private readonly Dictionary<string, ShaderParameterBinding> byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> boundNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// CRC of the original unbound allocation map in reflection/insertion order. Native FShader
    /// computes this before derived constructors bind parameters; subsequent binds do not change it.
    /// </summary>
    public uint ParameterMapCRC { get; }

    public IReadOnlyList<ShaderParameterBinding> UnboundParameters => Array.AsReadOnly(
        allocations.Where(p => !boundNames.Contains(p.Name)).ToArray());

    public LE3ShaderParameterBinder(IReadOnlyList<ShaderParameterBinding> parameterBindings)
    {
        ArgumentNullException.ThrowIfNull(parameterBindings);
        allocations = parameterBindings.ToArray();
        uint crc = 0;
        foreach (var allocation in allocations)
        {
            if (allocation == null || string.IsNullOrEmpty(allocation.Name) || allocation.Name.Contains('\0'))
                throw new ArgumentException("Parameter allocations require nonempty, NUL-free names.", nameof(parameterBindings));
            if (!Enum.IsDefined(allocation.Kind))
                throw new ArgumentException("Unknown parameter allocation kind.", nameof(parameterBindings));
            if (!byName.TryAdd(allocation.Name, allocation))
                throw new ArgumentException($"Duplicate LE3 parameter allocation: {allocation.Name}", nameof(parameterBindings));

            // FShaderKey::ConstructorWithCodeAndMap 0x7ff74ac09430: FString UTF-16LE without
            // terminator, then the 12-byte FParameterAllocation (four WORDs plus FALSE BOOL).
            // Preserve raw WCHAR code units, including unpaired surrogates; Encoding's fallback
            // would change the native CRC for such an FString.
            byte[] nameBytes = new byte[checked(allocation.Name.Length * 2)];
            for (int i = 0; i < allocation.Name.Length; i++)
                BinaryPrimitives.WriteUInt16LittleEndian(nameBytes.AsSpan(i * 2), allocation.Name[i]);
            crc = Crc32.Compute(nameBytes, 0, nameBytes.Length, crc);
            byte[] nativeAllocation = new byte[12];
            BinaryPrimitives.WriteUInt16LittleEndian(nativeAllocation.AsSpan(0), allocation.BufferIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(nativeAllocation.AsSpan(2), allocation.BaseIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(nativeAllocation.AsSpan(4), allocation.Size);
            BinaryPrimitives.WriteUInt16LittleEndian(nativeAllocation.AsSpan(6), allocation.SamplerIndex ??
                (allocation.Kind == ShaderParameterBindingKind.Constant ? (ushort)0 : ushort.MaxValue));
            crc = Crc32.Compute(nativeAllocation, 0, nativeAllocation.Length, crc);
        }
        ParameterMapCRC = crc;
    }

    /// <summary>
    /// Exact LE3 shared view-buffer layouts, owned by RHISetViewParameters (0x7ff74b23eda0),
    /// not serialized material shader fields. Installed Common.usf supplies the matching names.
    /// A familiar name in another buffer/offset/size or stage is not an exemption.
    /// </summary>
    public static bool IsRendererOwnedViewParameter(ShaderParameterBinding allocation, ShaderStage stage)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if (allocation.Kind != ShaderParameterBindingKind.Constant || allocation.SamplerIndex is not (null or 0)) return false;
        if (stage == ShaderStage.Vertex && allocation.BufferIndex == 1)
        {
            return Matches("ViewProjectionMatrix", 0, 64) || Matches("CameraPosition", 64, 16) || Matches("PreViewTranslation", 80, 16);
        }
        if (stage == ShaderStage.Pixel && allocation.BufferIndex == 2)
        {
            return Matches("ScreenPositionScaleBias", 0, 16) || Matches("MinZ_MaxZRatio", 16, 16) || Matches("DynamicScale", 32, 16);
        }
        return false;

        bool Matches(string name, ushort offset, ushort size) => allocation.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            allocation.BaseIndex == offset && allocation.Size == size;
    }

    public FShaderParameter BindConstant(string name, bool optional = true)
    {
        var allocation = Find(name, ShaderParameterBindingKind.Constant, optional);
        return allocation == null ? default : new FShaderParameter
        {
            BufferIndex = allocation.BufferIndex, BaseIndex = allocation.BaseIndex, NumBytes = allocation.Size
        };
    }

    /// <summary>Shared LE3 constant/linear-halfspace/sphere density Bind (0x7ff74aacc3e0).</summary>
    public FConstantDensityPolicy.VertexShaderParametersType BindFogDensityParameters() => new()
    {
        FirstDensityFunction = BindConstant("FirstDensityFunctionParameters"),
        SecondDensityFunction = BindConstant("SecondDensityFunctionParameters"),
        StartDistance = BindConstant("StartDistance"), FogVolumeBoxMin = BindConstant("FogVolumeBoxMin"),
        FogVolumeBoxMax = BindConstant("FogVolumeBoxMax"), ApproxFogColor = BindConstant("ApproxFogColor")
    };

    public FDirectionalLightPolicy.VertexParametersType BindDirectionalLightVertexParameters() => new()
        { LightDirection = BindConstant("LightDirection", optional: false) };

    public FPointLightPolicy.VertexParametersType BindPointLightVertexParameters() => new()
        { LightPositionAndInvRadius = BindConstant("LightPositionAndInvRadius", optional: false) };

    public FSpotLightPolicy.VertexParametersType BindSpotLightVertexParameters() => new()
        { LightPositionAndInvRadius = BindConstant("LightPositionAndInvRadius", optional: false) };

    /// <summary>Native directional PS policy Bind 0x7ff74aa8ed70; all optional.</summary>
    public FDirectionalLightPolicy.PixelParametersType BindDirectionalLightPixelParameters() => new()
    {
        LightColor = BindConstant("LightColor"), bReceiveDynamicShadows = BindConstant("bReceiveDynamicShadows"),
        bEnableDistanceShadowFading = BindConstant("bEnableDistanceShadowFading"), DistanceFadeParameters = BindConstant("DistanceFadeParameters")
    };

    public FPointLightPolicy.PixelParametersType BindPointLightPixelParameters() => new()
        { LightColorAndFalloffExponent = BindConstant("LightColorAndFalloffExponent") };

    public FSpotLightPolicy.PixelParametersType BindSpotLightPixelParameters() => new()
    {
        SpotAngles = BindConstant("SpotAngles"), SpotDirection = BindConstant("SpotDirection"),
        LightColorAndFalloffExponent = BindConstant("LightColorAndFalloffExponent")
    };

    public FShadowTexturePolicy.VertexParametersType BindShadowTextureVertexParameters() => new()
        { LightmapCoordinateScaleBias = BindConstant("LightmapCoordinateScaleBias") };

    public FShadowTexturePolicy.PixelParametersType BindShadowTexturePixelParameters() => new()
        { ShadowTexture = BindResource("ShadowTexture") };

    public FSignedDistanceFieldShadowTexturePolicy.PixelParametersType BindSignedDistanceFieldShadowPixelParameters() => new()
        { DistanceFieldParameters = BindConstant("DistanceFieldParameters"), ShadowTexture = BindResource("ShadowTexture") };

    public FShaderResourceParameter BindResource(string name, bool optional = true)
    {
        var allocation = Find(name, ShaderParameterBindingKind.Resource, optional);
        return allocation == null ? default : new FShaderResourceParameter
        {
            BaseIndex = allocation.BaseIndex, NumResources = allocation.Size,
            SamplerIndex = allocation.SamplerIndex ?? ushort.MaxValue
        };
    }

    private ShaderParameterBinding Find(string name, ShaderParameterBindingKind kind, bool optional)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!byName.TryGetValue(name, out var allocation))
        {
            if (!optional) throw new InvalidOperationException($"Missing required LE3 shader parameter: {name}");
            return null;
        }
        if (allocation.Kind != kind)
            throw new InvalidOperationException($"LE3 shader parameter {name} has allocation kind {allocation.Kind}, expected {kind}.");
        boundNames.Add(allocation.Name);
        return allocation;
    }

    /// <summary>Native FMaterialVertexShaderParameters::Bind, 0x7ff74ab36a90.</summary>
    public FMaterialVertexShaderParameters BindMaterialVertexParameters(TranslationResult translation)
    {
        RequireTranslation(translation);
        return new FMaterialVertexShaderParameters
        {
            CameraWorldPosition = BindConstant("CameraWorldPos"),
            ObjectWorldPositionAndRadius = BindConstant("ObjectWorldPositionAndRadius"),
            ObjectOrientation = BindConstant("ObjectOrientation"),
            WindDirectionAndSpeed = BindConstant("WindDirectionAndSpeed"),
            FoliageImpulseDirection = BindConstant("FoliageImpulseDirection"),
            FoliageNormalizedRotationAxisAndAngle = BindConstant("FoliageNormalizedRotationAxisAndAngle"),
            UniformVertexScalarShaderParameters = BindUniformConstants("UniformVertexScalars_", translation.UniformVertexScalarExpressions.Count, true),
            UniformVertexVectorShaderParameters = BindUniformConstants("UniformVertexVector_", translation.UniformVertexVectorExpressions.Count, false)
        };
    }

    /// <summary>Native FMaterialPixelShaderParameters::Bind, 0x7ff74ab363d0.</summary>
    public FMaterialPixelShaderParameters BindMaterialPixelParameters(TranslationResult translation)
    {
        RequireTranslation(translation);
        var parameters = new FMaterialPixelShaderParameters
        {
            CameraWorldPosition = BindConstant("CameraWorldPos"),
            ObjectWorldPositionAndRadius = BindConstant("ObjectWorldPositionAndRadius"),
            ObjectOrientation = BindConstant("ObjectOrientation"),
            WindDirectionAndSpeed = BindConstant("WindDirectionAndSpeed"),
            FoliageImpulseDirection = BindConstant("FoliageImpulseDirection"),
            FoliageNormalizedRotationAxisAndAngle = BindConstant("FoliageNormalizedRotationAxisAndAngle"),
            UniformPixelScalarShaderParameters = BindUniformConstants("UniformPixelScalars_", translation.UniformPixelScalarExpressions.Count, true),
            UniformPixelVectorShaderParameters = BindUniformConstants("UniformPixelVector_", translation.UniformPixelVectorExpressions.Count, false),
            UniformPixel2DShaderResourceParameters = BindUniformResources("Texture2D_", translation.Uniform2DTextureExpressions.Count),
            UniformPixelCubeShaderResourceParameters = BindUniformResources("TextureCube_", translation.UniformCubeTextureExpressions.Count),
            LocalToWorld = BindConstant("LocalToWorldMatrix"),
            WorldToLocal = BindConstant("WorldToLocalMatrix"),
            WorldToView = BindConstant("WorldToViewMatrix"),
            InvViewProjection = BindConstant("InvViewProjectionMatrix"),
            ViewProjection = BindConstant("ViewProjectionMatrix"),
            SceneTextureParameters = BindSceneTextureParameters(),
            TwoSidedSign = BindConstant("TwoSidedSign"),
            InvGamma = BindConstant("MatInverseGamma"),
            DecalFarPlaneDistance = BindConstant("DecalFarPlaneDistance"),
            ObjectPostProjectionPosition = BindConstant("ObjectPostProjectionPosition"),
            ObjectMacroUVScales = BindConstant("ObjectMacroUVScales"),
            ObjectNDCPosition = BindConstant("ObjectNDCPosition"),
            OcclusionPercentage = BindConstant("OcclusionPercentage"),
            EnableScreenDoorFade = BindConstant("bEnableScreenDoorFade"),
            ScreenDoorFadeSettings = BindConstant("ScreenDoorFadeSettings"),
            ScreenDoorFadeSettings2 = BindConstant("ScreenDoorFadeSettings2"),
            ScreenDoorNoiseTexture = BindResource("ScreenDoorNoiseTexture"),
            WrapLightingParameters = BindConstant("WrapLightingParameters")
        };
        parameters.RecomputeLE3UniformParameterValidity();
        return parameters;
    }

    public FSceneTextureShaderParameters BindSceneTextureParameters() => new()
    {
        SceneColorTexture = BindResource("SceneColorTexture"), SceneDepthTexture = BindResource("SceneDepthTexture"),
        MinZ_MaxZRatio = BindConstant("MinZ_MaxZRatio"), ScreenPositionScaleBias = BindConstant("ScreenPositionScaleBias")
    };

    public FLocalVertexFactoryShaderParameters BindLocalVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld"),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal")
    };

    /// <summary>Exact recovered factory dispatch; unsupported factories cannot fall back to Local.</summary>
    public FVertexFactoryShaderParameters BindVertexFactoryParameters(string vertexFactoryTypeName)
    {
        ArgumentNullException.ThrowIfNull(vertexFactoryTypeName);
        return vertexFactoryTypeName switch
        {
            "FLocalVertexFactory" => BindLocalVertexFactoryParameters(),
            "FLocalVertexFactoryApex" => BindLocalVertexFactoryApexParameters(),
            "FGPUSkinVertexFactoryApex" => BindGPUSkinVertexFactoryApexParameters(),
            "FGPUSkinVertexFactory" or "FGPUSkinMorphVertexFactory" => BindGPUSkinVertexFactoryParameters(),
            "FInstancedStaticMeshVertexFactory" => BindInstancedStaticMeshVertexFactoryParameters(),
            "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" => BindGPUSkinDecalVertexFactoryParameters(),
            "FLocalDecalVertexFactory" => BindLocalDecalVertexFactoryParameters(),
            "FSplineMeshVertexFactory" => BindSplineMeshVertexFactoryParameters(),
            "FParticleInstancedMeshVertexFactory" => BindParticleInstancedMeshVertexFactoryParameters(),
            "FLensFlareVertexFactory" => BindLensFlareVertexFactoryParameters(),
            "FFoliageVertexFactory" => BindFoliageVertexFactoryParameters(),
            "FFluidVertexFactory" or "FFluidTessellationVertexFactory" => BindFluidVertexFactoryParameters(),
            "FTerrainVertexFactory" or "FTerrainMorphVertexFactory" or "FTerrainFullMorphVertexFactory" => BindTerrainVertexFactoryParameters(false),
            "FTerrainDecalVertexFactory" or "FTerrainMorphDecalVertexFactory" or "FTerrainFullMorphDecalVertexFactory" => BindTerrainVertexFactoryParameters(true),
            "FParticleBeamTrailVertexFactory" or "FParticleBeamTrailDynamicParameterVertexFactory" => BindParticleBeamTrailVertexFactoryParameters(),
            "FParticleVertexFactory" or "FParticleDynamicParameterVertexFactory" or "FParticleSubUVVertexFactory" or
                "FParticleSubUVDynamicParameterVertexFactory" => BindParticleVertexFactoryParameters(),
            _ => throw new InvalidOperationException($"Unverified LE3 vertex-factory parameter layout: {vertexFactoryTypeName}")
        };
    }

    /// <summary>LE3 ab5bb30: Local archive layout, but LocalToWorld is required.</summary>
    public FLocalVertexFactoryShaderParameters BindLocalVertexFactoryApexParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld", optional: false),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal")
    };

    /// <summary>LE3 ab5baa0: Local/APEX prefix, required bone matrices and factory discriminator.</summary>
    public FGPUSkinVertexFactoryApexShaderParameters BindGPUSkinVertexFactoryApexParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld", optional: false),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal"),
        BoneMatrices = BindConstant("BoneMatrices", optional: false),
        ApexDummy = BindConstant("ApexDummy", optional: false)
    };

    /// <summary>LE3 aaf1a30: Local prefix, required view translation, optional instancing parameters.</summary>
    public FInstancedStaticMeshVertexFactoryShaderParameters BindInstancedStaticMeshVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld"),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal"),
        InstancedViewTranslation = BindConstant("InstancedViewTranslation", optional: false),
        InstancingParameters = BindConstant("InstancingParameters")
    };

    /// <summary>LE3 ab69290: three optional fields, with no Local/GPU prefix.</summary>
    public FParticleInstancedMeshVertexFactoryShaderParameters BindParticleInstancedMeshVertexFactoryParameters() => new()
    {
        InvNumVerticesPerInstance = BindConstant("InvNumVerticesPerInstance"),
        NumVerticesPerInstance = BindConstant("NumVerticesPerInstance"),
        InstancedPreViewTranslation = BindConstant("InstancedPreViewTranslation")
    };

    /// <summary>LE3 ab7d120: four required sprite transform/axis fields, shared by SubUV/dynamic variants.</summary>
    public FParticleVertexFactoryShaderParameters BindParticleVertexFactoryParameters() => new()
    {
        CameraWorldPosition = BindConstant("CameraWorldPosition"), CameraRight = BindConstant("CameraRight"),
        CameraUp = BindConstant("CameraUp"), ScreenAlignment = BindConstant("ScreenAlignment"),
        LocalToWorld = BindConstant("LocalToWorld", optional: false),
        AxisRotationVectorSourceIndex = BindConstant("AxisRotationVectorSourceIndex", optional: false),
        AxisRotationVectors = BindConstant("AxisRotationVectors", optional: false),
        ParticleUpRightResultScalars = BindConstant("ParticleUpRightResultScalars", optional: false),
        NormalsType = BindConstant("NormalsType"), NormalsSphereCenter = BindConstant("NormalsSphereCenter"),
        NormalsCylinderUnitDirection = BindConstant("NormalsCylinderUnitDirection")
    };

    /// <summary>LE3 ac39de0/ac39c60: required LocalToWorld, optional terrain/decal fields.
    /// The archive-only Unused slot has native NumBytes=0; use a canonical zero record, not a binding.</summary>
    public FTerrainVertexFactoryShaderParameters BindTerrainVertexFactoryParameters(bool decals)
    {
        FTerrainVertexFactoryShaderParameters result = decals ? new FTerrainDecalVertexFactoryShaderParameters() : new FTerrainVertexFactoryShaderParameters();
        result.LocalToWorld = BindConstant("LocalToWorld", optional: false);
        result.WorldToLocal = BindConstant("WorldToLocal"); result.LocalToView = BindConstant("LocalToView");
        result.TerrainLightmapCoordinateScaleBias = BindConstant("TerrainLightmapCoordinateScaleBias");
        result.TessellationInterpolation = BindConstant("TessellationInterpolation");
        result.InvMaxTesselationLevel_ZScale = BindConstant("InvMaxTesselationLevel_ZScale");
        result.InvTerrainSize_SectionBase = BindConstant("InvTerrainSize_SectionBase");
        result.TessellationDistanceScale = BindConstant("TessellationDistanceScale"); result.TessInterpDistanceValues = BindConstant("TessInterpDistanceValues");
        if (result is FTerrainDecalVertexFactoryShaderParameters decal)
        {
            decal.DecalMatrix = BindConstant("DecalMatrix"); decal.DecalLocation = BindConstant("DecalLocation");
            decal.DecalOffset = BindConstant("DecalOffset"); decal.DecalLocalBinormal = BindConstant("DecalLocalBinormal");
            decal.DecalLocalTangent = BindConstant("DecalLocalTangent");
        }
        return result;
    }

    /// <summary>LE3 aacc310: optional Local prefix, required GridSize, optional fluid fields.</summary>
    public FFluidTessellationVertexFactoryShaderParameters BindFluidVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld"), LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal"), GridSize = BindConstant("GridSize", optional: false),
        TessellationParameters = BindConstant("TessellationParameters"), TessellationFactors1 = BindConstant("TessellationFactors1"),
        TessellationFactors2 = BindConstant("TessellationFactors2"), Heightmap = BindResource("Heightmap"),
        TexcoordScaleBias = BindConstant("TexcoordScaleBias"), SplineParameters = BindConstant("SplineParameters")
    };

    /// <summary>LE3 aacc490: two optional instance constants, no Local prefix.</summary>
    public FFoliageVertexFactoryShaderParameters BindFoliageVertexFactoryParameters() => new()
    {
        InvNumVerticesPerInstance = BindConstant("InvNumVerticesPerInstance"),
        NumVerticesPerInstance = BindConstant("NumVerticesPerInstance")
    };

    /// <summary>LE3 aaf1a80: optional CameraRight/CameraUp, required LocalToWorld.</summary>
    public FLensFlareVertexFactoryShaderParameters BindLensFlareVertexFactoryParameters() => new()
    {
        CameraRight = BindConstant("CameraRight"), CameraUp = BindConstant("CameraUp"),
        LocalToWorld = BindConstant("LocalToWorld", optional: false)
    };

    /// <summary>LE3 ab691f0: four optional camera fields followed by required LocalToWorld.</summary>
    public FParticleBeamTrailVertexFactoryShaderParameters BindParticleBeamTrailVertexFactoryParameters() => new()
    {
        CameraWorldPosition = BindConstant("CameraWorldPosition"), CameraRight = BindConstant("CameraRight"),
        CameraUp = BindConstant("CameraUp"), ScreenAlignment = BindConstant("ScreenAlignment"),
        LocalToWorld = BindConstant("LocalToWorld", optional: false)
    };

    /// <summary>LE3 ac255f0: optional Local prefix followed by fourteen required spline fields.</summary>
    public FSplineMeshVertexFactoryShaderParameters BindSplineMeshVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld"),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal"),
        SplineStartPos = BindConstant("SplineStartPos", optional: false), SplineStartTangent = BindConstant("SplineStartTangent", optional: false),
        SplineStartRoll = BindConstant("SplineStartRoll", optional: false), SplineStartScale = BindConstant("SplineStartScale", optional: false),
        SplineStartOffset = BindConstant("SplineStartOffset", optional: false), SplineEndPos = BindConstant("SplineEndPos", optional: false),
        SplineEndTangent = BindConstant("SplineEndTangent", optional: false), SplineEndRoll = BindConstant("SplineEndRoll", optional: false),
        SplineEndScale = BindConstant("SplineEndScale", optional: false), SplineEndOffset = BindConstant("SplineEndOffset", optional: false),
        SplineXDir = BindConstant("SplineXDir", optional: false), SmoothInterpRollScale = BindConstant("SmoothInterpRollScale", optional: false),
        MeshMinZ = BindConstant("MeshMinZ", optional: false), MeshRangeZ = BindConstant("MeshRangeZ", optional: false)
    };

    /// <summary>LE3 ab046b0: all three Local and seven decal fields are optional.</summary>
    public FLocalDecalVertexFactoryShaderParameters BindLocalDecalVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld"),
        LocalToWorldRotDeterminantFlip = BindConstant("LocalToWorldRotDeterminantFlip"),
        WorldToLocal = BindConstant("WorldToLocal"),
        DecalMatrix = BindConstant("DecalMatrix"), DecalLocation = BindConstant("DecalLocation"),
        DecalOffset = BindConstant("DecalOffset"), DecalLocalBinormal = BindConstant("DecalLocalBinormal"),
        DecalLocalTangent = BindConstant("DecalLocalTangent"), DecalLocalNormal = BindConstant("DecalLocalNormal"),
        DecalBlendInterval = BindConstant("DecalBlendInterval")
    };

    /// <summary>LE3 aaf1820: the GPU prefix precedes three optional decal fields.</summary>
    public FGPUSkinDecalVertexFactoryShaderParameters BindGPUSkinDecalVertexFactoryParameters()
    {
        var gpu = BindGPUSkinVertexFactoryParameters();
        return new FGPUSkinDecalVertexFactoryShaderParameters
        {
            LocalToWorld = gpu.LocalToWorld, WorldToLocal = gpu.WorldToLocal, BoneMatrices = gpu.BoneMatrices,
            MaxBoneInfluences = gpu.MaxBoneInfluences, MeshOrigin = gpu.MeshOrigin, MeshExtension = gpu.MeshExtension,
            WoundEllipse0 = gpu.WoundEllipse0, WoundEllipse1 = gpu.WoundEllipse1,
            BoneToDecalRow0 = BindConstant("BoneToDecalRow0"), BoneToDecalRow1 = BindConstant("BoneToDecalRow1"),
            DecalLocation = BindConstant("DecalLocation")
        };
    }

    /// <summary>LE3 binder 0x7ff74aaf1950: LocalToWorld and BoneMatrices are non-optional.</summary>
    public FGPUSkinVertexFactoryShaderParameters BindGPUSkinVertexFactoryParameters() => new()
    {
        LocalToWorld = BindConstant("LocalToWorld", optional: false),
        WorldToLocal = BindConstant("WorldToLocal"),
        BoneMatrices = BindConstant("BoneMatrices", optional: false),
        MaxBoneInfluences = BindConstant("MaxBoneInfluences"), MeshOrigin = BindConstant("MeshOrigin"),
        MeshExtension = BindConstant("MeshExtension"), WoundEllipse0 = BindConstant("WoundEllipse0"),
        WoundEllipse1 = BindConstant("WoundEllipse1")
    };

    public FHeightFogVertexShaderParameters BindHeightFogParameters() => new()
    {
        FogMinHeight = BindConstant("FogMinHeight"), FogMaxHeight = BindConstant("FogMaxHeight"),
        FogDistanceScale = BindConstant("FogDistanceScale"), FogExtinctionDistance = BindConstant("FogExtinctionDistance"),
        FogInScattering = BindConstant("FogInScattering"), FogStartDistance = BindConstant("FogStartDistance")
    };

    private TUniformParameter<FShaderParameter>[] BindUniformConstants(string prefix, int expressionCount, bool packedScalars)
    {
        var result = new List<TUniformParameter<FShaderParameter>>();
        int groupCount = packedScalars ? (int)(((long)expressionCount + 3) / 4) : expressionCount;
        for (int i = 0; i < groupCount; i++)
        {
            var parameter = BindConstant(prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Native checks NumBytes, not IsBound(): shared buffers are retained too.
            if (parameter.NumBytes != 0) result.Add(new() { Index = i, Param = parameter });
        }
        return result.ToArray();
    }

    private TUniformParameter<FShaderResourceParameter>[] BindUniformResources(string prefix, int count)
    {
        var result = new List<TUniformParameter<FShaderResourceParameter>>();
        for (int i = 0; i < count; i++)
        {
            var parameter = BindResource(prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (parameter.NumResources != 0) result.Add(new() { Index = i, Param = parameter });
        }
        return result.ToArray();
    }

    private static void RequireTranslation(TranslationResult translation)
    {
        ArgumentNullException.ThrowIfNull(translation);
        if (translation.Errors == null || !translation.Success)
            throw new ArgumentException("LE3 parameter binding requires a successful translation.", nameof(translation));
    }
}
