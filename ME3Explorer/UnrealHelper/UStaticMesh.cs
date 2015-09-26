using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class UStaticMesh
    {
#region Declarations
        public byte[] memory;
        public int memsize;
        public string[] names;
        public int currpos;
        public int index;
        public int index2;
        public int index3;
        public List<Property> OProps;
        public List<RawFace> RawFaces;
        public List<Material> Materials;
        public List<Vector> Vertices;
        public List<UVSet> UVSets;
        public Bounding bound;
        public byte[] Unknown1;
        public byte[] Unknown2;
        public byte[] Unknown3;
        public byte[] Unknown4;
        public byte[] Unknown5;
        public byte[] GUID;
        public UInt16[] IndexBuffer;
        public int BodySetup = -1;
        
        public ULevel.LevelProperties LP;
        public ULevel.DirectXObject DirectX;

        public int offVert = -1;
        public int offFace = -1;
        public int offIndex = -1;
        public int offUV = -1;
        public int offUnk = -1;        

        public struct Property
        {
            public string name;
            public string value;
            public byte[] raw;
        }

        public struct Material
        {
            public byte[] raw;
        }

        public struct UVSet
        {
            public float[] U;
            public float[] V;
            public byte[] raw;
        }

        public struct InjectOffset
        {
            public int type;
            public int off;
        }

        public string[] Props = new string[]
        {"BodySetup\0",//0
         "UseSimpleBoxCollision\0", 
         "UseSimpleLineCollision\0",
         "UseSimpleKarmaCollision\0",
         "UseVertexColor\0",
         "UseVertexColor\0",//5
         "ForceDoubleSidedShadowVolumes\0",
         "LightMapCoordinateIndex\0",                                 
         "LightMapResolution\0",
         "None\0",
         "UseSimpleRigidBodyCollision\0",//10
         "bUsedForInstancing\0"
        };

        public struct Vector
        {
            public float x;
            public float y;
            public float z;
        }

        public struct Bounding
        {
            public Vector origin;
            public Vector bbox;
            public Vector unk1;
            public Vector unk2;
            public float radius;            
        }

        public struct RawFace
        {
            public UInt16 e0;
            public UInt16 e1;
            public UInt16 e2;
        }
#endregion

        public UStaticMesh()
        {
        }

        public UStaticMesh(byte[] mem,string[] Names)
        {
            memory = mem;
            memsize = memory.Length;
            names = Names;
            OProps = new List<Property>();
            RawFaces = new List<RawFace>();
            Materials = new List<Material>();
            Vertices = new List<Vector>();
            UVSets = new List<UVSet>();
            bound = new Bounding();
            Deserialize();
        }

#region Serialize

        public byte[] Serialize()
        {
            MemoryStream buff = new MemoryStream();
            StreamAppend(buff, BitConverter.GetBytes(index));
            StreamAppend(buff, SerializeProps());
            StreamAppend(buff, SerializeBoundings());
            StreamAppend(buff, Unknown1);
            StreamAppend(buff, SerializeRawFaces());
            StreamAppend(buff, GUID);
            StreamAppend(buff, SerializeMaterials());
            StreamAppend(buff, SerializeVertices());
            StreamAppend(buff, Unknown2);
            StreamAppend(buff, SerializeUVs());
            StreamAppend(buff, Unknown3);
            StreamAppend(buff, SerializeIndexBuffers());
            StreamAppend(buff, Unknown4);
            return buff.ToArray();
        }
               
        public byte[] SerializeProps()
        {
            MemoryStream rbuff = new MemoryStream();
            for (int i = 0; i < OProps.Count; i++)
                for (int j = 0; j < OProps[i].raw.Length; j++)
                    rbuff.WriteByte((byte)OProps[i].raw[j]);
            return rbuff.ToArray();
        }

        public byte[] SerializeBoundings()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(bound.origin.x);
            rbuff.Write(buff,0,4);
            buff = BitConverter.GetBytes(bound.origin.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.origin.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.bbox.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.bbox.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.bbox.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.radius);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk1.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk1.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk1.z);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk2.x);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk2.y);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(bound.unk2.z);
            rbuff.Write(buff, 0, 4);
            return rbuff.ToArray();
        }

        public byte[] SerializeRawFaces()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes((Int32)8);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(RawFaces.Count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < RawFaces.Count; i++)
            {
                buff = BitConverter.GetBytes(RawFaces[i].e0);
                rbuff.Write(buff, 0, 2);
                buff = BitConverter.GetBytes(RawFaces[i].e1);
                rbuff.Write(buff, 0, 2);
                buff = BitConverter.GetBytes(RawFaces[i].e2);
                rbuff.Write(buff, 0, 2);
                buff = BitConverter.GetBytes((Int16)0);
                rbuff.Write(buff, 0, 2);
            }
            return rbuff.ToArray();
        }

        public byte[] SerializeMaterials()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes((Int32)Materials.Count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < Materials.Count; i++)
                StreamAppend(rbuff, Materials[i].raw);
            return rbuff.ToArray();
        }

        public byte[] SerializeVertices()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes((Int32)12);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Vertices.Count);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes((Int32)1);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes((Int32)12);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(Vertices.Count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < Vertices.Count; i++)
            {
                StreamAppend(rbuff, BitConverter.GetBytes((Single)Vertices[i].x));
                StreamAppend(rbuff, BitConverter.GetBytes((Single)Vertices[i].y));
                StreamAppend(rbuff, BitConverter.GetBytes((Single)Vertices[i].z));
            }
            return rbuff.ToArray();
        }

        public byte[] SerializeUVs()
        {
            MemoryStream rbuff = new MemoryStream();
            int count = UVSets.Count;
            if (count <= 0)
                return null;
            int subc = UVSets[0].U.Length;
            byte[] buff = BitConverter.GetBytes((Int32)subc*4);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(count);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < count; i++)
            {
                UVSet uv = UVSets[i];
                //for (int j = 0; j < subc; j++)
                //{

                //    StreamAppend(rbuff, BitConverter.GetBytes(FloatToHalf(uv.U[j])));
                //    StreamAppend(rbuff, BitConverter.GetBytes(FloatToHalf(uv.V[j])));
                //}
                StreamAppend(rbuff, uv.raw);
            }
            return rbuff.ToArray();
        }

        public byte[] SerializeIndexBuffers()
        {
            MemoryStream rbuff = new MemoryStream();
            byte[] buff = BitConverter.GetBytes((Int32)2);
            rbuff.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(IndexBuffer.Length);
            rbuff.Write(buff, 0, 4);
            for (int i = 0; i < IndexBuffer.Length; i++)
                StreamAppend(rbuff, BitConverter.GetBytes(IndexBuffer[i]));
            return rbuff.ToArray();
        }
#endregion

#region Deserialize
        public void Deserialize()
        {
            currpos = 0;
            offFace = -1;
            offIndex = -1;
            offVert = -1;
            offUV = -1;
            offUnk = -1;
            ReadIndex(currpos);
            ReadProperties(currpos);
            ReadBoundings(currpos);
            ReadUnknown1(currpos);
            ReadRawFaces(currpos);
            if (RawFaces.Count == 0)
            {
                IndexBufferToFaces(IndexBuffer);
                return;
            }
            ReadGUID(currpos);
            ReadMaterials(currpos);
            ReadVertices(currpos);
            ReadUnknown2(currpos);
            ReadUVSets(currpos);
            ReadUnknown3(currpos);
            ReadIndexBuffers(currpos);
            if (IndexBuffer == null)
                return;
            ReadUnknown4(currpos);
        }

        public void ReadUnknown4(int off)
        {
            int count = memsize - off;
            Unknown4 = new byte[count];
            for (int i = 0; i < count; i++)
                Unknown4[i] = memory[off + i];
            currpos = memsize;
        }
        public void ReadIndexBuffers(int off)
        {
            int size = BitConverter.ToInt32(memory, off);
            if (size != 2)
            {
                IndexBuffer = new UInt16[0];
                return;
            }
            offIndex = off;
            int count = BitConverter.ToInt32(memory, off + 4);
            IndexBuffer = new UInt16[count];
            for (int i = 0; i < count; i++)
                IndexBuffer[i] = BitConverter.ToUInt16(memory, off + i * 2 + 8);
            currpos = off + count * 2 + 8;
        }
        public void ReadUnknown3(int off)
        {            
            int test = BitConverter.ToInt32(memory, off);
            if (test == 0)
            {
                Unknown3 = new byte[28];
                for (int i = 0; i < 28; i++)
                    Unknown3[i] = memory[off + i];
                currpos = off + 28;
            }
            else
            {
                int size = BitConverter.ToInt32(memory, off + 8);
                int count = BitConverter.ToInt32(memory, off + 12);
                Unknown3 = new byte[size*count + 36];
                for (int i = 0; i < count * size + 36; i++)
                    Unknown3[i] = memory[off + i];
                currpos = off + count * size + 36;
            }
        }
        public void ReadUVSets(int off)
        {
            offUV = off;
            int size = BitConverter.ToInt32(memory, off);
            int count = BitConverter.ToInt32(memory, off + 4);
            int subc = size / 4;
            int pos = off + 8;
            if (count > Vertices.Count)
            {
                currpos = pos;
                return;
            }
            for (int i = 0; i < count; i++)
            {
                UVSet uv = new UVSet();
                uv.U = new float[subc];
                uv.V = new float[subc];
                uv.raw = new byte[subc * 4];
                for (int j = 0; j < subc; j++)
                {
                    uv.U[j] = getFloat16(pos);
                    uv.V[j] = getFloat16(pos + 2);
                    for (int k = 0; k < 4; k++)
                        uv.raw[j * 4 + k] = memory[pos + k];
                    pos += 4;
                }
                UVSets.Add(uv);
            }
            currpos = pos;
        }
        public void ReadUnknown2(int off)
        {
            Unknown2 = new byte[20];
            for (int i = 0; i < 20; i++)
                Unknown2[i] = memory[off + i];
            currpos = off + 20;
        }
        public void ReadVertices(int off)
        {
            offVert = off;
            int size = BitConverter.ToInt32(memory, off);
            int count = BitConverter.ToInt32(memory, off + 4);
            int pos = off + 8;
            for (int i = 0; i < count; i++)
            {
                Vertices.Add(ReadVector(pos));
                pos += 12;
            }
            currpos = pos;
        }
        public void ReadMaterials(int off)
        {
            int count = BitConverter.ToInt32(memory, off);

            int pos = off + 4;
            for (int i = 0; i < count; i++)
            {
                Material m = new Material();
                m.raw = new byte[49];
                for (int j = 0; j < 49; j++)
                    m.raw[j] = memory[pos + j];
                pos += 49;
                Materials.Add(m);
            }
            int test = BitConverter.ToInt32(memory, pos);
            if (test == 0)
                currpos = pos + 4;
            else
                currpos = pos + test;
        }
        public void ReadGUID(int off)
        {
            GUID = new byte[24];
            for (int i = 0; i < 24; i++)
                GUID[i] = memory[off + i];
            currpos = off + 24;
        }
        public void ReadUnknown1(int off)
        {
            int size = BitConverter.ToInt32(memory, off);
            int count = BitConverter.ToInt32(memory, off + 4);
            int pos = off;
            offUnk = pos;
            int start = 0;
            if (count == 0)
            {
                int test = BitConverter.ToInt32(memory, pos + 12);                
                pos += 12;
                if (test == 0)
                {
                    pos += 4;
                    int test2 = BitConverter.ToInt32(memory, pos);
                    if (test2 == 0x12)
                    {
                        ReadGUID(pos);
                        pos = currpos;
                        ReadMaterials(pos);
                        pos = currpos;
                        offUnk = pos;
                        size = BitConverter.ToInt32(memory, pos);
                        count = BitConverter.ToInt32(memory, pos + 4);
                    }
                }
            }
            else
            {
                pos += 8;
            }
            start = pos - off;
            if (size == 12)
            {
                Unknown1 = new byte[start];
                for (int i = 0; i < start; i++)
                    Unknown1[i] = memory[off + i];
                currpos = off + start;
                ReadVertices(pos);
            }
            else
            {
                Unknown1 = new byte[count * size + start];
                for (int i = 0; i < count * size + start; i++)
                    Unknown1[i] = memory[off + i];
                currpos = off + count * size + start;
            }
        }
        public void ReadUnknown5(int off)
        {
            int count = BitConverter.ToInt32(memory, off);
            int pos = off + count * 8 + 4;
            ReadUVSets(pos);
            int test = BitConverter.ToInt32(memory, currpos);
            if (test == 0)
            {
                Unknown5 = new byte[28];
                for (int i = 0; i < 28; i++)
                    Unknown5[i] = memory[currpos + i];
                currpos += 28;
            }
            ReadIndexBuffers(currpos);
            ReadUnknown4(currpos);
        }
        public void ReadRawFaces(int off)
        {
            RawFaces = new List<RawFace>();
            int size = BitConverter.ToInt32(memory, off);
            if (size != 8)
            {
                ReadUnknown5(off);
                return;
            }
            offFace = off;
            int count = BitConverter.ToInt32(memory, off + 4);
            int pos = off + 8;
            RawFace f = new RawFace();
            for (int i = 0; i < count; i++)
            {
                f.e0 = BitConverter.ToUInt16(memory, pos);
                f.e1 = BitConverter.ToUInt16(memory, pos + 2);
                f.e2 = BitConverter.ToUInt16(memory, pos + 4);
                RawFaces.Add(f);
                pos += size;
            }
            currpos = pos;
        }
        public void ReadBoundings(int off)
        {
            bound.origin = ReadVector(off);
            bound.bbox = ReadVector(off + 12);
            bound.radius = BitConverter.ToSingle(memory, off + 24);
            bound.unk1 = ReadVector(off + 28);
            bound.unk2 = ReadVector(off + 40);
            currpos += 52;
        }
        public void ReadIndex(int off)
        {
            index = BitConverter.ToInt32(memory, off);
            currpos += 4;
        }

        
#endregion

#region Export

        public Vector3 getNormal(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 firstvec = p0 - p1;
            Vector3 secondvec = p0 - p2;
            Vector3 normal = Vector3.Cross(firstvec, secondvec);
            normal.Normalize();
            return normal;
        }

        public CustomVertex.PositionNormalTextured[] ExportDirectX(int Col)
        {            
            int count = RawFaces.Count() * 3;
            CustomVertex.PositionNormalTextured[] t = new CustomVertex.PositionNormalTextured[count];
            int sel = 0;
            bool hasUVs = true;
            if (UVSets == null || UVSets.Count == 0)
                hasUVs = false;
            if(hasUVs)
                sel = UVSets[0].U.Length - 1;
            for (int i = 0; i < RawFaces.Count(); i++)
            {
                CustomVertex.PositionNormalTextured t2 = new CustomVertex.PositionNormalTextured();
                t2.Normal = getNormal(new Vector3(Vertices[RawFaces[i].e0].x, Vertices[RawFaces[i].e0].y, Vertices[RawFaces[i].e0].z),
                                      new Vector3(Vertices[RawFaces[i].e1].x, Vertices[RawFaces[i].e1].y, Vertices[RawFaces[i].e1].z),
                                      new Vector3(Vertices[RawFaces[i].e2].x, Vertices[RawFaces[i].e2].y, Vertices[RawFaces[i].e2].z));
                t2.X = Vertices[RawFaces[i].e0].x;
                t2.Y = Vertices[RawFaces[i].e0].y;
                t2.Z = Vertices[RawFaces[i].e0].z;
                if (hasUVs)
                {
                    t2.Tu = UVSets[RawFaces[i].e0].U[sel];
                    t2.Tv = UVSets[RawFaces[i].e0].V[sel];
                }
                t[i * 3] = t2;
                t2.X = Vertices[RawFaces[i].e1].x;
                t2.Y = Vertices[RawFaces[i].e1].y;
                t2.Z = Vertices[RawFaces[i].e1].z;
                if (hasUVs)
                {
                    t2.Tu = UVSets[RawFaces[i].e1].U[sel];
                    t2.Tv = UVSets[RawFaces[i].e1].V[sel];
                }
                t[i * 3 + 1] = t2;
                t2.X = Vertices[RawFaces[i].e2].x;
                t2.Y = Vertices[RawFaces[i].e2].y;
                t2.Z = Vertices[RawFaces[i].e2].z;
                if (hasUVs)
                {
                    t2.Tu = UVSets[RawFaces[i].e2].U[sel];
                    t2.Tv = UVSets[RawFaces[i].e2].V[sel];
                }
                t[i * 3 + 2] = t2;
            }
            return t;
        }

        public PSKFile ExportToPsk()
        {
            PSKFile PSKf = new PSKFile();
            PSKFile.PSKObject PSK = PSKf.PSK;
            PSK.Points = new PSKFile.Vector[Vertices.Count];
            for (int i = 0; i < Vertices.Count; i++)
            {
                PSK.Points[i].x = Vertices[i].x;
                PSK.Points[i].y = Vertices[i].y;
                PSK.Points[i].z = Vertices[i].z;
            }
            PSK.Edges = new PSKFile.Edge[UVSets.Count];
            int subc = UVSets[0].U.Length - 1;
            for (int i = 0; i < UVSets.Count; i++)
            {
                PSK.Edges[i].index = i;
                PSK.Edges[i].mat = 0;
                PSK.Edges[i].U = UVSets[i].U[subc];
                PSK.Edges[i].V = UVSets[i].U[subc];
            }
            PSK.Faces = new PSKFile.Face[RawFaces.Count];
            for (int i = 0; i < RawFaces.Count; i++)
            {
                PSK.Faces[i].e0 = RawFaces[i].e0;
                PSK.Faces[i].e1 = RawFaces[i].e1;
                PSK.Faces[i].e2 = RawFaces[i].e2;
            }
            PSK.Mats = new PSKFile.Material[1];
            PSK.Mats[0].name = "";
            PSK.Bones = new PSKFile.PSKBone[0];
            PSK.Weights = new PSKFile.Weight[0];
            PSKf.PSK = PSK;
            return PSKf;
        }

        public TreeNode ExportToTree()
        {
            TreeNode ret = new TreeNode("Static Mesh");
            ret.Nodes.Add(PropsToTree());
            ret.Nodes.Add(BoundingToTree());
            ret.Nodes.Add(RawFacesToTree());
            ret.Nodes.Add(GUIDToTree());
            ret.Nodes.Add(VerticesToTree());
            ret.Nodes.Add(UVsToTree());
            ret.Nodes.Add(IndexBufferToTree());
            return ret;
        }

        public TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            for (int i = 0; i < OProps.Count; i++)
            {
                TreeNode t = new TreeNode(OProps[i].name);
                TreeNode t2 = new TreeNode(OProps[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public TreeNode BoundingToTree()
        {
            TreeNode ret = new TreeNode("Bounding");
            TreeNode t = new TreeNode("Origin");
            TreeNode t2 = new TreeNode("(" + bound.origin.x.ToString() + "; " + bound.origin.y.ToString() + "; " + bound.origin.z.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            t = new TreeNode("Bounding Box");
            t2 = new TreeNode("(" + bound.bbox.x.ToString() + "; " + bound.bbox.y.ToString() + "; " + bound.bbox.z.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            t = new TreeNode("Radius");
            t2 = new TreeNode("(" + bound.radius.ToString() + ")");
            t.Nodes.Add(t2);
            ret.Nodes.Add(t);
            return ret;
        }

        public TreeNode RawFacesToTree()
        {
            TreeNode ret = new TreeNode("Faces");
            for (int i = 0; i < RawFaces.Count; i++)
            {
                TreeNode t = new TreeNode("Face " + i.ToString() + " : (" + RawFaces[i].e0.ToString() + "; " + RawFaces[i].e1.ToString() + "; " + RawFaces[i].e2.ToString() + ")");
                ret.Nodes.Add(t);
            }            
            return ret;
        }

        public TreeNode GUIDToTree()
        {
            TreeNode ret = new TreeNode("GUID");
            string s = "";
            int k;
            if (GUID == null)
                return ret;
            for (int i = 8; i < GUID.Length; i++)
            {
                k = GUID[i];
                if (k > 15)
                    s += k.ToString("X");
                else
                    s += "0" + k.ToString("X");
            }
            TreeNode t = new TreeNode(s);
            ret.Nodes.Add(t);
            return ret;
        }

        public TreeNode VerticesToTree()
        {
            TreeNode ret = new TreeNode("Vertices");
            for (int i = 0; i < Vertices.Count; i++)
            {
                TreeNode t = new TreeNode("Point " + i.ToString() + " : (" + Vertices[i].x.ToString() + "; " + Vertices[i].y.ToString() + "; " + Vertices[i].z.ToString() + ")");
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public TreeNode UVsToTree()
        {
            TreeNode ret = new TreeNode("UVSets");
            string s = "";
            for (int i = 0; i < UVSets.Count; i++)
            {
                s = "Point " + i.ToString() + " UVSets: ";
                for (int j = 0; j < UVSets[i].U.Length; j++)
                    s += "(" + UVSets[i].U[j].ToString() + "; " + UVSets[i].V[j].ToString() + ") ";
                TreeNode t = new TreeNode(s);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public TreeNode IndexBufferToTree()
        {
            TreeNode ret = new TreeNode("Index Buffer");
            if (IndexBuffer == null)
                return ret;
            for (int i = 0; i < IndexBuffer.Length; i++)
                ret.Nodes.Add(new TreeNode("Index " + i.ToString() + " = " + IndexBuffer[i].ToString()));
            return ret;
        }

        #endregion

#region Import
        public void ImportFromPSK(PSKFile.PSKObject PSK)
        {
            Vertices.Clear();
            //UVSets.Clear();
            RawFaces.Clear();
            int count = PSK.Points.Length;
            for (int i = 0; i < count; i++)
            {
                Vector v;
                v.x = PSK.Points[i].x;
                v.y = PSK.Points[i].y;
                v.z = PSK.Points[i].z;
                Vertices.Add(v);
            }
            //count = PSK.Edges.Length;
            //for (int i = 0; i < count; i++)
            //{
            //    int index = PSK.Edges[i].index;
            //    UVSet uv = new UVSet();
            //    uv.U = new float[1];
            //    uv.V = new float[1];
            //    uv.U[0] = PSK.Edges[i].U;
            //    uv.V[0] = PSK.Edges[i].V;
            //    UVSets.Add(uv);
            //}
            count = PSK.Faces.Length;
            for (int i = 0; i < count; i++)
            {
                RawFace f;
                f.e0 = PSK.Faces[i].e0;
                f.e1 = PSK.Faces[i].e1;
                f.e2 = PSK.Faces[i].e2;
                RawFaces.Add(f);
            }
            //Unknown1 = new byte[count / 4 + 8];
            //Unknown1[0] = 0x6;
            //byte[] buff = BitConverter.GetBytes((Int32)(count / 4));
            //for (int i = 0; i < 4; i++)
            //    Unknown1[4 + i] = buff[i];
        }

        public byte[] InjectFromPSK(PSKFile.PSKObject PSK)
        {
            byte[] buff = new byte[0];
            if (offFace == -1 || offIndex == -1 || offUV == -1 || offVert == -1 || offUnk == -1)
                return buff;
            List<InjectOffset> l = new List<InjectOffset>();
            MemoryStream m = new MemoryStream();
            l.Add(newInject(offFace ,0));
            l.Add(newInject(offIndex, 1));
            l.Add(newInject(offUV, 2));
            l.Add(newInject(offVert, 3));
            l.Add(newInject(offUnk, 4));
            bool run = true;
            while (run)
            {
                run = false;
                for (int i = 0; i < 4; i++)
                    if (l[i].off > l[i + 1].off)
                    {
                        InjectOffset t = l[i];
                        l[i] = l[i + 1];
                        l[i + 1] = t;
                        run = true;
                    }
            }
            int nextoff =l[0].off;
            int pos = 0;
            int size;
            int count;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < nextoff - pos; j++)
                    m.WriteByte(memory[pos + j]);
                switch (l[i].type)
                {
#region Faces
                    case 0:
                        count = PSK.Faces.Length;
                        buff = BitConverter.GetBytes((Int32)8);
                        m.Write(buff, 0, 4);
                        buff = BitConverter.GetBytes((Int32)count);
                        m.Write(buff, 0, 4);
                        for (int j = 0; j < count; j++)
                        {
                            buff = BitConverter.GetBytes((UInt16)PSK.Faces[j].e0);
                            m.Write(buff, 0, 2);
                            buff = BitConverter.GetBytes((UInt16)PSK.Faces[j].e1);
                            m.Write(buff, 0, 2);
                            buff = BitConverter.GetBytes((UInt16)PSK.Faces[j].e2);
                            m.Write(buff, 0, 2);
                            buff = BitConverter.GetBytes((UInt16)0);
                            m.Write(buff, 0, 2);
                        }
                        break;
#endregion

#region IndexBuffer
                    case 1:
                        ushort[] Indexes = new ushort[PSK.Faces.Length*3];
                        int n = 0;
                        for (int j = 0; j < PSK.Faces.Length; j++)
                        {
                            Indexes[n] = PSK.Faces[j].e0;
                            n++;
                            Indexes[n] = PSK.Faces[j].e1;
                            n++;
                            Indexes[n] = PSK.Faces[j].e2;
                            n++;
                        }
                        count = Indexes.Length;
                        buff = BitConverter.GetBytes((Int32)2);
                        m.Write(buff, 0, 4);
                        buff = BitConverter.GetBytes((Int32)count);
                        m.Write(buff, 0, 4);
                        for (int j = 0; j < count; j++)
                        {
                            buff = BitConverter.GetBytes((UInt16)Indexes[j]);
                            m.Write(buff, 0, 2);
                        }
                        break;
#endregion

#region UV
                    case 2:
                        size = BitConverter.ToInt32(memory, l[i].off);
                        count = BitConverter.ToInt32(memory, l[i].off);
                        int subc = size / 4;
                        count = PSK.Edges.Length;
                        buff = BitConverter.GetBytes((Int32)size);
                        m.Write(buff, 0, 4);
                        buff = BitConverter.GetBytes((Int32)count);
                        m.Write(buff, 0, 4);
                        for (int j = 0; j < count; j++)
                            for (int k = 0; k < subc; k++)
                            {
                                buff = BitConverter.GetBytes(FloatToHalf(PSK.Edges[j].U));
                                m.Write(buff, 0, 2);
                                buff = BitConverter.GetBytes(FloatToHalf(PSK.Edges[j].V));
                                m.Write(buff, 0, 2);
                            }
                        break;
#endregion

#region Verts
                    case 3:
                        count = PSK.Edges.Length;
                        buff = BitConverter.GetBytes((Int32)12);
                        m.Write(buff, 0, 4);
                        buff = BitConverter.GetBytes((Int32)count);
                        m.Write(buff, 0, 4);
                        for (int j = 0; j < count; j++)
                        {
                            buff = BitConverter.GetBytes(PSK.Points[PSK.Edges[j].index].x);
                            m.Write(buff, 0, 4);
                            buff = BitConverter.GetBytes(PSK.Points[PSK.Edges[j].index].y);
                            m.Write(buff, 0, 4);
                            buff = BitConverter.GetBytes(PSK.Points[PSK.Edges[j].index].z);
                            m.Write(buff, 0, 4);
                        }
                        break;
#endregion

#region Unkown
                    case 4:
                        size = BitConverter.ToInt32(memory, nextoff);
                        count = BitConverter.ToInt32(memory, nextoff + 4);
                        if (size == 6)
                        {
                            buff = BitConverter.GetBytes((Int32)size);
                            m.Write(buff, 0, 4);
                            count = 1;
                            while (count * 8 <= PSK.Faces.Length)
                                count *= 2;
                            buff = BitConverter.GetBytes((Int32)count);
                            m.Write(buff, 0, 4);
                            for (int j = 0; j < size * count; j++)
                                m.WriteByte(0);
                        }
                        else
                            m.Write(Unknown1, 0, Unknown1.Length);
                        break;
#endregion
                }
                if (i < 4)
                {
                    pos = nextoff;
                    nextoff = l[i + 1].off;
                    //if (l[i + 1].type == 3)
                    //{
                    //    size = BitConverter.ToInt32(memory, nextoff - 12);
                    //    if (size == 12)
                    //    {
                    //        buff = BitConverter.GetBytes((Int32)PSK.Edges.Length);
                    //        for (int j = 0; j < 4; j++)
                    //            memory[nextoff - 8 + j] = buff[j];
                    //    }
                    //}
                    //if (l[i + 1].type == 2)
                    //{
                    //    size = BitConverter.ToInt32(memory, nextoff - 16);
                    //    if (size == 12)
                    //    {
                    //        buff = BitConverter.GetBytes((Int32)PSK.Edges.Length);
                    //        for (int j = 0; j < 4; j++)
                    //            memory[nextoff - 12 + j] = buff[j];
                    //    }
                    //}
                    //if (l[i + 1].type == 1)
                    //{
                    //    buff = BitConverter.GetBytes((Int32)PSK.Faces.Length * 3);
                    //    for (int j = 0; j < 4; j++)
                    //        memory[nextoff - 4 + j] = buff[j];
                    //}
                    size = BitConverter.ToInt32(memory, pos);
                    count = BitConverter.ToInt32(memory, pos + 4);
                    pos += size * count + 8;

                }
                else
                {
                    pos = l[i].off;
                    size = BitConverter.ToInt32(memory, pos);
                    count = BitConverter.ToInt32(memory, pos + 4);
                    pos += size * count + 8;
                    nextoff = memsize;
                    for (int j = 0; j < nextoff - pos; j++)
                        m.WriteByte(memory[pos + j]);
                }
            }
            //SaveFileDialog d = new SaveFileDialog();
            //d.Filter = "*.bin|*.bin";
            //buff = m.ToArray();
            //if (d.ShowDialog() == DialogResult.OK)
            //{
            //    FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
            //    fs.Write(buff,0,buff.Length);
            //    fs.Close();
            //    MessageBox.Show("Done.");
            //}
            return m.ToArray();
        }


#endregion

#region Helpers

        public InjectOffset newInject(int off, int type)
        {
            InjectOffset t = new InjectOffset();
            t.type = type;
            t.off = off;
            return t;
        }

        public MemoryStream StreamAppend(MemoryStream s, byte[] buff)
        {
            MemoryStream m = s;
            for (int i = 0; i < buff.Length; i++)
                m.WriteByte((byte)buff[i]);
            return m;
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
                return FloatToHalf(-1.0f);
            UInt16 exponentBits = (UInt16)((15 + placement) << 10);
            UInt16 mantissaBits = (UInt16)(mantissa >> 42);
            UInt16 signBits = (UInt16)(sign >> 48);
            return (UInt16)(exponentBits | mantissaBits | signBits);
        }

        public float getFloat16(int pos)
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

        public Vector ReadVector(int pos)
        {
            Vector v = new Vector();
            if (memsize - pos > 3)
            {            
                v.x = BitConverter.ToSingle(memory, pos);
                v.y = BitConverter.ToSingle(memory, pos + 4);
                v.z = BitConverter.ToSingle(memory, pos + 8);
            }
            return v;
        }

        public void ReadProperties(int off)
        {
            int pos = off;
            int n = BitConverter.ToInt32(memory, pos);
            if (n >= names.Length || n < 0)
                return;
            n = getName(names[n]);
            switch (n)
            {
                case 0:
                    pos += 28;
                    int m = BitConverter.ToInt32(memory, pos - 4);
                    OProps.Add(makeProp(Props[n], m.ToString(), ReadRaw(pos - 28, 28)));
                    BodySetup = m;
                    break;
                case 7:
                case 8:
                    pos += 28;
                    int m2 = BitConverter.ToInt32(memory, pos - 4);
                    OProps.Add(makeProp(Props[n], m2.ToString(), ReadRaw(pos - 28, 28)));
                    break;
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 10:
                    pos += 25;
                    OProps.Add(makeProp(Props[n], memory[pos - 1].ToString(), ReadRaw(pos - 25, 25)));
                    break;
                case 9:
                    OProps.Add(makeProp(Props[n], "", ReadRaw(pos, 12)));
                    pos += 12;
                    break;
                case 11:
                    OProps.Add(makeProp(Props[n], "", ReadRaw(pos, 25)));
                    pos += 25;
                    break;
                default:
                    return;
            }
            currpos = pos;
            ReadProperties(currpos);
        }

        public byte[] ReadRaw(int off, int len)
        {
            byte[] buff = new byte[len];
            for (int i = 0; i < len; i++)
                buff[i] = memory[off + i];
            return buff;
        }

        public Property makeProp(string n, string v, byte[] raw)
        {
            Property p = new Property();
            p.name = n;
            p.value = v;
            p.raw = raw;
            return p;
        }

        public int getName(string s)
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

        public void IndexBufferToFaces(UInt16[] IBuff)
        {
            if (IBuff.Length % 3 != 0)
                return;
            int count = IBuff.Length / 3;
            RawFaces = new List<RawFace>();
            RawFace f;
            for (int i = 0; i < count; i++)
            {
                f.e0 = (UInt16)IBuff[i * 3];
                f.e1 = (UInt16)IBuff[i * 3 + 1];
                f.e2 = (UInt16)IBuff[i * 3 + 2];
                RawFaces.Add(f);
            }
        }
#endregion
    }
}
