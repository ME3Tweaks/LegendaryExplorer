using System.IO;
using LegendaryExplorerCore.Packages;

// ReSharper disable InconsistentNaming

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

public struct FVertexFactoryParameterRef
{
    public NameReference VertexFactoryType;
    public FVertexFactoryShaderParameters Parameters;

    public void Serialize(SerializingContainer sc)
    {
        sc.Serialize(ref VertexFactoryType);
        var offsetWriter = sc.SerializeDefferedFileOffset();
        FVertexFactoryShaderParameters.Serialize(sc, VertexFactoryType.Name, ref Parameters);
        offsetWriter.SetPosition(sc);
    }
}

public abstract class FVertexFactoryShaderParameters
{
    protected abstract void Serialize(SerializingContainer sc);

    internal static void Serialize(SerializingContainer sc, string vertexFactoryType, ref FVertexFactoryShaderParameters sp)
    {
        if (sc.IsLoading)
        {
            sp = vertexFactoryType switch
            {
                "FLocalVertexFactory" or "FLocalVertexFactoryApex" => new FLocalVertexFactoryShaderParameters(),
                "FFluidTessellationVertexFactory" => new FFluidTessellationVertexFactoryShaderParameters(),
                "FFoliageVertexFactory" => new FFoliageVertexFactoryShaderParameters(),
                "FGPUSkinMorphDecalVertexFactory" or "FGPUSkinDecalVertexFactory" => new FGPUSkinDecalVertexFactoryShaderParameters(),
                "FGPUSkinVertexFactory" or "FGPUSkinMorphVertexFactory" => new FGPUSkinVertexFactoryShaderParameters(),
                "FInstancedStaticMeshVertexFactory" => new FInstancedStaticMeshVertexFactoryShaderParameters(),
                "FLensFlareVertexFactory" => new FLensFlareVertexFactoryShaderParameters(),
                "FLocalDecalVertexFactory" => new FLocalDecalVertexFactoryShaderParameters(),
                "FGPUSkinVertexFactoryApex" => new FGPUSkinVertexFactoryApexShaderParameters(),
                "FParticleBeamTrailVertexFactory" or "FParticleBeamTrailDynamicParameterVertexFactory" => new FParticleBeamTrailVertexFactoryShaderParameters(),
                "FParticleInstancedMeshVertexFactory" => new FParticleInstancedMeshVertexFactoryShaderParameters(),
                "FParticleVertexFactory" or "FParticleSubUVVertexFactory" or "FParticleDynamicParameterVertexFactory" or "FParticleSubUVDynamicParameterVertexFactory" => new FParticleVertexFactoryShaderParameters(),
                "FSplineMeshVertexFactory" => new FSplineMeshVertexFactoryShaderParameters(),
                "FTerrainFullMorphDecalVertexFactory" or "FTerrainMorphDecalVertexFactory" or "FTerrainDecalVertexFactory" => new FTerrainDecalVertexFactoryShaderParameters(),
                "FTerrainFullMorphVertexFactory" or "FTerrainMorphVertexFactory" or "FTerrainVertexFactory" => new FTerrainVertexFactoryShaderParameters(),
                _ => throw new InvalidDataException($"Unexpected VertexFactory type: '{vertexFactoryType}'")
            };
        }
        sp.Serialize(sc);
    }
}

public class FLocalVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter LocalToWorld;
    public FShaderParameter LocalToWorldRotDeterminantFlip;
    public FShaderParameter WorldToLocal;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref LocalToWorld);
        sc.SerializeUnmanaged(ref LocalToWorldRotDeterminantFlip);
        sc.SerializeUnmanaged(ref WorldToLocal);
    }
}

public class FFluidTessellationVertexFactoryShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter GridSize;
    public FShaderParameter TessellationParameters;
    public FShaderResourceParameter Heightmap;
    public FShaderParameter TessellationFactors1;
    public FShaderParameter TessellationFactors2;
    public FShaderParameter TexcoordScaleBias;
    public FShaderParameter SplineParameters;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref GridSize);
        sc.SerializeUnmanaged(ref TessellationParameters);
        sc.SerializeUnmanaged(ref Heightmap);
        sc.SerializeUnmanaged(ref TessellationFactors1);
        sc.SerializeUnmanaged(ref TessellationFactors2);
        sc.SerializeUnmanaged(ref TexcoordScaleBias);
        sc.SerializeUnmanaged(ref SplineParameters);
    }
}

public class FFoliageVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter InvNumVerticesPerInstance;
    public FShaderParameter NumVerticesPerInstance;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref InvNumVerticesPerInstance);
        sc.SerializeUnmanaged(ref NumVerticesPerInstance);
    }
}

public class FGPUSkinDecalVertexFactoryShaderParameters : FGPUSkinVertexFactoryShaderParameters
{
    public FShaderParameter BoneToDecalRow0;
    public FShaderParameter BoneToDecalRow1;
    public FShaderParameter DecalLocation;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref BoneToDecalRow0);
        sc.SerializeUnmanaged(ref BoneToDecalRow1);
        sc.SerializeUnmanaged(ref DecalLocation);
    }
}

public class FGPUSkinVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter LocalToWorld;
    public FShaderParameter WorldToLocal;
    public FShaderParameter BoneMatrices;
    public FShaderParameter MaxBoneInfluences;
    public FShaderParameter MeshOrigin;
    public FShaderParameter MeshExtension;
    public FShaderParameter WoundEllipse0;
    public FShaderParameter WoundEllipse1;

    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref LocalToWorld);
        sc.SerializeUnmanaged(ref WorldToLocal);
        sc.SerializeUnmanaged(ref BoneMatrices);
        sc.SerializeUnmanaged(ref MaxBoneInfluences);
        sc.SerializeUnmanaged(ref MeshOrigin);
        sc.SerializeUnmanaged(ref MeshExtension);

        // TODO: Verify this.
        // Not in LE2 - Serialization of Decal subclass at 0x7ff7c627a160 does not show this
        if (sc.Game != MEGame.LE2)
        {
            sc.SerializeUnmanaged(ref WoundEllipse0);
            sc.SerializeUnmanaged(ref WoundEllipse1);
        }
    }
}

public class FInstancedStaticMeshVertexFactoryShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter InstancedViewTranslation;
    public FShaderParameter InstancingParameters;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref InstancedViewTranslation);
        sc.SerializeUnmanaged(ref InstancingParameters);
    }
}

public class FLensFlareVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter CameraRight;
    public FShaderParameter CameraUp;
    public FShaderParameter LocalToWorld;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref CameraRight);
        sc.SerializeUnmanaged(ref CameraUp);
        sc.SerializeUnmanaged(ref LocalToWorld);
    }
}

public class FLocalDecalVertexFactoryShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter DecalMatrix;
    public FShaderParameter DecalLocation;
    public FShaderParameter DecalOffset;
    public FShaderParameter DecalLocalBinormal;
    public FShaderParameter DecalLocalTangent;
    public FShaderParameter DecalLocalNormal;
    public FShaderParameter DecalBlendInterval;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref DecalMatrix);
        sc.SerializeUnmanaged(ref DecalLocation);
        sc.SerializeUnmanaged(ref DecalOffset);
        sc.SerializeUnmanaged(ref DecalLocalBinormal);
        sc.SerializeUnmanaged(ref DecalLocalTangent);
        sc.SerializeUnmanaged(ref DecalLocalNormal);
        sc.SerializeUnmanaged(ref DecalBlendInterval);
    }
}

public class FGPUSkinVertexFactoryApexShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter BoneMatrices;
    public FShaderParameter ApexDummy;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref BoneMatrices);
        sc.SerializeUnmanaged(ref ApexDummy);
    }
}

public class FParticleBeamTrailVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter CameraWorldPosition;
    public FShaderParameter CameraRight;
    public FShaderParameter CameraUp;
    public FShaderParameter ScreenAlignment;
    public FShaderParameter LocalToWorld;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref CameraWorldPosition);
        sc.SerializeUnmanaged(ref CameraRight);
        sc.SerializeUnmanaged(ref CameraUp);
        sc.SerializeUnmanaged(ref ScreenAlignment);
        sc.SerializeUnmanaged(ref LocalToWorld);
    }
}

public class FParticleInstancedMeshVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter InvNumVerticesPerInstance;
    public FShaderParameter NumVerticesPerInstance;
    public FShaderParameter InstancedPreViewTranslation;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref InvNumVerticesPerInstance);
        sc.SerializeUnmanaged(ref NumVerticesPerInstance);
        sc.SerializeUnmanaged(ref InstancedPreViewTranslation);
    }
}

public class FParticleVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter CameraWorldPosition;
    public FShaderParameter CameraRight;
    public FShaderParameter CameraUp;
    public FShaderParameter ScreenAlignment;
    public FShaderParameter LocalToWorld;
    public FShaderParameter AxisRotationVectorSourceIndex;
    public FShaderParameter AxisRotationVectors;
    public FShaderParameter ParticleUpRightResultScalars;
    public FShaderParameter NormalsType;
    public FShaderParameter NormalsSphereCenter;
    public FShaderParameter NormalsCylinderUnitDirection;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref CameraWorldPosition);
        sc.SerializeUnmanaged(ref CameraRight);
        sc.SerializeUnmanaged(ref CameraUp);
        sc.SerializeUnmanaged(ref ScreenAlignment);
        sc.SerializeUnmanaged(ref LocalToWorld);
        sc.SerializeUnmanaged(ref AxisRotationVectorSourceIndex);
        sc.SerializeUnmanaged(ref AxisRotationVectors);
        sc.SerializeUnmanaged(ref ParticleUpRightResultScalars);
        sc.SerializeUnmanaged(ref NormalsType);
        sc.SerializeUnmanaged(ref NormalsSphereCenter);
        sc.SerializeUnmanaged(ref NormalsCylinderUnitDirection);
    }
}

public class FSplineMeshVertexFactoryShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter SplineStartPos;
    public FShaderParameter SplineStartTangent;
    public FShaderParameter SplineStartRoll;
    public FShaderParameter SplineStartScale;
    public FShaderParameter SplineStartOffset;
    public FShaderParameter SplineEndPos;
    public FShaderParameter SplineEndTangent;
    public FShaderParameter SplineEndRoll;
    public FShaderParameter SplineEndScale;
    public FShaderParameter SplineEndOffset;
    public FShaderParameter SplineXDir;
    public FShaderParameter SmoothInterpRollScale;
    public FShaderParameter MeshMinZ;
    public FShaderParameter MeshRangeZ;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref SplineStartPos);
        sc.SerializeUnmanaged(ref SplineStartTangent);
        sc.SerializeUnmanaged(ref SplineStartRoll);
        sc.SerializeUnmanaged(ref SplineStartScale);
        sc.SerializeUnmanaged(ref SplineStartOffset);
        sc.SerializeUnmanaged(ref SplineEndPos);
        sc.SerializeUnmanaged(ref SplineEndTangent);
        sc.SerializeUnmanaged(ref SplineEndRoll);
        sc.SerializeUnmanaged(ref SplineEndScale);
        sc.SerializeUnmanaged(ref SplineEndOffset);
        sc.SerializeUnmanaged(ref SplineXDir);
        sc.SerializeUnmanaged(ref SmoothInterpRollScale);
        sc.SerializeUnmanaged(ref MeshMinZ);
        sc.SerializeUnmanaged(ref MeshRangeZ);
    }
}

public class FTerrainDecalVertexFactoryShaderParameters : FTerrainVertexFactoryShaderParameters
{
    public FShaderParameter DecalMatrix;
    public FShaderParameter DecalLocation;
    public FShaderParameter DecalOffset;
    public FShaderParameter DecalLocalBinormal;
    public FShaderParameter DecalLocalTangent;
    protected override void Serialize(SerializingContainer sc)
    {
        base.Serialize(sc);
        sc.SerializeUnmanaged(ref DecalMatrix);
        sc.SerializeUnmanaged(ref DecalLocation);
        sc.SerializeUnmanaged(ref DecalOffset);
        sc.SerializeUnmanaged(ref DecalLocalBinormal);
        sc.SerializeUnmanaged(ref DecalLocalTangent);
    }
}

public class FTerrainVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter LocalToWorld;
    public FShaderParameter WorldToLocal;
    public FShaderParameter LocalToView;
    public FShaderParameter TerrainLightmapCoordinateScaleBias;
    public FShaderParameter TessellationInterpolation;
    public FShaderParameter InvMaxTesselationLevel_ZScale;
    public FShaderParameter InvTerrainSize_SectionBase;
    public FShaderParameter Unused;
    public FShaderParameter TessellationDistanceScale;
    public FShaderParameter TessInterpDistanceValues;
    protected override void Serialize(SerializingContainer sc)
    {
        sc.SerializeUnmanaged(ref LocalToWorld);
        sc.SerializeUnmanaged(ref WorldToLocal);
        sc.SerializeUnmanaged(ref LocalToView);
        sc.SerializeUnmanaged(ref TerrainLightmapCoordinateScaleBias);
        sc.SerializeUnmanaged(ref TessellationInterpolation);
        sc.SerializeUnmanaged(ref InvMaxTesselationLevel_ZScale);
        sc.SerializeUnmanaged(ref InvTerrainSize_SectionBase);
        sc.SerializeUnmanaged(ref Unused);
        sc.SerializeUnmanaged(ref TessellationDistanceScale);
        sc.SerializeUnmanaged(ref TessInterpDistanceValues);
    }
}