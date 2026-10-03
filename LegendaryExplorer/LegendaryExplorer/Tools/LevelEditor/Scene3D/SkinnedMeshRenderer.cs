using System;
using System.Numerics;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Animation;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using DeviceContext = SharpDX.Direct3D11.DeviceContext;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Per-vertex bone skinning data. Stores bind-pose positions/normals and bone influence data.
/// Performs CPU skinning each frame and updates the vertex buffer.
/// </summary>
public struct SkinVertex
{
    public Vector3 BindPosition;  // Unreal space (Z-up)
    public Vector3 BindNormal;    // Unreal space
    public Vector3 BindTangent;   // Unreal space
    public float BinormalSign;    // -1 or 1
    public Vector2 UV;
    public int Bone0, Bone1, Bone2, Bone3;       // skeleton-wide bone indices
    public float Weight0, Weight1, Weight2, Weight3; // normalized weights
}

public class SkinnedMeshRenderer
{
    public bool NeedsUpdate { get; set; }
    private SkinVertex[] _skinVertices;

    /// <summary>
    /// Builds per-vertex skinning data from a SkeletalMesh LOD model.
    /// Resolves chunk-local bone indices to skeleton-wide indices via chunk.BoneMap.
    /// </summary>
    public void BuildFromSkeletalMesh(MEGame game, StaticLODModel lodModel)
    {
        bool isME1 = game == MEGame.ME1;
        int vertexCount = isME1 ? lodModel.ME1VertexBufferGPUSkin.Length : (int)lodModel.NumVertices;
        _skinVertices = new SkinVertex[vertexCount];

        if (isME1)
        {
            // ME1 uses SoftSkinVertex in ME1VertexBufferGPUSkin
            for (int v = 0; v < vertexCount; v++)
            {
                var sv = lodModel.ME1VertexBufferGPUSkin[v];
                var chunk = FindChunkForVertex(lodModel, v);
                ref var skinVert = ref _skinVertices[v];
                skinVert.BindPosition = sv.Position;
                skinVert.BindNormal = (Vector3)sv.TangentZ;
                skinVert.BindTangent = (Vector3)sv.TangentX;
                skinVert.BinormalSign = ((Vector4)sv.TangentZ).W < 0 ? -1f : 1f;
                skinVert.UV = sv.UV;
                ResolveInfluences(ref skinVert, sv.InfluenceBones, sv.InfluenceWeights, chunk);
            }
        }
        else
        {
            // ME2+ uses GPUSkinVertex in VertexBufferGPUSkin.VertexData
            for (int v = 0; v < vertexCount; v++)
            {
                var gv = lodModel.VertexBufferGPUSkin.VertexData[v];
                var chunk = FindChunkForVertex(lodModel, v);
                ref var skinVert = ref _skinVertices[v];
                skinVert.BindPosition = gv.Position;
                skinVert.BindNormal = (Vector3)gv.TangentZ;
                skinVert.BindTangent = (Vector3)gv.TangentX;
                skinVert.BinormalSign = ((Vector4)gv.TangentZ).W < 0 ? -1f : 1f;
                skinVert.UV = gv.UV;
                ResolveInfluences(ref skinVert, gv.InfluenceBones, gv.InfluenceWeights, chunk);
            }
        }
    }

    private static SkelMeshChunk FindChunkForVertex(StaticLODModel lodModel, int vertexIndex)
    {
        foreach (var chunk in lodModel.Chunks)
        {
            int chunkStart = (int)chunk.BaseVertexIndex;
            int chunkEnd = chunkStart + chunk.NumRigidVertices + chunk.NumSoftVertices;
            if (vertexIndex >= chunkStart && vertexIndex < chunkEnd)
                return chunk;
        }
        // Fallback to first chunk if not found
        return lodModel.Chunks[0];
    }

    private static void ResolveInfluences(ref SkinVertex skinVert, Influences bones, Influences weights, SkelMeshChunk chunk)
    {
        // Resolve chunk-local bone indices to skeleton-wide indices via BoneMap
        skinVert.Bone0 = bones[0] < chunk.BoneMap.Length ? chunk.BoneMap[bones[0]] : 0;
        skinVert.Bone1 = bones[1] < chunk.BoneMap.Length ? chunk.BoneMap[bones[1]] : 0;
        skinVert.Bone2 = bones[2] < chunk.BoneMap.Length ? chunk.BoneMap[bones[2]] : 0;
        skinVert.Bone3 = bones[3] < chunk.BoneMap.Length ? chunk.BoneMap[bones[3]] : 0;

        // Normalize weights (byte -> float)
        float w0 = weights[0] / 255f;
        float w1 = weights[1] / 255f;
        float w2 = weights[2] / 255f;
        float w3 = weights[3] / 255f;
        float total = w0 + w1 + w2 + w3;
        if (total > 0)
        {
            skinVert.Weight0 = w0 / total;
            skinVert.Weight1 = w1 / total;
            skinVert.Weight2 = w2 / total;
            skinVert.Weight3 = w3 / total;
        }
        else
        {
            skinVert.Weight0 = 1f;
            skinVert.Weight1 = 0f;
            skinVert.Weight2 = 0f;
            skinVert.Weight3 = 0f;
        }
    }

    /// <summary>
    /// Performs CPU skinning: blends skinning matrices per vertex, transforms bind-pose position/normal,
    /// writes results to the mesh vertex list with Unreal-to-renderer coordinate conversion,
    /// then rebuilds the D3D vertex buffer.
    /// </summary>
    public void UpdateSkinning(DeviceContext context, Mesh<WorldVertex> mesh, AnimPlayer animPlayer)
    {
        UpdateSkinning(context, mesh, animPlayer, static (in SkinVertex sv, Vector3 position, Vector3 normal, Vector3 _) =>
            new WorldVertex(position, new Vector4(normal.X, normal.Z, normal.Y, 1), sv.UV));
    }

    /// <summary>
    /// Performs CPU skinning for a mesh rendered with the game's shaders. Unlike the <see cref="WorldVertex"/> overload,
    /// the tangent basis is skinned too, and the normals are left in Unreal space.
    /// </summary>
    public void UpdateSkinning(DeviceContext context, Mesh<LEVertex> mesh, AnimPlayer animPlayer)
    {
        UpdateSkinning(context, mesh, animPlayer, static (in SkinVertex sv, Vector3 position, Vector3 normal, Vector3 tangent) =>
        {
            Fixed4<Vector4> uvs = default;
            uvs[0] = new Vector4(sv.UV, 0, 0);
            return (LEVertex)LEVertex.Create(position, tangent, new Vector4(normal, sv.BinormalSign), uvs);
        });
    }

    private delegate TVertex SkinnedVertexBuilder<out TVertex>(in SkinVertex sv, Vector3 position, Vector3 normal, Vector3 tangent);

    private void UpdateSkinning<TVertex>(DeviceContext context, Mesh<TVertex> mesh, AnimPlayer animPlayer, SkinnedVertexBuilder<TVertex> buildVertex)
        where TVertex : IVertexBase
    {
        NeedsUpdate = false;
        if (_skinVertices == null || mesh == null) return;

        var skinningMatrices = animPlayer.ComputeSkinningMatrices();
        if (skinningMatrices == null) return;

        int vertexCount = Math.Min(_skinVertices.Length, mesh.Vertices.Count);
        for (int i = 0; i < vertexCount; i++)
        {
            ref var sv = ref _skinVertices[i];

            // Blend skinning matrices by bone weights
            var blended = BlendMatrix(
                skinningMatrices, sv.Bone0, sv.Weight0,
                sv.Bone1, sv.Weight1,
                sv.Bone2, sv.Weight2,
                sv.Bone3, sv.Weight3);

            // Transform bind position and normal in Unreal space
            var skinnedPos = Vector3.Transform(sv.BindPosition, blended);
            var skinnedNormal = Vector3.TransformNormal(sv.BindNormal, blended);
            var skinnedTangent = Vector3.TransformNormal(sv.BindTangent, blended);

            mesh.Vertices[i] = buildVertex(in sv, skinnedPos, skinnedNormal, skinnedTangent);
        }

        mesh.UpdateVertices(context);
    }

    private static Matrix4x4 BlendMatrix(Matrix4x4[] matrices, int b0, float w0, int b1, float w1, int b2, float w2, int b3, float w3)
    {
        var m = matrices[b0] * w0;
        if (w1 > 0 && b1 < matrices.Length) m += matrices[b1] * w1;
        if (w2 > 0 && b2 < matrices.Length) m += matrices[b2] * w2;
        if (w3 > 0 && b3 < matrices.Length) m += matrices[b3] * w3;
        return m;
    }

    public void UpdateVertexPositions(Vector3[] positions)
    {
        for (int i = 0; i < _skinVertices.Length && i < positions.Length; i++)
        {
            _skinVertices[i].BindPosition = positions[i];
        }
    }
}
