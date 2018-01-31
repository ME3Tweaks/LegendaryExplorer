using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SharpDX;
using ME3Explorer.Packages;
using System.Globalization;
using System.Diagnostics;

namespace ME3Explorer.Unreal.Classes
{
    public class SkeletalMesh
    {
        public struct BoundingStruct
        {
            public Vector3 origin;
            public Vector3 size;
            public float r;

            public BoundingStruct(Vector3 origin, Vector3 size, float radius)
            {
                this.origin = origin;
                this.size = size;
                this.r = radius;
            }

            public BoundingStruct(UDKExplorer.UDK.Classes.SkeletalMesh.BoundingStruct udkBounds)
            {
                this.origin = udkBounds.origin;
                this.size = udkBounds.size;
                this.r = udkBounds.r;
            }
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

            public BoneStruct(int name, int flags, int unk1, Vector4 orientation, Vector3 position,
                int numChildren, int parent, int boneColor)
            {
                Name = name;
                Flags = flags;
                Unk1 = unk1;
                Orientation = orientation;
                Position = position;
                NumChildren = numChildren;
                Parent = parent;
                BoneColor = boneColor;
            }

            public static BoneStruct ImportFromUDK(UDKExplorer.UDK.Classes.SkeletalMesh.BoneStruct udkBone, UDKExplorer.UDK.UDKObject udkPackage, ME3Explorer.Packages.MEPackage mePackage)
            {
                BoneStruct result = new BoneStruct(0, udkBone.Flags, udkBone.Unk1, udkBone.Orientation, udkBone.Position, udkBone.NumChildren, udkBone.Parent, udkBone.BoneColor);
                string name = udkPackage.GetName(udkBone.Name);
                result.Name = mePackage.FindNameOrAdd(name);
                return result;
            }
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

            public TreeNode ToTree(int MyIndex)
            {
                TreeNode res = new TreeNode("Section " + MyIndex);
                res.Nodes.Add("Material Index : " + MaterialIndex);
                res.Nodes.Add("Chunk Index : " + ChunkIndex);
                res.Nodes.Add("Base Index : " + BaseIndex);
                res.Nodes.Add("Num Triangles : " + NumTriangles);
                //res.Nodes.Add("Triangle Sorting : " + TriangleSorting);                
                return res;
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

            public TreeNode ToTree()
            {
                TreeNode res = new TreeNode("MultiSizeIndexContainer");
                //res.Nodes.Add("NeedsCPUAccess : " + NeedsCPUAccess);
                //res.Nodes.Add("DataTypeSize : " + DataTypeSize);
                res.Nodes.Add("IndexSize : " + IndexSize);
                res.Nodes.Add("IndexCount : " + IndexCount);
                TreeNode t = new TreeNode("Indexes");
                for (int i = 0; i < Indexes.Count; i++)
                    t.Nodes.Add(i + " : " + Indexes[i]);
                res.Nodes.Add(t);
                return res;
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

            public TreeNode ToTree(int MyIndex)
            {
                string s = MyIndex + " : Position : X(";
                s += Position.X + ") Y(" + Position.Y + ") Z(" + Position.Z + ") ";
                s += "TangentX(" + TangentX.ToString("X8") + ") TangentY(" + TangentY.ToString("X8") + ") TangentZ(" + TangentZ.ToString("X8") + ") ";
                for (int i = 0; i < 4; i++)
                    s += "UV[" + i + "](" + UV[i].X + " " + UV[i].Y + ") ";
                s += "Color : " + Color.ToString("X8") + " Bone : " + Bone;
                return new TreeNode(s);
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
            public TreeNode ToTree(int MyIndex)
            {
                string s = MyIndex + " : Position : X(";
                s += Position.X + ") Y(" + Position.Y + ") Z(" + Position.Z + ") ";
                s += "TangentX(" + TangentX.ToString("X8") + ") TangentY(" + TangentY.ToString("X8") + ") TangentZ(" + TangentZ.ToString("X8") + ") ";
                for (int i = 0; i < 4; i++)
                    s += "UV[" + i + "](" + UV[i].X + " " + UV[i].Y + ") ";
                s += "Color : " + Color.ToString("X8") + " InfluenceBones (";
                for (int i = 0; i < 3; i++)
                    s += InfluenceBones[i] + ", ";
                s += InfluenceBones[3] + ") InfluenceWeights (";
                for (int i = 0; i < 3; i++)
                    s += InfluenceWeights[i].ToString("X2") + ", ";
                s += InfluenceWeights[3].ToString("X2") + ")";
                return new TreeNode(s);
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

            public TreeNode ToTree(int MyIndex)
            {
                TreeNode res = new TreeNode("SkelMeshChunk " + MyIndex);
                res.Nodes.Add("Base Vertex Index : " + BaseVertexIndex);
                TreeNode t = new TreeNode("RigidSkinVertices (" + RiginSkinVertices.Count() + ")");
                for (int i = 0; i < RiginSkinVertices.Count; i++)
                    t.Nodes.Add(RiginSkinVertices[i].ToTree(i));
                res.Nodes.Add(t);
                t = new TreeNode("SoftSkinVertices (" + SoftSkinVertices.Count() + ")");
                for (int i = 0; i < SoftSkinVertices.Count; i++)
                    t.Nodes.Add(SoftSkinVertices[i].ToTree(i));
                res.Nodes.Add(t);
                t = new TreeNode("BoneMap (" + BoneMap.Count() + ")");
                for (int i = 0; i < BoneMap.Count; i++)
                    t.Nodes.Add(i + " : " + BoneMap[i]);
                res.Nodes.Add(t);
                res.Nodes.Add("NumRigidVertices : " + NumRigidVertices);
                res.Nodes.Add("NumSoftVertices : " + NumSoftVertices);
                res.Nodes.Add("MaxBoneInfluences : " + MaxBoneInfluences);
                return res;
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
            public TreeNode ToTree(int MyIndex)
            {
                string s = MyIndex + " : TanX : 0x" + TangentX.ToString("X8") + " ";
                s += "TanZ : 0x" + TangentZ.ToString("X8") + ") Position : X(";
                s += Position.X + ") Y(" + Position.Y + ") Z(" + Position.Z + ") ";
                s += "Influences  : [";
                for (int i = 0; i < 4; i++)
                    s += "(B:0x" + InfluenceBones[i].ToString("X2") + " W:" + InfluenceWeights[i].ToString("X2") + ")";
                s += "] UV : U(" + HalfToFloat(U) + ") V(" + HalfToFloat(V) + ") ";
                TreeNode res = new TreeNode(s);
                return res;
            }

            private float HalfToFloat(UInt16 val)
            {

                UInt16 u = val;
                int sign = (u >> 15) & 0x00000001;
                int exp = (u >> 10) & 0x0000001F;
                int mant = u & 0x000003FF;
                exp = exp + (127 - 15);
                int i = (sign << 31) | (exp << 23) | (mant << 13);
                byte[] buff = BitConverter.GetBytes(i);
                return BitConverter.ToSingle(buff, 0);
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

                    if (VertexDiff > 0)
                    {
                        byte b = 0;
                        for (int j = 0; j < VertexDiff; j++)
                            b = Container + b;
                    }
                    Vertices[i] = v;
                }
            }

            public TreeNode ToTree()
            {
                TreeNode res = new TreeNode("VertexBufferGPUSkin");
                res.Nodes.Add("NumTexCoords : " + NumTexCoords);
                //res.Nodes.Add("UseFullPrecisionUVs : " + UseFullPrecisionUVs);
                //res.Nodes.Add("UsePackedPosition : " + UsePackedPosition);
                res.Nodes.Add("Extension : X(" + Extension.X + ") Y(" + Extension.Y + ") Z(" + Extension.Z + ")");
                res.Nodes.Add("Origin : X(" + Origin.X + ") Y(" + Origin.Y + ") Z(" + Origin.Z + ")");
                res.Nodes.Add("VertexSize : " + VertexSize);
                TreeNode t = new TreeNode("Vertices (" + Vertices.Count + ")");
                for (int i = 0; i < Vertices.Count; i++)
                    t.Nodes.Add(Vertices[i].ToTree(i));
                res.Nodes.Add(t);
                return res;
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

            public TreeNode ToTree(int MyIndex)
            {
                TreeNode res = new TreeNode("LOD " + MyIndex);
                TreeNode t = new TreeNode("Sections");
                for (int i = 0; i < Sections.Count; i++)
                    t.Nodes.Add(Sections[i].ToTree(i));
                res.Nodes.Add(t);
                res.Nodes.Add(IndexBuffer.ToTree());
                res.Nodes.Add("Unk1 : 0x" + Unk1.ToString("X8"));
                t = new TreeNode("Active Bones");
                for (int i = 0; i < ActiveBones.Count; i++)
                    t.Nodes.Add(i + " : " + ActiveBones[i]);
                res.Nodes.Add(t);
                res.Nodes.Add("Unk2 : 0x" + Unk2.ToString("X8"));
                t = new TreeNode("Chunks");
                for (int i = 0; i < Chunks.Count; i++)
                    t.Nodes.Add(Chunks[i].ToTree(i));
                res.Nodes.Add(t);
                res.Nodes.Add("Size : " + Size);
                res.Nodes.Add("NumVertices : " + NumVertices);
                res.Nodes.Add("Unk3 : 0x" + Unk3.ToString("X8"));
                t = new TreeNode("Required Bones (" + RequiredBones.Count + ")");
                for (int i = 0; i < RequiredBones.Count; i++)
                    t.Nodes.Add(i + " : " + RequiredBones[i]);
                res.Nodes.Add(t);
                res.Nodes.Add("RawPointIndicesFlag: 0x" + RawPointIndicesFlag.ToString("X8"));
                res.Nodes.Add("RawPointIndicesCount: " + RawPointIndicesCount);
                res.Nodes.Add("RawPointIndicesSize: 0x" + RawPointIndicesSize.ToString("X8"));
                res.Nodes.Add("RawPointIndicesOffset: 0x" + RawPointIndicesOffset.ToString("X8"));
                t = new TreeNode("RawPointIndices (" + RawPointIndices.Count + ")");
                for (int i = 0; i < RawPointIndices.Count; i++)
                    t.Nodes.Add(i + " : " + RawPointIndices[i]);
                res.Nodes.Add(t);
                res.Nodes.Add("NumTexCoords : " + NumTexCoords);
                res.Nodes.Add(VertexBufferGPUSkin.ToTree());
                res.Nodes.Add("Unk4 : 0x" + Unk4.ToString("X8"));
                return res;
            }
        }

        public struct TailNamesStruct
        {
            public int Name;
            public int Unk1;
            public int Unk2;

            public TailNamesStruct(int nameIndex, int boneIndex)
            {
                Name = nameIndex;
                Unk1 = 0;
                Unk2 = boneIndex;
            }

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
        public List<MaterialInstanceConstant> MatInsts;
        public Vector3 Origin;
        public Vector3 Rotation;
        public List<BoneStruct> Bones;
        public int SkeletonDepth;
        public List<LODModelStruct> LODModels;
        public List<TailNamesStruct> TailNames;
        public int Unk1;
        public int Unk2;
        public List<int> Unk3;

        public IMEPackage Owner;
        public int MyIndex;
        public bool Loaded = false;
        private int ReadEnd;

        public SkeletalMesh()
        {
            Loaded = true;
        }
        
        public SkeletalMesh(IExportEntry export)
        {
            LoadSkeletalMesh(export);
        }

        private void LoadSkeletalMesh(IExportEntry export)
        {
            Loaded = true;
            MyIndex = export.Index;
            Owner = export.FileRef;
            Flags = (int)(export.ObjectFlags >> 32);
            int start = GetPropertyEnd();
            byte[] data = export.Data;
            byte[] buff = new byte[data.Length - start];
            //for (int i = 0; i < data.Length - start; i++)
            //    buff[i] = data[i + start];
            Buffer.BlockCopy(data, start, buff, 0, buff.Length);
            MemoryStream m = new MemoryStream(buff);
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = true;
            Serialize(Container);
            try
            {
                for (int i = 0; i < Materials.Count; i++)
                {

                    MatInsts.Add(new MaterialInstanceConstant(Owner, Materials[i] - 1));
                }
            }
            catch
            {
            }
        }

        public SkeletalMesh(IMEPackage pcc, int Index)
        {
            LoadSkeletalMesh(pcc.Exports[Index]);
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
                MatInsts = new List<MaterialInstanceConstant>();
                for (int i = 0; i < count; i++)
                {
                    Materials.Add(0);
                }
            }
            for (int i = 0; i < count; i++)
            {
                Materials[i] = Container + Materials[i];
            }
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

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("Skeletal Mesh");
            res.Nodes.Add(GetFlags(MyIndex));
            res.Nodes.Add(GetProperties(MyIndex));
            res.Nodes.Add(BoundingsToTree());
            res.Nodes.Add(MaterialsToTree());
            res.Nodes.Add(OrgRotToTree());
            res.ExpandAll();
            res.Nodes.Add(BonesToTree());
            res.Nodes.Add(LODsToTree());
            res.Nodes.Add(TailToTree());
            res.Nodes.Add("Read End @0x" + ReadEnd.ToString("X8"));
            return res;
        }

        public int GetPropertyEnd()
        {

            int pos = 0x00;
            try
            {
                byte[] data = Owner.Exports[MyIndex].Data;
                int test = BitConverter.ToInt32(data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((Flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(data, pos);
                    if (Owner.getNameEntry(idxname) == "None" || Owner.getNameEntry(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(data, pos + 8);
                    int size = BitConverter.ToInt32(data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (Owner.getNameEntry(idxtype) == "StructProperty")
                        size += 8;
                    if (Owner.getNameEntry(idxtype) == "ByteProperty")
                        size += 8;
                    pos += 24 + size;
                    if (pos > data.Length)
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

        private TreeNode GetFlags(int n)
        {
            TreeNode res = new TreeNode("Flags 0x" + Flags.ToString("X8"));
            foreach (string row in UnrealFlags.flagdesc)//0x02000000
            {
                string[] t = row.Split(',');
                long l = long.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                l = l >> 32;
                if ((l & Flags) != 0)
                    res.Nodes.Add(t[0].Trim());
            }
            return res;
        }

        private TreeNode GetProperties(int n)
        {
            TreeNode res = new TreeNode("Properties");

            int pos = 0x00;
            try
            {
                byte[] data = Owner.Exports[n].Data;
                int test = BitConverter.ToInt32(data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((Flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(data, pos);
                    if (Owner.getNameEntry(idxname) == "None" || Owner.getNameEntry(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(data, pos + 8);
                    int size = BitConverter.ToInt32(data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (Owner.getNameEntry(idxtype) == "StructProperty")
                        size += 8;
                    if (Owner.getNameEntry(idxtype) == "ByteProperty")
                        size += 8;
                    string s = pos.ToString("X8") + " " + Owner.getNameEntry(idxname) + " (" + Owner.getNameEntry(idxtype) + ") : ";
                    switch (Owner.getNameEntry(idxtype))
                    {
                        case "ObjectProperty":
                        case "IntProperty":
                            int val = BitConverter.ToInt32(data, pos + 24);
                            s += val.ToString();
                            break;
                        case "NameProperty":
                        case "StructProperty":
                            int name = BitConverter.ToInt32(data, pos + 24);
                            s += Owner.getNameEntry(name);
                            break;
                        case "FloatProperty":
                            float f = BitConverter.ToSingle(data, pos + 24);
                            s += f.ToString();
                            break;
                        case "BoolProperty":
                            s += (data[pos + 24] == 1).ToString();
                            break;
                        case "StrProperty":
                            int len = BitConverter.ToInt32(data, pos + 24);
                            for (int i = 0; i < len - 1; i++)
                                s += (char)data[pos + 28 + i];
                            break;
                    }
                    res.Nodes.Add(s);
                    pos += 24 + size;
                    if (pos > data.Length)
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
            res.Nodes.Add(pos.ToString("X8") + " None");
            return res;
        }

        private TreeNode BoundingsToTree()
        {
            TreeNode res = new TreeNode("Boundings");
            res.Nodes.Add("Origin : X(" + Bounding.origin.X + ") Y(" + Bounding.origin.Y + ") Z(" + Bounding.origin.Z + ")");
            res.Nodes.Add("Size : X(" + Bounding.size.X + ") Y(" + Bounding.size.Y + ") Z(" + Bounding.size.Z + ")");
            res.Nodes.Add("Radius : R(" + Bounding.r + ")");
            return res;
        }

        private TreeNode MaterialsToTree()
        {
            TreeNode res = new TreeNode("Materials");
            foreach (MaterialInstanceConstant matconst in MatInsts)
                res.Nodes.Add(matconst.ToTree());
            return res;
        }

        private TreeNode OrgRotToTree()
        {
            TreeNode res = new TreeNode("Origin/Rotation");
            res.Nodes.Add("Origin : X(" + Origin.X + ") Y(" + Origin.Y + ") Z(" + Origin.Z + ")");
            res.Nodes.Add("Rotation : X(" + Rotation.X + ") Y(" + Rotation.Y + ") Z(" + Rotation.Z + ")");
            return res;
        }

        private TreeNode BonesToTree()
        {
            TreeNode res = new TreeNode("Bones (" + Bones.Count + ") Depth : " + SkeletonDepth);
            for (int i = 0; i < Bones.Count; i++)
            {
                BoneStruct b = Bones[i];
                string s = "Name : \"" + Owner.getNameEntry(b.Name) + "\" ";
                s += "Flags : 0x" + b.Flags.ToString("X8") + " ";
                s += "Unk1 : 0x" + b.Unk1.ToString("X8") + " ";
                s += "Orientation : X(" + b.Orientation.X + ") Y(" + b.Orientation.X + ") Z(" + b.Orientation.Z + ") W(" + b.Orientation.W + ")";
                s += "Position : X(" + b.Position.X + ") Y(" + b.Position.X + ") Z(" + b.Position.Z + ")";
                s += "NumChildren : " + b.NumChildren + " ";
                s += "Parent : " + b.Parent + " ";
                s += "Color : 0x" + b.BoneColor.ToString("X8");
                res.Nodes.Add(s);
            }
            return res;
        }

        private TreeNode LODsToTree()
        {
            TreeNode res = new TreeNode("LOD Models");
            for (int i = 0; i < LODModels.Count; i++)
                res.Nodes.Add(LODModels[i].ToTree(i));
            return res;
        }

        private TreeNode TailToTree()
        {
            TreeNode res = new TreeNode("Tail");
            TreeNode t = new TreeNode("Weird Bone List (" + TailNames.Count + ")");
            for (int i = 0; i < TailNames.Count; i++)
                t.Nodes.Add(i + " : Name \"" + Owner.getNameEntry(TailNames[i].Name) + "\" Unk1 (" + TailNames[i].Unk1.ToString("X8") + ") Unk2(" + TailNames[i].Unk2.ToString("X8") + ")");
            res.Nodes.Add(t);
            res.Nodes.Add("Unk1 : " + Unk1.ToString("X8"));
            res.Nodes.Add("Unk2 : " + Unk2.ToString("X8"));
            t = new TreeNode("Unk3 (" + Unk3.Count + ")");
            for (int i = 0; i < Unk3.Count; i++)
                t.Nodes.Add(i + " : " + Unk3[i]);
            res.Nodes.Add(t);
            return res;
        }

        private float HalfToFloat(UInt16 val)
        {

            UInt16 u = val;
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }

        public void ExportOBJ(string path)
        {
            using (StreamWriter writer = new StreamWriter(path))
            using (StreamWriter mtlWriter = new StreamWriter(Path.ChangeExtension(path, ".mtl")))
            {
                writer.WriteLine("mtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl");
                // Vertices
                List<Vector3> points = null;
                List<Vector2> uvs = null;
                Dictionary<int, int> LODVertexOffsets = new Dictionary<int, int>(); // offset into the OBJ vertices that each buffer starts at
                points = new List<Vector3>();
                uvs = new List<Vector2>();
                int index = 0;
                int lodIndex = 0;
                foreach (var lod in LODModels)
                {
                    LODVertexOffsets.Add(lodIndex, index);
                    foreach (var gpuVertex in lod.VertexBufferGPUSkin.Vertices)
                    {
                        points.Add(gpuVertex.Position);
                        uvs.Add(new Vector2(HalfToFloat(gpuVertex.U), HalfToFloat(gpuVertex.V)));
                    }

                    foreach (var mat in MatInsts)
                    {
                        mtlWriter.WriteLine("newmtl " + Owner.getObjectName(mat.index));
                    }

                    lodIndex++;
                    index += lod.NumVertices;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 v = points[i];
                    writer.WriteLine("v " + v.X.ToString(CultureInfo.InvariantCulture) + " " + v.Z.ToString(CultureInfo.InvariantCulture) + " " + v.Y.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("vt " + uvs[i].X.ToString(CultureInfo.InvariantCulture) + " " + uvs[i].Y.ToString(CultureInfo.InvariantCulture));
                }

                // Triangles
                foreach (var lod in LODModels)
                {
                    int lodStart = LODVertexOffsets[lodIndex];
                    foreach (var section in lod.Sections)
                    {
                        writer.WriteLine("usemtl " + Owner.getObjectName(MatInsts[section.MaterialIndex].index));
                        writer.WriteLine("g LOD" + lodIndex + "-" + Owner.getObjectName(MatInsts[section.MaterialIndex].index));

                        for (int i = section.BaseIndex; i < section.BaseIndex + section.NumTriangles * 3; i += 3)
                        {
                            writer.WriteLine("f " + (lodStart + lod.IndexBuffer.Indexes[i] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (lodStart + lod.IndexBuffer.Indexes[i] + 1).ToString(CultureInfo.InvariantCulture) + " "
                                + (lodStart + lod.IndexBuffer.Indexes[i + 1] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (lodStart + lod.IndexBuffer.Indexes[i + 1] + 1).ToString(CultureInfo.InvariantCulture) + " "
                                + (lodStart + lod.IndexBuffer.Indexes[i + 2] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (lodStart + lod.IndexBuffer.Indexes[i + 2] + 1).ToString(CultureInfo.InvariantCulture));
                        }
                    }
                    lodIndex++;
                }
            }
        }
    }
}
