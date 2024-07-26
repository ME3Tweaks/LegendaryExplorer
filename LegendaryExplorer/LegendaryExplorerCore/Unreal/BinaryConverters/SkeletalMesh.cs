using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SkeletalMesh : ObjectBinary
    {
        public BoxSphereBounds Bounds;
        public UIndex[] Materials;
        public Vector3 Origin;
        public Rotator RotOrigin;
        public MeshBone[] RefSkeleton;
        public int SkeletalDepth;
        public StaticLODModel[] LODModels;
        public UMultiMap<NameReference, int> NameIndexMap; //TODO: Make this a UMap
        public PerPolyBoneCollisionData[] PerPolyBoneKDOPs;
        public string[] BoneBreakNames; //ME3 and UDK
        public UIndex[] ClothingAssets; //ME3 and UDK
        public uint unk1; //UDK
        public uint unk2; //UDK
        public float[] unkFloats; //UDK
        public uint unk3; //UDK

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Bounds);
            sc.Serialize(ref Materials, sc.Serialize);
            sc.Serialize(ref Origin);
            sc.Serialize(ref RotOrigin);
            sc.Serialize(ref RefSkeleton, sc.Serialize);
            sc.Serialize(ref SkeletalDepth);
            sc.Serialize(ref LODModels, sc.Serialize);
            sc.Serialize(ref NameIndexMap, sc.Serialize, sc.Serialize);
            sc.Serialize(ref PerPolyBoneKDOPs, sc.Serialize);

            if (sc.Game >= MEGame.ME3)
            {
                if (sc.IsSaving && sc.Game == MEGame.UDK)
                {
                    ClothingAssets = [];
                }
                sc.Serialize(ref BoneBreakNames, sc.Serialize);
                sc.Serialize(ref ClothingAssets, sc.Serialize);
            }
            else
            {
                BoneBreakNames = [];
                ClothingAssets = [];
            }

            if (sc.Game == MEGame.UDK)
            {
                sc.Serialize(ref unk1);
                sc.Serialize(ref unk2);
                sc.Serialize(ref unkFloats, sc.Serialize);
                sc.Serialize(ref unk3);
            }
            else if (sc.IsLoading)
            {
                unk1 = 1;
                unkFloats = [1f, 0f, 0f, 0f];
            }
        }

        public static SkeletalMesh Create()
        {
            return new()
            {
                Bounds = new BoxSphereBounds(),
                Materials = [],
                RefSkeleton = [],
                LODModels = [],
                NameIndexMap = [],
                PerPolyBoneKDOPs = [],
                BoneBreakNames = [],
                ClothingAssets = [],
                unk1 = 1,
                unkFloats = [1f, 0f, 0f, 0f]
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(RefSkeleton.Select((bone, i) => (bone.Name, $"RefSkeleton[{i}].BoneName")));
            names.AddRange(NameIndexMap.Select((kvp, i) => (kvp.Key, $"NameIndexMap[{i}]")));

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexInSpan(action, Materials.AsSpan(), nameof(Materials));
            if (game is MEGame.ME3 || game.IsLEGame())
            {
                ForEachUIndexInSpan(action, ClothingAssets.AsSpan(), nameof(ClothingAssets));
            }
        }
    }

    public class MeshBone
    {
        public NameReference Name;
        public uint Flags;
        public Quaternion Orientation;
        public Vector3 Position;
        public int NumChildren;
        public int ParentIndex;
        public SharpDX.Color BoneColor; //ME3 and UDK
    }

    public class StaticLODModel
    {
        public SkelMeshSection[] Sections;
        public bool NeedsCPUAccess; //UDK
        public byte DataTypeSize; //UDK
        public ushort[] IndexBuffer; //BulkSerialized
        public ushort[] ShadowIndices; //not in UDK
        public ushort[] ActiveBoneIndices;
        public byte[] ShadowTriangleDoubleSided; //not in UDK
        public SkelMeshChunk[] Chunks;
        public uint Size;
        public uint NumVertices;
        public MeshEdge[] Edges; //Not in UDK
        public byte[] RequiredBones;
        public ushort[] RawPointIndices; //BulkData
        public uint NumTexCoords; //UDK
        public SoftSkinVertex[] ME1VertexBufferGPUSkin; //BulkSerialized
        public SkeletalMeshVertexBuffer VertexBufferGPUSkin;
    }

    public class SkelMeshSection
    {
        public ushort MaterialIndex;
        public ushort ChunkIndex;
        public uint BaseIndex;
        public int NumTriangles; //ushort in ME1 and ME2
        public byte TriangleSorting; //UDK
    }

    public class SkelMeshChunk
    {
        public uint BaseVertexIndex;
        public RigidSkinVertex[] RigidVertices;
        public SoftSkinVertex[] SoftVertices;
        public ushort[] BoneMap;
        public int NumRigidVertices;
        public int NumSoftVertices;
        public int MaxBoneInfluences;
    }

    public class RigidSkinVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX;
        public PackedNormal TangentY;
        public PackedNormal TangentZ;
        public Vector2 UV;
        public Vector2 UV2; //UDK
        public Vector2 UV3; //UDK
        public Vector2 UV4; //UDK
        public SharpDX.Color BoneColor; //UDK
        public byte Bone;
    }

    public class SoftSkinVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX; // Tangent, U-direction
        public PackedNormal TangentY; // Binormal, V-direction
        public PackedNormal TangentZ; // Normal
        public Vector2 UV;
        public Vector2 UV2; //UDK
        public Vector2 UV3; //UDK
        public Vector2 UV4; //UDK
        public SharpDX.Color BoneColor; //UDK
        public Influences InfluenceBones;
        public Influences InfluenceWeights;
    }

    public class SkeletalMeshVertexBuffer
    {
        public int NumTexCoords; //UDK
        public bool bUseFullPrecisionUVs; //should always be false
        public bool bUsePackedPosition; //ME3 or UDK
        public Vector3 MeshExtension; //ME3 or UDK
        public Vector3 MeshOrigin; //ME3 or UDK
        public GPUSkinVertex[] VertexData; //BulkSerialized
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct GPUSkinVertex
    {
        public PackedNormal TangentX;
        public PackedNormal TangentZ;
        public Influences InfluenceBones;
        public Influences InfluenceWeights;
        public Vector3 Position; //serialized first in ME2
        public Vector2DHalf UV;
    }

    public class PerPolyBoneCollisionData
    {
        public kDOPTree kDOPTreeME1ME2;
        public kDOPTreeCompact kDOPTreeME3UDK;
        public Vector3[] CollisionVerts;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref MeshBone mb)
        {
            if (IsLoading)
            {
                mb = new MeshBone();
            }
            Serialize(ref mb.Name);
            Serialize(ref mb.Flags);
            Serialize(ref mb.Orientation);
            Serialize(ref mb.Position);
            Serialize(ref mb.NumChildren);
            Serialize(ref mb.ParentIndex);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mb.BoneColor);
            }
            else if (IsLoading)
            {
                mb.BoneColor = new SharpDX.Color(255, 255, 255, 255);
            }
        }
        public void Serialize(ref SkelMeshSection sms)
        {
            if (IsLoading)
            {
                sms = new SkelMeshSection();
            }
            Serialize(ref sms.MaterialIndex);
            Serialize(ref sms.ChunkIndex);
            Serialize(ref sms.BaseIndex);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref sms.NumTriangles);
            }
            else
            {
                ushort tmp = (ushort)sms.NumTriangles;
                Serialize(ref tmp);
                sms.NumTriangles = tmp;
            }

            if (Game == MEGame.UDK)
            {
                Serialize(ref sms.TriangleSorting);
            }
        }
        public void Serialize(ref RigidSkinVertex rsv)
        {
            if (IsLoading)
            {
                rsv = new RigidSkinVertex();
            }
            Serialize(ref rsv.Position);
            Serialize(ref rsv.TangentX);
            Serialize(ref rsv.TangentY);
            Serialize(ref rsv.TangentZ);
            Serialize(ref rsv.UV);
            if (Game == MEGame.UDK)
            {
                Serialize(ref rsv.UV2);
                Serialize(ref rsv.UV3);
                Serialize(ref rsv.UV4);
                Serialize(ref rsv.BoneColor);
            }
            else if (IsLoading)
            {
                rsv.BoneColor = new SharpDX.Color(255,255,255,255);
            }
            Serialize(ref rsv.Bone);
        }
        public void Serialize(ref SoftSkinVertex ssv)
        {
            if (IsLoading)
            {
                ssv = new SoftSkinVertex();
            }
            Serialize(ref ssv.Position);
            Serialize(ref ssv.TangentX);
            Serialize(ref ssv.TangentY);
            Serialize(ref ssv.TangentZ);
            Serialize(ref ssv.UV);
            if (Game == MEGame.UDK)
            {
                Serialize(ref ssv.UV2);
                Serialize(ref ssv.UV3);
                Serialize(ref ssv.UV4);
                Serialize(ref ssv.BoneColor);
            }
            else if (IsLoading)
            {
                ssv.BoneColor = new SharpDX.Color(255, 255, 255, 255);
            }
            Serialize(ref ssv.InfluenceBones);
            Serialize(ref ssv.InfluenceWeights);
        }
        public void Serialize(ref SkelMeshChunk smc)
        {
            if (IsLoading)
            {
                smc = new SkelMeshChunk();
            }
            Serialize(ref smc.BaseVertexIndex);
            Serialize(ref smc.RigidVertices, Serialize);
            Serialize(ref smc.SoftVertices, Serialize);
            Serialize(ref smc.BoneMap);
            Serialize(ref smc.NumRigidVertices);
            Serialize(ref smc.NumSoftVertices);
            Serialize(ref smc.MaxBoneInfluences);
        }

        public void Serialize(ref SkeletalMeshVertexBuffer svb)
        {
            if (IsLoading)
            {
                svb = new SkeletalMeshVertexBuffer();
            }

            if (Game == MEGame.UDK)
            {
                svb.bUsePackedPosition = true;
            }
            else
            {
                svb.bUsePackedPosition = false;
                svb.bUseFullPrecisionUVs = false;
            }

            if (Game == MEGame.UDK)
            {
                svb.NumTexCoords = 1;
                Serialize(ref svb.NumTexCoords);
            }
            else if (IsLoading)
            {
                svb.NumTexCoords = 1;
            }
            Serialize(ref svb.bUseFullPrecisionUVs);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref svb.bUsePackedPosition);
                Serialize(ref svb.MeshExtension);
                Serialize(ref svb.MeshOrigin);
            }
            int elementSize = 32;
            Serialize(ref elementSize);

            //vertexData
            int count = svb.VertexData?.Length ?? 0;
            Serialize(ref count);
            if (IsLoading)
            {
                svb.VertexData = new GPUSkinVertex[count];
            }

            //slow path
            if (Game is MEGame.ME2 || svb.bUseFullPrecisionUVs || svb.NumTexCoords > 1 || !ms.Endian.IsNative)
            {
                for (int j = 0; j < count; j++)
                {
                    ref GPUSkinVertex gsv = ref svb.VertexData[j];
                    if (IsLoading)
                    {
                        gsv = new GPUSkinVertex();
                    }

                    if (Game == MEGame.ME2)
                    {
                        Serialize(ref gsv.Position);
                    }
                    Serialize(ref gsv.TangentX);
                    Serialize(ref gsv.TangentZ);
                    Serialize(ref gsv.InfluenceBones);
                    Serialize(ref gsv.InfluenceWeights);
                    if (Game >= MEGame.ME3)
                    {
                        Serialize(ref gsv.Position);
                    }

                    if (svb.bUseFullPrecisionUVs)
                    {
                        Vector2 fullUV = gsv.UV;
                        Serialize(ref fullUV);
                        gsv.UV = fullUV;
                    }
                    else
                    {
                        Serialize(ref gsv.UV);
                    }

                    if (svb.NumTexCoords > 1)
                    {
                        if (IsLoading)
                        {
                            ms.Skip((svb.NumTexCoords - 1) * (svb.bUseFullPrecisionUVs ? 8 : 4));
                        }
                        else
                        {
                            throw new Exception("Should never be saving more than one UV! Num UVs (NumTexCoords): "+svb.NumTexCoords);
                        }
                    }
                }
            }
            //fast path
            else
            {
                if (IsLoading)
                {
                    ms.Read(MemoryMarshal.AsBytes<GPUSkinVertex>(svb.VertexData));
                }
                else
                {
                    ms.Writer.Write(MemoryMarshal.AsBytes<GPUSkinVertex>(svb.VertexData));
                }
            }

            if (IsLoading)
            {
                svb.NumTexCoords = 1;
            }
        }
        public void Serialize(ref StaticLODModel slm)
        {
            if (IsLoading)
            {
                slm = new StaticLODModel();
            }
            Serialize(ref slm.Sections, Serialize);
            int indexSize = 2;
            slm.DataTypeSize = 2;
            if (Game == MEGame.UDK && IsSaving && slm.IndexBuffer.Length > ushort.MaxValue)
            {
                slm.DataTypeSize = 4;
                indexSize = 4;
            }
            if (Game == MEGame.UDK)
            {
                slm.NeedsCPUAccess = true;
                Serialize(ref slm.NeedsCPUAccess);
                Serialize(ref slm.DataTypeSize);
            }
            Serialize(ref indexSize);
            if (Game == MEGame.UDK && indexSize == 4)
            {
                //have to do this manually due to the size mismatch
                //as far as I know, despite being saved as uints when the IndexBuffer is longer than ushort.MaxValue,
                //the actual indicies themselves should not exceed the range of a ushort
                int count = slm.IndexBuffer?.Length ?? 0;
                Serialize(ref count);
                if (IsLoading)
                {
                    slm.IndexBuffer = new ushort[count];
                }

                for (int i = 0; i < count; i++)
                {
                    if (IsLoading)
                        slm.IndexBuffer[i] = (ushort)ms.ReadUInt32();
                    else
                        ms.Writer.WriteUInt32(slm.IndexBuffer[i]);
                }
            }
            else
            {
                Serialize(ref slm.IndexBuffer);
            }
            if (Game != MEGame.UDK)
            {
                Serialize(ref slm.ShadowIndices);
            }
            Serialize(ref slm.ActiveBoneIndices);
            if (Game != MEGame.UDK)
            {
                Serialize(ref slm.ShadowTriangleDoubleSided);
            }
            Serialize(ref slm.Chunks, Serialize);
            Serialize(ref slm.Size);
            Serialize(ref slm.NumVertices);
            if (Game <= MEGame.LE3 && slm.NumVertices > ushort.MaxValue)
            {
                throw new Exception($"Mass Effect games do not support SkeletalMeshes with more than {ushort.MaxValue} vertices!");
            }
            if (Game != MEGame.UDK)
            {
                Serialize(ref slm.Edges, Serialize);
            }
            Serialize(ref slm.RequiredBones);
            if (Game == MEGame.UDK)
            {
                int[] UDKRawPointIndices = IsSaving ? Array.ConvertAll(slm.RawPointIndices, u => (int)u) : [];
                SerializeBulkData(ref UDKRawPointIndices, Serialize);
                slm.RawPointIndices = Array.ConvertAll(UDKRawPointIndices, i => (ushort)i);
            }
            else
            {
                SerializeBulkData(ref slm.RawPointIndices, Serialize);
            }
            if (Game == MEGame.UDK)
            {
                Serialize(ref slm.NumTexCoords);
            }
            else if (IsLoading)
            {
                slm.NumTexCoords = 1;
            }
            if (Game == MEGame.ME1)
            {
                if (IsSaving && slm.ME1VertexBufferGPUSkin == null)
                {
                    GPUSkinVertex[] vertexData = slm.VertexBufferGPUSkin.VertexData;
                    slm.ME1VertexBufferGPUSkin = new SoftSkinVertex[vertexData.Length];
                    for (int i = 0; i < vertexData.Length; i++)
                    {
                        GPUSkinVertex vert = vertexData[i];
                        var normal = (Vector4)vert.TangentZ;
                        var tangent = (Vector4)vert.TangentX;
                        slm.ME1VertexBufferGPUSkin[i] = new SoftSkinVertex
                        {
                            Position = vert.Position,
                            TangentX = vert.TangentX,
                            TangentY = (PackedNormal)(new Vector4(Vector3.Cross((Vector3)vert.TangentZ, (Vector3)vert.TangentX), normal.W * tangent.W) * normal.W),
                            TangentZ = vert.TangentZ,
                            UV = new Vector2(vert.UV.X, vert.UV.Y),
                            InfluenceBones = vert.InfluenceBones,
                            InfluenceWeights = vert.InfluenceWeights
                        };
                    }
                }

                int softSkinVertexSize = 40;
                Serialize(ref softSkinVertexSize);
                Serialize(ref slm.ME1VertexBufferGPUSkin, Serialize);
            }
            else
            {
                if (IsSaving && slm.VertexBufferGPUSkin == null)
                {
                    slm.VertexBufferGPUSkin = new SkeletalMeshVertexBuffer
                    {
                        MeshExtension = new Vector3(1, 1, 1),
                        NumTexCoords = 1,
                        VertexData = new GPUSkinVertex[slm.ME1VertexBufferGPUSkin.Length]
                    };
                    for (int i = 0; i < slm.ME1VertexBufferGPUSkin.Length; i++)
                    {
                        SoftSkinVertex vert = slm.ME1VertexBufferGPUSkin[i];
                        slm.VertexBufferGPUSkin.VertexData[i] = new GPUSkinVertex
                        {
                            Position = vert.Position,
                            TangentX = vert.TangentX,
                            TangentZ = vert.TangentZ,
                            UV = new Vector2DHalf(vert.UV.X, vert.UV.Y),
                            InfluenceBones = vert.InfluenceBones,
                            InfluenceWeights = vert.InfluenceWeights
                        };
                    }
                }
                Serialize(ref slm.VertexBufferGPUSkin);
            }

            if (Game >= MEGame.ME3)
            {
                if (IsLoading)
                {
                    int vertexInfluenceSize = 0;
                    Serialize(ref vertexInfluenceSize);
                    if (vertexInfluenceSize > 0)
                    {
                        if (Game == MEGame.UDK)
                        {
                            int[] vertexInfluences = null;
                            Serialize(ref vertexInfluences, Serialize);
                            int dummy = 0;
                            Serialize(ref dummy);
                        }
                        else
                        {
                            throw new Exception($"VertexInfluences exist on this SkeletalMesh! Mesh in: {Pcc.FilePath}");
                        }
                    }
                }
                else
                {
                    ms.Writer.WriteInt32(0);
                }
            }

            if (Game == MEGame.UDK)
            {
                Serialize(ref slm.NeedsCPUAccess);
                Serialize(ref slm.DataTypeSize);
                int elementSize = 2;
                Serialize(ref elementSize);
                if (elementSize == 4)
                {
                    uint[] secondIndexBuffer = [];
                    Serialize(ref secondIndexBuffer, Serialize);
                }
                else
                {
                    ushort[] secondIndexBuffer = [];
                    Serialize(ref secondIndexBuffer, Serialize);
                }
            }
        }
        public void Serialize(ref PerPolyBoneCollisionData bcd)
        {
            if (IsLoading)
            {
                bcd = new PerPolyBoneCollisionData();
            }
            if (IsSaving)
            {
                if (Game >= MEGame.ME3 && bcd.kDOPTreeME3UDK == null)
                {
                    bcd.kDOPTreeME3UDK = KDOPTreeBuilder.ToCompact(bcd.kDOPTreeME1ME2.Triangles, bcd.CollisionVerts);
                }
                else if (Game <= MEGame.ME2 && bcd.kDOPTreeME1ME2 == null)
                {
                    //todo: need to convert kDOPTreeCompact to kDOPTree
                    throw new NotImplementedException("Cannot convert this SkeletalMesh to ME1 or ME2 format :(");
                }
            }
            if (Game >= MEGame.ME3)
            {
                Serialize(ref bcd.kDOPTreeME3UDK);
            }
            else
            {
                Serialize(ref bcd.kDOPTreeME1ME2);
            }

            Serialize(ref bcd.CollisionVerts);
        }
    }
}