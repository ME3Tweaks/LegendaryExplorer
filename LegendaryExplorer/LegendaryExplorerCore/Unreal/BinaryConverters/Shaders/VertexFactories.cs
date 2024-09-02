using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

public struct FVertexFactoryParameterRef
{
    public NameReference VertexFactoryType;
    public FVertexFactoryShaderParameters Parameters;

    public void Serialize(SerializingContainer sc)
    {
        sc.Serialize(ref VertexFactoryType);
        FVertexFactoryShaderParameters.Serialize(sc, VertexFactoryType.Name, ref Parameters);
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

public class FFoliageVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter InvNumVerticesPerInstance;
    public FShaderParameter NumVerticesPerInstance;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
    }
}

public class FGPUSkinDecalVertexFactoryShaderParameters : FGPUSkinVertexFactoryShaderParameters
{
    public FShaderParameter BoneToDecalRow0;
    public FShaderParameter BoneToDecalRow1;
    public FShaderParameter DecalLocation;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

public class FInstancedStaticMeshVertexFactoryShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter InstancedViewTranslation;
    public FShaderParameter InstancingParameters;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
    }
}

public class FLensFlareVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter CameraRight;
    public FShaderParameter CameraUp;
    public FShaderParameter LocalToWorld;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

public class FGPUSkinVertexFactoryApexShaderParameters : FLocalVertexFactoryShaderParameters
{
    public FShaderParameter BoneMatrices;
    public FShaderParameter ApexDummy;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

public class FParticleInstancedMeshVertexFactoryShaderParameters : FVertexFactoryShaderParameters
{
    public FShaderParameter InvNumVerticesPerInstance;
    public FShaderParameter NumVerticesPerInstance;
    public FShaderParameter InstancedPreViewTranslation;
    protected override void Serialize(SerializingContainer sc)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}