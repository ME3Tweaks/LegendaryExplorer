using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
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
    public interface IVertexBase
    {
        public Vector3 Position { get; }

        public float[] ToFloats();

        public static abstract InputElement[] InputElements { get; }

        public static abstract int VertexLength { get; }

        public static abstract IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2[] uvs);
    }

    public class WorldVertex(Vector3 position, Vector3 normal, Vector2 uv) : IVertexBase
    {
        public Vector3 Normal = normal;
        public Vector2 UV = uv;

        public Vector3 Position => position;


        public float[] ToFloats() => [Position.X, Position.Y, Position.Z, Normal.X, Normal.Y, Normal.Z, UV.X, UV.Y];

        public static InputElement[] InputElements =>
        [
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 0)
        ];

        public static int VertexLength => 4 * 3 + 4 * 3 + 4 * 2;

        public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2[] uvs)
        {
            return new WorldVertex(position, new Vector3(normal.X, normal.Y, normal.Z), uvs[0]);
        }
    }

    public class LEVertex : IVertexBase
    {
        private Vector4 position;
        private Vector3 tangent;
        private Vector4 normal;
        private Vector4 color;
        private Vector2[] uvs;
        public Vector3 Position => new(position.X, position.Y, position.Z);

        public int NumTexCoords => uvs.Length;


        private LEVertex(Vector4 position, Vector3 tangent, Vector4 normal, Vector4 color, Vector2[] uvs)
        {
            this.position = position;
            this.tangent = tangent;
            this.normal = normal;
            this.color = color;
            this.uvs = uvs;
        }

        public float[] ToFloats()
        {
            float[] floats = new float[15 + 4 * uvs.Length - 2];
            floats[0] = position.X;
            floats[1] = position.Y;
            floats[2] = position.Z;
            floats[3] = position.W;

            floats[4] = tangent.X;
            floats[5] = tangent.Y;
            floats[6] = tangent.Z;

            floats[7] = normal.X;
            floats[8] = normal.Y;
            floats[9] = normal.Z;
            floats[10] = normal.W;

            floats[11] = color.X;
            floats[12] = color.Y;
            floats[13] = color.Z;
            floats[14] = color.W;

            int floatIndex = 15;
            for (int i = 0; i < uvs.Length - 1; i++)
            {
                Vector2 uv = uvs[i];
                floats[floatIndex++] = uv.X;
                floats[floatIndex++] = uv.Y;
                floats[floatIndex++] = 0;
                floats[floatIndex++] = 0;
            }
            floats[floatIndex++] = uvs[^1].X;
            floats[floatIndex] = uvs[^1].Y;

            return floats;
        }

        public static IVertexBase Create(Vector3 position, Vector3 tangent, Vector4 normal, Vector2[] uvs)
        {
            return new LEVertex(new Vector4(position, 1), tangent, normal, Vector4.Zero, uvs);
        }
        public unsafe int VertexLength => sizeof(Vector4) + sizeof(Vector3) + sizeof(Vector4) + sizeof(Vector4) + sizeof(Vector4) * (uvs.Length - 1) + sizeof(Vector2);


        private static readonly InputElement[] CommonInputElements =
        [
            new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
            new InputElement("TANGENT", 0, Format.R32G32B32_Float, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32A32_Float, 0),
            new InputElement("COLOR", 1, Format.R32G32B32A32_Float, 0),
        ];

        static InputElement[] IVertexBase.InputElements => throw new NotSupportedException($"NumTexCoords must be specified for {nameof(LEVertex)}");
        public static InputElement[] InputElements(int numTexCoords)
        {
            var inputElements = new InputElement[4 + numTexCoords];
            CommonInputElements.CopyTo(inputElements, 0);
            int i = 0;
            for (; i < numTexCoords - 1; i++)
            {
                inputElements[4 + i] = new InputElement("TEXCOORD", i, Format.R32G32B32A32_Float, 0);
            }
            inputElements[4 + i] = new InputElement("TEXCOORD", i, Format.R32G32_Float, 0);
            return inputElements;
        }

        static int IVertexBase.VertexLength => throw new NotSupportedException($"NumTexCoords must be specified for {nameof(LEVertex)}");

    }
}
