using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class USkelMesh
    {

#region Declaration
        public byte[] memory;
        public int memsize;
        public string[] names;
        public int currpos;
        public int bonecount;
        public int multi;

        public SkeletalMesh Mesh;

        public struct Property
        {
            public string name;
            public string value;
            public byte[] raw;
        }

        public struct SkeletalMesh
        {
            public int index;
            public List<Property> OProps;
            public Bounding bound;
            public Vector origin;
            public Vector rotate;
            public List<int> materials;
            public List<Bone> Bones;
            public int treedepth;
            public List<LOD> LODs;
            public byte[] unk1;
            public byte[] unk2;
        }

        public struct Bounding
        {
            public Vector origin;
            public Vector bbox;
            public float radius;
        }

        public struct Vector
        {
            public float x;
            public float y;
            public float z;
        }

        public struct Quad
        {
            public float w;
            public float x;
            public float y;
            public float z;
        }

        public struct Bone
        {
            public int name;
            public Quad orientation;
            public Vector position;
            public int childcount;
            public int parent;
            public int index;
            public int unk1;
            public int unk2;
        }

        public class LOD
        {
            public List<LODSection> sections;
            public int offVert = -1;
            public int offFace = -1;
            public int offIndex = -1;
            public int offUV = -1;
            public int offUnk = -1;
            public List<Point> points;
            public int allFaces;
            public int unk0;
            public byte[] unk1;
            public int unk2;
            public byte[] unk3;
            public List<byte[]> unk4;
            public byte[] unk5;
            public byte[] unk6;
            public int unk7;

            public LOD()
            {
                sections = new List<LODSection>();
                points = new List<Point>();
            }
        }

        public class LODSection
        {
            public UInt16 index;
            public UInt16 matindex;
            public Int32 offset;
            public UInt32 count;
            public Face[] Faces;
            public Int16 unk1;
        }

        public struct Face
        {
            public UInt16 e0;
            public UInt16 e1;
            public UInt16 e2;
            public byte mat;
            public int smooth;
            public uint off;
        }

        public class Influence
        {
            public byte[] bone;
            public byte[] weight;
        }

        public class Point
        {
            public uint off;
            public Influence infl;
            public int unknown3;
            public int unknown4;
            public float X;
            public float Y;
            public float Z;
            public float U;
            public float V;
            public UInt16 Uraw;
            public UInt16 Vraw;

            public override string ToString()
            {
                string s1 = "", s2 = "";
                if (infl != null)
                    for (int i = 0; i < 4; i++)
                    {
                        s1 += " Bone" + i.ToString() + ": " + infl.bone[i].ToString();
                        s2 += " Weight" + i.ToString() + ": " + ((float)infl.weight[i] / 255f).ToString();
                    }
                return "Point (X=" + X + ", Y=" + Y + ", Z=" + Z + ", U=" + U + ", V=" + V + s1 + s2 + ")";
            }
        }

        public string[] Props = new string[]
        {"Sockets\0",               //0
         "LODInfo\0",
         "DisplayFactor\0",
         "LODHysteresis\0",
         "LODMaterialMap\0",
         "bEnableShadowCasting\0",  //5
         "TriangleSorting\0",
         "None\0",
         "ClothTearFactor\0"
        };
#endregion

        public USkelMesh(byte[] mem, string[] Names)
        {
            memory = mem;
            memsize = memory.Length;
            names = Names;
            Mesh = new SkeletalMesh();            
            Mesh.OProps = new List<Property>();
            Mesh.bound = new Bounding();
            Mesh.materials = new List<int>();
            Mesh.Bones = new List<Bone>();
            Deserialize();
        }

#region Deserialize
        public void Deserialize()
        {
            currpos = 0;
            ReadIndex(currpos);
            ReadProps(currpos);
            ReadBoundSphere(currpos);
            ReadMaterials(currpos);
            ReadOrgRot(currpos);
            ReadBones(currpos);
            ReadLODs(currpos);
            ReadUnkown(currpos);
        }

        private void ReadIndex(int off)
        {
            Mesh.index = BitConverter.ToInt32(memory, off);
            currpos += 4;
        }

        private void ReadProps(int off)
        {
            int pos = off;
            int n = BitConverter.ToInt32(memory, pos);
            if (n >= names.Length || n < 0)
                return;
            n = getName(names[n]);
            int len;
            string v = "";
            switch (n)
            {
                case 0:
                    pos += 24;
                    int c = getInt(pos);
                    pos += 4;
                    for(int i = 0; i < c - 1; i++)
                    {
                        v +=getInt(pos).ToString() + ",";
                        pos += 4;
                    }
                    v += getInt(pos).ToString();
                    pos += 4;
                    break;
                case 1:
                case 2:
                case 3:
                case 8:
                    pos += 28;
                    v = getInt(pos - 4).ToString();
                    break;
                case 5:
                    pos += 24;
                    len = getInt(pos);
                    pos += 4 + len;
                    break;
                case 6:
                case 4:
                    pos += 16;
                    len = getInt(pos);
                    pos += 8 + len;
                    break;
                case 7:
                    pos += 8;
                    break;
                default:
                    return;
            }
            Mesh.OProps.Add(makeProp(Props[n], v, ReadRaw(currpos, pos - currpos)));
            currpos = pos;
            ReadProps(currpos);
        }

        private void ReadBoundSphere(int off)
        {
            int pos = off;
            Mesh.bound.origin  = ReadVector(pos);
            pos += 12;
            Mesh.bound.bbox = ReadVector(pos);
            pos += 12;
            Mesh.bound.radius = getFloat(pos);
            currpos = pos + 4;
        }

        private void ReadMaterials(int off)
        {
            int pos = off;
            int count = getInt(pos);
            pos += 4;
            for (int i = 0; i < count; i++)
            {
                int n = getInt(pos);
                Mesh.materials.Add(n - 1);
                pos += 4;
            }
            currpos = pos;
        }

        private void ReadOrgRot(int off)
        {
            int pos = off;
            Mesh.origin = ReadVector(pos);
            pos += 12;
            Mesh.rotate = ReadVector(pos);
            currpos = pos + 12;
        }

        private void ReadBones(int off)
        {
            int pos = off;
            int count = getInt(pos);
            pos += 4;
            for (int i = 0; i < count; i++)
            {
                Bone t = new Bone();
                t.name = getInt(pos);
                t.unk1 = getInt(pos + 4);
                t.unk2 = getInt(pos + 8);
                pos += 12;
                t.orientation = ReadQuad(pos);
                pos += 16;
                t.position = ReadVector(pos);
                pos += 12;
                t.childcount = getInt(pos);
                pos += 4;
                t.parent = getInt(pos);
                pos += 8;
                Mesh.Bones.Add(t);
            }
            Mesh.treedepth = getInt(pos);
            pos += 4;
            currpos = pos;
        }

        private void ReadLODs(int off)
        {
            int pos = off;
            int lodCount = getInt(pos);
            pos += 4;
            Mesh.LODs = new List<LOD>(lodCount);
            for (int l = 0; l < lodCount; l++)
            {
                LOD curLOD = new LOD();
                #region Section headers
                int sectionCount = getInt(pos);
                pos += 4;

                for (int i = 0; i < sectionCount; i++)
                {
                    LODSection t = new LODSection
                    {
                        index = (ushort)getInt16(pos),
                        matindex = (ushort)getInt16(pos + 2),
                        offset = getInt(pos + 4),
                        count = (uint)getInt16(pos + 8),
                        unk1 = getInt16(pos + 10)
                    };
                    pos += 12;
                    t.Faces = new Face[t.count];
                    curLOD.sections.Add(t);
                }
                #endregion


                #region Faces
                curLOD.offFace = pos;
                multi = getInt(pos);
                if (multi == 0) multi = 1;
                pos += 4;
                int valuesCount = getInt(pos);
                valuesCount = (valuesCount * multi) / 6;
                curLOD.allFaces = valuesCount;
                pos += 4;
                for (int i = 0; i < sectionCount; i++)
                {
                    for (int j = 0; j < curLOD.sections[i].count; j++)
                    {
                        Face t = new Face
                        {
                            off = (uint)pos,
                            e0 = (ushort)getInt16(pos),
                            e1 = (ushort)getInt16(pos + 2),
                            e2 = (ushort)getInt16(pos + 4)
                        };
                        curLOD.sections[i].Faces[j] = t;
                        pos += 6;
                    }
                }
                #endregion
                

                #region Unknown data

                curLOD.unk0 = getInt(pos);
                pos += 4;
                int arraySize = getInt(pos);
                curLOD.unk1 = new byte[arraySize * 2 + 4];
                for (int j = 0; j < arraySize * 2 + 4; j++)
                    curLOD.unk1[j] = memory[pos + j];
                pos += arraySize * 2 + 8;
                curLOD.unk2 = getInt(pos - 4);
                int unkHeadCount = getInt(pos);
                curLOD.unk3 = new byte[12];
                for (int j = 0; j < 12; j++)
                    curLOD.unk3[j] = memory[pos + 4 + j];
                pos += 16;
                curLOD.unk4 = new List<byte[]>();
                for (int j = 0; j < unkHeadCount; j++)
                {
                    arraySize = getInt(pos);
                    byte[] buff = new byte[arraySize * 2 + 28];
                    for (int k = 0; k < arraySize * 2 + 28; k++)
                        buff[k] = memory[pos + k];
                    curLOD.unk4.Add(buff);
                    pos += arraySize * 2 + 28;
                }
                int actBonesCount = getInt(pos);
                curLOD.unk5 = new byte[actBonesCount + 4];
                for (int j = 0; j < actBonesCount + 4; j++)
                    curLOD.unk5[j] = memory[pos + j];
                pos += 4 + actBonesCount;
                curLOD.unk6 = new byte[52];
                for (int j = 0; j < 52; j++)
                    curLOD.unk6[j] = memory[pos + j];
                pos += 52; 
                #endregion


                #region Points
                curLOD.offVert = pos;
                int pointsCount = getInt(pos);
                pos += 4;
                for (int j = 0; j < pointsCount; j++)
                {
                    Influence tmp = new Influence();
                    tmp.bone = new byte[4];
                    tmp.weight = new byte[4];
                    for (int k = 0; k < 4; k++)
                    {
                        tmp.bone[k] = memory[pos + k + 8];
                        tmp.weight[k] = memory[pos + k + 12];
                    }
                    Point p = new Point
                    {
                        off = (uint)pos + 16,
                        infl = tmp,
                        unknown3 = getInt(pos),
                        unknown4 = getInt(pos + 4),
                        X = getFloat(pos + 16),
                        Y = getFloat(pos + 20),
                        Z = getFloat(pos + 24),
                        U = getFloat16(pos + 28),
                        V = getFloat16(pos + 30),
                        Uraw = (UInt16)getInt16(pos + 28),
                        Vraw = (UInt16)getInt16(pos + 30)
                    };                    
                    curLOD.points.Add(p);
                    pos += 32;
                }
                curLOD.unk7 = getInt(pos);
                pos += 4;
                Mesh.LODs.Add(curLOD);
                #endregion
            }
            currpos = pos;
        }

        private void ReadUnkown(int off)
        {
            int pos = off;
            int count = getInt(pos);
            Mesh.unk1 = new byte [count * 12 + 4];
            for (int i = 0; i < count * 12 + 4; i++)
                Mesh.unk1[i] = memory[pos + i];
            pos += count * 12 + 4;
            int len = memsize - pos;
            Mesh.unk2 = new byte[len];
            for (int i = 0; i < len; i++)
                Mesh.unk2[i] = memory[pos + i];
            currpos = memsize;
        }

        private byte[] ReadRaw(int off, int len)
        {
            byte[] buff = new byte[len];
            for (int i = 0; i < len; i++)
                buff[i] = memory[off + i];
            return buff;
        }

        private Property makeProp(string n, string v, byte[] raw)
        {
            Property p = new Property();
            p.name = n;
            p.value = v;
            p.raw = raw;
            return p;
        }

        private int getName(string s)
        {
            int r = -1;
            for (int i = 0; i < Props.Length; i++)
                if (Props[i] == s)
                {
                    r = i;
                    break;
                }
            return r;
        }

        private int getInt(int pos)
        {
            return BitConverter.ToInt32(memory, pos);
        }

        private Int16 getInt16(int pos)
        {
            return BitConverter.ToInt16(memory, pos);
        }

        private float getFloat(int pos)
        {
            return BitConverter.ToSingle(memory, pos);
        }

        private float getFloat16(int pos)
        {
            UInt16 u = BitConverter.ToUInt16(memory, pos);
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }

        private Vector ReadVector(int pos)
        {
            Vector v;
            v.x = BitConverter.ToSingle(memory, pos);
            v.y = BitConverter.ToSingle(memory, pos + 4);
            v.z = BitConverter.ToSingle(memory, pos + 8);
            return v;
        }

        private Quad ReadQuad(int pos)
        {
            Quad v;
            v.x = BitConverter.ToSingle(memory, pos);
            v.y = BitConverter.ToSingle(memory, pos + 4);
            v.z = BitConverter.ToSingle(memory, pos + 8);
            v.w = BitConverter.ToSingle(memory, pos + 12);
            return v;
        }
#endregion

#region Serialize

        public byte[] Serialize()
        {
            MemoryStream rbuff = new MemoryStream();
            StreamAppend(rbuff, SerializeIndex());
            StreamAppend(rbuff, SerializeProps());
            StreamAppend(rbuff, SerializeBoundings());
            StreamAppend(rbuff, SerializeMaterials());
            StreamAppend(rbuff, SerializeRotOrg());
            StreamAppend(rbuff, SerializeBones());
            StreamAppend(rbuff, SerializeLODs());
            StreamAppend(rbuff, SerializeUnknown());
            return rbuff.ToArray();
        }

        public byte[] SerializeIndex()
        {
            return BitConverter.GetBytes(Mesh.index);
        }
        
        private byte[] SerializeProps()
        {
            MemoryStream rbuff = new MemoryStream();
            for (int i = 0; i < Mesh.OProps.Count; i++)
                for (int j = 0; j < Mesh.OProps[i].raw.Length; j++)
                    rbuff.WriteByte((byte)Mesh.OProps[i].raw[j]);
            return rbuff.ToArray();
        }

        private byte[] SerializeBoundings()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(Mesh.bound.origin.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.origin.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.origin.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.bbox.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.bbox.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.bbox.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.bound.radius);
            rbuff.Write(buff, 0, 4);
            return rbuff.ToArray();
        }

        private byte[] SerializeMaterials()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(Mesh.materials.Count);
            rbuff.Write(buff, 0, buff.Length);
            for (int i = 0; i < Mesh.materials.Count; i++)
            {
                buff = BitConverter.GetBytes(Mesh.materials[i] + 1);
                rbuff.Write(buff, 0, buff.Length);
            }
            return rbuff.ToArray();
        }

        private byte[] SerializeRotOrg()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(Mesh.origin.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.origin.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.origin.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.rotate.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.rotate.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Mesh.rotate.z);
            rbuff.Write(buff, 0, 4);
            return rbuff.ToArray();
        }

        private byte[] SerializeBones()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(Mesh.Bones.Count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < Mesh.Bones.Count; i++)
            {
                buff = BitConverter.GetBytes(Mesh.Bones[i].name);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].unk1);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].unk2);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].orientation.x);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].orientation.y);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].orientation.z);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].orientation.w);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].position.x);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].position.y);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].position.z);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].childcount);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(Mesh.Bones[i].parent);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes((UInt32)0xFFFFFFFF);
                rbuff.Write(buff, 0, 4);
            }
            buff = BitConverter.GetBytes(Mesh.treedepth);
            rbuff.Write(buff, 0, 4);
            return rbuff.ToArray();
        }

        private byte[] SerializeLODs()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(Mesh.LODs.Count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < Mesh.LODs.Count; i++)
            {
                LOD curLOD = Mesh.LODs[i];
                buff = BitConverter.GetBytes(curLOD.sections.Count);
                rbuff.Write(buff, 0, 4);
                for (int j = 0; j < curLOD.sections.Count; j++)
                {
                    LODSection curSec = curLOD.sections[j];
                    buff = BitConverter.GetBytes((Int16)curSec.index);
                    rbuff.Write(buff, 0, 2);
                    buff = BitConverter.GetBytes((Int16)curSec.matindex);
                    rbuff.Write(buff, 0, 2);
                    buff = BitConverter.GetBytes((Int32)curSec.offset);
                    rbuff.Write(buff, 0, 4);
                    buff = BitConverter.GetBytes((Int16)curSec.count);
                    rbuff.Write(buff, 0, 2);
                    buff = BitConverter.GetBytes((Int16)curSec.unk1);
                    rbuff.Write(buff, 0, 2);
                }
                buff = BitConverter.GetBytes(multi);
                rbuff.Write(buff, 0, 4);
                buff = BitConverter.GetBytes((curLOD.allFaces * 6) / multi);
                rbuff.Write(buff, 0, 4);
                for (int j = 0; j < curLOD.sections.Count; j++)
                {
                    LODSection curSec = curLOD.sections[j];
                    for (int k = 0; k < curSec.count; k++)
                    {
                        buff = BitConverter.GetBytes(curSec.Faces[k].e0);
                        rbuff.Write(buff, 0, 2);
                        buff = BitConverter.GetBytes(curSec.Faces[k].e1);
                        rbuff.Write(buff, 0, 2);
                        buff = BitConverter.GetBytes(curSec.Faces[k].e2);
                        rbuff.Write(buff, 0, 2);
                    }
                }
                StreamAppend(rbuff, BitConverter.GetBytes(curLOD.unk0));
                StreamAppend(rbuff, curLOD.unk1);
                StreamAppend(rbuff, BitConverter.GetBytes(curLOD.unk2));
                StreamAppend(rbuff, BitConverter.GetBytes(curLOD.unk4.Count));
                StreamAppend(rbuff, curLOD.unk3);
                for (int j = 0; j < curLOD.unk4.Count; j++)
                    StreamAppend(rbuff, curLOD.unk4[j]);
                StreamAppend(rbuff, curLOD.unk5);
                StreamAppend(rbuff, curLOD.unk6);
                StreamAppend(rbuff, BitConverter.GetBytes(curLOD.points.Count));
                for (int j = 0; j < curLOD.points.Count; j++)
                {
                    Point p = curLOD.points[j];
                    StreamAppend(rbuff, BitConverter.GetBytes(p.unknown3));
                    StreamAppend(rbuff, BitConverter.GetBytes(p.unknown4));
                    for(int k=0;k<4;k++)
                         rbuff.WriteByte(p.infl.bone[k]);
                    for (int k = 0; k < 4; k++)
                        rbuff.WriteByte(p.infl.weight[k]);
                    StreamAppend(rbuff, BitConverter.GetBytes(p.X));
                    StreamAppend(rbuff, BitConverter.GetBytes(p.Y));
                    StreamAppend(rbuff, BitConverter.GetBytes(p.Z));
                    StreamAppend(rbuff, BitConverter.GetBytes(p.Uraw));
                    StreamAppend(rbuff, BitConverter.GetBytes(p.Vraw));
                }
                StreamAppend(rbuff, BitConverter.GetBytes(curLOD.unk7));
            }
            return rbuff.ToArray();
        }

        private byte[] SerializeUnknown()
        {
            MemoryStream rbuff = new MemoryStream();
            StreamAppend(rbuff, Mesh.unk1);
            StreamAppend(rbuff, Mesh.unk2);
            return rbuff.ToArray();
        }

        private MemoryStream StreamAppend(MemoryStream s, byte[] buff)
        {
            MemoryStream m = s;
            for (int i = 0; i < buff.Length; i++)
                m.WriteByte((byte)buff[i]);
            return m;
        }

#endregion

#region Export

        public Vector3 getNormal(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 firstvec = p0 - p1;
            Vector3 secondvec = p1 - p2;
            Vector3 normal = Vector3.Cross(firstvec, secondvec);
            normal.Normalize();
            return normal;
        }


        public CustomVertex.PositionNormalTextured[] ExportDirectX(int col,int LOD)
        {
            int count = Mesh.LODs[LOD].allFaces;
            CustomVertex.PositionNormalTextured[] t = new CustomVertex.PositionNormalTextured[count * 3];
            int n = 0;
            int c = 0;
            for (int i = 0; i < count; i++)
            {
                CustomVertex.PositionNormalTextured t2 = new CustomVertex.PositionNormalTextured();
                t2.Normal = getNormal(new Vector3(Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].X, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].Z, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].Y * -1),
                                      new Vector3(Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].X, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].Z, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].Y * -1),
                                      new Vector3(Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].X, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].Z, Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].Y * -1));

                t2.X = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].X;
                t2.Z = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].Y * -1;
                t2.Y = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e0].Z;
                //t2.Color = col;
                t[i * 3] = t2;
                t2.X = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].X;
                t2.Z = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].Y * -1;
                t2.Y = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e1].Z;
                //t2.Color = col;
                t[i * 3 + 1] = t2;
                t2.X = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].X;
                t2.Z = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].Y * -1;
                t2.Y = Mesh.LODs[LOD].points[Mesh.LODs[LOD].sections[n].Faces[c].e2].Z;
                //t2.Color = col;
                t[i * 3 + 2] = t2;
                c++;
                if (c == Mesh.LODs[LOD].sections[n].count)
                {
                    c = 0;
                    n++;
                }
            }
            return t;
        }


        public TreeNode ExportToTree()
        {
            TreeNode ret = new TreeNode("Skeletal Mesh");
            ret.Nodes.Add(PropsToTree());
            ret.Nodes.Add(BoundingToTree());
            ret.Nodes.Add(MaterialsToTree());
            ret.Nodes.Add(OrgRotToTree());
            ret.Nodes.Add(BonesToTree());
            ret.Nodes.Add(LODsToTree());
            return ret;
        }

        private TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            for (int i = 0; i < Mesh.OProps.Count; i++)
            {
                TreeNode t = new TreeNode(Mesh.OProps[i].name);
                TreeNode t2 = new TreeNode(Mesh.OProps[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        private TreeNode BoundingToTree()
        {
            TreeNode ret = new TreeNode("Bounding");
            TreeNode t = new TreeNode("Origin");
            TreeNode t2 = new TreeNode("(" + Mesh.bound.origin.x.ToString() + "; " + Mesh.bound.origin.y.ToString() + "; " + Mesh.bound.origin.z.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            t = new TreeNode("Bounding Box");
            t2 = new TreeNode("(" + Mesh.bound.bbox.x.ToString() + "; " + Mesh.bound.bbox.y.ToString() + "; " + Mesh.bound.bbox.z.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            t = new TreeNode("Radius");
            t2 = new TreeNode("(" + Mesh.bound.radius.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            return ret;
        }

        private TreeNode MaterialsToTree()
        {
            TreeNode ret = new TreeNode("Materials");
            for (int i = 0; i < Mesh.materials.Count; i++)
            {
                TreeNode t = new TreeNode((Mesh.materials[i]-1).ToString());
                ret.Nodes.Add(t);
            }
            return ret;
        }

        private TreeNode OrgRotToTree()
        {
            TreeNode ret = new TreeNode("Origin/Rotate");
            TreeNode t = new TreeNode("Origin: (" + Mesh.origin.x.ToString() + "; " + Mesh.origin.y.ToString() + "; " + Mesh.origin.z.ToString() + ")" );
            TreeNode t2 = new TreeNode("Rotate: (" + Mesh.rotate.x.ToString() + "; " + Mesh.rotate.y.ToString() + "; " + Mesh.rotate.z.ToString() + ")"); 
            ret.Nodes.Add(t);
            ret.Nodes.Add(t2);
            return ret;
        }

        private TreeNode BonesToTree()
        {
            TreeNode ret = new TreeNode("Bones");
            for (int i = 0; i < Mesh.Bones.Count; i++)
            {
                Bone b = Mesh.Bones[i];
                TreeNode t = new TreeNode("Bone " + i.ToString());
                TreeNode t1 = new TreeNode("Position: (" + b.position.x.ToString() + "; " + b.position.y.ToString() + "; " + b.position.z.ToString() + ")");
                t.Nodes.Add(t1);
                t1 = new TreeNode("Orientation: (" + b.orientation.x.ToString() + "; " + b.orientation.y.ToString() + "; " + b.orientation.z.ToString() + "; " + b.orientation.w.ToString() + ")");
                t.Nodes.Add(t1);
                t1 = new TreeNode("Parent = " + b.parent);
                t.Nodes.Add(t1);
                t1 = new TreeNode("Child Count = " + b.childcount);
                t.Nodes.Add(t1);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        private TreeNode LODsToTree()
        {
            TreeNode ret = new TreeNode("LODs");
            for (int i = 0; i < Mesh.LODs.Count; i++)
            {
                LOD currLOD = Mesh.LODs[i];
                TreeNode t = new TreeNode("LOD " + i.ToString());                
                TreeNode t1 = new TreeNode("Points");
                    for (int k = 0; k < currLOD.points.Count; k++)
                    {
                        TreeNode t2 = new TreeNode(currLOD.points[k].ToString());
                        t1.Nodes.Add(t2);
                    }
                    t.Nodes.Add(t1);
                for (int j = 0; j < currLOD.sections.Count; j++)
                {
                    
                    t1 = new TreeNode("Section " + j.ToString());
                    LODSection currSec = currLOD.sections[j];
                    for (int k = 0; k < currSec.count; k++)
                    {
                        TreeNode t2 = new TreeNode("Face " + k.ToString() + " (" + currSec.Faces[k].e0.ToString() + "; " + currSec.Faces[k].e1.ToString() + "; " + currSec.Faces[k].e2.ToString() + ")" );
                        t1.Nodes.Add(t2);
                    }
                    t.Nodes.Add(t1);
                }
                ret.Nodes.Add(t);
            }
            return ret;
        }

        private PSKFile.PSKObject PSK;

        public PSKFile ExportToPSK(int LOD = 0)
        {
            PSKFile PSKf = new PSKFile();
            PSK = PSKf.PSK;
            LOD currLOD = Mesh.LODs[LOD];
            PSK.Points = new PSKFile.Vector[currLOD.points.Count];
            for (int i = 0; i < currLOD.points.Count; i++)
            {
                PSK.Points[i].x = currLOD.points[i].X;
                PSK.Points[i].y = currLOD.points[i].Y;
                PSK.Points[i].z = currLOD.points[i].Z;
            }
            PSK.Edges = new PSKFile.Edge[currLOD.points.Count];
            for (int i = 0; i < currLOD.points.Count; i++)
            {
                PSK.Edges[i].U = currLOD.points[i].U;
                PSK.Edges[i].V = currLOD.points[i].U;
                PSK.Edges[i].index = i;
            }
            PSK.Mats = new PSKFile.Material[1];
            PSK.Mats[0].name = "";
            PSK.Faces = new PSKFile.Face[currLOD.allFaces];
            int n = 0;
            for (int i = 0; i < currLOD.sections.Count; i++)
            {
                LODSection currSel = currLOD.sections[i];
                for (int j = 0; j < currSel.count; j++)
                {
                    PSK.Faces[n].e0 = currSel.Faces[j].e0;
                    PSK.Faces[n].e1 = currSel.Faces[j].e1;
                    PSK.Faces[n].e2 = currSel.Faces[j].e2;
                    n++;
                }
            }
            PSKWriteBones();
            PSKWriteWeights(LOD);
            PSKf.PSK = PSK;
            return PSKf;
        }

        private TreeNode GetChild(TreeNode tn)
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

        private void WriteChild(TreeNode t, int index)
        {
            int bn = Convert.ToInt32(t.Text);
            PSKFile.PSKBone b = PSK.Bones[bn];
            Bone mb = Mesh.Bones[bn];
            b.name = names[mb.name];
            b.childcount = mb.childcount;
            int p = 0;
            if (t.Parent != null)
                p = Convert.ToInt32(t.Parent.Text);
            if (bn != 0)
                b.index = Mesh.Bones[p].index;
            else
                b.index = 0;
            mb.index = index;
            Mesh.Bones[bn] = mb;
            b.position.x = mb.position.x;
            b.position.y = mb.position.y;
            b.position.z = mb.position.z;
            b.orientation.x = mb.orientation.x;
            b.orientation.y = mb.orientation.y;
            b.orientation.z = mb.orientation.z;
            b.orientation.w = mb.orientation.w;
            if (bn == 0)
                b.orientation.w *= -1;
            b.orientation.y *= -1;
            PSK.Bones[bn] = b;
            bonecount++;
        }

        private void WriteBone(TreeNode t)
        {
            int bn = Convert.ToInt32(t.Text);
            WriteChild(t, bonecount);
            for (int i = 0; i < t.Nodes.Count; i++)
                WriteBone(t.Nodes[i]);
        }

        private void PSKWriteBones()
        {
            PSK.Bones = new PSKFile.PSKBone[Mesh.Bones.Count];
            TreeNode skel = new TreeNode("0");
            skel = GetChild(skel);
            bonecount = 0;
            WriteBone(skel);
        }

        private void PSKWriteWeights(int LOD)
        {
            int count = Mesh.LODs[LOD].points.Count;
            int count2 = 0;
            for (int i = 0; i < count; i++)
                for (int j = 0; j < 4; j++)
                    if (Mesh.LODs[LOD].points[i].infl.weight[j] != 0)
                        count2++;
            PSK.Weights = new PSKFile.Weight[count2];
            int n = 0;
            for (int i = 0; i < count; i++)
                for (int j = 0; j < 4; j++)
                    if (Mesh.LODs[LOD].points[i].infl.weight[j] != 0)
                    {
                        float w = Mesh.LODs[LOD].points[i].infl.weight[j];
                        int b = Mesh.LODs[LOD].points[i].infl.bone[j];
                        w /= 255f;
                        PSK.Weights[n].boneid = b;
                        PSK.Weights[n].pointid = i;
                        PSK.Weights[n].w = w;
                    }
        }

#endregion

#region Import
        public void ImportFromPsk(PSKFile.PSKObject PSK)
        {
            for (int i = 0; i < Mesh.LODs.Count; i++)
            {
                if (Mesh.LODs[i].points.Count == PSK.Points.Length)
                {
                    for (int j = 0; j < PSK.Points.Length; j++)
                    {
                        Mesh.LODs[i].points[j].X = PSK.Points[j].x;
                        Mesh.LODs[i].points[j].Y = PSK.Points[j].y;
                        Mesh.LODs[i].points[j].Z = PSK.Points[j].z;
                    }
                    return;
                }
            }
            MessageBox.Show("Found no LOD with same vertice count!");
        }

#endregion
    }
}
