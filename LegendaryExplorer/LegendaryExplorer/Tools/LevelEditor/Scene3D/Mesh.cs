using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public class Mesh<TVertex> : IDisposable where TVertex : IVertexBase
{
    public readonly List<Triangle> Triangles;
    public readonly List<TVertex> Vertices;
    public SharpDX.Direct3D11.Buffer VertexBuffer { get; private set; }
    public SharpDX.Direct3D11.Buffer IndexBuffer { get; private set; }

    private bool _isDynamic;

    public BoxSphereBounds BaseBounds;

    public BoxSphereBounds TransformedBounds;

    private Matrix4x4 localToWorld = Matrix4x4.Identity;
    public Matrix4x4 LocalToWorld
    {
        get => localToWorld;
        set
        {
            localToWorld = value;
            TransformedBounds = BaseBounds.TransformBy(localToWorld);
            Matrix4x4.Invert(LocalToWorld, out Matrix4x4 wtl);
            worldToLocal = new SharpDX.Matrix3x3(wtl.M11, wtl.M12, wtl.M13, wtl.M21, wtl.M22, wtl.M23, wtl.M31, wtl.M32, wtl.M33);
        }
    }

    private SharpDX.Matrix3x3 worldToLocal = SharpDX.Matrix3x3.Identity;
    public SharpDX.Matrix3x3 WorldToLocal => worldToLocal;

    // Creates a new blank mesh.

    // Creates a blank mesh with the given data.
    public Mesh(Device device, List<Triangle> triangles, List<TVertex> vertices, bool isDynamic = false)
    {
        Triangles = triangles;
        Vertices = vertices;
        _isDynamic = isDynamic;
        RebuildBuffer(device);
    }
    public void RebuildBuffer(Device device)
    {
        // Dispose all the old stuff
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();

        // Update the AABB
        Box boundingBox = new();
        foreach (TVertex v in Vertices)
        {
            boundingBox.Add(v.Position);
        }
        TransformedBounds = BaseBounds = new BoxSphereBounds(boundingBox);

        IndexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.IndexBuffer, Triangles.ToArray());

        int stride = TVertex.Stride;
        if (!_isDynamic)
        {
            int floatsPerVertex = stride / 4;
            float[] vertexdata = new float[floatsPerVertex * Vertices.Count];
            Span<float> vertexDataSpan = vertexdata.AsSpan();
            for (int vertIdx = 0, floatIdx = 0; vertIdx < Vertices.Count; vertIdx++, floatIdx += floatsPerVertex)
            {
                Vertices[vertIdx].ToFloats(vertexDataSpan[floatIdx..]);
            }
            VertexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.VertexBuffer, vertexdata);
        }
        else
        {
            VertexBuffer = new SharpDX.Direct3D11.Buffer(device, new BufferDescription(
                stride * Vertices.Count,
                ResourceUsage.Dynamic,
                BindFlags.VertexBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                stride));
            UpdateVertices(device.ImmediateContext);
        }
    }

    public unsafe void UpdateVertices(DeviceContext context)
    {
        if (!_isDynamic || Vertices.Count == 0) return;

        int stride = TVertex.Stride;
        int floatsPerVertex = stride / 4;

        var dataBox = context.MapSubresource(VertexBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
        var dest = new Span<float>((void*)dataBox.DataPointer, Vertices.Count * floatsPerVertex);

        Box boundingBox = new();
        for (int vi = 0, fi = 0; vi < Vertices.Count; vi++, fi += floatsPerVertex)
        {
            TVertex v = Vertices[vi];
            boundingBox.Add(v.Position);
            v.ToFloats(dest[fi..]);
        }
        context.UnmapSubresource(VertexBuffer, 0);

        BaseBounds = new BoxSphereBounds(boundingBox);
        TransformedBounds = BaseBounds.TransformBy(localToWorld);
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
    }
}

/// <summary>
/// Contains the indices of the three vertices that make up a triangle.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Triangle(uint vertex1, uint vertex2, uint vertex3)
{
    public uint Vertex1 = vertex1;
    public uint Vertex2 = vertex2;
    public uint Vertex3 = vertex3;
}

/// <summary>
/// The base class for vertices that can be rendered. They must have a position. This is necessary for builtin AABB computation as well.
/// </summary>
public interface IVertexBase
{
    public Vector3 Position { get; }

    public void ToFloats(Span<float> dest);

    public static abstract InputElement[] InputElements { get; }

    public static abstract int Stride { get; }

    public static abstract IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Fixed4<Vector4> uvs);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
//vertex used by LEX's generic shader
public struct WorldVertex : IVertexBase
{
    private Vector4 _position;
    private Vector3 HitTestID;
    public Vector4 Normal;
    public Vector4 Color;
    public Vector2 UV;
    public readonly Vector3 Position => new(_position.X, _position.Y, _position.Z);

    public WorldVertex(Vector3 position, Vector4 normal, Vector2 uv)
    {
        _position = new Vector4(position, 1);
        Normal = normal;
        UV = uv;
    }

    //for use by the level editors primitives
    public WorldVertex(Vector3 position, Vector4 color, Vector3 hitTestId)
    {
        _position = new Vector4(position, 1);
        Color = color;
        HitTestID = hitTestId;
    }

    public void ToFloats(Span<float> dest) => this.AsSpanOf<WorldVertex, float>().CopyTo(dest);

    public static InputElement[] InputElements =>
    [
        new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
        new InputElement("TANGENT", 0, Format.R32G32B32_Float, 0),
        new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 0),
        new InputElement("COLOR", 1, Format.R32G32B32A32_Float, 0),
        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0)
    ];

    public static unsafe int Stride => sizeof(Vector4) + sizeof(Vector3) + sizeof(Vector4) + sizeof(Vector4) + sizeof(Vector2);

    public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Fixed4<Vector4> uvs)
    {
        return new WorldVertex(position, normal, new Vector2(uvs[0].X, uvs[0].Y));
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
//Vertex used for FLocalVertexFactory vertex shaders in LE games
public struct LEVertex : IVertexBase
{
    private Vector4 position;
    private Vector3 tangent;
    private Vector4 normal;
    private Vector4 color;
    //actual number of UVs used by FLocalVertexFactory vertex shaders varies between 1 float2, and 3 float4s + 1 float2.
    //however, it's perfectly fine for the vertex buffer stride to be longer than the parameters for a vertex shader
    //and for the InputLayout to be bigger. So for simplicity, all vertexes are the maximum size regardless of shader
    private Fixed4<Vector4> uvs;
    public readonly Vector3 Position => new(position.X, position.Y, position.Z);

    private LEVertex(Vector4 position, Vector3 tangent, Vector4 normal, Vector4 color, Fixed4<Vector4> uvs)
    {
        this.position = position;
        this.tangent = tangent;
        this.normal = normal;
        this.color = color;
        this.uvs = uvs;
    }

    public void ToFloats(Span<float> floats) => MemoryMarshal.CreateSpan(ref Unsafe.As<LEVertex, float>(ref this), Stride / 4).CopyTo(floats);

    public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Fixed4<Vector4> uvs)
    {
        return new LEVertex(new Vector4(position, 1), tangent, normal, Vector4.Zero, uvs);
    }
    public static unsafe int Stride => sizeof(Vector4) + sizeof(Vector3) + sizeof(Vector4) + sizeof(Vector4) + sizeof(Vector4) * 3 + sizeof(Vector2);


    public static InputElement[] InputElements { get; } =
    [
        new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
        new InputElement("TANGENT", 0, Format.R32G32B32_Float, 0),
        new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 0),
        new InputElement("COLOR", 1, Format.R32G32B32A32_Float, 0),
        new InputElement("TEXCOORD", 0, Format.R32G32B32A32_Float, 0),
        new InputElement("TEXCOORD", 1, Format.R32G32B32A32_Float, 0),
        new InputElement("TEXCOORD", 2, Format.R32G32B32A32_Float, 0),
        new InputElement("TEXCOORD", 3, Format.R32G32B32A32_Float, 0),
    ];
}