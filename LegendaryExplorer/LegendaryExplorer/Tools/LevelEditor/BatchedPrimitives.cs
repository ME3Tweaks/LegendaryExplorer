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

/// <param name="depthTested">Whether the primitives are hidden behind the scene. Otherwise they're drawn on top of it</param>
public class BatchedPrimitives(bool depthTested = false)
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

        if (!depthTested) context.ClearDepthBuffer();
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

        /// <summary>
        /// A vertex with its own color, for shading a mesh's faces
        /// </summary>
        public void AddVertex(Vector3 point, Vector4 color)
        {
            batcher.MeshVerts.Add(new WorldVertex(Vector3.Transform(point, LocalToWorld), color, HitVec));
        }

        public void AddTriangle(int v1, int v2, int v3)
        {
            batcher.MeshIndices.Add(v1 + vertOffset);
            batcher.MeshIndices.Add(v2 + vertOffset);
            batcher.MeshIndices.Add(v3 + vertOffset);
        }
    }

    internal static Vector3 GetHitVec(int hitId)
    {
        return new Vector3((hitId & 0xFF) / 255f, ((hitId >> 8) & 0xFF) / 255f, ((hitId >> 16) & 0xFF) / 255f);
    }
}

/// <summary>
/// Camera-facing textured quads, drawn into the scene so its geometry hides them. Transparent texels are discarded,
/// so only the sprite's shape occludes and can be clicked. The texture is tinted by the sprite's color
/// </summary>
public class SpriteBatch
{
    private readonly Dictionary<SharpDX.Direct3D11.ShaderResourceView, List<WorldVertex>> Sprites = [];

    /// <param name="right">Half the sprite's width, along the view's right axis</param>
    /// <param name="up">Half the sprite's height, along the view's up axis</param>
    public void Add(SharpDX.Direct3D11.ShaderResourceView texture, Vector3 center, Vector3 right, Vector3 up, Vector4 color, int hitId)
    {
        if (texture is null) return;
        if (!Sprites.TryGetValue(texture, out List<WorldVertex> vertices))
        {
            Sprites[texture] = vertices = [];
        }
        Vector3 hitVec = BatchedPrimitives.GetHitVec(hitId);
        vertices.Add(new WorldVertex(center - right + up, color, hitVec, new Vector2(0, 0)));
        vertices.Add(new WorldVertex(center + right + up, color, hitVec, new Vector2(1, 0)));
        vertices.Add(new WorldVertex(center + right - up, color, hitVec, new Vector2(1, 1)));
        vertices.Add(new WorldVertex(center - right - up, color, hitVec, new Vector2(0, 1)));
    }

    public void Clear() => Sprites.Clear();

    public void Render(LevelEditorRenderContext context)
    {
        if (Sprites.Count is 0) return;
        context.RenderFlags |= RenderContext.ShaderFlags.PrimitiveRendering | RenderContext.ShaderFlags.Sprite;
        context.DefaultEffect.PrepDraw(context.ImmediateContext, context.AlphaBlendState, context.GetWorldConstants(Matrix4x4.Identity));
        //drawn with or without culling, the quads face the camera either way
        foreach ((SharpDX.Direct3D11.ShaderResourceView texture, List<WorldVertex> vertices) in Sprites)
        {
            if (vertices.Count is 0) continue;
            Span<int> indices = QuadIndices(vertices.Count / 4);
            context.DefaultEffect.PrepPrimitiveBuffers(context, [], CollectionsMarshal.AsSpan(vertices), indices);
            context.ImmediateContext.PixelShader.SetShaderResource(0, texture);
            context.DefaultEffect.RenderPrimitives(context, SharpDX.Direct3D.PrimitiveTopology.TriangleList, 0, indices.Length, 0);
            vertices.Clear();
        }
        context.RenderFlags &= ~(RenderContext.ShaderFlags.PrimitiveRendering | RenderContext.ShaderFlags.Sprite);
    }

    //every quad's indices are the same, so they're kept between frames, and only extended when there are more quads than ever before
    private int[] Indices = [];

    private Span<int> QuadIndices(int quadCount)
    {
        int count = quadCount * 6;
        if (Indices.Length < count)
        {
            int filled = Indices.Length;
            Array.Resize(ref Indices, Math.Max(count, filled * 2));
            for (int i = filled, quad = filled / 6 * 4; i < Indices.Length; quad += 4, i += 6)
            {
                (Indices[i], Indices[i + 1], Indices[i + 2], Indices[i + 3], Indices[i + 4], Indices[i + 5]) = (quad, quad + 1, quad + 2, quad + 2, quad + 3, quad);
            }
        }
        return Indices.AsSpan(0, count);
    }
}