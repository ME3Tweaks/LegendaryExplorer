using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class SkeletalMesh
    {
        public struct BoundingStruct
        {
            public Vector3 origin;
            public Vector3 size;
            public float r;
        }

        public struct BoneStruct
        {
            public int Name;
            public int Flags;
            public int Unk1;
            public Vector4 Orientation;
            public Vector3 Position;
            public int NumChildren;
            public int Parent;
            public int BoneColor;
        }

        public struct SectionStruct
        {
            public short MaterialIndex;
            public short ChunkIndex;
            public int BaseIndex;
            public int NumTriangles;
            public void Serialize(SerializingContainer Container)
            {
                MaterialIndex = Container + MaterialIndex;
                ChunkIndex = Container + ChunkIndex;
                BaseIndex = Container + BaseIndex;
                NumTriangles = Container + NumTriangles;
                //TriangleSorting = Container + TriangleSorting;
            }
        }

        public struct MultiSizeIndexContainerStruct
        {
            //public int NeedsCPUAccess;
            //public byte DataTypeSize;
            public int IndexSize;
            public int IndexCount;
            public List<ushort> Indexes;

            public void Serialize(SerializingContainer Container)
            {
                //NeedsCPUAccess = Container + NeedsCPUAccess;
                //DataTypeSize = Container + DataTypeSize;
                IndexSize = Container + IndexSize;
                IndexCount = Container + IndexCount;
                if (Container.isLoading)
                {
                    Indexes = new List<ushort>();
                    for (int i = 0; i < IndexCount; i++)
                        Indexes.Add(0);
                }
                for (int i = 0; i < IndexCount; i++)
                    Indexes[i] = Container + Indexes[i];
            }
        }

        public struct RigidSkinVertexStruct
        {
            public Vector3 Position;
            public int TangentX;
            public int TangentY;
            public int TangentZ;
            public Vector2[] UV;
            public int Color;
            public byte Bone;
            public void Serialize(SerializingContainer Container)
            {
                Position.X = Container + Position.X;
                Position.Y = Container + Position.Y;
                Position.Z = Container + Position.Z;
                TangentX = Container + TangentX;
                TangentY = Container + TangentY;
                TangentZ = Container + TangentZ;                
                if (Container.isLoading)
                    UV = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    UV[i].X = Container + UV[i].X;
                    UV[i].Y = Container + UV[i].Y;
                }
                Color = Container + Color;
                Bone = Container + Bone;  
            }
        }

        public struct SoftSkinVertexStruct
        {
            public Vector3 Position;
            public int TangentX;
            public int TangentY;
            public int TangentZ;
            public Vector2[] UV;
            public int Color;
            public byte[] InfluenceBones;
            public byte[] InfluenceWeights;

            public void Serialize(SerializingContainer Container)
            {
                Position.X = Container + Position.X;
                Position.Y = Container + Position.Y;
                Position.Z = Container + Position.Z;
                TangentX = Container + TangentX;
                TangentY = Container + TangentY;
                TangentZ = Container + TangentZ;
                if (Container.isLoading)
                {
                    UV = new Vector2[4];
                    InfluenceBones = new byte[4];
                    InfluenceWeights = new byte[4];
                }
                for (int i = 0; i < 4; i++)
                {
                    UV[i].X = Container + UV[i].X;
                    UV[i].Y = Container + UV[i].Y;
                }
                Color = Container + Color;
                for (int i = 0; i < 4; i++)
                    InfluenceBones[i] = Container + InfluenceBones[i];
                for (int i = 0; i < 4; i++)
                    InfluenceWeights[i] = Container + InfluenceWeights[i];
            }

        }

        public struct SkelMeshChunkStruct
        {
            public int BaseVertexIndex;
            public List<RigidSkinVertexStruct> RiginSkinVertices;
            public List<SoftSkinVertexStruct> SoftSkinVertices;
            public List<ushort> BoneMap;
            public int NumRigidVertices;
            public int NumSoftVertices;
            public int MaxBoneInfluences;

            public void Serialize(SerializingContainer Container)
            {
                //basevertex
                BaseVertexIndex = Container + BaseVertexIndex;
                //rigid vertices
                int count = 0;
                if (!Container.isLoading)
                    count = RiginSkinVertices.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    RiginSkinVertices = new List<RigidSkinVertexStruct>();
                    for (int i = 0; i < count; i++)
                        RiginSkinVertices.Add(new RigidSkinVertexStruct());
                }
                for (int i = 0; i < count; i++)
                {
                    RigidSkinVertexStruct v = RiginSkinVertices[i];
                    v.Serialize(Container);
                    RiginSkinVertices[i] = v;
                }
                //soft vertices
                if (!Container.isLoading)
                    count = SoftSkinVertices.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    SoftSkinVertices = new List<SoftSkinVertexStruct>();
                    for (int i = 0; i < count; i++)
                        SoftSkinVertices.Add(new SoftSkinVertexStruct());
                }
                for (int i = 0; i < count; i++)
                {
                    SoftSkinVertexStruct v = SoftSkinVertices[i];
                    v.Serialize(Container);
                    SoftSkinVertices[i] = v;
                }
                //bonemap
                if (!Container.isLoading)
                    count = BoneMap.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    BoneMap = new List<ushort>();
                    for (int i = 0; i < count; i++)
                        BoneMap.Add(0);
                }
                for (int i = 0; i < count; i++)
                    BoneMap[i] = Container + BoneMap[i];
                //rest
                NumRigidVertices = Container + NumRigidVertices;
                NumSoftVertices = Container + NumSoftVertices;
                MaxBoneInfluences = Container + MaxBoneInfluences;
            }
        }

        public struct GPUSkinVertexStruct
        {
            public int TangentX;
            public int TangentZ;
            public byte[] InfluenceBones;
            public byte[] InfluenceWeights;
            public Vector3 Position;
            public ushort U;
            public ushort V;
            public void Serialize(SerializingContainer Container)
            {
                TangentX = Container + TangentX;
                TangentZ = Container + TangentZ;
                if (Container.isLoading)
                {
                    InfluenceBones = new byte[4];
                    InfluenceWeights = new byte[4];
                }
                for (int i = 0; i < 4; i++)
                    InfluenceBones[i] = Container + InfluenceBones[i];
                for (int i = 0; i < 4; i++)
                    InfluenceWeights[i] = Container + InfluenceWeights[i];
                Position.X = Container + Position.X;
                Position.Y = Container + Position.Y;
                Position.Z = Container + Position.Z;
                U = Container + U;
                V = Container + V;
            }
        }

        public struct VertexBufferGPUSkinStruct
        {
            public int NumTexCoords;
            //public int UseFullPrecisionUVs;
            //public int UsePackedPosition;
            public Vector3 Extension;
            public Vector3 Origin;
            public int VertexSize;
            public List<GPUSkinVertexStruct> Vertices;

            public void Serialize(SerializingContainer Container)
            {
                //NumTexCoords
                NumTexCoords = Container + NumTexCoords;
                ////UseFullPrecisionUVs
                //UseFullPrecisionUVs = Container + UseFullPrecisionUVs;
                ////UsePackedPosition
                //UsePackedPosition = Container + UsePackedPosition;
                //Extension
                Extension.X = Container + Extension.X;
                Extension.Y = Container + Extension.Y;
                Extension.Z = Container + Extension.Z;
                //origin
                Origin.X = Container + Origin.X;
                Origin.Y = Container + Origin.Y;
                Origin.Z = Container + Origin.Z;
                //vertexsize
                VertexSize = Container + VertexSize;
                int count = 0;
                if (!Container.isLoading)
                    count = Vertices.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    Vertices = new List<GPUSkinVertexStruct>();
                    for (int i = 0; i < count; i++)
                        Vertices.Add(new GPUSkinVertexStruct());
                }
                int VertexDiff = VertexSize - 32;
                for (int i = 0; i < count; i++)
                {
                    GPUSkinVertexStruct v = Vertices[i];
                    v.Serialize(Container);
                    
                    if(VertexDiff > 0)
                    {
                        byte b = 0;
                        for (int j = 0; j < VertexDiff; j++)
                            b = Container + b;
                    }
                    Vertices[i] = v;
                }
            }

        }

        public struct LODModelStruct
        {
            public List<SectionStruct> Sections;
            public MultiSizeIndexContainerStruct IndexBuffer;
            public int Unk1;
            public List<ushort> ActiveBones;
            public int Unk2;
            public List<SkelMeshChunkStruct> Chunks;
            public int Size;
            public int NumVertices;
            public int Unk3;
            public List<byte> RequiredBones;
            public int RawPointIndicesFlag;
            public int RawPointIndicesCount;
            public int RawPointIndicesSize;
            public int RawPointIndicesOffset;
            public List<int> RawPointIndices;
            public int NumTexCoords;
            public VertexBufferGPUSkinStruct VertexBufferGPUSkin;
            public int Unk4;

            public void Serialize(SerializingContainer Container)
            {
                //Sections
                int count = 0;
                if (!Container.isLoading)
                    count = Sections.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    Sections = new List<SectionStruct>();
                    for (int i = 0; i < count; i++)
                        Sections.Add(new SectionStruct());
                }
                for (int i = 0; i < count; i++)
                {
                    SectionStruct sec = Sections[i];
                    sec.Serialize(Container);
                    Sections[i] = sec;
                }
                //IndexBuffer
                if (Container.isLoading)
                    IndexBuffer = new MultiSizeIndexContainerStruct();
                IndexBuffer.Serialize(Container);
                //unk1
                Unk1 = Container + Unk1;
                //Active Bones
                if (!Container.isLoading)
                    count = ActiveBones.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    ActiveBones = new List<ushort>();
                    for (int i = 0; i < count; i++)
                        ActiveBones.Add(0);
                }
                for (int i = 0; i < count; i++)
                    ActiveBones[i] = Container + ActiveBones[i];
                //unk2
                Unk2 = Container + Unk2;
                //Chunks
                if (!Container.isLoading)
                    count = Chunks.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    Chunks = new List<SkelMeshChunkStruct>();
                    for (int i = 0; i < count; i++)
                        Chunks.Add(new SkelMeshChunkStruct());
                }
                for (int i = 0; i < count; i++)
                {
                    SkelMeshChunkStruct c = Chunks[i];
                    c.Serialize(Container);
                    Chunks[i] = c;
                }
                //Size
                Size = Container + Size;
                //NumVertices
                NumVertices = Container + NumVertices;
                //unk3
                Unk3 = Container + Unk3;
                //RequiredBones
                if (!Container.isLoading)
                    count = RequiredBones.Count();
                count = Container + count;
                if (Container.isLoading)
                {
                    RequiredBones = new List<byte>();
                    for (int i = 0; i < count; i++)
                        RequiredBones.Add(0);
                }
                for (int i = 0; i < count; i++)
                    RequiredBones[i] = Container + RequiredBones[i];
                //RawPointIndicesFlag
                RawPointIndicesFlag = Container + RawPointIndicesFlag;
                //RawPointIndicesCount
                RawPointIndicesCount = Container + RawPointIndicesCount;
                //RawPointIndicesSize
                RawPointIndicesSize = Container + RawPointIndicesSize;
                //RawPointIndicesOffset
                RawPointIndicesOffset = Container + RawPointIndicesOffset;
                //RawPointIndices
                if (Container.isLoading)
                {
                    RawPointIndices = new List<int>();
                    for (int i = 0; i < RawPointIndicesCount; i++)
                        RawPointIndices.Add(0);
                }
                for (int i = 0; i < RawPointIndicesCount; i++)
                    RawPointIndices[i] = Container + RawPointIndices[i];
                //NumTexCoords
                NumTexCoords = Container + NumTexCoords;
                //VertexBufferGPUSkin
                if (Container.isLoading)
                    VertexBufferGPUSkin = new VertexBufferGPUSkinStruct();
                VertexBufferGPUSkin.Serialize(Container);
                //unk4
                Unk4 = Container + Unk4;
            }
        }

        public struct TailNamesStruct
        {
            public int Name;
            public int Unk1;
            public int Unk2;

            public void Serialize(SerializingContainer Container)
            {
                Name = Container + Name;
                Unk1 = Container + Unk1;
                Unk2 = Container + Unk2;
            }
        }

        public int Flags;
        public BoundingStruct Bounding = new BoundingStruct();
        public List<int> Materials;
        public Vector3 Origin;
        public Vector3 Rotation;
        public List<BoneStruct> Bones;
        public int SkeletonDepth;
        public List<LODModelStruct> LODModels;
        public List<TailNamesStruct> TailNames;
        public int Unk1;
        public int Unk2;
        public List<int> Unk3;

        public PCCPackage Owner;
        public int MyIndex;
        public bool Loaded = false;
        private long ReadEnd;

        public List<CustomVertex.PositionTextured[]> DirectXSections;

        public SkeletalMesh()
        {
            Loaded = true;
        }

        public SkeletalMesh(PCCPackage pcc, int Index)
        {
            Loaded = true;
            MyIndex = Index;
            Owner = pcc;
            Flags = (int)(pcc.Exports[Index].ObjectFlags >> 32);
            int start = GetPropertyEnd();
            byte[] buff = new byte[pcc.Exports[Index].Data.Length - start];
            for (int i = 0; i < pcc.Exports[Index].Data.Length - start; i++)
                buff[i] = pcc.Exports[Index].Data[i + start];
            MemoryStream m = new MemoryStream(buff);
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = true;
            Serialize(Container);
        }

        public void Serialize(SerializingContainer Container)
        {
            SerializeBoundings(Container);
            SerializeMaterials(Container);
            SerializeOrgRot(Container);
            SerializeBones(Container);
            SerializeLODs(Container);
            SerializeTail(Container);
            ReadEnd = Container.GetPos();
        }

        private void SerializeBoundings(SerializingContainer Container)
        {
            Bounding.origin.X = Container + Bounding.origin.X;
            Bounding.origin.Y = Container + Bounding.origin.Y;
            Bounding.origin.Z = Container + Bounding.origin.Z;
            Bounding.size.X = Container + Bounding.size.X;
            Bounding.size.Y = Container + Bounding.size.Y;
            Bounding.size.Z = Container + Bounding.size.Z;
            Bounding.r = Container + Bounding.r;
        }

        private void SerializeMaterials(SerializingContainer Container)
        {
            int count = 0;
            if (!Container.isLoading)
                count = Materials.Count();
            count = Container + count;
            if (Container.isLoading)
            {
                Materials = new List<int>();
                for (int i = 0; i < count; i++)
                    Materials.Add(0);
            }
            for (int i = 0; i < count; i++)
                Materials[i] = Container + Materials[i];
        }

        private void SerializeOrgRot(SerializingContainer Container)
        {
            Origin.X = Container + Origin.X;
            Origin.Y = Container + Origin.Y;
            Origin.Z = Container + Origin.Z;
            Rotation.X = Container + Rotation.X;
            Rotation.Y = Container + Rotation.Y;
            Rotation.Z = Container + Rotation.Z;
            
        }

        private void SerializeBones(SerializingContainer Container)
        {
            int count = 0;
            if (!Container.isLoading)
                count = Bones.Count();
            count = Container + count;
            if (Container.isLoading)
            {
                Bones = new List<BoneStruct>();
                for (int i = 0; i < count; i++)
                    Bones.Add(new BoneStruct());
            }
            for (int i = 0; i < count; i++)
            {
                BoneStruct b = Bones[i];
                b.Name = Container + b.Name;
                b.Flags = Container + b.Flags;
                b.Unk1 = Container + b.Unk1;
                b.Orientation.X = Container + b.Orientation.X;
                b.Orientation.Y = Container + b.Orientation.Y;
                b.Orientation.Z = Container + b.Orientation.Z;
                b.Orientation.W = Container + b.Orientation.W;
                b.Position.X = Container + b.Position.X;
                b.Position.Y = Container + b.Position.Y;
                b.Position.Z = Container + b.Position.Z;
                b.NumChildren = Container + b.NumChildren;
                b.Parent = Container + b.Parent;
                b.BoneColor = Container + b.BoneColor;
                Bones[i] = b;
            }
            SkeletonDepth = Container + SkeletonDepth;
        }

        private void SerializeLODs(SerializingContainer Container)
        {
            int count = 0;
            if (!Container.isLoading)
                count = LODModels.Count();
            count = Container + count;
            if (Container.isLoading)
            {
                LODModels = new List<LODModelStruct>();
                for (int i = 0; i < count; i++)
                    LODModels.Add(new LODModelStruct());
            }
            for (int i = 0; i < count; i++)
            {
                LODModelStruct lod = LODModels[i];
                lod.Serialize(Container);
                LODModels[i] = lod;
            }
        }

        private void SerializeTail(SerializingContainer Container)
        {
            int count = 0;
            if (!Container.isLoading)
                count = TailNames.Count();
            count = Container + count;
            if (Container.isLoading)
            {
                TailNames = new List<TailNamesStruct>();
                for (int i = 0; i < count; i++)
                    TailNames.Add(new TailNamesStruct());
            }
            for (int i = 0; i < count; i++)
            {
                TailNamesStruct t = TailNames[i];
                t.Serialize(Container);
                TailNames[i] = t;
            }
            Unk1 = Container + Unk1;
            Unk2 = Container + Unk2;
            if (!Container.isLoading)
                count = Unk3.Count();
            count = Container + count;
            if (Container.isLoading)
            {
                Unk3 = new List<int>();
                for (int i = 0; i < count; i++)
                    Unk3.Add(0);
            }
            for (int i = 0; i < count; i++)
                Unk3[i] = Container + Unk3[i];
        }

        public int GetPropertyEnd()
        {
            int pos = 0x00;
            try
            {

                int test = BitConverter.ToInt32(Owner.Exports[MyIndex].Data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((Flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(Owner.Exports[MyIndex].Data, pos);
                    if (Owner.GetName(idxname) == "None" || Owner.GetName(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(Owner.Exports[MyIndex].Data, pos + 8);
                    int size = BitConverter.ToInt32(Owner.Exports[MyIndex].Data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (Owner.GetName(idxtype) == "StructProperty")
                        size += 8;
                    if (Owner.GetName(idxtype) == "ByteProperty")
                        size += 8;
                    pos += 24 + size;
                    if (pos > Owner.Exports[MyIndex].Data.Length)
                    {
                        pos -= 24 + size;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            return pos + 8;
        }
    }
}
