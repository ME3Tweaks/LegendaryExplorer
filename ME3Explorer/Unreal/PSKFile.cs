using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.Unreal
{
    public class PSKFile
    {

        #region declarations
        public struct PSKPoint
        {
            public float x;
            public float y;
            public float z;

            public static bool operator == (PSKPoint p1, PSKPoint p2)
            {
                if (p1.x == p2.x &&
                    p1.y == p2.y &&
                    p1.z == p2.z) return true;
                return false;
            }

            public static bool operator != (PSKPoint p1, PSKPoint p2)
            {
                if (p1.x == p2.x &&
                    p1.y == p2.y &&
                    p1.z == p2.z) return false;
                return true;
            }


            public PSKPoint(float _x, float _y, float _z)
            {
                x = _x;
                y = _y;
                z = _z;
            }

            public PSKPoint(Vector3 v)
            {
                x = v.X;
                y = v.Y;
                z = v.Z;
            }

            public Vector3  ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }

        public struct PSKQuad
        {
            public float w;
            public float x;
            public float y;
            public float z;

            public PSKQuad(float _w, float _x, float _y, float _z)
            {
                w = _w;
                x = _x;
                y = _y;
                z = _z;
            }

            public PSKQuad(Vector4 v)
            {
                w = v.W;
                x = v.X;
                y = v.Y;
                z = v.Z;
            }

            public Vector4 ToVector4()
            {
                return new Vector4(x, y, z, w);
            }
        }

        public struct PSKEdge
        {
            public Int16 index;
            public Int16 padding1;
            public float U;
            public float V;
            public byte material;
            public byte reserved;
            public Int16 padding2;

            public PSKEdge(Int16 _index, float _U, float _V, byte _material)
            {
                index = _index;
                padding1 = 0;
                U = _U;
                V = _V;
                material = _material;
                reserved = 0;
                padding2 = 0;
            }

            public PSKEdge(Int16 _index, Vector2 _UV, byte _material)
            {
                index = _index;
                padding1 = 0;
                U = _UV.X;
                V = _UV.Y;
                material = _material;
                reserved = 0;
                padding2 = 0;
            }
        }

        public struct PSKFace
        {
            public int v0;
            public int v1;
            public int v2;
            public byte material;
            public byte auxmaterial;
            public int smoothgroup;

            public PSKFace(int _v0, int _v1, int _v2, byte _material)
            {
                v0 = _v0;
                v1 = _v1;
                v2 = _v2;
                material = _material;
                auxmaterial = 0;
                smoothgroup = 0;
            }
        }

        public struct PSKMaterial
        {
            public string name;
            public int texture;
            public int polyflags;
            public int auxmaterial;
            public int auxflags;
            public int LODbias;
            public int LODstyle;

            public PSKMaterial(string _name, int _texture)
            {
                name = _name;
                texture = _texture;
                polyflags = 0;
                auxmaterial = 0;
                auxflags = 0;
                LODbias = 0;
                LODstyle = 0;
            }
        }

        public struct PSKBone
        {
            public string name;
            public int flags;
            public int childs;
            public int parent;
            public PSKQuad rotation;
            public PSKPoint location;
            public float length;
            public PSKPoint size;
            public int index; //for bone tree
        }

        public struct PSKWeight
        {
            public float weight;
            public int point;
            public int bone;
            public PSKWeight(float _weight, int _point, int _bone)
            {
                weight = _weight;
                point = _point;
                bone = _bone;
            }
        }

        public struct PSKExtraUV
        {
            public float U;
            public float V;
            public PSKExtraUV(float _U, float _V)
            {
                U = _U;
                V = _V;
            }
        }

        public struct PSKContainer
        {
            public List<PSKPoint> points;
            public List<PSKEdge> edges;
            public List<PSKFace> faces;
            public List<PSKMaterial> materials;
            public List<PSKBone> bones;
            public List<PSKWeight> weights;
            public List<PSKExtraUV> extrauv1;
            public List<PSKExtraUV> extrauv2;
            public List<PSKExtraUV> extrauv3;
        }

        public struct PSKHeader
        {
            public string name;
            public int flags;
            public int size;
            public int count;
        }

        #endregion

        public PSKContainer psk;

        #region Export functions

        public PSKFile()
        {
        }

        public void Export(string path)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            byte[] buff = ChunkHeader("ACTRHEAD", 0x1E83B9, 0, 0);
            fs.Write(buff, 0, 32);
            WritePoints(fs);
            WriteEdges(fs);
            WriteFaces(fs);
            WriteMaterials(fs);
            WriteBones(fs);
            WriteWeights(fs);
            //WriteExtraUV(fs);
            fs.Close();
        }

        public void WritePoints(FileStream fs)
        {
            byte[] buff = ChunkHeader("PNTS0000", 0x1E83B9, 0xC, psk.points.Count());
            fs.Write(buff, 0, 32);
            foreach (PSKPoint p in psk.points)
            {
                fs.Write(BitConverter.GetBytes(p.x), 0, 4);
                fs.Write(BitConverter.GetBytes(p.y), 0, 4);
                fs.Write(BitConverter.GetBytes(p.z), 0, 4);
            }
        }

        public void WriteEdges(FileStream fs)
        {
            byte[] buff = ChunkHeader("VTXW0000", 0x1E83B9, 0x10, psk.edges.Count());
            fs.Write(buff, 0, 32);
            foreach (PSKEdge e in psk.edges)
            {
                fs.Write(BitConverter.GetBytes((Int16)e.index), 0, 2);
                fs.Write(BitConverter.GetBytes((Int16)0), 0, 2);
                fs.Write(BitConverter.GetBytes(e.U), 0, 4);
                fs.Write(BitConverter.GetBytes(e.V), 0, 4);
                fs.WriteByte(e.material);
                fs.Write(BitConverter.GetBytes((Int32)0), 0, 3);
            }
        }

        public void WriteFaces(FileStream fs)
        {
            byte[] buff = ChunkHeader("FACE0000", 0x1E83B9, 0xC, psk.faces.Count());
            fs.Write(buff, 0, 32);
            foreach (PSKFace f in psk.faces)
            {
                fs.Write(BitConverter.GetBytes((Int16)f.v0), 0, 2);
                fs.Write(BitConverter.GetBytes((Int16)f.v1), 0, 2);
                fs.Write(BitConverter.GetBytes((Int16)f.v2), 0, 2);
                fs.WriteByte(f.material);
                fs.WriteByte(0);
                fs.Write(BitConverter.GetBytes((Int32)0), 0, 4);
            }
        }

        public void WriteMaterials(FileStream fs)
        {
            byte[] buff = ChunkHeader("MATT0000", 0x1E83B9, 0x58, psk.materials.Count());
            fs.Write(buff, 0, 32);
            foreach (PSKMaterial m in psk.materials)
            {
                for (int i = 0; i < 64; i++)
                    if (i < m.name.Length)
                        fs.WriteByte((byte)m.name[i]);
                    else
                        fs.WriteByte(0);
                fs.Write(BitConverter.GetBytes(m.texture), 0, 4);
                fs.Write(BitConverter.GetBytes(m.polyflags), 0, 4);
                fs.Write(BitConverter.GetBytes(m.auxmaterial), 0, 4);
                fs.Write(BitConverter.GetBytes(m.auxflags), 0, 4);
                fs.Write(BitConverter.GetBytes(m.LODbias), 0, 4);
                fs.Write(BitConverter.GetBytes(m.LODstyle), 0, 4);
            }
        }

        public void WriteBones(FileStream fs)
        {
            byte[] buff = ChunkHeader("REFSKELT", 0x1E83B9, 0x78, psk.bones.Count);
            fs.Write(buff, 0, 32);
            foreach(PSKBone b in psk.bones)
            {
                buff = new byte[64];
                for (int j = 0; j < 64 && j < b.name.Length; j++)
                    buff[j] = (byte)b.name[j];
                fs.Write(buff, 0, 64);
                buff = new byte[4];
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.childs);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.parent);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.rotation.x);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.rotation.y);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.rotation.z);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.rotation.w);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.location.x);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.location.y);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(b.location.z);
                fs.Write(buff, 0, 4);
                buff = new byte[4];
                fs.Write(buff, 0, 4);
                fs.Write(buff, 0, 4);
                fs.Write(buff, 0, 4);
                fs.Write(buff, 0, 4);
            }
        }

        public void WriteWeights(FileStream fs)
        {
            byte[] buff = ChunkHeader("RAWWEIGHTS", 0x1E83B9, 0xC, psk.weights.Count());
            fs.Write(buff, 0, 32);
            foreach (PSKWeight w in psk.weights)
            {
                fs.Write(BitConverter.GetBytes(w.weight), 0, 4);
                fs.Write(BitConverter.GetBytes(w.point), 0, 4);
                fs.Write(BitConverter.GetBytes(w.bone), 0, 4);
            }
        }

        public void WriteExtraUV(FileStream fs)
        {
            byte[] buff = ChunkHeader("EXTRAUV0", 0x1E83B9, 0xC, 0);
            fs.Write(buff, 0, 32);
        }

        public byte[] ChunkHeader(string id, int flags, int size, int count)
        {
            byte[] h = new byte[32];
            for (int i = 0; i < 32 && i < id.Length; i++)
                h[i] = (byte)id[i];
            byte[] buff = BitConverter.GetBytes(flags);
            for (int i = 0; i < 4; i++)
                h[i + 20] = buff[i];
            buff = BitConverter.GetBytes(size);
            for (int i = 0; i < 4; i++)
                h[i + 24] = buff[i];
            buff = BitConverter.GetBytes(count);
            for (int i = 0; i < 4; i++)
                h[i + 28] = buff[i];
            return h;
        }

        #endregion

        public void ImportPSK(string path)
        {
            psk = new PSKContainer();
            BitConverter.IsLittleEndian = true;
            FileStream pskFile = new FileStream(path, FileMode.Open, FileAccess.Read);
             

             do
             {
                 PSKHeader h = ReadHeader(pskFile);
                 byte[] buffer;
                 switch (h.name)
                 {

                     case "ACTRHEAD":
                         break;
                     case "PNTS0000":
                         {
                             #region PNTS0000
                             psk.points = new List<PSKPoint>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKPoint pskPoint = new PSKPoint();                                 
                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskPoint.x = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 pskPoint.y = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 pskPoint.z = BitConverter.ToSingle(buffer, 0);
                                 psk.points.Add(pskPoint);

                             }
                             #endregion
                         }; break;

                     case "VTXW0000":
                         {
                             #region VTXW0000
                             psk.edges = new List<PSKEdge>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKEdge pskEdge = new PSKEdge();

                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskEdge.index = BitConverter.ToInt16(buffer, 0);

                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskEdge.padding1 = BitConverter.ToInt16(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskEdge.U = BitConverter.ToSingle(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskEdge.V = BitConverter.ToSingle(buffer, 0);
                                 
                                 pskEdge.material = (byte)pskFile.ReadByte();

                                 pskEdge.reserved = (byte)pskFile.ReadByte();

                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskEdge.padding2 = BitConverter.ToInt16(buffer, 0);

                                 psk.edges.Add(pskEdge);
                             }
                             #endregion
                         }; break;


                     case "FACE0000":
                         {
                             #region FACE0000
                             psk.faces = new List<PSKFace>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKFace pskFace = new PSKFace();
                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskFace.v0 = BitConverter.ToInt16(buffer, 0);

                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskFace.v1 = BitConverter.ToInt16(buffer, 0);

                                 buffer = new byte[2];
                                 pskFile.Read(buffer, 0, 2);
                                 pskFace.v2 = BitConverter.ToInt16(buffer, 0);

                                 pskFace.material = (byte)pskFile.ReadByte(); 

                                 pskFace.auxmaterial = (byte)pskFile.ReadByte();

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskFace.smoothgroup = BitConverter.ToInt32(buffer, 0);

                                 psk.faces.Add(pskFace);
                             }
                             #endregion
                         }; break;

                     case "MATT0000":
                         {
                            #region MATT0000 
                             psk.materials = new List<PSKMaterial>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKMaterial pskMaterial = new PSKMaterial();
                                 buffer = new byte[64]; 
                                 pskFile.Read(buffer, 0, 64);

                                 pskMaterial.name = "";
                                 for (int j = 0; j < 64; j++)
                                     if (buffer[j] != 0)
                                         pskMaterial.name += (char)buffer[j];

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.texture = BitConverter.ToInt32(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.polyflags = BitConverter.ToInt32(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.auxmaterial = BitConverter.ToInt32(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.auxflags = BitConverter.ToInt32(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.LODbias = BitConverter.ToInt32(buffer, 0);

                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskMaterial.LODstyle = BitConverter.ToInt32(buffer, 0);

                                 psk.materials.Add(pskMaterial);
                             }

                            #endregion
                         }; break;
                     case "REFSKELT":
                         {
                             #region REFSKELT
                             psk.bones = new List<PSKBone>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKBone b = new PSKBone();
                                 buffer = new byte[64];
                                 pskFile.Read(buffer, 0, 64);
                                 b.name = "";
                                 for (int j = 0; j < 64; j++)
                                     if (buffer[j] != 0)
                                         b.name += (char)buffer[j];
                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 pskFile.Read(buffer, 0, 4);
                                 b.childs = BitConverter.ToInt32(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.parent = BitConverter.ToInt32(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.rotation.x = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.rotation.y = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.rotation.z = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.rotation.w = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.location.x = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.location.y = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 b.location.z = BitConverter.ToSingle(buffer, 0);
                                 pskFile.Read(buffer, 0, 4);
                                 pskFile.Read(buffer, 0, 4);
                                 pskFile.Read(buffer, 0, 4);
                                 pskFile.Read(buffer, 0, 4);
                                 psk.bones.Add(b);
                             }

                             #endregion
                         }; break;
                     case "RAWWEIGHTS":
                         {
                             psk.weights = new List<PSKWeight>();
                             for (int i = 0; i < h.count; i++)
                             {
                                 PSKWeight w = new PSKWeight();
                                 buffer = new byte[4];
                                 pskFile.Read(buffer, 0, 4);
                                 w.weight = BitConverter.ToSingle(buffer,0);
                                 pskFile.Read(buffer, 0, 4);
                                 w.point = BitConverter.ToInt32(buffer,0);
                                 pskFile.Read(buffer, 0, 4);
                                 w.bone = BitConverter.ToInt32(buffer, 0);
                                 psk.weights.Add(w);
                             }
                         }; break;

                     #region The rest

                     case "EXTRAUVS0":
                         {
                             psk.extrauv1 = new List<PSKExtraUV>();
                             buffer = new byte[h.size * h.count];
                             pskFile.Read(buffer, 0, h.size * h.count);

                             //buffer = new byte[4];
                             //pskFile.Read(buffer, 0, 4);
                          

                             //float size = BitConverter.ToInt32(buffer, 0);

                             //buffer = new byte[4];
                             //pskFile.Read(buffer, 0, 4);
                             

                             //int count = BitConverter.ToInt32(buffer, 0);

                             //for (int i = 0; i < count; i++)
                             //{
                             //    PSKExtraUV uvSet = new PSKExtraUV();
                             //    buffer = new byte[4];
                             //    pskFile.Read(buffer, 0, 4);
                              

                             //    uvSet.U = BitConverter.ToSingle(buffer, 0);

                             //    buffer = new byte[4];
                             //    pskFile.Read(buffer, 0, 4);
                              

                             //    uvSet.V = BitConverter.ToSingle(buffer, 0);

                             //    psk.extrauv1.Add(uvSet);
                             //}

                         }; break;
                     //yeah, so much about not typing too much :p
                     //Well you wrote everything in the cases, you could have easily used functions :p
                     #endregion
                 }

             } while (pskFile.Position < pskFile.Length);
            
            
        }

        public PSKHeader ReadHeader(FileStream fs)
        {
            PSKHeader res = new PSKHeader();
            res.name = "";
            for (int i = 0; i < 20; i++)
            {
                byte b = (byte)fs.ReadByte();
                if(b!=0)
                    res.name += (char)b;
            }
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.flags = BitConverter.ToInt32(buff, 0);
            buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.size = BitConverter.ToInt32(buff, 0);
            buff = new byte[4];
            fs.Read(buff, 0, 4);
            res.count = BitConverter.ToInt32(buff, 0);
            return res;
        }


    }
}

