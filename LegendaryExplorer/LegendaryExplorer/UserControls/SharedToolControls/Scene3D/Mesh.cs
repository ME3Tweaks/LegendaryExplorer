using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;

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
                minx = v.Position.X < minx ? v.Position.X : minx;
                miny = v.Position.Y < miny ? v.Position.Y : miny;
                minz = v.Position.Z < minz ? v.Position.Z : minz;
                maxx = v.Position.X > maxx ? v.Position.X : maxx;
                maxy = v.Position.Y > maxy ? v.Position.Y : maxy;
                maxz = v.Position.Z > maxz ? v.Position.Z : maxz;
            }
            AABBMin = new Vector3(minx, miny, minz);
            AABBMax = new Vector3(maxx, maxy, maxz);

            // Build the list of floats for the vertex buffer
            var vertexdata = new List<float>();
            foreach (TVertex v in Vertices)
            {
                vertexdata.AddRange(v.ToFloats());
            }

            // Create and populate the vertex and index buffers
            VertexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.VertexBuffer, vertexdata.ToArray());
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
    //[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public interface IVertexBase
    {
        public Vector3 Position { get; }

        public float[] ToFloats();

        public static abstract InputElement[] InputElements { get; }

        public static abstract int VertexLength { get; } // four bytes for each of the three channels

        public static abstract IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2 uv);
    }

    /// <summary>
    /// A simple vertex for testing purposes. 
    /// </summary>
    //[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    //public class PositionColorVertex(Vector3 position, Vector3 color) : IVertexBase
    //{
    //    public Vector3 Position => position;

    //    public float[] ToFloats() => [Position.X, Position.Y, Position.Z, color.X, color.Y, color.Z];

    //    public static InputElement[] InputElements => 
    //    [
    //        new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0), 
    //        new InputElement("COLOR", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0)
    //    ];

    //    public static int VertexLength => 4 * 3 + 4 * 3; // four bytes for each of three channels for both position and color.
    //}

    public class WorldVertex(Vector3 position, Vector3 normal, Vector2 uv) : IVertexBase
    {
        public Vector3 Position => position;
        public Vector3 Normal = normal;
        public Vector2 UV = uv;

        public WorldVertex() : this(Vector3.Zero, new Vector3(), new Vector2())
        {
        }

        public float[] ToFloats() => [Position.X, Position.Y, Position.Z, Normal.X, Normal.Y, Normal.Z, UV.X, UV.Y];

        public static InputElement[] InputElements =>
        [
            new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0),
            new InputElement("NORMAL", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0),
            new InputElement("TEXCOORD", 0, SharpDX.DXGI.Format.R32G32_Float, 0)
        ];

        public static int VertexLength => 4 * 3 + 4 * 3 + 4 * 2;
        public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2 uv)
        {
            return new WorldVertex(position, new Vector3(normal.X, normal.Y, normal.Z), uv);
        }
    }

    public class LEVertex() : IVertexBase
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Elems 
        {
            public Vector4 Position;
            public Vector3 Tangent;
            public Vector4 Normal;
            public Vector4 Color;
            public Vector2 UV;
        }

        public Vector3 Position => new(Elements.Position.X, Elements.Position.Y, Elements.Position.Z);

        private Elems Elements;

        public LEVertex(Vector4 position, Vector3 tangent, Vector4 normal, Vector4 color, Vector2 uv) : this()
        {
            Elements.Position = position;
            Elements.Tangent = tangent;
            Elements.Normal = normal;
            Elements.Color = color;
            Elements.UV = uv;
        }

        public float[] ToFloats() => MemoryMarshal.Cast<Elems, float>(new Span<Elems>(ref Elements)).ToArray();

        public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2 uv)
        {
            return new LEVertex(new Vector4(position, 1), tangent, normal, Vector4.Zero, uv);
        }

        public static InputElement[] InputElements =>
        [
            new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 0),
            new InputElement("TANGENT", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0),
            new InputElement("NORMAL", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 0),
            new InputElement("COLOR", 1, SharpDX.DXGI.Format.R32G32B32A32_Float, 0),
            new InputElement("TEXCOORD", 0, SharpDX.DXGI.Format.R32G32_Float, 0)
        ];

        public static unsafe int VertexLength => sizeof(Elems);
    }
}
