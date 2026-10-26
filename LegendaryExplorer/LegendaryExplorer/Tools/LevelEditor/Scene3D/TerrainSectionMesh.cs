using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>A terrain vertex for LEX's terrain shader: section-local position and height gradient (see VSMainTerrain)</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TerrainVertex(Vector3 Position, Vector2 Gradient)
{
    public static readonly unsafe int Stride = sizeof(TerrainVertex);

    public static readonly InputElement[] InputElements =
    [
        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
    ];
}

/// <summary>
/// FTerrainVertex as LE's FTerrainVertexFactory reads it: section-local X and Y and the raw height (VET_UByte4 POSITION), and the packed height gradients
/// (VET_Short2 TANGENT). LE's terrain shaders also read a displacement along the normal (BLENDWEIGHT0, a separate stream in the game). Every shipped
/// terrain has MaxCollisionDisplacement 0, so it is 0 here; a terrain with displacement isn't drawn with game shaders
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct TerrainGameVertex(byte X, byte Y, byte HeightLow, byte HeightHigh, short GradientX, short GradientY, float Displacement)
{
    public const int Stride = 12;

    public static readonly InputElement[] InputElements =
    [
        new InputElement("POSITION", 0, Format.R8G8B8A8_UInt, 0, 0),
        new InputElement("TANGENT", 0, Format.R16G16_SInt, 4, 0),
        new InputElement("BLENDWEIGHT", 0, Format.R32_Float, 8, 0),
    ];
}

/// <summary>
/// FTerrainVertexFactoryShaderParameters' values for one section (FTerrainObject): (1, TERRAIN_ZSCALE, DrawScale3D.Z / X, DrawScale3D.Z / Y),
/// (1 / NumVerticesX, 1 / NumVerticesY, SectionBaseX, SectionBaseY), the section's light-map coordinate scale and bias, and TesselationDistanceScale
/// </summary>
internal sealed record TerrainVertexFactoryValues(Vector4 InvMaxTesselationLevel_ZScale, Vector4 InvTerrainSize_SectionBase, Vector4 LightmapCoordinateScaleBias,
    float TessellationDistanceScale);

/// <summary>
/// One terrain section at maximum tessellation. The grid is read from the height field when the section is first drawn, so terrains with
/// thousands of sections only allocate GPU memory for the parts that have been in view. Sections without holes or flipped quads share
/// their index buffer through <see cref="MeshRenderContext.GetRegularTerrainIndices"/>.
/// </summary>
public sealed class TerrainSectionMesh : IDisposable
{
    internal TerrainHeightField HeightField { get; }
    public TerrainSection Section { get; }
    /// <summary>Section-local bounds</summary>
    public BoxSphereBounds LocalBounds { get; }
    /// <summary>Triangles drawn at maximum tessellation</summary>
    public int TriangleCount { get; }
    public bool IsRegular { get; }

    private Buffer VertexBuffer;
    private Buffer IndexBuffer;
    /// <summary>Whether the section has been drawn, and so holds GPU buffers</summary>
    public bool HasGpuBuffers => VertexBuffer is not null || GameVertexBuffer is not null;
    private Format IndexFormat;
    private int IndexCount;

    public TerrainSectionMesh(TerrainHeightField heightField, TerrainSection section)
    {
        HeightField = heightField;
        Section = section;
        IsRegular = heightField.IsRegular(section);
        TriangleCount = IsRegular ? section.TrueSizeX * section.TrueSizeY * 2 : heightField.SectionIndices(section).Count / 3;
        var box = new Box();
        for (int y = 0; y <= section.TrueSizeY; y++)
        for (int x = 0; x <= section.TrueSizeX; x++)
            box.Add(LocalPosition(x, y));
        LocalBounds = new BoxSphereBounds(box);
    }

    private Vector3 LocalPosition(int x, int y) => HeightField.LocalVertex(Section.BaseX + x, Section.BaseY + y) - new Vector3(Section.BaseX, Section.BaseY, 0);

    /// <summary>The section's vertices, row by row: (TrueSizeX + 1) × (TrueSizeY + 1)</summary>
    public TerrainVertex[] BuildVertices()
    {
        int stride = Section.TrueSizeX + 1;
        var vertices = new TerrainVertex[stride * (Section.TrueSizeY + 1)];
        for (int y = 0; y <= Section.TrueSizeY; y++)
        for (int x = 0; x <= Section.TrueSizeX; x++)
            vertices[y * stride + x] = new TerrainVertex(LocalPosition(x, y), HeightField.Gradient(Section.BaseX + x, Section.BaseY + y));
        return vertices;
    }

    /// <summary>Whether the section's vertices fit FTerrainVertex's byte coordinates</summary>
    public bool FitsGameVertices => Section.TrueSizeX <= byte.MaxValue && Section.TrueSizeY <= byte.MaxValue;

    /// <summary>The section's vertices as the game's vertex buffer holds them, in the same order as <see cref="BuildVertices"/></summary>
    /// <param name="gradientScale">DrawScale3D.Z / DrawScale3D.X and DrawScale3D.Z / DrawScale3D.Y</param>
    public TerrainGameVertex[] BuildGameVertices(Vector2 gradientScale)
    {
        int stride = Section.TrueSizeX + 1;
        var vertices = new TerrainGameVertex[stride * (Section.TrueSizeY + 1)];
        for (int y = 0; y <= Section.TrueSizeY; y++)
        for (int x = 0; x <= Section.TrueSizeX; x++)
        {
            ushort height = HeightField.Height(Section.BaseX + x, Section.BaseY + y);
            (short gradientX, short gradientY) = HeightField.PackedGradient(Section.BaseX + x, Section.BaseY + y, gradientScale.X, gradientScale.Y);
            vertices[y * stride + x] = new TerrainGameVertex((byte)x, (byte)y, (byte)(height & 0xFF), (byte)(height >> 8), gradientX, gradientY, 0);
        }
        return vertices;
    }

    private Buffer GameVertexBuffer;
    private Vector2 GameVertexGradientScale;

    private void EnsureIndices(MeshRenderContext context)
    {
        if (IndexBuffer is not null) return;
        if (IsRegular)
        {
            (IndexBuffer, IndexFormat, IndexCount) = context.GetRegularTerrainIndices(Section.TrueSizeX, Section.TrueSizeY);
        }
        else
        {
            List<int> indices = HeightField.SectionIndices(Section);
            (IndexBuffer, IndexFormat) = CreateIndexBuffer(context.Device, indices, (Section.TrueSizeX + 1) * (Section.TrueSizeY + 1));
            IndexCount = indices.Count;
        }
    }

    internal void Draw(MeshRenderContext context, Matrix4x4 localToWorld)
    {
        if (TriangleCount == 0) return;
        VertexBuffer ??= Buffer.Create(context.Device, BindFlags.VertexBuffer, BuildVertices());
        EnsureIndices(context);
        context.RenderTerrainSection(VertexBuffer, IndexBuffer, IndexFormat, IndexCount, localToWorld);
    }

    /// <summary>
    /// Draws the section's triangles with the shaders and parameters already set on <paramref name="effect"/>, from the game's vertex format (rebuilt if the
    /// terrain's scale ratios change, which its gradients include). Returns false if there's nothing to draw
    /// </summary>
    internal bool DrawGame(MeshRenderContext context, LEEffect effect, Vector2 gradientScale, LEHitProxyConstants? hitProxy)
    {
        if (TriangleCount == 0) return false;
        if (GameVertexBuffer is null || GameVertexGradientScale != gradientScale)
        {
            GameVertexBuffer?.Dispose();
            GameVertexBuffer = Buffer.Create(context.Device, BindFlags.VertexBuffer, BuildGameVertices(gradientScale));
            GameVertexGradientScale = gradientScale;
        }
        EnsureIndices(context);
        effect.RenderObject(context.ImmediateContext, GameVertexBuffer, TerrainGameVertex.Stride, IndexBuffer, IndexFormat, 0, IndexCount);
        if (hitProxy is { } constants) effect.RenderHitProxy(context.ImmediateContext, constants, 0, IndexCount);
        return true;
    }

    internal static (Buffer, Format) CreateIndexBuffer(Device device, List<int> indices, int vertexCount)
    {
        if (vertexCount <= ushort.MaxValue + 1)
            return (Buffer.Create(device, BindFlags.IndexBuffer, indices.ConvertAll(i => (ushort)i).ToArray()), Format.R16_UInt);
        return (Buffer.Create(device, BindFlags.IndexBuffer, indices.ToArray()), Format.R32_UInt);
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        VertexBuffer = null;
        GameVertexBuffer?.Dispose();
        GameVertexBuffer = null;
        // Shared regular index buffers belong to the render context
        if (!IsRegular) IndexBuffer?.Dispose();
        IndexBuffer = null;
    }
}
