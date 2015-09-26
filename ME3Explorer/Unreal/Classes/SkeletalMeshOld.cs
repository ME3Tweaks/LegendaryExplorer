using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using KFreonLib.Debugging;

namespace ME3Explorer.Unreal.Classes
{
    public class SkeletalMeshOld
    {
        public byte[] memory;
        public int memsize;
        public PCCObject pcc;
        public List<PropertyReader.Property> props;
        public int readerpos;
        public PSKFile psk;
        public SkelMesh Mesh;
        public List<CustomVertex.PositionTextured[]> DirectXSections;
        public Texture DefaultTex;
        public List<TailEntry> Tail;

        #region Structures

        public struct SkelMesh
        {
            public Bounds Bounding;
            public List<int> Materials;
            public Vector3 Origin;
            public Rotator Rotation;
            public List<Bone> Bones;
            public int BoneTreeDepth;
            public List<LOD> LODs;
            public byte[] Tail;
        }

        public struct Bounds
        {
            public Vector3 org;
            public Vector3 box;
            public float r;
        }

        public struct Rotator
        {
            public int yaw;
            public int pitch;
            public int roll;
        }

        public struct Quad
        {
            public float w;
            public float x;
            public float y;
            public float z;
            public Quad(Vector4 v)
            {
                x = v.X;
                y = v.Y;
                z = v.Z;
                w = v.W;
            }
            public Vector4 ToVec4()
            {
                return new Vector4(x, y, z, w);
            }            
        }

        public struct Bone
        {
            public int name;
            public Quad orientation;
            public Vector3 position;
            public int childcount;
            public int parent;
            public int index;
            public int unk1;
            public int unk2;
            public int unk3;
            public int _offset;
        }

        public struct LOD
        {
            public List<LODHeader> Headers;
            public List<UInt16> Indexes;
            public UInt32 SoftVerticeCount;
            public List<UInt16> UnkIndexes1;
            public int UnkCount1;
            public List<UInt32> UnkIndexes2;
            public List<UnknownSection> UnkSec1;
            public List<byte> ActiveBones;
            public byte[] UnkSec2;
            public List<Edge> Edges;
            public UInt32 unk2;
            public int _offset;
        }

        public struct LODHeader
        {
            public UInt16 index;
            public UInt16 matindex;
            public UInt32 offset;
            public UInt32 count;
            public UInt16 unk1;
        }

        public struct UnknownSection
        {
            public List<UInt16> Indexes;
            public byte[] Unkn;
        }

        public struct Edge
        {
            public int Unk1;
            public int Unk2;
            public List<Influence> Influences;
            public Vector3 Position;
            public Vector2 UV;
            public int _offset;
            public bool _imported;
        }

        public struct Influence
        {
            public byte bone;
            public byte weight;

            public Influence(float _f, int _bone)
            {
                weight = (byte)(_f * 255);
                bone = (byte)_bone;
            }
        }

        public struct TailEntry
        {
            public int Unk0;
            public int Unk1;
            public int Unk2;
        }

        #endregion 

        public SkeletalMeshOld(PCCObject Pcc, byte[] Raw)
        {
            pcc = Pcc;
            memory = Raw;
            memsize = memory.Length;
            props = PropertyReader.getPropList(pcc, memory);
            Deserialize();
        }

        #region Deserialize

        public void Deserialize()
        {
            Mesh = new SkelMesh();
            readerpos = props[props.Count - 1].offend;
            ReadBounds();
            ReadMaterial();
            ReadOrgRot();
            ReadBones();
            ReadLODs();
            ReadTail();
            GenerateDirectXMesh();
        }

        public void ReadBounds()
        {
            Mesh.Bounding = new Bounds();
            Mesh.Bounding.org = ReadVector(readerpos);
            Mesh.Bounding.box = ReadVector(readerpos + 12);
            Mesh.Bounding.r = BitConverter.ToSingle(memory, readerpos + 24);
            readerpos += 28;
        }

        public void ReadMaterial()
        {
            Mesh.Materials = new List<int>();
            int count = BitConverter.ToInt32(memory, readerpos);
            readerpos += 4;
            for (int i = 0; i < count; i++)
            {
                Mesh.Materials.Add(BitConverter.ToInt32(memory, readerpos));
                readerpos += 4;
            }
        }

        public void ReadOrgRot()
        {
            Mesh.Origin = ReadVector(readerpos);
            Mesh.Rotation = ReadRotator(readerpos + 12);
            readerpos += 24;
        }

        public void ReadBones()
        {
            int count = BitConverter.ToInt32(memory, readerpos);
            readerpos += 4;
            Mesh.Bones = new List<Bone>();
            for (int i = 0; i < count; i++)
            {
                Bone b = new Bone();
                b._offset = readerpos;
                b.name = BitConverter.ToInt32(memory, readerpos);
                b.unk1 = BitConverter.ToInt32(memory, readerpos + 4);
                b.unk2 = BitConverter.ToInt32(memory, readerpos + 8);
                b.orientation = ReadQuad(readerpos + 12);
                b.position = ReadVector(readerpos + 28);
                b.childcount = BitConverter.ToInt32(memory, readerpos + 40);
                b.parent = BitConverter.ToInt32(memory, readerpos + 44);
                b.unk3 = BitConverter.ToInt32(memory, readerpos + 48);
                readerpos += 52;
                Mesh.Bones.Add(b);
            }
            Mesh.BoneTreeDepth = BitConverter.ToInt32(memory, readerpos);
            readerpos += 4;
        }

        public void ReadLODs()
        {
            int count = BitConverter.ToInt32(memory, readerpos);
            readerpos += 4;
            Mesh.LODs = new List<LOD>();
            for (int i = 0; i < count; i++)
            {
                LOD lod = new LOD();
                lod._offset = readerpos;
                lod.Headers = new List<LODHeader>();
                int sectioncount = BitConverter.ToInt32(memory, readerpos);
                readerpos += 4;
                for (int j = 0; j < sectioncount; j++)
                {
                    LODHeader lodsec = new LODHeader();
                    lodsec.matindex = BitConverter.ToUInt16(memory, readerpos);
                    lodsec.index = BitConverter.ToUInt16(memory, readerpos + 2);                    
                    lodsec.offset = BitConverter.ToUInt32(memory, readerpos + 4);
                    lodsec.count = BitConverter.ToUInt16(memory, readerpos + 8);
                    lodsec.unk1 = BitConverter.ToUInt16(memory, readerpos + 10);
                    readerpos += 12;
                    lod.Headers.Add(lodsec);
                }
                int indexsize = BitConverter.ToInt32(memory, readerpos);
                int indexcount = BitConverter.ToInt32(memory, readerpos + 4);
                readerpos += 8;
                lod.Indexes = new List<ushort>();
                for (int j = 0; j < sectioncount; j++)
                {
                    for (int k = 0; k < lod.Headers[j].count * 3; k++)
                    {
                        lod.Indexes.Add(BitConverter.ToUInt16(memory, readerpos));
                        readerpos += 2;
                    }
                }
                indexcount = BitConverter.ToInt32(memory, readerpos); // ?? SoftVertices ??
                readerpos += 4;
                if (indexcount != 0)
                {
                    MessageBox.Show("Not implemented!");
                    return;
                }
                indexcount = BitConverter.ToInt32(memory, readerpos); // ?? RigidVertices ??
                readerpos += 4;
                lod.UnkIndexes1 = new List<ushort>();
                for (int k = 0; k < indexcount; k++)
                {
                    lod.UnkIndexes1.Add(BitConverter.ToUInt16(memory, readerpos));
                    readerpos += 2;
                }
                indexcount = BitConverter.ToInt32(memory, readerpos); // ?? ShadowIndices ??
                readerpos += 4;
                if (indexcount != 0)
                {
                    MessageBox.Show("Not implemented!");
                    return;
                }
                lod.UnkCount1 = BitConverter.ToInt32(memory, readerpos); // ?? Sections ??
                readerpos += 4;
                lod.UnkIndexes2 = new List<UInt32>();
                for (int k = 0; k < 3; k++)                         // ?? Rot/Loc ??
                {
                    lod.UnkIndexes2.Add(BitConverter.ToUInt32(memory, readerpos));
                    readerpos += 4;
                }
                lod.UnkSec1 = new List<UnknownSection>();
                for (int k = 0; k < lod.UnkCount1; k++)    // ?? Sections ?? Materials ??
                {
                    UnknownSection unk = new UnknownSection();
                    indexcount = BitConverter.ToInt32(memory, readerpos);
                    readerpos += 4;
                    unk.Indexes = new List<ushort>();
                    for (int l = 0; l < indexcount; l++) // ?? active bones ??
                    {
                        unk.Indexes.Add(BitConverter.ToUInt16(memory, readerpos));
                        readerpos += 2;
                    }
                    unk.Unkn = new byte[24];
                    for (int l = 0; l < 24; l++)
                        unk.Unkn[l] = memory[readerpos + l];
                    readerpos += 24;
                    lod.UnkSec1.Add(unk);
                }
                indexcount = BitConverter.ToInt32(memory, readerpos);
                readerpos += 4;
                lod.ActiveBones = new List<byte>();
                for (int k = 0; k < indexcount; k++)
                    lod.ActiveBones.Add(memory[readerpos + k]);
                readerpos += indexcount;
                lod.UnkSec2 = new byte[48];
                for (int k = 0; k < 48; k++)
                    lod.UnkSec2[k] = memory[readerpos + k];
                readerpos += 48;
                indexsize = BitConverter.ToInt32(memory, readerpos);
                indexcount = BitConverter.ToInt32(memory, readerpos + 4);
                readerpos += 8;
                lod.Edges = new List<Edge>();
                for (int k = 0; k < indexcount; k++)
                {
                    Edge e = new Edge();
                    e._offset = readerpos;
                    e.Unk1 = BitConverter.ToInt32(memory, readerpos);
                    e.Unk2 = BitConverter.ToInt32(memory, readerpos + 4);
                    e.Influences = new List<Influence>();
                    for (int l = 0; l < 4; l++)
                    {
                        Influence inf = new Influence();
                        inf.bone = memory[readerpos + 8 + l];
                        inf.weight = memory[readerpos + 12 + l];
                        e.Influences.Add(inf);
                    }
                    e.Position = ReadVector(readerpos + 16);
                    UInt16 u = BitConverter.ToUInt16(memory, readerpos + 28);
                    UInt16 v = BitConverter.ToUInt16(memory, readerpos + 30); 
                    e.UV = new Vector2(HalfToFloat(u), HalfToFloat(v));
                    e._imported = false;
                    lod.Edges.Add(e);
                    readerpos += 32;
                }
                lod.unk2 = BitConverter.ToUInt32(memory, readerpos);
                readerpos += 4;                
                Mesh.LODs.Add(lod);
            }
        }

        public void ReadTail()
        {
            byte[] buffer = new byte[memory.Length - readerpos];
            for (int i = readerpos; i < memory.Length; i++)
                buffer[i - readerpos] = memory[i];
            Mesh.Tail = buffer;
            Tail = new List<TailEntry>();
            int count = BitConverter.ToInt32(buffer, 0);
            int pos = 4;
            for (int i = 0; i < count; i++)
            {
                TailEntry e = new TailEntry();
                e.Unk0 = BitConverter.ToInt32(buffer, pos);
                e.Unk1 = BitConverter.ToInt32(buffer, pos + 4);
                e.Unk2 = BitConverter.ToInt32(buffer, pos + 8);
                pos += 12;
                Tail.Add(e);
            }
        }

        

        #endregion

        #region Serialize

        public byte[] SerializeToBuffer()
        {
            MemoryStream m = new MemoryStream();
            BitConverter.IsLittleEndian = true;
            WriteBounds(m);
            WriteMaterials(m);
            WriteOrgRot(m);
            WriteBoneSer(m);
            WriteLODs(m);
            WriteTail(m);
            return m.ToArray();
        }

        public byte[] Serialize()
        {
            MemoryStream m = new MemoryStream();
            byte[] buff = SerializeToBuffer();
            int end = props[props.Count - 1].offend;
            m.Write(memory, 0, end);//properties
            m.Write(buff,0,buff.Length);//binary part
            return m.ToArray();
        }

        public void SerializeToFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            byte[] buff = SerializeToBuffer();
            fs.Write(buff, 0, buff.Length);
            fs.Close();
        }

        public void WriteBounds(MemoryStream m)
        {
            Bounds b = Mesh.Bounding;
            WriteVector(m, b.org);
            WriteVector(m, b.box);
            m.Write(BitConverter.GetBytes(b.r),0,4);
        }

        public void WriteMaterials(MemoryStream m)
        {
            m.Write(BitConverter.GetBytes(Mesh.Materials.Count()), 0, 4);
            foreach (int i in Mesh.Materials)
                m.Write(BitConverter.GetBytes(i), 0, 4);
        }

        public void WriteOrgRot(MemoryStream m)
        {
            WriteVector(m, Mesh.Origin);
            WriteRotator(m, Mesh.Rotation);
        }

        public void WriteBoneSer(MemoryStream m)
        {
            m.Write(BitConverter.GetBytes(Mesh.Bones.Count()), 0, 4);
            foreach (Bone b in Mesh.Bones)
            {
                m.Write(BitConverter.GetBytes(b.name), 0, 4);
                m.Write(BitConverter.GetBytes(b.unk1), 0, 4);
                m.Write(BitConverter.GetBytes(b.unk2), 0, 4);
                WriteQuad(m, b.orientation);
                WriteVector(m, b.position);
                m.Write(BitConverter.GetBytes(b.childcount), 0, 4);
                m.Write(BitConverter.GetBytes(b.parent), 0, 4);
                m.Write(BitConverter.GetBytes(b.unk3), 0, 4);
            }
        }

        public void WriteLODs(MemoryStream m)
        {
            //LOD levels, size and count
            m.Write(BitConverter.GetBytes(Mesh.BoneTreeDepth), 0, 4);
            m.Write(BitConverter.GetBytes(Mesh.LODs.Count()), 0, 4);

            for (int i=0; i< Mesh.LODs.Count(); i++)
            {  //For each LOD, do all the stuff below
                
                //Headers, count and data
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].Headers.Count()), 0, 4);
                foreach (LODHeader X in Mesh.LODs[i].Headers)
                {
                    m.Write(BitConverter.GetBytes(X.matindex), 0, 2);
                    m.Write(BitConverter.GetBytes(X.index), 0, 2);                    
                    m.Write(BitConverter.GetBytes(X.offset), 0, 4);
                    m.Write(BitConverter.GetBytes(X.count), 0, 2);
                    m.Write(BitConverter.GetBytes(X.unk1), 0, 2);

                }
                
                //Indexes - Size and count. Size is always 2.
                m.Write(BitConverter.GetBytes(2), 0, 4);
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].Indexes.Count()), 0, 4);

                foreach (UInt16 X in Mesh.LODs[i].Indexes)
                {
                    m.Write(BitConverter.GetBytes(X), 0, 2);
                }
                
                //Here is first unknown Not Implemented Index count = writing 4 zeroes. Probably need to fix this later!
                    m.Write(BitConverter.GetBytes(0), 0, 4);

                  //  Unknown Indexes 1
                    m.Write(BitConverter.GetBytes(Mesh.LODs[i].UnkIndexes1.Count()), 0, 4);
                    foreach (UInt16 X in Mesh.LODs[i].UnkIndexes1) m.Write(BitConverter.GetBytes(X), 0, 2);

                //Shadow stuff I guess, second Not Implemented, write 4 zeroes
                   m.Write(BitConverter.GetBytes(0), 0, 4);


                //UnkCount1 standard integer
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].UnkCount1), 0, 4);


                //UnkIndexes2, 3 elements always, but still, foreach ^^
                foreach (UInt32 X in Mesh.LODs[i].UnkIndexes2) m.Write(BitConverter.GetBytes(X), 0, 4);

                //Unknown section, bunch of 2-byte ints and byte array of whatever
                
                foreach (UnknownSection X in Mesh.LODs[i].UnkSec1)
                {
                    m.Write(BitConverter.GetBytes(X.Indexes.Count()), 0, 4);

                    foreach (UInt16 Y in X.Indexes)
                    {
                        m.Write(BitConverter.GetBytes(Y), 0, 2);
                    }

                    m.Write(X.Unkn, 0, X.Unkn.Length);
                    //See? Byte array of whatever. 

                    //Actually byte array is possibly a collection of characters to use, Hex view offers entire
                    //Alphabet and some symbols
                }

                //Active Bones byte array, number of bytes:
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].ActiveBones.Count()), 0, 4);

                //And bytes themselves:
                foreach (byte X in Mesh.LODs[i].ActiveBones)
                {
                    m.Write(BitConverter.GetBytes(X), 0, 1);
                }

                //Unknown Section 2, is 48 bytes by default for now
                m.Write(Mesh.LODs[i].UnkSec2, 0, Mesh.LODs[i].UnkSec2.Length);


                //Edge size and count
                m.Write(BitConverter.GetBytes(32), 0, 4);
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].Edges.Count), 0, 4);


                //Edges
                foreach (Edge X in Mesh.LODs[i].Edges)
                {
                    m.Write(BitConverter.GetBytes(X.Unk1), 0, 4);
                    m.Write(BitConverter.GetBytes(X.Unk2), 0, 4);
                    for (int j = 0; j < 4; j++)
                        m.WriteByte((byte)X.Influences[j].bone);
                    for (int j = 0; j < 4; j++)
                        m.WriteByte((byte)X.Influences[j].weight);
                    //Took me a while for this!
                    
                    WriteVector(m, X.Position);

                    
                        m.Write(BitConverter.GetBytes(FloatToHalf(BitConverter.ToSingle(BitConverter.GetBytes(X.UV.X), 0))), 0, 2);
                        m.Write(BitConverter.GetBytes(FloatToHalf(BitConverter.ToSingle(BitConverter.GetBytes(X.UV.Y), 0))), 0, 2);
                        //Dat syntax! (⌐■_■)

                }

                //Unk2, unsigned integer
                m.Write(BitConverter.GetBytes(Mesh.LODs[i].unk2), 0, 4);

            }

        }

        public void WriteTail(MemoryStream m)
        {
            //Tail data. Very sparse, unknown. Maybe holds Tali's face.

            m.Write(Mesh.Tail, 0, Mesh.Tail.Length);

            //
            //End of struct!
            //
        }

        #endregion

        #region helpers

        public void WriteVector(MemoryStream m, Vector3 v)
        {
            m.Write(BitConverter.GetBytes(v.X), 0, 4);
            m.Write(BitConverter.GetBytes(v.Y), 0, 4);
            m.Write(BitConverter.GetBytes(v.Z), 0, 4);
        }

        public void WriteVector2(MemoryStream m, Vector2 Vector)
        {
            m.Write(BitConverter.GetBytes(Vector.X), 0, 4);
            m.Write(BitConverter.GetBytes(Vector.Y), 0, 4);
        }

        public void WriteQuad(MemoryStream m, Quad q)
        {
            m.Write(BitConverter.GetBytes(q.x), 0, 4);
            m.Write(BitConverter.GetBytes(q.y), 0, 4);
            m.Write(BitConverter.GetBytes(q.z), 0, 4);
            m.Write(BitConverter.GetBytes(q.w), 0, 4);
        }

        public void WriteRotator(MemoryStream m, Rotator r)
        {
            m.Write(BitConverter.GetBytes(r.yaw), 0, 4);
            m.Write(BitConverter.GetBytes(r.pitch), 0, 4);
            m.Write(BitConverter.GetBytes(r.roll), 0, 4);
        }

        public Vector3 ReadVector(int pos)
        {
            return new Vector3(BitConverter.ToSingle(memory,pos), 
                               BitConverter.ToSingle(memory,pos + 4), 
                               BitConverter.ToSingle(memory,pos + 8));
        }

        public Quad ReadQuad(int pos)
        {
            Quad q = new Quad();
            q.x = BitConverter.ToSingle(memory, pos);
            q.y = BitConverter.ToSingle(memory, pos + 4);
            q.z = BitConverter.ToSingle(memory, pos + 8);
            q.w = BitConverter.ToSingle(memory, pos + 12);
            return q;
        }

        public Rotator ReadRotator(int pos)
        {
            Rotator r = new Rotator();
            r.yaw = BitConverter.ToInt32(memory, pos);
            r.pitch = BitConverter.ToInt32(memory, pos + 4);
            r.roll = BitConverter.ToInt32(memory, pos + 8);
            return r;
        }

        public float HalfToFloat(UInt16 val)
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

        public UInt16 FloatToHalf(float f)
        {
            byte[] bytes = BitConverter.GetBytes((double)f);
            ulong bits = BitConverter.ToUInt64(bytes, 0);
            ulong exponent = bits & 0x7ff0000000000000L;
            ulong mantissa = bits & 0x000fffffffffffffL;
            ulong sign = bits & 0x8000000000000000L;
            int placement = (int)((exponent >> 52) - 1023);
            if (placement > 15 || placement < -14)
                return 0;
            UInt16 exponentBits = (UInt16)((15 + placement) << 10);
            UInt16 mantissaBits = (UInt16)(mantissa >> 42);
            UInt16 signBits = (UInt16)(sign >> 48);
            return (UInt16)(exponentBits | mantissaBits | signBits);
        }
        
        public Vector3 ToVec3(PSKFile.PSKPoint p)
        {
            return new Vector3(p.x, p.y, p.z);
        }

        public List<Vector3> ToVec3(List<PSKFile.PSKPoint> points)
        {
            List<Vector3> v = new List<Vector3>();
            foreach (PSKFile.PSKPoint p in points)
                v.Add(new Vector3(p.x, p.y, p.z));
            return v;
        }

        public Vector3[] ToVec3(List<Edge> e)
        {
            Vector3[] v = new Vector3[e.Count];
            for (int i = 0; i < e.Count; i++)
                v[i] = e[i].Position;
            return v;
        }

        public void CalcTangentSpace(LOD l)
        {
            int vertexCount = l.Edges.Count();
            Vector3[] vertices = ToVec3(l.Edges);
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] texcoords = new Vector2[vertexCount];
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                if (MPOpt.SKM_cullfaces)
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 2];
                    i2 = l.Indexes[i * 3 + 1];
                }
                else
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 1];
                    i2 = l.Indexes[i * 3 + 2];
                }
                Vector3 v1 = l.Edges[i0].Position;
                Vector3 v2 = l.Edges[i1].Position;
                Vector3 v3 = l.Edges[i2].Position;
                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                normals[i0] += Vector3.Cross(edge1, edge2);
                edge1 = v3 - v2;
                edge2 = v1 - v2;
                normals[i1] += Vector3.Cross(edge1, edge2);
                edge1 = v1 - v3;
                edge2 = v2 - v3;
                normals[i2] += Vector3.Cross(edge1, edge2);
                texcoords[i0] = l.Edges[i0].UV;
                texcoords[i1] = l.Edges[i1].UV;
                texcoords[i2] = l.Edges[i2].UV;
            }
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                if (MPOpt.SKM_cullfaces)
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 2];
                    i2 = l.Indexes[i * 3 + 1];
                }
                else
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 1];
                    i2 = l.Indexes[i * 3 + 2];
                }
                normals[i0].Normalize();
                normals[i1].Normalize();
                normals[i2].Normalize();
            }
            Vector4[] tangents = new Vector4[vertexCount];
            Vector4[] bitangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                if (MPOpt.SKM_cullfaces)
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 2];
                    i2 = l.Indexes[i * 3 + 1];
                }
                else
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 1];
                    i2 = l.Indexes[i * 3 + 2];
                }
                Vector3 v1 = vertices[i0];
                Vector3 v2 = vertices[i1];
                Vector3 v3 = vertices[i2];
                Vector2 w1 = texcoords[i0];
                Vector2 w2 = texcoords[i1];
                Vector2 w3 = texcoords[i2];

                float x1 = v2.X - v1.X;
                float x2 = v3.X - v1.X;
                float y1 = v2.Y - v1.Y;
                float y2 = v3.Y - v1.Y;
                float z1 = v2.Z - v1.Z;
                float z2 = v3.Z - v1.Z;

                float s1 = w2.X - w1.X;
                float s2 = w3.X - w1.X;
                float t1 = w2.Y - w1.Y;
                float t2 = w3.Y - w1.Y;

                float r = 1.0f / (s1 * t2 - s2 * t1);

                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i0] += sdir;
                tan1[i1] += sdir;
                tan1[i2] += sdir;

                tan2[i0] += tdir;
                tan2[i1] += tdir;
                tan2[i2] += tdir;
            }
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan1[i];
                Vector3 t2 = tan2[i];
                Vector3 tmp = (t - n * Vector3.Dot(n, t));
                tmp.Normalize();
                t2.Normalize();
                tangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z, 0);
                tangents[i].W = (Vector3.Dot(Vector3.Cross(n, t), t2) < 0.0f) ? -1.0f : 1.0f;
                tangents[i].Normalize();
            }
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan2[i];
                Vector3 t2 = tan1[i];
                Vector3 tmp = (t - n * Vector3.Dot(n, t));
                tmp.Normalize();
                t2.Normalize();
                bitangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z, 0);
                bitangents[i].W = (Vector3.Dot(Vector3.Cross(n, t), t2) < 0.0f) ? -1.0f : 1.0f;
                bitangents[i].Normalize();
            }
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                if (MPOpt.SKM_cullfaces)
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 2];
                    i2 = l.Indexes[i * 3 + 1];
                }
                else
                {
                    i0 = l.Indexes[i * 3];
                    i1 = l.Indexes[i * 3 + 1];
                    i2 = l.Indexes[i * 3 + 2];
                }                

                Vector4 bi0 = bitangents[i0];
                Vector4 bi1 = bitangents[i1];
                Vector4 bi2 = bitangents[i2];
                Vector4 tn0 = tangents[i0];
                Vector4 tn1 = tangents[i1];
                Vector4 tn2 = tangents[i2];
                if (MPOpt.SKM_fixtexcoord)
                {
                    Vector2 t1 = texcoords[i1] - texcoords[i0];
                    Vector2 t2 = texcoords[i2] - texcoords[i1];
                    Vector3 tc = Vector3.Cross(Vector3.Normalize(new Vector3(t1.X, t1.Y, 0)), Vector3.Normalize(new Vector3(t2.X, t2.Y, 0)));
                    if (tc.Z > 0) //facing backwards?
                    {
                        bi0 *= -1;
                        bi1 *= -1;
                        bi2 *= -1;
                        tn0 *= -1;
                        tn1 *= -1;
                        tn2 *= -1;
                    }
                }
                #region component flips
                if (MPOpt.SKM_tnflipX)
                {
                    tn0.X *= -1;
                    tn1.X *= -1;
                    tn2.X *= -1;
                }
                if (MPOpt.SKM_tnflipY)
                {
                    tn0.Y *= -1;
                    tn1.Y *= -1;
                    tn2.Y *= -1;
                }
                if (MPOpt.SKM_tnflipZ)
                {
                    tn0.Z *= -1;
                    tn1.Z *= -1;
                    tn2.Z *= -1;
                }
                if (MPOpt.SKM_tnflipW)
                {
                    tn0.W *= -1;
                    tn1.W *= -1;
                    tn2.W *= -1;
                }
                if (MPOpt.SKM_biflipX)
                {
                    bi0.X *= -1;
                    bi1.X *= -1;
                    bi2.X *= -1;
                }
                if (MPOpt.SKM_biflipY)
                {
                    bi0.Y *= -1;
                    bi1.Y *= -1;
                    bi2.Y *= -1;
                }
                if (MPOpt.SKM_biflipZ)
                {
                    bi0.Z *= -1;
                    bi1.Z *= -1;
                    bi2.Z *= -1;
                }
                if (MPOpt.SKM_biflipW)
                {
                    bi0.W *= -1;
                    bi1.W *= -1;
                    bi2.W *= -1;
                }
                #endregion
                if (MPOpt.SKM_tnW100)
                {
                    tn0.W *= 0.01f;
                    tn1.W *= 0.01f;
                    tn2.W *= 0.01f;
                    tn0.Normalize();
                    tn1.Normalize();
                    tn2.Normalize();
                }
                if (MPOpt.SKM_normalize)
                {
                    bi0.Normalize();
                    bi1.Normalize();
                    bi2.Normalize();
                    tn0.Normalize();
                    tn1.Normalize();
                    tn2.Normalize();
                }
                if (MPOpt.SKM_swaptangents)
                {
                    ApplyTangents(l, i0, tn0, bi0);
                    ApplyTangents(l, i1, tn1, bi1);
                    ApplyTangents(l, i2, tn2, bi2);
                }
                else
                {
                    ApplyTangents(l, i0, bi0, tn0);
                    ApplyTangents(l, i1, bi1, tn1);
                    ApplyTangents(l, i2, bi2, tn2);
                }
            }
        }

        public float InvSqrt(float x)
        {
            float xhalf = 0.5f * x;
            int i = BitConverter.ToInt32(BitConverter.GetBytes(x), 0);
            i = 0x5f3759df - (i >> 1);
            x = BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
            x = x * (1.5f - xhalf * x * x);
            return x;
        }

        public float VecSqr(Vector3 v)
        {
            return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        }

        public void CalcTangentSpace2(LOD l)
        {
            BitConverter.IsLittleEndian = true;
            int vertexCount = l.Edges.Count();
            Vector3[] vertices = ToVec3(l.Edges);
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] texcoords = new Vector2[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector4[] bitangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                i0 = l.Indexes[i * 3];
                i1 = l.Indexes[i * 3 + 1];
                i2 = l.Indexes[i * 3 + 2];
                Vector3 v1 = l.Edges[i0].Position;
                Vector3 v2 = l.Edges[i1].Position;
                Vector3 v3 = l.Edges[i2].Position;
                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                Vector3 normal = Vector3.Cross(edge1, edge2);
                normal *= InvSqrt(VecSqr(normal));
                normals[i0] += normal;
                normals[i1] += normal;
                normals[i2] += normal;
                texcoords[i0] = l.Edges[i0].UV;
                texcoords[i1] = l.Edges[i1].UV;
                texcoords[i2] = l.Edges[i2].UV;
                Vector2 w1 = texcoords[i0];
                Vector2 w2 = texcoords[i1];
                Vector2 w3 = texcoords[i2];

                float x1 = v2.X - v1.X;
                float x2 = v3.X - v1.X;
                float y1 = v2.Y - v1.Y;
                float y2 = v3.Y - v1.Y;
                float z1 = v2.Z - v1.Z;
                float z2 = v3.Z - v1.Z;

                float s1 = w2.X - w1.X;
                float s2 = w3.X - w1.X;
                float t1 = w2.Y - w1.Y;
                float t2 = w3.Y - w1.Y;

                float r = (s1 * t2 - s2 * t1);
                if (r != 0)
                {
                    r = 1 / r;
                    Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                    Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                    tan1[i0] += sdir;
                    tan1[i1] += sdir;
                    tan1[i2] += sdir;

                    tan2[i0] += tdir;
                    tan2[i1] += tdir;
                    tan2[i2] += tdir;
                }
            }
            for (int i = 0; i < vertexCount; i++)
            {
                normals[i] *= InvSqrt(VecSqr(normals[i]));
                Vector3 n = normals[i];
                Vector3 t = tan1[i];
                Vector3 t2 = tan2[i];
                t -= n * Vector3.Dot(n, t);
                t *= InvSqrt(VecSqr(t));
                Vector3 nxt = Vector3.Cross(n, t);
                float sign =(Vector3.Dot(t2, nxt)  < 0.0f) ? -1.0f : 1.0f;
                t2 = nxt * sign;
                bitangents[i] = new Vector4(t.X, t.Y, t.Z, sign);
                tangents[i] = new Vector4(t2.X, t2.Y, t2.Z, -0.004f);
            }
            for (int i = 0; i < l.Indexes.Count() / 3; i++)
            {
                int i0, i1, i2;
                i0 = l.Indexes[i * 3];
                i1 = l.Indexes[i * 3 + 1];
                i2 = l.Indexes[i * 3 + 2];
                Vector4 bi0 = bitangents[i0];
                Vector4 bi1 = bitangents[i1];
                Vector4 bi2 = bitangents[i2];
                Vector4 tn0 = tangents[i0];
                Vector4 tn1 = tangents[i1];
                Vector4 tn2 = tangents[i2];
                ApplyTangents(l, i0, tn0, bi0);
                ApplyTangents(l, i1, tn1, bi1);
                ApplyTangents(l, i2, tn2, bi2);
            }
        }

        public Vector3[] ComputeTangentAndBinormal(Vector2[] tv, Vector3[] v)
        {
            Vector3[] bvec = new Vector3[2];
            float uva, uvb, uvc, uvd, uvk;
            Vector3 v1, v2;
            uva = tv[1].X - tv[0].X;
            uvb = tv[2].X - tv[0].X;
            uvc = tv[1].Y - tv[0].Y;
            uvd = tv[2].Y - tv[0].Y;
            uvk = uvb * uvc - uva * uvd;
            v1 = v[1] - v[0];
            v2 = v[2] - v[0];
            if (uvk != 0)
            {
                bvec[0] = Vector3.Normalize((uvc * v2 - uvd * v1) * (1f / uvk));
            }
            else
            {
                if (uva != 0)
                    bvec[0] = Vector3.Normalize(v1 * (1f / uva));
                else if (uvb != 0)
                    bvec[0] = Vector3.Normalize(v2 * (1f / uvb));
                else
                    bvec[0] = new Vector3(0.0f, 0.0f, 0.0f);
            }
            Vector3 normal = Vector3.Normalize(Vector3.Cross((v[1] - v[0]), (v[2] - v[1])));
            bvec[1] = Vector3.Normalize(Vector3.Cross(normal, bvec[0]));
            return bvec;
        } 

        public void ApplyTangents(LOD l, int edge, Vector4 tan, Vector4 bitan)
        {
            byte[] buff = new byte[4];
            Edge e = l.Edges[edge];
            buff[0] = Convert.ToByte((tan.X * 0.5f + 0.5f) * 0xFF);
            buff[1] = Convert.ToByte((tan.Y * 0.5f + 0.5f) * 0xFF);
            buff[2] = Convert.ToByte((tan.Z * 0.5f + 0.5f) * 0xFF);
            buff[3] = Convert.ToByte((tan.W * 0.5f + 0.5f) * 0xFF);
            e.Unk1 = BitConverter.ToInt32(buff, 0);
            buff[0] = Convert.ToByte((bitan.X * 0.5f + 0.5f) * 0xFF);
            buff[1] = Convert.ToByte((bitan.Y * 0.5f + 0.5f) * 0xFF);
            buff[2] = Convert.ToByte((bitan.Z * 0.5f + 0.5f) * 0xFF);
            buff[3] = Convert.ToByte((bitan.W * 0.5f + 0.5f) * 0xFF);
            e.Unk2 = BitConverter.ToInt32(buff, 0);
            l.Edges[edge] = e;            
        }

        private TreeNode SearchNodes(TreeNodeCollection nodes,string name)
        {
            TreeNode result = null;

            foreach (TreeNode node in nodes)
            {
                // Here check the search condition.
                // Sample:
                if (node.Name == name)
                {
                    result = node;
                }
                else
                {
                    result = SearchNodes(node.Nodes, name);
                }

                if (result != null)
                {
                    break;
                }
            }

            return result;
        }

        #endregion

        #region Direct X

        public void GenerateDirectXMesh(int LOD = 0)
        {
            LOD l = Mesh.LODs[LOD];
            DirectXSections = new List<CustomVertex.PositionTextured[]>();
            foreach (LODHeader h in l.Headers)
            {
                CustomVertex.PositionTextured[] res = new CustomVertex.PositionTextured[h.count * 3];
                for (int i = 0; i < h.count * 3; i++)
                {
                    Edge e = l.Edges[l.Indexes[(int)h.offset + i]];
                    res[i] = new CustomVertex.PositionTextured(e.Position, e.UV.X, e.UV.Y);
                }
                DirectXSections.Add(res);
            }
        }

        public void DrawMesh(Device device)
        {
            try
            {                
                device.SetTexture(0, DirectXGlobal.Tex_Default);
                device.VertexFormat = CustomVertex.PositionTextured.Format;
                device.RenderState.Lighting = false;
                device.RenderState.FillMode = FillMode.Solid;
                device.RenderState.CullMode = Cull.None;
                foreach (CustomVertex.PositionTextured[] list in DirectXSections)
                    if (list.Length != 0)
                        device.DrawUserPrimitives(PrimitiveType.TriangleList, list.Length / 3, list);
                device.SetTexture(0, null);
                device.VertexFormat = CustomVertex.PositionTextured.Format;
                device.RenderState.Lighting = true;
                device.RenderState.FillMode = FillMode.WireFrame;
                foreach (CustomVertex.PositionTextured[] list in DirectXSections)
                    if (list.Length != 0)
                        device.DrawUserPrimitives(PrimitiveType.TriangleList, list.Length / 3, list);
                
            }
            catch (Exception e)
            {
            }
        }

        #endregion

        #region Export

        public TreeNode ToTree()
        {
            TreeNode t = new TreeNode("Skeletal Mesh");
            t = ToTreeBoundings(t);
            t = ToTreeMaterials(t);
            t = ToTreeOrgRot(t);
            t = ToTreeBones(t);
            t = ToTreeLODs(t);
            t = ToTreeTail(t);
            return t;
        }

        private TreeNode ToTreeBoundings(TreeNode t)
        {
            TreeNode t1 = new TreeNode("Boundings");
            Bounds b = Mesh.Bounding;
            t1.Nodes.Add("Origin: " + b.org.X + " ; " + b.org.Y + " ; " + b.org.Z);
            t1.Nodes.Add("BBox: " + b.box.X + " ; " + b.box.Y + " ; " + b.box.Z);
            t1.Nodes.Add("Radius: " + b.r);
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeMaterials(TreeNode t)
        {
            TreeNode t1 = new TreeNode("Materials");
            foreach (int n in Mesh.Materials)
                t1.Nodes.Add("#" + n + " : " + pcc.getObjectName(n));
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeTail(TreeNode t)
        {
            TreeNode t1 = new TreeNode("Tail");
            foreach (TailEntry e in Tail)
                t1.Nodes.Add(e.Unk2 + " " + pcc.getNameEntry(e.Unk0) + " " + e.Unk1);
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeOrgRot(TreeNode t)
        {
            TreeNode t1 = new TreeNode("Origin/Rotation");
            t1.Nodes.Add("Origin: " + Mesh.Origin.X + " ; " + Mesh.Origin.Y + " ; " + Mesh.Origin.Z);
            t1.Nodes.Add("Rotation: Yaw " + Mesh.Rotation.yaw + " ; Pitch " + Mesh.Rotation.pitch + " ; Roll " + Mesh.Rotation.roll);
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeBones(TreeNode t)
        {
            TreeNode t1 = new TreeNode("Bones");
            TreeNode t2 = new TreeNode("List");
            for (int i = 0; i < Mesh.Bones.Count(); i++)
            {
                Bone b = Mesh.Bones[i];
                string s = "" + i.ToString("d4") + " : ";
                s += "\"" + pcc.getNameEntry(b.name) + "\" ";
                s += "Parent:" + b.parent + " Childs:" + b.childcount;
                s += "Position(" + b.position.X + "; " + b.position.Y + "; " + b.position.Z + ") ";
                s += "Orientation(" + b.orientation.x + "; " + b.orientation.y + "; " + b.orientation.z + "; " + b.orientation.w + ") ";
                s += "Unkn(" + b.unk1 + "; " + b.unk2 + "; " + b.unk3 + ")";
                TreeNode tt = new TreeNode(s);
                tt.Name = b._offset.ToString();
                t2.Nodes.Add(tt);
            }
            t1.Nodes.Add(t2);
            TreeNode t3 = new TreeNode("Tree");
            for (int i = 0; i < Mesh.Bones.Count(); i++)
            {
                Bone b = Mesh.Bones[i];
                TreeNode f = SearchNodes(t3.Nodes, b.parent.ToString());
                string s = "" + i.ToString("d4") + " : \"" + pcc.getNameEntry(b.name) + "\"";
                if (f != null)
                {                    
                    f.Nodes.Add(i.ToString(), s);
                }
                else
                {
                    t3.Nodes.Add(i.ToString(), s);
                }
            }
            t3.ExpandAll();
            t1.Nodes.Add(t3);
            t2 = new TreeNode("Bonedepth = " + Mesh.BoneTreeDepth);
            t1.Nodes.Add(t2);
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeLODs(TreeNode t)
        {
            TreeNode t1 = new TreeNode("LODs");
            int count = 0;
            foreach(LOD l in Mesh.LODs)
            {
                TreeNode t2 = new TreeNode("LOD " + count);
                t2 = ToTreeSection(t2, l);
                t2.Name = l._offset.ToString();
                t1.Nodes.Add(t2);
                count++;
            }
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeSection(TreeNode t, LOD sec)
        {
            TreeNode t1 = new TreeNode("Headers");
            for (int i = 0; i < sec.Headers.Count(); i++)
            {
                LODHeader h = sec.Headers[i];
                TreeNode t2 = new TreeNode(i.ToString());
                t2.Nodes.Add(new TreeNode("Index: " + h.index));
                t2.Nodes.Add(new TreeNode("MatIndex: " + h.matindex));
                t2.Nodes.Add(new TreeNode("Offset: " + h.offset));
                t2.Nodes.Add(new TreeNode("Count: " + h.count));
                t2.Nodes.Add(new TreeNode("Unknown: " + h.unk1));
                t1.Nodes.Add(t2);
            }
            t.Nodes.Add(t1);
            t1 = new TreeNode("Indexbuffer");
            for(int i=0;i<sec.Indexes.Count;i++)
                t1.Nodes.Add(new TreeNode(i.ToString("d4") + " : " + sec.Indexes[i]));
            t.Nodes.Add(t1);
            t1 = new TreeNode("Unknown Buffer 1");
            for (int i = 0; i < sec.UnkIndexes1.Count; i++)
                t1.Nodes.Add(new TreeNode(i.ToString("d4") + " : " + sec.UnkIndexes1[i]));
            t.Nodes.Add(t1);
            t1 = new TreeNode("Unknown Buffer 2");
            for (int i = 0; i < sec.UnkIndexes2.Count; i++)
                t1.Nodes.Add(new TreeNode(i.ToString("d4") + " : " + sec.UnkIndexes2[i]));
            t.Nodes.Add(t1);
            t1 = new TreeNode("Unknown Buffer 3");
            for (int i = 0; i < sec.UnkSec2.Length / 4; i++)
                t1.Nodes.Add(new TreeNode(i.ToString("d4") + " : " + BitConverter.ToInt32(sec.UnkSec2, i * 4).ToString("X8") + " " + BitConverter.ToSingle(sec.UnkSec2, i * 4)));
            t.Nodes.Add(t1);
            t1 = new TreeNode("Unknown Sections");
            for (int i = 0; i < sec.UnkSec1.Count; i++)
            {
                TreeNode t2 = new TreeNode("Unknown Section " + i);
                t2 = ToTreeUnkSection(t2, sec.UnkSec1[i]);
                t1.Nodes.Add(t2);
            }
            t.Nodes.Add(t1);
            t1 = new TreeNode("Active Bones");
            for (int i = 0; i < sec.ActiveBones.Count; i++)
                t1.Nodes.Add(new TreeNode(i.ToString() + " : " + sec.ActiveBones[i]));
            t.Nodes.Add(t1);
            t1 = new TreeNode("Edges");
            t1 = ToTreeEdges(t1, sec.Edges);
            t.Nodes.Add(t1);
            t1 = new TreeNode("UnknownValue = " + sec.unk2);
            t.Nodes.Add(t1);
            return t;
        }

        private TreeNode ToTreeEdges(TreeNode t, List<Edge> edges)
        {
            for (int i = 0; i < edges.Count(); i++)
            {
                Edge e = edges[i];
                string s = i.ToString("d4") + " : Tangents(" + TanToStr(e.Unk1) + "; " + TanToStr(e.Unk2) + " ) ";
                s += "Influences(";
                for (int j = 0; j < 4; j++)
                    s += "{" + e.Influences[j].bone + " ; " + (float)(e.Influences[j].weight / 255f) + "}";
                s += " Position(" + e.Position.X + "; " + e.Position.Y + "; " + e.Position.Z + " )";
                s += " UV(" + e.UV.X + "; " + e.UV.Y + " )";
                TreeNode t2 = new TreeNode(s);
                t2.Name = e._offset.ToString();
                t.Nodes.Add(t2);
            }
            return t;
        }

        private string TanToStr(int t)
        {
            string s = "[";
            byte[] buff = BitConverter.GetBytes(t);
            for (int i = 0; i < 3; i++)
                s += String.Format("{0:0.000}", ((buff[i] - 128) / 256f) * 2f) + " ; ";
            s += String.Format("{0:0.000}", ((buff[3] - 128) / 256f) * 2f) + "]";
            return s;
        }

        private TreeNode ToTreeUnkSection(TreeNode t, UnknownSection sec)
        {
            TreeNode t1 = new TreeNode("Indexbuffer");
            for (int i = 0; i < sec.Indexes.Count; i++)
                t1.Nodes.Add(new TreeNode(i.ToString("d4") + " : " + sec.Indexes[i]));
            t.Nodes.Add(t1);            
            string s = "";
            for (int i = 0; i < 6; i++)
                s += BitConverter.ToInt32(sec.Unkn,i*4).ToString("d4") + " ";
            t.Nodes.Add(new TreeNode("Unknown : " + s));
            return t;
        }

        public byte[] Dump()
        {
            int startbinary = props[props.Count() - 1].offend;
            int lenofbinary = memsize - startbinary;
            byte[] buffer = new byte[lenofbinary];
            for (int i = 0; i < lenofbinary; i++)
                buffer[i] = memory[i + startbinary];
            return buffer;
        }

        public void ExportToPsk(string path,int LOD)
        {
            PSKFile.PSKContainer pskc = new PSKFile.PSKContainer();
            LOD l = Mesh.LODs[LOD];
            pskc = WriteBones(pskc);
            pskc = WriteWeights(pskc,LOD);
            pskc.points = new List<PSKFile.PSKPoint>();
            pskc.edges = new List<PSKFile.PSKEdge>();            
            for (int i = 0; i < l.Edges.Count; i++)
            {
                Edge e =l.Edges[i];
                Vector3 p = e.Position;
                p.Y *= -1;
                pskc.points.Add(new PSKFile.PSKPoint(p));
                pskc.edges.Add(new PSKFile.PSKEdge((short)i,e.UV,0));
            }
            pskc.faces = new List<PSKFile.PSKFace>();
            foreach (LODHeader h in l.Headers)
            {
                for (int i = 0; i < h.count; i++)
                {
                    PSKFile.PSKFace f = new PSKFile.PSKFace(l.Indexes[(int)h.offset + i * 3],
                                                       l.Indexes[(int)h.offset + i * 3 + 2],
                                                       l.Indexes[(int)h.offset + i * 3 + 1],  
                                                       (byte)h.matindex);
                    pskc.faces.Add(f);
                    PSKFile.PSKEdge e = pskc.edges[f.v0];
                    e.material = (byte)h.matindex;
                    pskc.edges[f.v0] = e;
                    e = pskc.edges[f.v1];
                    e.material = (byte)h.matindex;
                    pskc.edges[f.v1] = e;
                    e = pskc.edges[f.v2];
                    e.material = (byte)h.matindex;
                    pskc.edges[f.v2] = e;
                }
            }
            
            pskc.materials = new List<PSKFile.PSKMaterial>();
            foreach (int i in Mesh.Materials)
                pskc.materials.Add(new PSKFile.PSKMaterial(pcc.getObjectName(i), 0));
            psk = new PSKFile();
            psk.psk = pskc;
            psk.Export(path);
        }

        public TreeNode GetChild(TreeNode tn)
        {
            TreeNode ret = tn;
            int n = Convert.ToInt32(tn.Text);
            for (int i = 0; i < Mesh.Bones.Count; i++)
                if (i != n && Mesh.Bones[i].parent == n)
                {
                    TreeNode t = new TreeNode(i.ToString());
                    t = GetChild(t);
                    ret.Nodes.Add(t);
                }
            return ret;
        }

        public int bonecount;

        public PSKFile.PSKContainer WriteBones(PSKFile.PSKContainer PSK)
        {
            bonecount = 0;
            TreeNode rootbone = new TreeNode("0");
            rootbone = GetChild(rootbone);
            PSK.bones = new List<PSKFile.PSKBone>(); 
            WriteBone(rootbone, PSK);
            return PSK;
        }

        public PSKFile.PSKContainer WriteWeights(PSKFile.PSKContainer PSK, int lod)
        {
            PSK.weights = new List<PSKFile.PSKWeight>();
            for (int i = 0; i < Mesh.LODs[lod].Edges.Count; i++)
            {
                Edge e = Mesh.LODs[lod].Edges[i];
                e._imported = false;
                Mesh.LODs[lod].Edges[i] = e;
            }
            for (int i = 0; i < Mesh.LODs[lod].Headers.Count; i++)
            {
                LOD l = Mesh.LODs[lod];
                LODHeader h = l.Headers[i];                
                int start = (int)h.offset;
                int count = (int)h.count * 3;
                for (int j = start; j < start + count; j++)
                {
                    int index = l.Indexes[j];
                    Edge e = l.Edges[index];
                    if (!e._imported)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            float w = e.Influences[k].weight / 255f;
                            if (w != 0)
                            {
                                int bone = e.Influences[k].bone;                 //THIS IS
                                bone = l.UnkSec1[i].Indexes[bone];              //BEYOND
                                bone = Mesh.Bones[bone].index;                      //Ridiculose
                                PSK.weights.Add(new PSKFile.PSKWeight(w, index, bone));
                            }
                        }
                        e._imported = true;
                        Mesh.LODs[lod].Edges[index] = e;
                    }
                }
            }
            for (int i = 0; i < Mesh.LODs[lod].Edges.Count; i++)
            {
                bool found = false;
                Edge e = Mesh.LODs[lod].Edges[i];
                for (int j = 0; j < PSK.weights.Count; j++)
                    if (PSK.weights[j].point == i)
                        found = true;
                if(!found)
                    PSK.weights.Add(new PSKFile.PSKWeight(1.0f, i, 0));
            }
            return PSK;
        }

        private void WriteBone(TreeNode t,PSKFile.PSKContainer PSK)
        {
            int bn = Convert.ToInt32(t.Text);
            WriteChild(t, bonecount,PSK);
            for (int i = 0; i < t.Nodes.Count; i++)
                WriteBone(t.Nodes[i],PSK);
        }

        private void WriteChild(TreeNode t, int index,PSKFile.PSKContainer PSK)
        {
            int bn = Convert.ToInt32(t.Text);
            PSKFile.PSKBone b = new PSKFile.PSKBone();
            Bone mb = Mesh.Bones[bn];
            b.name = pcc.getNameEntry(mb.name);
            b.childs = mb.childcount;
            int p = 0;
            if (t.Parent != null)
                p = Convert.ToInt32(t.Parent.Text);
            if (bn != 0)
                b.parent = Mesh.Bones[p].index;
            else
                b.parent = 0;
            mb.index = bonecount;
            Mesh.Bones[bn] = mb;
            b.location.x = mb.position.X;
            b.location.y = mb.position.Y *-1;
            b.location.z = mb.position.Z;
            b.rotation.x = mb.orientation.x;
            b.rotation.y = mb.orientation.y;
            b.rotation.z = mb.orientation.z;
            b.rotation.w = mb.orientation.w;
            if (bn == 0)
                b.rotation.w *= -1;
            b.rotation.y *= -1;
            PSK.bones.Add(b);
            bonecount++;
        }

        #endregion

        #region Import

        public void ImportFromPsk(string path, int lod)
        {
            DebugOutput.Clear();
            DebugOutput.PrintLn("Loading \"" + Path.GetFileName(path) + "\"...");
            psk = new PSKFile();                                        //source psk object
            psk.ImportPSK(path);                                        //load file
            LOD l = Mesh.LODs[lod];                                     //target LOD object
            l.Indexes = new List<ushort>();                             //Init
            l.Headers = new List<LODHeader>();
            l.UnkSec1 = new List<UnknownSection>();
            DebugOutput.PrintLn("Creating bone conversion table...");
            List<int> pskBone2skm = new List<int>();                    //to convert psk boneindex to skm boneindex
            foreach (PSKFile.PSKBone b in psk.psk.bones)                //loop all psk bones
                for (int i = 0; i < Mesh.Bones.Count; i++)              //for each loop all skm bones
                    if (b.name.TrimEnd(' ') == pcc.getNameEntry(Mesh.Bones[i].name)) //if match, ...
                    {
                        pskBone2skm.Add(i);                             //...then add
                        if (MPOpt.SKM_importbones)
                        {
                            Bone t = Mesh.Bones[i];
                            t.position = b.location.ToVector3();
                            t.orientation = new Quad(b.rotation.ToVector4());
                            t.position.Y *= -1;
                            t.orientation.y *= -1;
                            if (i == 0)
                                t.orientation.w *= -1;
                            Mesh.Bones[i] = t;
                        }
                        break;                                          //break search for match on this bone
                    }
            DebugOutput.PrintLn("Importing Materials...");
            if (psk.psk.materials.Count > Mesh.Materials.Count())       //If more Materials are needed to import
            {
                int count = psk.psk.materials.Count - Mesh.Materials.Count();                
                int mat = Mesh.Materials[0];
                for (int i = 0; i < count; i++)
                    Mesh.Materials.Add(mat);                            //Fill up with first material used
            }
            l.UnkCount1 = psk.psk.materials.Count;
            DebugOutput.PrintLn("Importing Indexes...");
            int facesoffset = 0;                                        //faceoffset for current section
            for (int i = 0; i < psk.psk.materials.Count; i++)           //Section count = Material Count
            {
                int count = 0;                                          //Face count of this section
                LODHeader h = new LODHeader();                          //Header object for Current Section
                h.index = (ushort)i;                                    //yeah same as material
                h.matindex = (ushort)i;                                 //yeah same as material
                h.offset = (ushort)facesoffset;                         //offset in index buffer
                foreach (PSKFile.PSKFace f in psk.psk.faces)            //go through all faces 
                    if (f.material == i)                                //Face has current material?
                    {
                        l.Indexes.Add((ushort)psk.psk.edges[f.v0].index);                    //add corners to index buffer
                        l.Indexes.Add((ushort)psk.psk.edges[f.v2].index);
                        l.Indexes.Add((ushort)psk.psk.edges[f.v1].index);                    //culling swap
                        facesoffset += 3;                               //3 indexes added
                        count++;                                       //1 face added
                    }
                h.count = (uint)count;                                  //sum faces
                h.unk1 = 0;                                             //always zero
                l.Headers.Add(h);                                       //add new header

            }
            l.Edges = new List<Edge>();                                 //reset edge list, first step: import coords and UVS           
            DebugOutput.PrintLn("Importing Vertices and UVs...");
            for (int i = 0; i < psk.psk.points.Count; i++)               //loop all edges/vertices in psk
            {
                Edge ne = new Edge();                                   //the new skm edge
                PSKFile.PSKEdge e = new PSKFile.PSKEdge();
                PSKFile.PSKPoint p = psk.psk.points[i];                 //actual point from psk
                foreach(PSKFile.PSKEdge t in psk.psk.edges)
                    if (t.index == i)
                    {
                        e = t;
                        break;
                    }
                ne.Position = p.ToVector3();                            //set coords&UVs
                ne.Position.Y *= -1;                                    //flip Y axis                  
                ne.UV = new Vector2(e.U, e.V); //offsets for light fix
                ne.Influences = new List<Influence>();                  //prepare
                foreach (PSKFile.PSKWeight w in psk.psk.weights)
                    if (w.point == i)
                        ne.Influences.Add(new Influence(w.weight, pskBone2skm[w.bone]));
                ne.Unk1 = 0;                                            //tangent space quad1
                ne.Unk2 = 0;                                            //tangent space quad2
                l.Edges.Add(ne);
            }
            DebugOutput.PrintLn("Adding Dup Influences...");
            int ccnt = 0;
            int ccnt2 = 0;
            foreach (Edge e in l.Edges)
            {
                bool done = false;
                if (e.Influences.Count() == 0)
                {
                    foreach (Edge e2 in l.Edges)
                        if (e2.Position == e.Position && e2.Influences.Count() != 0 && !done)
                        {
                            foreach (Influence i in e2.Influences)
                            {
                                Influence ni;
                                ni.bone = i.bone;
                                ni.weight = i.weight;
                                e.Influences.Add(ni);
                            }
                            done = true;
                            ccnt2++;
                        }
                        else if (done)
                            break;
                    ccnt++;
                }
            }
            for (int i = 0; i < l.Edges.Count; i++)
            {
                List<Influence> tinf = new List<Influence>();
                for (int j = 0; j < l.Edges[i].Influences.Count; j++)
                    if (l.Edges[i].Influences[j].weight > 0)
                        tinf.Add(l.Edges[i].Influences[j]);
                Edge e = l.Edges[i];
                e.Influences = tinf;
                l.Edges[i] = e;
            }
            List<int> tempidx = new List<int>();
            for (int i = 0; i < l.Edges.Count; i++)
                if (l.Edges[i].Influences.Count == 1)
                    tempidx.Add(i);
            for (int i = 0; i < l.Edges.Count; i++)
                if (l.Edges[i].Influences.Count > 1)
                    tempidx.Add(i);
            List<Edge> tmpedges = new List<Edge>();
            foreach (int n in tempidx)
                tmpedges.Add(l.Edges[n]);
            l.Edges = tmpedges;
            for(int i=0;i<l.Indexes.Count;i++)
                for(int j=0;j<tmpedges.Count;j++)
                    if (tempidx[j] == l.Indexes[i])
                    {
                        l.Indexes[i] = (ushort)j;
                        break;                        
                    }
            DebugOutput.PrintLn(ccnt + " " + ccnt2 + " Filling up Influences...");
            foreach (Edge e in l.Edges)                                 //loop all edges and fill up influences to 4
            {
                int count = e.Influences.Count();                       //current influence count
                for (int i = count; i < 4; i++)                         //add until 4
                    e.Influences.Add(new Influence(0, 0));              //add placeholder
            }
            DebugOutput.PrintLn("Creating active bone list...");
            l.ActiveBones = new List<byte>();                           //Init List, Get Active Bone List
            l.ActiveBones.Add(0);                                       //add god node
            foreach (Edge e in l.Edges)                                 //loop all edges
                for (int i = 0; i < 4; i++)                                    //and each influence
                    if (e.Influences[i].weight != 0)                    //if has influence
                    {
                        byte bone = e.Influences[i].bone;               //get bone
                        bool found = false;                             //search if already in list
                        for (int j = 0; j < l.ActiveBones.Count; j++)
                            if (l.ActiveBones[j] == bone)
                                found = true;
                        if (!found)                                     //if not in list, add
                            l.ActiveBones.Add(bone);
                    }
            l.ActiveBones.Sort();                                       //Sort list
            int lastbone = l.ActiveBones[l.ActiveBones.Count - 1];
            l.ActiveBones = new List<byte>();
            for (byte i = 0; i <= lastbone; i++)
                l.ActiveBones.Add(i);
            DebugOutput.PrintLn("Creating sub bone lists...");
            foreach (LODHeader h in l.Headers)                          //Get sub bonelists, loop each section
            {
                UnknownSection s = new UnknownSection();                //init
                s.Unkn = new byte[24];                                  //zero for now, !!!TODO!!!
                s.Indexes = new List<ushort>();
                foreach (byte b in l.ActiveBones)
                    s.Indexes.Add(b);                                   //sort list
                l.UnkSec1.Add(s);                                       //add this section
            }
            DebugOutput.PrintLn("Creating unknown bone list...");
            l.UnkIndexes1 = new List<ushort>();
            foreach (byte b in l.ActiveBones)
                l.UnkIndexes1.Add(b);
            int edgeoffset = 0;
            DebugOutput.PrintLn("Creating unknown sections...");
            for (int i = 0; i < l.Headers.Count; i++)                   //update values in unknown section
            {
                LODHeader h = l.Headers[i];
                int pos = (int)h.offset;
                int maxsimple = -1;
                int maxmulti = -1;
                int minsimple = l.Edges.Count;
                int minmulti = l.Edges.Count;
                int lensimple = 0, lenmulti = 0;
                for (int j = 0; j < h.count * 3; j++)                   //determine min,max edge indexes
                {
                    Edge e = l.Edges[l.Indexes[pos]];
                    int index = l.Indexes[pos];
                    int count = 0;
                    foreach (Influence inf in e.Influences)
                        if (inf.weight != 0)
                            count++;
                    if (count == 0)
                    {
                        MessageBox.Show("Error: Found vertex without influences!");
                        return;
                    }
                    if (count == 1)
                    {
                        if (index < minsimple)
                            minsimple = index;
                        if (index > maxsimple)
                            maxsimple = index;
                    }
                    if (count > 1)
                    {
                        if (index < minmulti)
                            minmulti = index;
                        if (index > maxmulti)
                            maxmulti = index;
                    }
                    pos++;
                }
                lensimple = (maxsimple - minsimple) + 1;
                lenmulti = (maxmulti - minmulti) + 1;
                lensimple = Math.Max(lensimple, 0);
                lenmulti = Math.Max(lenmulti, 0);
                byte[] buff = BitConverter.GetBytes(lensimple);
                for (int j = 0; j < 4; j++)
                    l.UnkSec1[i].Unkn[j] = buff[j];
                buff = BitConverter.GetBytes(lenmulti);
                for (int j = 0; j < 4; j++)
                    l.UnkSec1[i].Unkn[j + 4] = buff[j];
                l.UnkSec1[i].Unkn[8] = 4;
                int sum = lensimple + lenmulti;
                edgeoffset += sum;
                if (i == l.Headers.Count - 1)
                {
                    buff = BitConverter.GetBytes(edgeoffset);
                    for (int j = 0; j < 4; j++)
                        l.UnkSec1[i].Unkn[j + 16] = buff[j];
                }
                else
                {
                    buff = BitConverter.GetBytes(edgeoffset);
                    for (int j = 0; j < 4; j++)
                        l.UnkSec1[i].Unkn[j + 12] = buff[j];
                }
            }
            DebugOutput.PrintLn("Calculating 4D tangent space quaternions...");
            CalcTangentSpace2(l);                                      //Calc 4D Tangents
            DebugOutput.PrintLn("Done.");
            DebugOutput.PrintLn("Saving pcc...");
            Mesh.LODs[lod] = l;                                         //assign lod
        }
        #endregion


    }


}
