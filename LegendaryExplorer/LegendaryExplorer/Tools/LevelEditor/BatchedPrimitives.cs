using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.LevelEditor;

public class BatchedPrimitives
{
    private readonly List<WorldVertex> LineVerts = [];

    private readonly List<int> MeshStarts = [];
    private readonly List<WorldVertex> MeshVerts = [];
    private readonly List<int> MeshIndices = [];


    public void AddLine(Vector3 start, Vector3 end, Vector4 color, int hitId)
    {
        var hitVec = GetHitVec(hitId);
        LineVerts.Add(new WorldVertex(start, color, hitVec));
        LineVerts.Add(new WorldVertex(end, color, hitVec));
    }

    //public void AddMesh(Vector4 color, int hitId, params Span<Vector3> verts)
    //{
    //    var hitVec = GetHitVec(hitId);
    //    MeshStarts.Add(MeshVerts.Count);
    //    foreach (Vector3 point in verts)
    //    {
    //        MeshVerts.Add(new WorldVertex(point, color, hitVec));
    //    }
    //}

    //must finish one mesh before starting another
    public MeshBuilder BuildMesh(Vector4 color, int hitId, Matrix4x4 localToWorld)
    {
        return new MeshBuilder(this, color, hitId, localToWorld);
    }

    public void Render(LevelEditorRenderContext context)
    {
        if (LineVerts.Count is 0 && MeshVerts.Count is 0) return; 

        context.ClearDepthBuffer();
        context.RenderFlags |= RenderContext.ShaderFlags.PrimitiveRendering;
        context.DefaultEffect.PrepDraw(context.ImmediateContext, context.AlphaBlendState, context.GetWorldConstants(Matrix4x4.Identity));
        context.DefaultEffect.PrepPrimitiveBuffers(context, CollectionsMarshal.AsSpan(LineVerts), CollectionsMarshal.AsSpan(MeshVerts), CollectionsMarshal.AsSpan(MeshIndices));

        int lineVertCount = LineVerts.Count;
        if (lineVertCount > 0)
        {
            context.DefaultEffect.RenderPrimitives(context, SharpDX.Direct3D.PrimitiveTopology.LineList, 0, lineVertCount);
        }

        if (MeshStarts.Count > 0)
        {
            int i = 0;
            int indexStart;
            int indexCount;
            for (; i < MeshStarts.Count - 1; i++)
            {
                indexStart = MeshStarts[i];
                indexCount = MeshStarts[i + 1] - indexStart;
                if (indexCount <= 0) continue;
                context.DefaultEffect.RenderPrimitives(context, SharpDX.Direct3D.PrimitiveTopology.TriangleList, indexStart, indexCount, lineVertCount);
            }
            indexStart = MeshStarts[i];
            indexCount = MeshIndices.Count - indexStart;
            if (indexCount > 0)
            {
                context.DefaultEffect.RenderPrimitives(context, SharpDX.Direct3D.PrimitiveTopology.TriangleList, indexStart, indexCount, lineVertCount);
            }
        }

        LineVerts.Clear();
        MeshVerts.Clear();
        MeshIndices.Clear();
        MeshStarts.Clear();

        context.RenderFlags &= ~RenderContext.ShaderFlags.PrimitiveRendering;
    }

    public readonly ref struct MeshBuilder
    {
        private readonly Matrix4x4 LocalToWorld;
        private readonly BatchedPrimitives batcher;
        private readonly Vector4 Color;
        private readonly Vector3 HitVec;
        private readonly int vertOffset;
        public MeshBuilder(BatchedPrimitives batchedPrimitives, Vector4 color, int hitId, Matrix4x4 localToWorld)
        {
            batcher = batchedPrimitives;
            Color = color;
            HitVec = GetHitVec(hitId);
            vertOffset = batchedPrimitives.MeshVerts.Count;
            LocalToWorld = localToWorld;
            batcher.MeshStarts.Add(batchedPrimitives.MeshIndices.Count);
        }

        public void AddVertex(Vector3 point)
        {
            batcher.MeshVerts.Add(new WorldVertex(Vector3.Transform(point, LocalToWorld), Color, HitVec));
        }

        public void AddVertex(float x, float y, float z) => AddVertex(new Vector3(x, y, z));

        public void AddTriangle(int v1, int v2, int v3)
        {
            batcher.MeshIndices.Add(v1 + vertOffset);
            batcher.MeshIndices.Add(v2 + vertOffset);
            batcher.MeshIndices.Add(v3 + vertOffset);
        }
    }

    private static Vector3 GetHitVec(int hitId)
    {
        return new Vector3((hitId & 0xFF) / 255f, ((hitId >> 8) & 0xFF) / 255f, ((hitId >> 16) & 0xFF) / 255f);
    }
}