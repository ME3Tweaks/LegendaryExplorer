using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.LevelEditor;

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
