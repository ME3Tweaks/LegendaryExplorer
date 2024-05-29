using System.Collections.Generic;
using System.Numerics;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    // This class exists because typing Mesh<WorldVertex> is a pain.
    public class WorldMesh : Mesh<WorldVertex>
    {
        public WorldMesh(Device Device) : base(Device)
        {
        }

        public WorldMesh(Device Device, List<Triangle> Triangles, List<WorldVertex> Vertices) : base(Device, Triangles, Vertices)
        {
        }
    }

    public class Mesh<Vertex> : System.IDisposable where Vertex : VertexBase
    {
        public List<Triangle> Triangles;
        public List<Vertex> Vertices;
        public SharpDX.Direct3D11.Buffer VertexBuffer { get; private set; } = null;
        public SharpDX.Direct3D11.Buffer IndexBuffer { get; private set; } = null;
        public Vector3 AABBMin { get; private set; }
        public Vector3 AABBMax { get; private set; }
        public Vector3 AABBCenter => AABBMin + AABBHalfSize;

        public Vector3 AABBHalfSize => (AABBMax - AABBMin) * 0.5f;

        // Creates a new blank mesh.
        public Mesh(SharpDX.Direct3D11.Device Device)
        {
            Triangles = new List<Triangle>();
            Vertices = new List<Vertex>();
            RebuildBuffer(Device);
        }

        // Creates a blank mesh with the given data.
        public Mesh(SharpDX.Direct3D11.Device Device, List<Triangle> Triangles, List<Vertex> Vertices)
        {
            this.Triangles = Triangles;
            this.Vertices = Vertices;
            RebuildBuffer(Device);
        }
        public void RebuildBuffer(SharpDX.Direct3D11.Device Device)
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
            foreach (Vertex v in Vertices)
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
            foreach (Vertex v in Vertices)
            {
                foreach (float f in v.ToFloats())
                {
                    vertexdata.Add(f);
                }
            }

            // Create and populate the vertex and index buffers
            VertexBuffer = SharpDX.Direct3D11.Buffer.Create<float>(Device, BindFlags.VertexBuffer, vertexdata.ToArray());
            IndexBuffer = SharpDX.Direct3D11.Buffer.Create<Triangle>(Device, BindFlags.IndexBuffer, Triangles.ToArray());
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
    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct Triangle
    {
        public uint Vertex1;
        public uint Vertex2;
        public uint Vertex3;

        public Triangle(uint Vertex1, uint Vertex2, uint Vertex3)
        {
            this.Vertex1 = Vertex1;
            this.Vertex2 = Vertex2;
            this.Vertex3 = Vertex3;
        }
    }

    /// <summary>
    /// The base class for vertices that can be rendered. They must have a position. This is necessary for builtin AABB computation as well.
    /// </summary>
    //[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public class VertexBase
    {
        public Vector3 Position { get; protected set; }

        public VertexBase(Vector3 Position)
        {
            this.Position = Position;
        }

        public virtual float[] ToFloats() => new[] { Position.X, Position.Y, Position.Z };

        public virtual InputElement[] InputElements => new[] { new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0) };

        public virtual int VertexLength => 4 * 3; // four bytes for each of the three channels
    }

    /// <summary>
    /// A simple vertex for testing purposes. 
    /// </summary>
    //[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public class PositionColorVertex : VertexBase
    {
        readonly Vector3 Color;

        public PositionColorVertex(Vector3 Position, Vector3 Color) : base(Position)
        {
            this.Color = Color;
        }

        public override float[] ToFloats() => new[] { Position.X, Position.Y, Position.Z, Color.X, Color.Y, Color.Z };

        public override InputElement[] InputElements
        {
            get
            {
                return new[] { new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0), new InputElement("COLOR", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0) };
            }
        }

        public override int VertexLength => 4 * 3 + 4 * 3; // four bytes for each of three channels for both position and color.
    }

    public class WorldVertex : VertexBase
    {
        public Vector3 Normal;
        public Vector2 UV;

        public WorldVertex() : base(Vector3.Zero)
        {
        }

        public WorldVertex(Vector3 Position, Vector3 Normal, Vector2 UV) : base(Position)
        {
            this.Normal = Normal;
            this.UV = UV;
        }

        public override float[] ToFloats() => new[] { Position.X, Position.Y, Position.Z, Normal.X, Normal.Y, Normal.Z, UV.X, UV.Y };

        public override InputElement[] InputElements =>
            new[]
            {
                new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0),
                new InputElement("NORMAL", 0, SharpDX.DXGI.Format.R32G32B32_Float, 0),
                new InputElement("TEXCOORD", 0, SharpDX.DXGI.Format.R32G32_Float, 0)
            };

        public override int VertexLength => 4 * 3 + 4 * 3 + 4 * 2;
    }
}
