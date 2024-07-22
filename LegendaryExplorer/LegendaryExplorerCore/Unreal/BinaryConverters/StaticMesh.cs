using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class StaticMesh : ObjectBinary
    {
        public BoxSphereBounds Bounds;
        public UIndex BodySetup;
        public kDOPTree kDOPTreeME1ME2;
        public kDOPTreeCompact kDOPTreeME3UDKLE;
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
        public uint unk8; //UDK
        public float[] unkFloats; //UDK
        public uint unk9; //UDK
        public uint unk10; //UDK
        public uint unk11; //UDK

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Bounds);
            sc.Serialize(ref BodySetup);
            if (sc.IsSaving)
            {
                if (sc.Game >= MEGame.ME3 && kDOPTreeME3UDKLE == null)
                {
                    kDOPTreeME3UDKLE = KDOPTreeBuilder.ToCompact(kDOPTreeME1ME2.Triangles, LODModels[0].PositionVertexBuffer.VertexData);
                }
                else if (sc.Game < MEGame.ME3 && kDOPTreeME1ME2 == null)
                {
                    //todo: need to convert kDOPTreeCompact to kDOPTree
                    throw new NotImplementedException("Cannot convert ME3 or LE StaticMeshes to ME1 or ME2 format :(");
                }
            }

            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref kDOPTreeME3UDKLE);
            }
            else
            {
                sc.Serialize(ref kDOPTreeME1ME2);
            }

            if (sc.IsSaving)
            {
                switch (sc.Game)
                {
                    //This will improve loading times by preventing the engine from rebuilding the mesh
                    case MEGame.ME1:
                        InternalVersion = 15;
                        break;
                    case MEGame.ME2:
                        InternalVersion = 16;
                        break;
                    case MEGame.ME3:
                    case MEGame.LE3:
                    case MEGame.LE2:
                    case MEGame.UDK:
                        InternalVersion = 18;
                        break;
                    case MEGame.LE1:
                        InternalVersion = 19;
                        break;
                }
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
                if (sc.IsSaving && sc.Game == MEGame.UDK)
                {
                    unk1 = 1; //no clue why, but UDK will crash if this isn't 1
                }
                sc.Serialize(ref unk1);
                sc.Serialize(ref ThumbnailAngle);
                sc.Serialize(ref ThumbnailDistance);
                if (sc.Game != MEGame.UDK)
                {
                    sc.Serialize(ref unk7);
                }
                if (sc.Game >= MEGame.ME3)
                {
                    if (HighResSourceMeshName != null)
                        sc.Serialize(ref HighResSourceMeshName);
                    else
                    {
                        // When porting ME1, ME2 to ME3 or LE
                        string blank = "";
                        sc.Serialize(ref blank);
                    }
                    sc.Serialize(ref HighResSourceMeshCRC);
                    sc.Serialize(ref LightingGuid);
                }
            }

            if (sc.IsLoading && sc.Game < MEGame.ME3)
            {
                LightingGuid = Guid.NewGuid();
            }
            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref unk8);
                sc.Serialize(ref unkFloats, SCExt.Serialize);
                sc.Serialize(ref unk9);
                sc.Serialize(ref unk10);
                sc.Serialize(ref unk11);
            }
            else if (sc.IsLoading)
            {
                unk8 = 1;
                unkFloats = Array.Empty<float>();
                unk9 = 1;
            }
        }

        public static StaticMesh Create()
        {
            return new()
            {
                Bounds = new BoxSphereBounds(),
                BodySetup = 0,
                kDOPTreeME3UDKLE = KDOPTreeBuilder.ToCompact(Array.Empty<kDOPCollisionTriangle>(), Array.Empty<Vector3>()),
                LODModels = Array.Empty<StaticMeshRenderData>(),
                HighResSourceMeshName = "",
                unkFloats = Array.Empty<float>()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            Unsafe.AsRef(in action).Invoke(ref BodySetup, nameof(BodySetup));
            for (int i = 0; i < LODModels.Length; i++)
            {
                for (int j = 0; j < LODModels[i].Elements.Length; j++)
                {
                    Unsafe.AsRef(in action).Invoke(ref LODModels[i].Elements[j].Material, $"LODModels[{i}].Elements[{j}].Material");
                }
            }
        }

        public StructProperty GetCollisionMeshProperty(IMEPackage pcc)
        {
            if (pcc.IsUExport(BodySetup))
            {
                ExportEntry rb_BodySetup = pcc.GetUExport(BodySetup);
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
        public Vector2[,] UVs = new Vector2[3, 8];
        public SharpDX.Color[] Colors = new SharpDX.Color[3];
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
            public SharpDX.Color Color; //ME1/2
            public Vector2[] FullPrecisionUVs;
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
        public SharpDX.Color[] VertexData; //BulkSerialize
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
            sc.Serialize(ref vBuff.VertexData, SCExt.Serialize);
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
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
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
                            ? new Vector2[buff.NumTexCoords]
                            //bUseFullPrecisionUVs was changed, copy data from the other one
                            : Array.ConvertAll(buff.VertexData[i].HalfPrecisionUVs, v2dHalf => new Vector2(v2dHalf.X, v2dHalf.Y));
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
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                sc.Serialize(ref buff.unk);
            }
            int elementsize = 12;
            sc.Serialize(ref elementsize);
            sc.Serialize(ref buff.VertexData);
        }
        public static void Serialize(this SerializingContainer2 sc, ref FragmentRange fRange)
        {
            if (sc.IsLoading)
            {
                fRange = new FragmentRange(sc.ms.ReadInt32(), sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.Writer.WriteInt32(fRange.BaseIndex);
                sc.ms.Writer.WriteInt32(fRange.NumPrimitives);
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
                    // Why is this written backwards?
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
            sc.Serialize(ref data.IndexBuffer);
            elementSize = 2;
            sc.Serialize(ref elementSize);
            sc.Serialize(ref data.WireframeIndexBuffer);
            if (sc.Game != MEGame.UDK)
            {
                elementSize = 16;
                sc.Serialize(ref elementSize);
                sc.Serialize(ref data.Edges, Serialize);
                sc.Serialize(ref data.ShadowTriangleDoubleSided);
            }
            else if (sc.IsLoading)
            {
                data.Edges = Array.Empty<MeshEdge>();
                data.ShadowTriangleDoubleSided = Array.Empty<byte>();
            }
            if (sc.Game == MEGame.UDK)
            {
                sc.BulkSerialize(ref data.unkBuffer, SCExt.Serialize, 2);
            }
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref data.unk1);

                int bulkDataFlags = 0;
                sc.Serialize(ref bulkDataFlags);
                int byteCount = data.xmlFile?.Length ?? 0;
                sc.Serialize(ref byteCount);
                sc.Serialize(ref byteCount);
                sc.SerializeFileOffset();
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
                sc.ms.Writer.WriteUInt16(kTri.Vertex1);
                sc.ms.Writer.WriteUInt16(kTri.Vertex2);
                sc.ms.Writer.WriteUInt16(kTri.Vertex3);
                sc.ms.Writer.WriteUInt16(kTri.MaterialIndex);
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