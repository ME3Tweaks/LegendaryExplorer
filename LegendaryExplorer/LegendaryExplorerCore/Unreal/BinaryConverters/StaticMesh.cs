using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Gammtek;
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

        protected override void Serialize(SerializingContainer sc)
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
            sc.Serialize(ref LODModels, sc.Serialize);
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
                sc.Serialize(ref unkFloats, sc.Serialize);
                sc.Serialize(ref unk9);
                sc.Serialize(ref unk10);
                sc.Serialize(ref unk11);
            }
            else if (sc.IsLoading)
            {
                unk8 = 1;
                unkFloats = [];
                unk9 = 1;
            }
        }

        public static StaticMesh Create()
        {
            return new()
            {
                Bounds = new BoxSphereBounds(),
                BodySetup = 0,
                kDOPTreeME3UDKLE = KDOPTreeBuilder.ToCompact([], []),
                LODModels = [],
                HighResSourceMeshName = "",
                unkFloats = []
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

        /// <summary>
        /// Returns the list of material uindexes on the top LOD of the mesh. For convenience only.
        /// </summary>
        /// <returns></returns>
        public UIndex[] GetMaterials()
        {
            if (LODModels.Length == 0)
                return [];

            return LODModels[0].Elements.Select(x => x.Material).ToArray();
        }

        /// <summary>
        /// Sets the material UIndexes on the top LOD of the mesh. For convenience only. You probably shouldn't use this for actual editing, this is used by LEX mesh preview.
        /// </summary>
        /// <param name="overlay">If null values should not be set</param>
        /// <returns></returns>
        public void SetMaterials(List<IEntry> materials, bool overlay = false)
        {
            if (LODModels.Length == 0)
                return;
            if (LODModels[0].Elements.Length != materials.Count)
                return; // Invalid length

            for (int i = 0; i < materials.Count; i++)
            {
                var mat = materials[i];
                if (mat != null || !overlay)
                {
                    LODModels[0].Elements[i].Material = mat?.UIndex ?? 0;
                }
            }
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
        public Fixed3<float> Min;
        public Fixed3<float> Max;
    }

    public class kDOPCompact
    {
        public Fixed3<byte> Min;
        public Fixed3<byte> Max;
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
        public ushort[] AdjacencyIndexBuffer; //UDK
        public uint unk1; //ME1
        public byte[] xmlFile; //ME1 BulkData
    }

    public class StaticMeshTriangle
    {
        public Fixed3<Vector3> Vertices;
        public Vector2[,] UVs = new Vector2[3, 8];
        public Fixed3<SharpDX.Color> Colors;
        public int MaterialIndex;
        public int FragmentIndex; //ME3/UDK
        public uint SmoothingMask;
        public int NumUVs;
        public bool bExplicitNormals; //UDK
        public Fixed3<Vector3> TangentX; //ME3/UDK
        public Fixed3<Vector3> TangentY; //ME3/UDK
        public Fixed3<Vector3> TangentZ; //ME3/UDK
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
        public Fixed2<int> Vertices;
        public Fixed2<int> Faces;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref MeshEdge edge)
        {
            if (IsLoading)
            {
                edge = new MeshEdge();
            }

            for (int i = 0; i < 2; i++)
            {
                Serialize(ref edge.Vertices[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                Serialize(ref edge.Faces[i]);
            }
        }
        public void Serialize(ref ExtrusionVertexBuffer vBuff)
        {
            if (IsLoading)
            {
                vBuff = new ExtrusionVertexBuffer();
            }

            Serialize(ref vBuff.Stride);
            Serialize(ref vBuff.NumVertices);
            int elementsize = 4;
            Serialize(ref elementsize);
            Serialize(ref vBuff.VertexData, Serialize);
        }
        public void Serialize(ref ColorVertexBuffer buff)
        {
            if (IsLoading)
            {
                buff = new ColorVertexBuffer();
            }

            if (IsSaving)
            {
                buff.Stride = buff.NumVertices > 0 ? 4u : 0u;
            }
            Serialize(ref buff.Stride);
            Serialize(ref buff.NumVertices);
            if (buff.NumVertices > 0)
            {
                int elementsize = 4;
                Serialize(ref elementsize);
                Serialize(ref buff.VertexData, Serialize);
            }
        }
        public void Serialize(ref StaticMeshVertexBuffer buff)
        {
            uint elementSize;
            if (IsLoading)
            {
                buff = new StaticMeshVertexBuffer();
            }
            else
            {
                elementSize = 8u + buff.NumTexCoords * (buff.bUseFullPrecisionUVs ? 8u : 4u);
                if (Game < MEGame.ME3)
                {
                    elementSize += 4;
                }

                buff.Stride = elementSize;
            }
            Serialize(ref buff.NumTexCoords);
            Serialize(ref buff.Stride);
            Serialize(ref buff.NumVertices);
            Serialize(ref buff.bUseFullPrecisionUVs);
            if (Game == MEGame.ME3 || Game.IsLEGame())
            {
                Serialize(ref buff.unk);
            }
            elementSize = buff.Stride;
            Serialize(ref elementSize);
            int count = buff.VertexData?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                buff.VertexData = new StaticMeshVertexBuffer.StaticMeshFullVertex[count];
            }

            for (int i = 0; i < count; i++)
            {
                if (IsLoading)
                {
                    buff.VertexData[i] = new StaticMeshVertexBuffer.StaticMeshFullVertex();
                }
                Serialize(ref buff.VertexData[i].TangentX);
                Serialize(ref buff.VertexData[i].TangentZ);
                if (Game < MEGame.ME3)
                {
                    Serialize(ref buff.VertexData[i].Color);
                }

                if (buff.bUseFullPrecisionUVs)
                {
                    if (buff.VertexData[i].FullPrecisionUVs == null)
                    {
                        buff.VertexData[i].FullPrecisionUVs = IsLoading
                            ? new Vector2[buff.NumTexCoords]
                            //bUseFullPrecisionUVs was changed, copy data from the other one
                            : Array.ConvertAll(buff.VertexData[i].HalfPrecisionUVs, v2dHalf => new Vector2(v2dHalf.X, v2dHalf.Y));
                    }
                    for (int j = 0; j < buff.NumTexCoords; j++)
                    {
                        Serialize(ref buff.VertexData[i].FullPrecisionUVs[j]);
                    }
                }
                else
                {
                    if (buff.VertexData[i].HalfPrecisionUVs == null)
                    {
                        buff.VertexData[i].HalfPrecisionUVs = IsLoading
                            ? new Vector2DHalf[buff.NumTexCoords]
                            //bUseFullPrecisionUVs was changed, copy data from the other one
                            : Array.ConvertAll(buff.VertexData[i].FullPrecisionUVs, v2d => new Vector2DHalf(v2d.X, v2d.Y));
                    }
                    for (int j = 0; j < buff.NumTexCoords; j++)
                    {
                        Serialize(ref buff.VertexData[i].HalfPrecisionUVs[j]);
                    }
                }
            }
        }
        public void Serialize(ref PositionVertexBuffer buff)
        {
            if (IsLoading)
            {
                buff = new PositionVertexBuffer();
            }

            Serialize(ref buff.Stride);
            Serialize(ref buff.NumVertices);
            if (Game == MEGame.ME3 || Game.IsLEGame())
            {
                Serialize(ref buff.unk);
            }
            int elementsize = 12;
            Serialize(ref elementsize);
            Serialize(ref buff.VertexData);
        }
        public void Serialize(ref FragmentRange fRange)
        {
            if (IsLoading)
            {
                fRange = new FragmentRange(ms.ReadInt32(), ms.ReadInt32());
            }
            else
            {
                ms.Writer.WriteInt32(fRange.BaseIndex);
                ms.Writer.WriteInt32(fRange.NumPrimitives);
            }
        }
        public void Serialize(ref StaticMeshElement meshElement)
        {
            if (IsLoading)
            {
                meshElement = new StaticMeshElement();
            }

            Serialize(ref meshElement.Material);
            Serialize(ref meshElement.EnableCollision);
            Serialize(ref meshElement.OldEnableCollision);
            Serialize(ref meshElement.bEnableShadowCasting);
            Serialize(ref meshElement.FirstIndex);
            Serialize(ref meshElement.NumTriangles);
            Serialize(ref meshElement.MinVertexIndex);
            Serialize(ref meshElement.MaxVertexIndex);
            Serialize(ref meshElement.MaterialIndex);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref meshElement.Fragments, Serialize);
                byte dummy = 0;
                Serialize(ref dummy);
            }
        }
        public void Serialize(ref StaticMeshTriangle tri)
        {
            if (IsLoading)
            {
                tri = new StaticMeshTriangle();
            }

            for (int i = 0; i < 3; i++)
            {
                Serialize(ref tri.Vertices[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    Serialize(ref tri.UVs[i, j]);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                Serialize(ref tri.Colors[i]);
            }
            Serialize(ref tri.MaterialIndex);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref tri.FragmentIndex);
            }
            Serialize(ref tri.SmoothingMask);
            Serialize(ref tri.NumUVs);
            if (Game == MEGame.UDK)
            {
                Serialize(ref tri.bExplicitNormals);
            }
            if (Game >= MEGame.ME3)
            {
                for (int i = 0; i < 3; i++)
                {
                    Serialize(ref tri.TangentX[i]);
                }
                for (int i = 0; i < 3; i++)
                {
                    Serialize(ref tri.TangentY[i]);
                }
                for (int i = 0; i < 3; i++)
                {
                    Serialize(ref tri.TangentZ[i]);
                }
                Serialize(ref tri.bOverrideTangentBasis);
            }
        }
        public void Serialize(ref StaticMeshRenderData data)
        {
            if (IsLoading)
            {
                data = new StaticMeshRenderData();
            }

            SerializeBulkData(ref data.RawTriangles, Serialize);
            Serialize(ref data.Elements, Serialize);
            Serialize(ref data.PositionVertexBuffer);
            if (IsSaving)
            {
                if (Game >= MEGame.ME3 && data.ColorVertexBuffer == null)
                {
                    //this was read in from ME1 or ME2, we need to seperate out the color data
                    data.ColorVertexBuffer = new ColorVertexBuffer
                    {
                        VertexData = data.VertexBuffer.VertexData.Select(vertex => vertex.Color).ToArray(),
                        NumVertices = (uint)data.VertexBuffer.VertexData.Length,
                        Stride = 4
                    };
                }
                else if (Game < MEGame.ME3 && data.ColorVertexBuffer != null)
                {
                    //this was read in from ME3 or UDK, we need to integrate the color data
                    // Why is this written backwards?
                    for (int i = data.VertexBuffer.VertexData.Length - 1; i >= 0; i--)
                    {
                        data.VertexBuffer.VertexData[i].Color = data.ColorVertexBuffer.VertexData[i];
                    }
                }
            }
            Serialize(ref data.VertexBuffer);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref data.ColorVertexBuffer);
            }

            if (Game < MEGame.UDK)
            {
                Serialize(ref data.ShadowExtrusionVertexBuffer);
            }
            else if (IsLoading)
            {
                data.ShadowExtrusionVertexBuffer = new ExtrusionVertexBuffer
                {
                    Stride = 4,
                    VertexData = Array.Empty<float>()
                };
            }
            Serialize(ref data.NumVertices);
            int elementSize = 2;
            Serialize(ref elementSize);
            Serialize(ref data.IndexBuffer);
            elementSize = 2;
            Serialize(ref elementSize);
            Serialize(ref data.WireframeIndexBuffer);
            if (Game != MEGame.UDK)
            {
                elementSize = 16;
                Serialize(ref elementSize);
                Serialize(ref data.Edges, Serialize);
                Serialize(ref data.ShadowTriangleDoubleSided);
            }
            else if (IsLoading)
            {
                data.Edges = [];
                data.ShadowTriangleDoubleSided = [];
            }
            if (Game == MEGame.UDK)
            {
                BulkSerialize(ref data.AdjacencyIndexBuffer, Serialize, 2);
            }
            if (Game == MEGame.ME1)
            {
                Serialize(ref data.unk1);

                int bulkDataFlags = 0;
                Serialize(ref bulkDataFlags);
                int byteCount = data.xmlFile?.Length ?? 0;
                Serialize(ref byteCount);
                Serialize(ref byteCount);
                SerializeFileOffset();
                Serialize(ref data.xmlFile, byteCount);
            }
            else if (IsLoading)
            {
                data.xmlFile = [];
            }
        }
        public void Serialize(ref kDOPCollisionTriangle kTri)
        {
            if (IsLoading)
            {
                kTri = new kDOPCollisionTriangle(ms.ReadUInt16(), ms.ReadUInt16(), ms.ReadUInt16(), ms.ReadUInt16());
            }
            else
            {
                ms.Writer.WriteUInt16(kTri.Vertex1);
                ms.Writer.WriteUInt16(kTri.Vertex2);
                ms.Writer.WriteUInt16(kTri.Vertex3);
                ms.Writer.WriteUInt16(kTri.MaterialIndex);
            }
        }
        public void Serialize(ref kDOPCompact kDop)
        {
            if (IsLoading)
            {
                kDop = new kDOPCompact();
            }

            for (int i = 0; i < 3; i++)
            {
                Serialize(ref kDop.Min[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                Serialize(ref kDop.Max[i]);
            }
        }
        public void Serialize(ref kDOP kDop)
        {
            if (IsLoading)
            {
                kDop = new kDOP();
            }

            for (int i = 0; i < 3; i++)
            {
                Serialize(ref kDop.Min[i]);
            }
            for (int i = 0; i < 3; i++)
            {
                Serialize(ref kDop.Max[i]);
            }
        }
        public void Serialize(ref kDOPNode kDopNode)
        {
            if (IsLoading)
            {
                kDopNode = new kDOPNode();
            }

            Serialize(ref kDopNode.BoundingVolume);
            Serialize(ref kDopNode.bIsLeaf);
            //depending on bIsLeaf, next two are either LeftNode and RightNode, or NumTriangles and StartIndex.
            //But since it's a union, they share space in memory, so it doesn't matter for serialization purposes
            Serialize(ref kDopNode.u.LeftNode);
            Serialize(ref kDopNode.u.RightNode);
        }
        public void Serialize(ref kDOPTreeCompact kDopTree)
        {
            if (IsLoading)
            {
                kDopTree = new kDOPTreeCompact();
            }

            Serialize(ref kDopTree.RootBound);
            int elementSize = 6;
            Serialize(ref elementSize);
            Serialize(ref kDopTree.Nodes, Serialize);
            elementSize = 8;
            Serialize(ref elementSize);
            Serialize(ref kDopTree.Triangles, Serialize);
        }
        public void Serialize(ref kDOPTree kDopTree)
        {
            if (IsLoading)
            {
                kDopTree = new kDOPTree();
            }

            int elementSize = 32;
            Serialize(ref elementSize);
            Serialize(ref kDopTree.Nodes, Serialize);
            elementSize = 8;
            Serialize(ref elementSize);
            Serialize(ref kDopTree.Triangles, Serialize);
        }
    }
}