using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class StaticMesh : ObjectBinary
    {
        public BoxSphereBounds Bounds;
        public UIndex BodySetup;
        public kDOPTree kDOPTreeME1ME2;
        public kDOPTreeCompact kDOPTreeME3UDK;
        public int InternalVersion;
        public StaticMeshRenderData[] LODModels;
        public uint unk2; //ME1
        public uint unk3; //ME1
        public uint unk4; //ME1
        public uint unk5; //ME1
        public uint unk6; //ME1
        public uint unk1; //ME2/3/UDK
        public Rotator ThumbnailAngle; //ME2/3/UDK
        public float ThumbnailDistance; //ME2/3/UDK
        public uint unk7; //ME2/3
        public string HighResSourceMeshName; //ME3/UDK
        public uint HighResSourceMeshCRC; //ME3/UDK
        public Guid LightingGuid; //ME3/UDK

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Bounds);
            sc.Serialize(ref BodySetup);
            if (sc.IsSaving)
            {
                if (sc.Game >= MEGame.ME3 && kDOPTreeME3UDK == null)
                {
                    kDOPTreeME3UDK = KDOPTreeBuilder.ToCompact(kDOPTreeME1ME2.Triangles, LODModels[0].PositionVertexBuffer.VertexData);
                }
                else if (sc.Game < MEGame.ME3 && kDOPTreeME1ME2 == null)
                {
                    //todo: need to convert kDOPTreeCompact to kDOPTree
                    throw new NotImplementedException("Cannot convert ME3 StaticMeshes to ME1 or ME2 format :(");
                }
            }

            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref kDOPTreeME3UDK);
            }
            else
            {
                sc.Serialize(ref kDOPTreeME1ME2);
            }

            if (sc.IsSaving)
            {
                //This will improve loading times by preventing the engine from rebuilding the mesh
                if (sc.Game == MEGame.ME1) InternalVersion = 15;
                if (sc.Game == MEGame.ME2) InternalVersion = 16;
                if (sc.Game == MEGame.ME3) InternalVersion = 18;
                if (sc.Game == MEGame.UDK) InternalVersion = 18;
            }
            sc.Serialize(ref InternalVersion);
            if (sc.Game == MEGame.UDK)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref LODModels, SCExt.Serialize);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref unk2);
                sc.Serialize(ref unk3);
                sc.Serialize(ref unk4);
                sc.Serialize(ref unk5);
                sc.Serialize(ref unk6);
            }
            else
            {
                sc.Serialize(ref unk1);
                sc.Serialize(ref ThumbnailAngle);
                sc.Serialize(ref ThumbnailDistance);
                if (sc.Game != MEGame.UDK)
                {
                    sc.Serialize(ref unk7);
                }
                if (sc.Game >= MEGame.ME3)
                {
                    sc.Serialize(ref HighResSourceMeshName);
                    sc.Serialize(ref HighResSourceMeshCRC);
                    sc.Serialize(ref LightingGuid);
                }
            }

            if (sc.IsLoading && sc.Game < MEGame.ME3)
            {
                LightingGuid = Guid.NewGuid();
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)> {(BodySetup, "BodySetup")};

            for (int i = 0; i < LODModels.Length; i++)
            {
                for (int j = 0; j < LODModels[i].Elements.Length; j++)
                {
                    uIndexes.Add((LODModels[i].Elements[j].Material, $"LODModels[{i}].Elements[{j}].Material"));
                }
            }

            return uIndexes;
        }

        public StructProperty GetCollisionMeshProperty(IMEPackage pcc)
        {
            if (pcc.isUExport(BodySetup))
            {
                ExportEntry rb_BodySetup = pcc.getUExport(BodySetup);
                return rb_BodySetup.GetProperty<StructProperty>("AggGeom");
            }
            return null;
        }
    }

    #region kDOPTree

    public class kDOPTree
    {
        public kDOPNode[] Nodes;
        public kDOPCollisionTriangle[] Triangles;
    }

    public class kDOPTreeCompact
    {
        public kDOP RootBound;
        public kDOPCompact[] Nodes;
        public kDOPCollisionTriangle[] Triangles;
    }

    public class kDOPNode
    {
        [StructLayout(LayoutKind.Explicit, Pack = 2, Size = 4)]
        public struct Union
        {
            [FieldOffset(0)]
            public ushort NumTriangles;
            [FieldOffset(2)]
            public ushort StartIndex;

            [FieldOffset(0)]
            public ushort LeftNode;
            [FieldOffset(2)]
            public ushort RightNode;
        }

        public kDOP BoundingVolume;
        public bool bIsLeaf;
        public Union u;

    }

    public class kDOP
    {
        public float[] Min = new float[3];
        public float[] Max = new float[3];
    }

    public class kDOPCompact
    {
        public byte[] Min = new byte[3];
        public byte[] Max = new byte[3];
    }

    public readonly struct kDOPCollisionTriangle
    {
        public readonly ushort Vertex1;
        public readonly ushort Vertex2;
        public readonly ushort Vertex3;
        public readonly ushort MaterialIndex;

        public kDOPCollisionTriangle(ushort vertex1, ushort vertex2, ushort vertex3, ushort materialIndex)
        {
            Vertex1 = vertex1;
            Vertex2 = vertex2;
            Vertex3 = vertex3;
            MaterialIndex = materialIndex;
        }
    }

    #endregion

    public class StaticMeshRenderData
    {
        public StaticMeshTriangle[] RawTriangles; //BulkData
        public StaticMeshElement[] Elements;
        public PositionVertexBuffer PositionVertexBuffer;
        public StaticMeshVertexBuffer VertexBuffer;
        public ColorVertexBuffer ColorVertexBuffer; //ME3/UDK
        public ExtrusionVertexBuffer ShadowExtrusionVertexBuffer; //not UDK
        public uint NumVertices;
        public ushort[] IndexBuffer; //BulkSerialize
        public ushort[] WireframeIndexBuffer; //BulkSerialize
        public MeshEdge[] Edges; //BulkSerialize //not UDK
        public byte[] ShadowTriangleDoubleSided; //not UDK
        public ushort[] unkBuffer; //UDK
        public uint unk1; //ME1
        public byte[] xmlFile; //ME1 BulkData
    }

    public class StaticMeshTriangle
    {
        public Vector3[] Vertices = new Vector3[3];
        public Vector2D[,] UVs = new Vector2D[3,8];
        public Color[] Colors = new Color[3];
        public int MaterialIndex;
        public int FragmentIndex; //ME3/UDK
        public uint SmoothingMask;
        public int NumUVs;
        public bool bExplicitNormals; //UDK
        public Vector3[] TangentX = new Vector3[3]; //ME3/UDK
        public Vector3[] TangentY = new Vector3[3]; //ME3/UDK
        public Vector3[] TangentZ = new Vector3[3]; //ME3/UDK
        public bool bOverrideTangentBasis; //ME3/UDK
    }

    public class StaticMeshElement
    {
        public UIndex Material;
        public bool EnableCollision;
        public bool OldEnableCollision;
        public bool bEnableShadowCasting;
        public uint FirstIndex;
        public uint NumTriangles;
        public uint MinVertexIndex;
        public uint MaxVertexIndex;
        public int MaterialIndex;
        public FragmentRange[] Fragments; //ME3/UDK
        //byte LoadPlatformData; //ME3/UDK, always false
    }

    public readonly struct FragmentRange
    {
        public readonly int BaseIndex;
        public readonly int NumPrimitives;

        public FragmentRange(int baseIndex, int numPrimitives)
        {
            BaseIndex = baseIndex;
            NumPrimitives = numPrimitives;
        }
    }

    public class PositionVertexBuffer
    {
        public uint Stride;
        public uint NumVertices;
        public uint unk; //ME3
        public Vector3[] VertexData; //BulkSerialize
    }

    public class StaticMeshVertexBuffer
    {
        public class StaticMeshFullVertex
        {
            public PackedNormal TangentX;
            public PackedNormal TangentZ;
            public Color Color; //ME1/2
            public Vector2D[] FullPrecisionUVs;
            public Vector2DHalf[] HalfPrecisionUVs;
        }

        public uint NumTexCoords;//NEVER CHANGE!
        public uint Stride;
        public uint NumVertices;
        public bool bUseFullPrecisionUVs;
        public uint unk; //ME3
        public StaticMeshFullVertex[] VertexData; //BulkSerialize
    }

    public class ColorVertexBuffer
    {
        public uint Stride;
        public uint NumVertices;
        public Color[] VertexData; //BulkSerialize
    }

    public class ExtrusionVertexBuffer
    {
        public uint Stride;
        public uint NumVertices;
        public float[] VertexData; //BulkSerialize
    }

    public class MeshEdge
    {
        public int[] Vertices = new int[2];
        public int[] Faces = new int[2];
    }

    public static class KDOPTreeBuilder
    {
        [DllImport("kDopTreeCompactor.dll")]
        static extern void Compact(int numTriangles, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] [In, Out] kDopBuildTriangle[] tris, 
                                  int numNodes, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] [In, Out] kDopCompressedNode[] nodes,
                                  ref kDopUnCompressedNode rootBound);

        [StructLayout(LayoutKind.Sequential, Size = 6)]
        struct kDopCompressedNode
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
            public byte[] Min;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
            public byte[] Max;

            public static implicit operator kDOPCompact(kDopCompressedNode node)
            {
                var result = new kDOPCompact();
                for (int i = 0; i < 3; i++)
                {
                    result.Min[i] = node.Min[i];
                    result.Max[i] = node.Max[i];
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        struct kDopUnCompressedNode
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4, SizeConst = 3)]
            public float[] Min;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4, SizeConst = 3)]
            public float[] Max;

            public static implicit operator kDOP(kDopUnCompressedNode node)
            {
                var result = new kDOP();
                for (int i = 0; i < 3; i++)
                {
                    result.Min[i] = node.Min[i];
                    result.Max[i] = node.Max[i];
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 12)]
        struct Vector
        {
            public float X;
            public float Y;
            public float Z;

            public static implicit operator Vector(Vector3 vec) => new Vector {X = vec.X, Y = vec.Y, Z = vec.Z};
        }

        [StructLayout(LayoutKind.Sequential, Size = 56)]
        struct kDopBuildTriangle
        {
            public ushort Vertex1;
            public ushort Vertex2;
            public ushort Vertex3;
            public ushort MaterialIndex;
            public Vector Centroid;
            public Vector V0;
            public Vector V1;
            public Vector V2;

            public kDopBuildTriangle(ushort i1, ushort i2, ushort i3, ushort matIndex, Vector3 v0, Vector3 v1, Vector3 v2)
            {
                Vertex1 = i1;
                Vertex2 = i2;
                Vertex3 = i3;
                MaterialIndex = matIndex;
                V0 = v0;
                V1 = v1;
                V2 = v2;
                Centroid = (v0 + v1 + v2) / 3f;
            }
            public kDopBuildTriangle(kDOPCollisionTriangle tri, Vector3 v0, Vector3 v1, Vector3 v2) 
                : this(tri.Vertex1, tri.Vertex2, tri.Vertex3, tri.MaterialIndex, v0, v1, v2){}

            public static implicit operator kDOPCollisionTriangle(kDopBuildTriangle buildTri) =>
                new kDOPCollisionTriangle(buildTri.Vertex1, buildTri.Vertex2, buildTri.Vertex3, buildTri.MaterialIndex);
        }

        public static kDOPTreeCompact ToCompact(kDOPCollisionTriangle[] oldTriangles, Vector3[] vertices)
        {
            var rootBound = new kDopUnCompressedNode
            {
                Min = new float[3],
                Max = new float[3]
            };
            for (int i = 0; i < 3; i++)
            {
                rootBound.Max[i] = float.MaxValue;
                rootBound.Min[i] = -float.MaxValue;
            }

            if (oldTriangles.IsEmpty())
            {
                return new kDOPTreeCompact
                {
                    RootBound = rootBound,
                    Nodes = new kDOPCompact[0],
                    Triangles = new kDOPCollisionTriangle[0]
                };
            }

            var buildTriangles = new kDopBuildTriangle[oldTriangles.Length];
            for (int i = 0; i < oldTriangles.Length; i++)
            {
                kDOPCollisionTriangle oldTri = oldTriangles[i];
                buildTriangles[i] = new kDopBuildTriangle(oldTri, vertices[oldTri.Vertex1], vertices[oldTri.Vertex2], vertices[oldTri.Vertex3]);
            }

            int numNodes = 0;
            if (buildTriangles.Length > 5)
            {
                numNodes = 1;
                while ((buildTriangles.Length + numNodes - 1) / numNodes > 10)
                {
                    numNodes *= 2;
                }
                numNodes = 2 * numNodes;
            }

            var nodes = new kDopCompressedNode[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                nodes[i].Min = new byte[3];
                nodes[i].Max = new byte[3];
                for (int j = 0; j < 3; j++)
                {
                    nodes[i].Min[j] = 0;
                    nodes[i].Max[j] = 0;
                }
            }

            Compact(buildTriangles.Length, buildTriangles, numNodes, nodes, ref rootBound);

            return new kDOPTreeCompact
            {
                RootBound = rootBound,
                Nodes = nodes.Select(node => (kDOPCompact)node).ToArray(),
                Triangles = buildTriangles.Select(tri => (kDOPCollisionTriangle)tri).ToArray()
            };
        }
    }

    public static class AggGeomBuilder
    {
        delegate void convexDecompCallback(uint vertsLength, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] double[] verts,
                                           uint trisLength, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] int[] tris);

        [DllImport("DecomposeSample.dll")]
        static extern void CreateConvexHull(int vertexCount, [MarshalAs(UnmanagedType.LPArray)] double[] vertices,
                                            int triangleCount, [MarshalAs(UnmanagedType.LPArray)] uint[] indices, 
                                            uint depth, double conservationThreshold, int maxVerts,
                                            [MarshalAs(UnmanagedType.FunctionPtr)] convexDecompCallback callback);

        public static StructProperty CreateAggGeom(ICollection<Vector3> vertexBuffer, ICollection<uint> indexBuffer, uint depth = 4, double conservationThreshold = 24, int maxVerts = 12)
        {
            double[] vertices = vertexBuffer.SelectMany(vert => vert.ToArray().Select(v => (double)v)).ToArray();
            uint[] indices = indexBuffer.ToArray();
            var convexElems = new ArrayProperty<StructProperty>("ConvexElems") { Reference = "KConvexElem" };

            #region Callback

            void DecompCallback(uint vertsLength, double[] verts, uint trisLength, int[] tris)
            {
                PropertyCollection props = new PropertyCollection();
                var convexElem = new StructProperty("KConvexElem", props);
                convexElems.Add(convexElem);

                Box box = new Box();

                //VertexData
                var vertexData = new ArrayProperty<StructProperty>("VertexData") {Reference = "Vector"};
                var vertexes = new Vector3[vertsLength / 3];
                for (int i = 0; i < vertsLength; i += 3)
                {
                    var vert = new Vector3((float)verts[i], (float)verts[i + 1], (float)verts[i + 2]);
                    vertexes[i / 3] = vert;
                    vertexData.Add(new StructProperty("Vector", true, new FloatProperty(vert.X, "X"), new FloatProperty(vert.Y, "Y"), new FloatProperty(vert.Z, "Z")));
                    box.Add(vert);
                }

                //PermutedVertexData
                int leftover = vertexes.Length % 4;
                int numPlanes = vertexes.Length / 4;
                var permutedVertexData = new ArrayProperty<StructProperty>("PermutedVertexData") {Reference = "Plane"};
                for (int i = 0; i < numPlanes; i++)
                {
                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vertexes[i * 4 + 3].X, "W"), new FloatProperty(vertexes[i * 4 + 0].X, "X"), new FloatProperty(vertexes[i * 4 + 1].X, "Y"), new FloatProperty(vertexes[i * 4 + 2].X, "Z")));
                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vertexes[i * 4 + 3].Y, "W"), new FloatProperty(vertexes[i * 4 + 0].Y, "X"), new FloatProperty(vertexes[i * 4 + 1].Y, "Y"), new FloatProperty(vertexes[i * 4 + 2].Y, "Z")));
                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vertexes[i * 4 + 3].Z, "W"), new FloatProperty(vertexes[i * 4 + 0].Z, "X"), new FloatProperty(vertexes[i * 4 + 1].Z, "Y"), new FloatProperty(vertexes[i * 4 + 2].Z, "Z")));
                }

                if (leftover > 0)
                {
                    Vector3 vec1 = vertexes[numPlanes * 4], vec2 = vec1, vec3 = vec1, vec4 = vec1;
                    switch (leftover)
                    {
                        case 3:
                            vec3 = vertexes[numPlanes * 4 + 2];
                            goto case 2; //fallthrough!
                        case 2:
                            vec2 = vertexes[numPlanes * 4 + 1];
                            break;
                    }

                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vec1.X, "W"), new FloatProperty(vec2.X, "X"), new FloatProperty(vec3.X, "Y"), new FloatProperty(vec4.X, "Z")));
                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vec1.Y, "W"), new FloatProperty(vec2.Y, "X"), new FloatProperty(vec3.Y, "Y"), new FloatProperty(vec4.Y, "Z")));
                    permutedVertexData.Add(new StructProperty("Plane", true, new FloatProperty(vec1.Z, "W"), new FloatProperty(vec2.Z, "X"), new FloatProperty(vec3.Z, "Y"), new FloatProperty(vec4.Z, "Z")));
                }

                //FaceTriData
                var faceTriData = new ArrayProperty<IntProperty>("FaceTriData");
                faceTriData.AddRange(tris.Select(idx => new IntProperty(idx)));


                var allEdges = new List<(int edge0, int edge1)>();
                var edgeDirs = new List<Vector3>();
                var allNormals = new List<Vector3>();
                var uniqueNormals = new List<Vector3>();
                var planes = new List<Plane>();
                for (int i = 0; i < trisLength; i += 3)
                {
                    int idx0 = tris[i], idx1 = tris[i + 1], idx2 = tris[i + 2];

                    if (!allEdges.Contains((idx0, idx1))) allEdges.Add((idx0, idx1));
                    if (!allEdges.Contains((idx1, idx2))) allEdges.Add((idx1, idx2));
                    if (!allEdges.Contains((idx2, idx0))) allEdges.Add((idx2, idx0));

                    Vector3 vert0 = vertexes[idx0];
                    Vector3 vert1 = vertexes[idx1];
                    Vector3 vert2 = vertexes[idx2];

                    Vector3 normal = Vector3.Normalize(Vector3.Cross(vert2 - vert0, vert1 - vert0));
                    allNormals.Add(normal);
                    AddVec(normal, uniqueNormals);
                    AddPlane(new Plane(vert0, normal));
                }

                //FacePlaneData
                var facePlaneData = new ArrayProperty<StructProperty>("FacePlaneData") {Reference = "Plane"};
                foreach (Plane plane in planes)
                {
                    facePlaneData.Add(new StructProperty("Plane", true, new FloatProperty(plane.D, "W"), new FloatProperty(plane.Normal.X, "X"), new FloatProperty(plane.Normal.Y, "Y"), new FloatProperty(plane.Normal.Z, "Z")));
                }

                //FaceNormalDirections
                var faceNormalDirections = new ArrayProperty<StructProperty>("FaceNormalDirections") {Reference = "Vector"};
                foreach (Vector3 normal in uniqueNormals)
                {
                    faceNormalDirections.Add(new StructProperty("Vector", true, new FloatProperty(normal.X, "X"), new FloatProperty(normal.Y, "Y"), new FloatProperty(normal.Z, "Z")));
                }


                //EdgeDirections
                var edgeDirections = new ArrayProperty<StructProperty>("EdgeDirections") {Reference = "Vector"};
                foreach ((int edge0, int edge1) in allEdges)
                {
                    int triIdx0 = -1, triIdx1 = -1;
                    for (int i = 0; i < trisLength; i += 3)
                    {
                        int idx0 = tris[i], idx1 = tris[i + 1], idx2 = tris[i + 2];
                        if (idx0 == edge0 && idx1 == edge1 || idx0 == edge0 && idx2 == edge1 || idx1 == edge0 && idx0 == edge1 || idx1 == edge0 && idx2 == edge1 || idx2 == edge0 && idx0 == edge1 || idx2 == edge0 && idx1 == edge1)
                        {
                            if (triIdx0 == -1)
                            {
                                triIdx0 = i / 3;
                            }
                            else if (triIdx1 == -1)
                            {
                                triIdx1 = i / 3;
                            }
                        }
                    }

                    if (triIdx0 != -1 && triIdx1 != -1 && Vector3.Dot(allNormals[triIdx0], allNormals[triIdx1]) < 1f - 0.0003)
                    {
                        AddVec(Vector3.Normalize(vertexes[edge0] - vertexes[edge1]), edgeDirs);
                    }
                }

                foreach (Vector3 edgeDir in edgeDirs)
                {
                    edgeDirections.Add(new StructProperty("Vector", true, new FloatProperty(edgeDir.X, "X"), new FloatProperty(edgeDir.Y, "Y"), new FloatProperty(edgeDir.Z, "Z")));
                }

                //ElemBox
                var elemBox = new StructProperty("Box", true, 
                                                 new StructProperty("Vector", true, 
                                                                    new FloatProperty(box.Min.X, "X"), 
                                                                    new FloatProperty(box.Min.Y, "Y"), 
                                                                    new FloatProperty(box.Min.Z, "Z")) {Name = "Min"}, 
                                                 new StructProperty("Vector", true, 
                                                                    new FloatProperty(box.Max.X, "X"), 
                                                                    new FloatProperty(box.Max.Y, "Y"), 
                                                                    new FloatProperty(box.Max.Z, "Z")) {Name = "Max"}, 
                                                 new ByteProperty(box.IsValid, "IsValid")
                                                 ) { Name = "ElemBox" };


                props.Add(vertexData);
                props.Add(permutedVertexData);
                props.Add(faceTriData);
                props.Add(edgeDirections);
                props.Add(faceNormalDirections);
                props.Add(facePlaneData);
                props.Add(elemBox);
                props.Add(new NoneProperty());

                void AddVec(Vector3 vec, List<Vector3> vecs)
                {
                    foreach (Vector3 uniqueVec in vecs)
                    {
                        float dot = Math.Abs(Vector3.Dot(uniqueVec, vec));
                        if (Math.Abs(dot - 1) < 0.0003)
                        {
                            return;
                        }
                    }

                    vecs.Add(vec);
                }

                void AddPlane(Plane plane)
                {
                    foreach (Plane uniquePlane in planes)
                    {
                        float dot = Vector3.Dot(uniquePlane.Normal, plane.Normal);
                        if (Math.Abs(dot - 1) < 0.0003 && Math.Abs(uniquePlane.D - plane.D) < 0.1)
                        {
                            return;
                        }
                    }

                    planes.Add(plane);
                }
            }

            #endregion

            CreateConvexHull(vertexBuffer.Count, vertices, indices.Length / 3, indices, depth, conservationThreshold, maxVerts, DecompCallback);

            return new StructProperty("KAggregateGeom", new PropertyCollection
            {
                convexElems,
                new NoneProperty()
            }, "AggGeom");
        }
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref MeshEdge edge)
        {
            if (sc.IsLoading)
            {
                edge = new MeshEdge();
            }

            for (int i = 0; i < 2; i++)
            {
                sc.Serialize(ref edge.Vertices[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                sc.Serialize(ref edge.Faces[i]);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref ExtrusionVertexBuffer vBuff)
        {
            if (sc.IsLoading)
            {
                vBuff = new ExtrusionVertexBuffer();
            }

            sc.Serialize(ref vBuff.Stride);
            sc.Serialize(ref vBuff.NumVertices);
            int elementsize = 4;
            sc.Serialize(ref elementsize);
            sc.Serialize(ref vBuff.VertexData, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref ColorVertexBuffer buff)
        {
            if (sc.IsLoading)
            {
                buff = new ColorVertexBuffer();
            }

            if (sc.IsSaving)
            {
                buff.Stride = buff.NumVertices > 0 ? 4u : 0u;
            }
            sc.Serialize(ref buff.Stride);
            sc.Serialize(ref buff.NumVertices);
            if (buff.NumVertices > 0)
            {
                int elementsize = 4;
                sc.Serialize(ref elementsize);
                sc.Serialize(ref buff.VertexData, Serialize);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticMeshVertexBuffer buff)
        {
            uint elementSize;
            if (sc.IsLoading)
            {
                buff = new StaticMeshVertexBuffer();
            }
            else
            {
                elementSize = 8u + buff.NumTexCoords * (buff.bUseFullPrecisionUVs ? 8u : 4u);
                if (sc.Game < MEGame.ME3)
                {
                    elementSize += 4;
                }

                buff.Stride = elementSize;
            }
            sc.Serialize(ref buff.NumTexCoords);
            sc.Serialize(ref buff.Stride);
            sc.Serialize(ref buff.NumVertices);
            sc.Serialize(ref buff.bUseFullPrecisionUVs);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref buff.unk);
            }
            elementSize = buff.Stride;
            sc.Serialize(ref elementSize);
            int count = buff.VertexData?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                buff.VertexData = new StaticMeshVertexBuffer.StaticMeshFullVertex[count];
            }

            for (int i = 0; i < count; i++)
            {
                if (sc.IsLoading)
                {
                    buff.VertexData[i] = new StaticMeshVertexBuffer.StaticMeshFullVertex();
                }
                sc.Serialize(ref buff.VertexData[i].TangentX);
                sc.Serialize(ref buff.VertexData[i].TangentZ);
                if (sc.Game < MEGame.ME3)
                {
                    sc.Serialize(ref buff.VertexData[i].Color);
                }

                if (buff.bUseFullPrecisionUVs)
                {
                    if (buff.VertexData[i].FullPrecisionUVs == null)
                    {
                        buff.VertexData[i].FullPrecisionUVs = sc.IsLoading
                            ? new Vector2D[buff.NumTexCoords]
                            //bUseFullPrecisionUVs was changed, copy data from the other one
                            : Array.ConvertAll(buff.VertexData[i].HalfPrecisionUVs, v2dHalf => new Vector2D(v2dHalf.X, v2dHalf.Y));
                    }
                    for (int j = 0; j < buff.NumTexCoords; j++)
                    {
                        sc.Serialize(ref buff.VertexData[i].FullPrecisionUVs[j]);
                    }
                }
                else
                {
                    if (buff.VertexData[i].HalfPrecisionUVs == null)
                    {
                        buff.VertexData[i].HalfPrecisionUVs = sc.IsLoading
                            ? new Vector2DHalf[buff.NumTexCoords]
                            //bUseFullPrecisionUVs was changed, copy data from the other one
                            : Array.ConvertAll(buff.VertexData[i].FullPrecisionUVs, v2d => new Vector2DHalf(v2d.X, v2d.Y));
                    }
                    for (int j = 0; j < buff.NumTexCoords; j++)
                    {
                        sc.Serialize(ref buff.VertexData[i].HalfPrecisionUVs[j]);
                    }
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref PositionVertexBuffer buff)
        {
            if (sc.IsLoading)
            {
                buff = new PositionVertexBuffer();
            }

            sc.Serialize(ref buff.Stride);
            sc.Serialize(ref buff.NumVertices);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref buff.unk);
            }
            int elementsize = 12;
            sc.Serialize(ref elementsize);
            sc.Serialize(ref buff.VertexData, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref FragmentRange fRange)
        {
            if (sc.IsLoading)
            {
                fRange = new FragmentRange(sc.ms.ReadInt32(), sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.WriteInt32(fRange.BaseIndex);
                sc.ms.WriteInt32(fRange.NumPrimitives);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticMeshElement meshElement)
        {
            if (sc.IsLoading)
            {
                meshElement = new StaticMeshElement();
            }

            sc.Serialize(ref meshElement.Material);
            sc.Serialize(ref meshElement.EnableCollision);
            sc.Serialize(ref meshElement.OldEnableCollision);
            sc.Serialize(ref meshElement.bEnableShadowCasting);
            sc.Serialize(ref meshElement.FirstIndex);
            sc.Serialize(ref meshElement.NumTriangles);
            sc.Serialize(ref meshElement.MinVertexIndex);
            sc.Serialize(ref meshElement.MaxVertexIndex);
            sc.Serialize(ref meshElement.MaterialIndex);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref meshElement.Fragments, Serialize);
                byte dummy = 0;
                sc.Serialize(ref dummy);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticMeshTriangle tri)
        {
            if (sc.IsLoading)
            {
                tri = new StaticMeshTriangle();
            }

            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref tri.Vertices[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    sc.Serialize(ref tri.UVs[i, j]);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref tri.Colors[i]);
            }
            sc.Serialize(ref tri.MaterialIndex);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref tri.FragmentIndex);
            }
            sc.Serialize(ref tri.SmoothingMask);
            sc.Serialize(ref tri.NumUVs);
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref tri.bExplicitNormals);
            }
            if (sc.Game >= MEGame.ME3)
            {
                for (int i = 0; i < 3; i++)
                {
                    sc.Serialize(ref tri.TangentX[i]);
                }
                for (int i = 0; i < 3; i++)
                {
                    sc.Serialize(ref tri.TangentY[i]);
                }
                for (int i = 0; i < 3; i++)
                {
                    sc.Serialize(ref tri.TangentZ[i]);
                }
                sc.Serialize(ref tri.bOverrideTangentBasis);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticMeshRenderData data)
        {
            if (sc.IsLoading)
            {
                data = new StaticMeshRenderData();
            }

            sc.SerializeBulkData(ref data.RawTriangles, Serialize);
            sc.Serialize(ref data.Elements, Serialize);
            sc.Serialize(ref data.PositionVertexBuffer);
            if (sc.IsSaving)
            {
                if (sc.Game >= MEGame.ME3 && data.ColorVertexBuffer == null)
                {
                    //this was read in from ME1 or ME2, we need to seperate out the color data
                    data.ColorVertexBuffer = new ColorVertexBuffer
                    {
                        VertexData = data.VertexBuffer.VertexData.Select(vertex => vertex.Color).ToArray(),
                        NumVertices = (uint)data.VertexBuffer.VertexData.Length,
                        Stride = 4
                    };
                }
                else if (sc.Game < MEGame.ME3 && data.ColorVertexBuffer != null)
                {
                    //this was read in from ME3 or UDK, we need to integrate the color data
                    for (int i = data.VertexBuffer.VertexData.Length - 1; i >= 0; i--)
                    {
                        data.VertexBuffer.VertexData[i].Color = data.ColorVertexBuffer.VertexData[i];
                    }
                }
            }
            sc.Serialize(ref data.VertexBuffer);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref data.ColorVertexBuffer);
            }

            if (sc.Game < MEGame.UDK)
            {
                sc.Serialize(ref data.ShadowExtrusionVertexBuffer);
            }
            else if (sc.IsLoading)
            {
                data.ShadowExtrusionVertexBuffer = new ExtrusionVertexBuffer
                {
                    Stride = 4,
                    VertexData = Array.Empty<float>()
                };
            }
            sc.Serialize(ref data.NumVertices);
            int elementSize = 2;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref data.IndexBuffer, Serialize);
            elementSize = 2;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref data.WireframeIndexBuffer, Serialize);
            if (sc.Game != MEGame.UDK)
            {
                elementSize = 16;
                sc.Serialize(ref elementSize);
                sc.Serialize(ref data.Edges, Serialize);
                sc.Serialize(ref data.ShadowTriangleDoubleSided, Serialize);
            }
            else if (sc.IsLoading)
            {
                data.Edges = Array.Empty<MeshEdge>();
                data.ShadowTriangleDoubleSided = Array.Empty<byte>();
            }
            if (sc.Game == MEGame.UDK)
            {
                sc.BulkSerialize(ref data.unkBuffer, Serialize, 2);
            }
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref data.unk1);

                int bulkDataFlags = 0;
                sc.Serialize(ref bulkDataFlags);
                int byteCount = data.xmlFile?.Length ?? 0;
                sc.Serialize(ref byteCount);
                sc.Serialize(ref byteCount);
                int xmlOffsetInFile = sc.FileOffset + 4;
                sc.Serialize(ref xmlOffsetInFile);
                sc.Serialize(ref data.xmlFile, byteCount);
            }
            else if (sc.IsLoading)
            {
                data.xmlFile = Array.Empty<byte>();
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOPCollisionTriangle kTri)
        {
            if (sc.IsLoading)
            {
                kTri = new kDOPCollisionTriangle(sc.ms.ReadUInt16(), sc.ms.ReadUInt16(), sc.ms.ReadUInt16(), sc.ms.ReadUInt16());
            }
            else
            {
                sc.ms.WriteUInt16(kTri.Vertex1);
                sc.ms.WriteUInt16(kTri.Vertex2);
                sc.ms.WriteUInt16(kTri.Vertex3);
                sc.ms.WriteUInt16(kTri.MaterialIndex);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOPCompact kDop)
        {
            if (sc.IsLoading)
            {
                kDop = new kDOPCompact();
            }

            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref kDop.Min[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref kDop.Max[i]);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOP kDop)
        {
            if (sc.IsLoading)
            {
                kDop = new kDOP();
            }

            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref kDop.Min[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                sc.Serialize(ref kDop.Max[i]);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOPNode kDopNode)
        {
            if (sc.IsLoading)
            {
                kDopNode = new kDOPNode();
            }

            sc.Serialize(ref kDopNode.BoundingVolume);
            sc.Serialize(ref kDopNode.bIsLeaf);
            //depending on bIsLeaf, next two are either LeftNode and RightNode, or NumTriangles and StartIndex.
            //But since it's a union, they share space in memory, so it doesn't matter for serialization purposes
            sc.Serialize(ref kDopNode.u.LeftNode);
            sc.Serialize(ref kDopNode.u.RightNode);
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOPTreeCompact kDopTree)
        {
            if (sc.IsLoading)
            {
                kDopTree = new kDOPTreeCompact();
            }

            sc.Serialize(ref kDopTree.RootBound);
            int elementSize = 6;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref kDopTree.Nodes, Serialize);
            elementSize = 8;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref kDopTree.Triangles, Serialize);
        }
        public static void Serialize(this SerializingContainer2 sc, ref kDOPTree kDopTree)
        {
            if (sc.IsLoading)
            {
                kDopTree = new kDOPTree();
            }

            int elementSize = 32;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref kDopTree.Nodes, Serialize);
            elementSize = 8;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref kDopTree.Triangles, Serialize);
        }
    }
}
