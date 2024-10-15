using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    // This class exists because typing Mesh<WorldVertex> is a pain.
    public class WorldMesh : Mesh<WorldVertex>
    {
        public WorldMesh(Device device, List<Triangle> triangles, List<WorldVertex> vertices) : base(device, triangles, vertices)
        {
        }
    }

    public class Mesh<TVertex> : IDisposable where TVertex : IVertexBase
    {
        public readonly List<Triangle> Triangles;
        public readonly List<TVertex> Vertices;
        public SharpDX.Direct3D11.Buffer VertexBuffer { get; private set; }
        public SharpDX.Direct3D11.Buffer IndexBuffer { get; private set; }
        public Vector3 AABBMin { get; private set; }
        public Vector3 AABBMax { get; private set; }
        public Vector3 AABBCenter => AABBMin + AABBHalfSize;

        public Vector3 AABBHalfSize => (AABBMax - AABBMin) * 0.5f;

        // Creates a new blank mesh.

        // Creates a blank mesh with the given data.
        public Mesh(Device device, List<Triangle> triangles, List<TVertex> vertices)
        {
            Triangles = triangles;
            Vertices = vertices;
            RebuildBuffer(device);
        }
        public void RebuildBuffer(Device device)
        {
            // Dispose all the old stuff
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            if (Triangles.Count == 0 || Vertices.Count == 0) return; // Why build and empty buffer?

            // Reset the AABB
            if (Vertices.Count == 0)
            {
                AABBMin = Vector3.Zero;
                AABBMax = Vector3.Zero;
            }
            else
            {
                AABBMin = Vertices[0].Position;
                AABBMax = Vertices[0].Position;
            }

            // Update the AABB
            float minx = AABBMin.X;
            float miny = AABBMin.Y;
            float minz = AABBMin.Z;
            float maxx = AABBMax.X;
            float maxy = AABBMax.Y;
            float maxz = AABBMax.Z;
            foreach (TVertex v in Vertices)
            {
                Vector3 pos = v.Position;
                minx = pos.X < minx ? pos.X : minx;
                miny = pos.Y < miny ? pos.Y : miny;
                minz = pos.Z < minz ? pos.Z : minz;
                maxx = pos.X > maxx ? pos.X : maxx;
                maxy = pos.Y > maxy ? pos.Y : maxy;
                maxz = pos.Z > maxz ? pos.Z : maxz;
            }
            AABBMin = new Vector3(minx, miny, minz);
            AABBMax = new Vector3(maxx, maxy, maxz);

            int floatsPerVertex = TVertex.Stride / 4;
            int numFloats = floatsPerVertex * Vertices.Count;
            float[] vertexdata = new float[numFloats];
            Span<float> vertexDataSpan = vertexdata.AsSpan();
            for (int vertIdx = 0, floatIdx = 0; vertIdx < Vertices.Count; vertIdx++, floatIdx += floatsPerVertex)
            {
                Vertices[vertIdx].ToFloats(vertexDataSpan[floatIdx..]);
            }

            // Create and populate the vertex and index buffers
            VertexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.VertexBuffer, vertexdata);
            IndexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.IndexBuffer, Triangles.ToArray());
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
        public Vector3 Position => _position;
        private readonly Vector3 _position;
        public Vector3 Normal;
        public Vector2 UV;

        public WorldVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _position = position;
            Normal = normal;
            UV = uv;
        }



        public void ToFloats(Span<float> dest) => this.AsSpanOf<WorldVertex, float>().CopyTo(dest);

        public static InputElement[] InputElements =>
        [
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0)
        ];

        public static unsafe int Stride => sizeof(Vector3) + sizeof(Vector3) + sizeof(Vector2);

        public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Fixed4<Vector4> uvs)
        {
            return new WorldVertex(position, new Vector3(normal.X, normal.Y, normal.Z), new Vector2(uvs[0].X, uvs[0].Y));
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
        public Vector3 Position => new(position.X, position.Y, position.Z);

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
}
