using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Media;
using ME3Explorer.Unreal;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using lib3ds.Net;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Unreal.Classes
{
    public class StaticMesh
    {
        public byte[] memory;
        public int index;
        public int memsize;
        public PCCObject pcc;
        public List<PropertyReader.Property> props;
        public int readerpos;
        public PSKFile psk;

        #region MeshStructs
        public MeshStruct Mesh;

        public struct MeshStruct
        {
            public Bounding Bounds;
            public UnknownList kDOPTree;
            public RawTris RawTris;
            public int InternalVersion;
            public Materials Mat;
            public Verts Vertices;
            public Buffers Buffers;
            public Edges Edges;
            public UnknownP UnknownPart;
            public IndexBuffer IdxBuf;
            public EndOfStruct End;
        }

        public struct EndOfStruct
        {
            public byte[] data;
            public TreeNode t;
        }

        public struct IndexBuffer
        {
            public int size, count;
            public List<UInt16> Indexes;
            public TreeNode t;
        }

        public struct UnknownP
        {
            public TreeNode t;
            public byte[] data;
        }

        public struct Edges
        {
            public int size, count;
            public List<UVSet> UVSet;
            public TreeNode t;
        }

        public struct UVSet
        {
            public List<Vector2> UVs;
            public byte x1, x2, y1, y2, z1, z2, w1, w2;
        }

        public struct Verts
        {
            public List<Vector3> Points;
            public TreeNode t;
        }

        public struct RawTris
        {
            public List<RawTriangle> RawTriangles;
            public TreeNode t;
        }

        public struct Materials
        {
            public int LodCount;
            public List<Lod> Lods;
            public List<MaterialInstanceConstant> MatInst;
            public TreeNode t;
        }

        public struct Lod
        {
            public byte[] Guid;
            public int SectionCount;
            public List<Section> Sections;
            public int SizeVert;
            public int NumVert;
            public int LodCount;
        }

        public struct Section
        {
            public int Name;
            public int Unk1;
            public int Unk2;
            public int Unk3;
            public int FirstIdx1;
            public int NumFaces1;
            public int MatEff1;
            public int MatEff2;
            public int Unk4;
            public int Unk5;
            public int FirstIdx2;
            public int NumFaces2;
            public byte Unk6;
            public CustomVertex.PositionNormalTextured[] RawTriangles;
        }

        public struct Bounding
        {
            public Vector3 Origin;
            public Vector3 Box;
            public float R;
            public float[] unk;
            public byte[] raw;
            public TreeNode t;
        }

        public struct UnknownList
        {
            public int size;
            public int count;
            public byte[] data;
            public TreeNode t;
        }

        public struct RawTriangle
        {
            public Int16 v0;
            public Int16 v1;
            public Int16 v2;
            public Int16 mat;
        }
        public struct Buffers
        {
            public int UV1, UV2, IndexBuffer;
            public byte[] Wireframe1, Wireframe2;
            public TreeNode t;
        }
        public struct kDOPNode
        {
            public Vector3 min;
            public Vector3 max;
        }

        public List<kDOPNode> kdNodes;
        public List<RawTriangle> RawTriangles;
        public List<Vector3> Vertices;
        public string MyName;
        public bool isVolumetric = false; //draw debug meshes as wireframe
        public bool isSelected = false;
        #endregion

        public StaticMesh()
        {
        }

        public StaticMesh(PCCObject Pcc, int Index)
        {
            pcc = Pcc;
            index = Index;
            MyName = pcc.Exports[index].ObjectName;
            if (MyName.ToLower().Contains("volumetric") || MyName.ToLower().Contains("spheremesh"))
                isVolumetric = true;
            memory = pcc.Exports[index].Data;
            memsize = memory.Length;
            props = PropertyReader.getPropList(pcc, memory);
            Deserialize();

        }

        #region Deserialize Binary

        private void Deserialize()
        {
            byte[] binary = Dump();
            if (binary == null || binary.Length == 0)
                return;
            DeserializeDump(binary);
        }

        public void DeserializeDump(byte[] raw)
        {
            Mesh = new MeshStruct();
            readerpos = 0;
            try
            {
                ReadBoundings(raw);
                ReadkDOPTree(raw);
                ReadRawTris(raw);
                ReadMaterials(raw);
                ReadVerts(raw);
                ReadBuffers(raw);
                ReadEdges(raw);
                UnknownPart(raw);
                ReadIndexBuffer(raw);
                ReadEnd(raw);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("StaticMesh Error: " + e.Message);
            }
        }

        public void ReadBoundings(byte[] memory)
        {
            TreeNode res = new TreeNode("Bounding pos: 0x" + readerpos.ToString("X4"));
            Bounding b = new Bounding();
            b.Origin.X = BitConverter.ToSingle(memory, readerpos);
            b.Origin.Y = BitConverter.ToSingle(memory, readerpos + 4);
            b.Origin.Z = BitConverter.ToSingle(memory, readerpos + 8);
            b.Box.X = BitConverter.ToSingle(memory, readerpos + 12);
            b.Box.Y = BitConverter.ToSingle(memory, readerpos + 16);
            b.Box.Z = BitConverter.ToSingle(memory, readerpos + 20);            
            Meshplorer.Preview3D.Cubes = new List<Meshplorer.Preview3D.DXCube>();
            Meshplorer.Preview3D.DXCube c = Meshplorer.Preview3D.NewCubeByOrigSize(b.Origin - b.Box, b.Box * 2.0f, 0);
            Meshplorer.Preview3D.CamDistance = (float)Math.Sqrt(b.Box.X * b.Box.X + b.Box.Y * b.Box.Y + b.Box.Z * b.Box.Z) * 2;
            Meshplorer.Preview3D.Cubes.Add(c);
            b.R = BitConverter.ToSingle(memory, readerpos + 24);
            b.unk = new float[7];
            int pos = readerpos + 28;
            string unk = "Unknown{";
            for (int i = 0; i < 7; i++)
            {
                b.unk[i] = BitConverter.ToSingle(memory, pos);
                unk += b.unk[i].ToString() + " ";
                pos += 4;
            }
            unk += "}";
            b.raw = new byte[56];
            for (int i = 0; i < 56; i++)
            {
                b.raw[i] = memory[readerpos];
                readerpos++;
            }
            res.Nodes.Add(new TreeNode("Origin: {" + b.Origin.X.ToString() + " ; " + b.Origin.Y.ToString() + " ; " + b.Origin.Z.ToString() + "}"));
            res.Nodes.Add(new TreeNode("Box: {" + b.Box.X.ToString() + " ; " + b.Box.Y.ToString() + " ; " + b.Box.Z.ToString() + "}"));
            res.Nodes.Add(new TreeNode("Radius: {" + b.R.ToString() + "}"));
            res.Nodes.Add(unk);
            b.t = res;
            Mesh.Bounds = b;
        }

        public void ReadkDOPTree(byte[] memory)
        {
            TreeNode res = new TreeNode("kDOP-tree pos: 0x" + readerpos.ToString("X4"));
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode("Size : " + l.size.ToString()));
            res.Nodes.Add(new TreeNode("Count : " + l.count.ToString()));
            TreeNode t = new TreeNode("Data");
            kdNodes = new List<kDOPNode>();
            for (int i = 0; i < l.count; i++)
            {
                kDOPNode nd = new kDOPNode();
                nd.min = new Vector3(memory[readerpos] / 256f, memory[readerpos + 1] / 256f, memory[readerpos + 2] / 256f);
                nd.max = new Vector3(memory[readerpos + 3] / 256f, memory[readerpos + 4] / 256f, memory[readerpos + 5] / 256f);
                kdNodes.Add(nd);
                string s = "";
                for (int j = 0; j < l.size; j++)
                {
                    s += (memory[readerpos] / 256f).ToString() + " ";
                    readerpos++;
                }
                t.Nodes.Add(new TreeNode("#" + i.ToString("D4") + " : " + s));
            }
            ReadkdNodes(Meshplorer.Preview3D.Cubes[0], memory);
            res.Nodes.Add(t);
            l.t = res;
            Mesh.kDOPTree = l;
        }

        public void ReadkdNodes(Meshplorer.Preview3D.DXCube bound, byte[] memory)
        {
            if (kdNodes != null && kdNodes.Count() > 3)
            {
                #region Level 1
                Meshplorer.Preview3D.DXCube c = Meshplorer.Preview3D.NewCubeByCubeMinMax(bound, kdNodes[0].min, kdNodes[0].max, Color.LightBlue.ToArgb());
                Meshplorer.Preview3D.Cubes.Add(c);
                #endregion
                #region Level2
                Vector3 org1 = c.origin;
                Vector3 size1 = c.size;
                size1.X /= 2f;
                Vector3 org2 = c.origin + new Vector3(size1.X, 0, 0);
                Vector3 size2 = size1;
                Meshplorer.Preview3D.DXCube c1 = Meshplorer.Preview3D.NewCubeByOrigSize(org1, size1, Color.Orange.ToArgb());
                Meshplorer.Preview3D.DXCube c2 = Meshplorer.Preview3D.NewCubeByOrigSize(org2, size2, Color.Orange.ToArgb());
                Meshplorer.Preview3D.DXCube c3 = Meshplorer.Preview3D.NewCubeByCubeMinMax(c1, kdNodes[2].min, kdNodes[2].max, Color.Orange.ToArgb());
                Meshplorer.Preview3D.DXCube c4 = Meshplorer.Preview3D.NewCubeByCubeMinMax(c2, kdNodes[1].min, kdNodes[1].max, Color.Orange.ToArgb());
                Meshplorer.Preview3D.Cubes.Add(c3);
                Meshplorer.Preview3D.Cubes.Add(c4);
                #endregion
            }
        }

        public void ReadRawTris(byte[] memory)
        {
            TreeNode res = new TreeNode("Raw triangles pos: 0x" + readerpos.ToString("X4"));
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode("Size : " + l.size.ToString()));
            res.Nodes.Add(new TreeNode("Count : " + l.count.ToString()));
            TreeNode t = new TreeNode("Data");
            RawTriangles = new List<RawTriangle>();
            for (int i = 0; i < l.count; i++)
            {
                RawTriangle r = new RawTriangle();
                r.v0 = BitConverter.ToInt16(memory, readerpos);
                r.v1 = BitConverter.ToInt16(memory, readerpos + 2);
                r.v2 = BitConverter.ToInt16(memory, readerpos + 4);
                r.mat = BitConverter.ToInt16(memory, readerpos + 6);
                RawTriangles.Add(r);
                string s = "";
                for (int j = 0; j < l.size; j++)
                {
                    s += memory[readerpos].ToString("X2") + " ";
                    readerpos++;
                }
                t.Nodes.Add(new TreeNode("#" + i.ToString("D4") + " : " + s));
            }
            res.Nodes.Add(t);
            RawTris rt = new RawTris();
            rt.RawTriangles = RawTriangles;
            rt.t = res;
            Mesh.RawTris = rt;
        }

        public void ReadMaterials(byte[] memory)
        {
            TreeNode res = new TreeNode("Materials pos: 0x" + readerpos.ToString("X4"));
            Materials m = new Materials();
            Mesh.InternalVersion = BitConverter.ToInt32(memory, readerpos);
            m.LodCount = BitConverter.ToInt32(memory, readerpos + 4);
            res.Nodes.Add("LodCount : " + m.LodCount);
            TreeNode t1 = new TreeNode("Lods");
            readerpos += 8;
            m.Lods = new List<Lod>();
            m.MatInst = new List<MaterialInstanceConstant>();
            for (int i = 0; i < m.LodCount; i++)
            {
                Lod l = new Lod();
                l.Guid = new byte[16];
                string t = "Guid: ";
                for (int j = 0; j < 16; j++)
                {
                    l.Guid[j] = memory[readerpos];
                    t += l.Guid[j].ToString("X2") + " ";
                    readerpos++;
                }
                t1.Nodes.Add(new TreeNode(t));
                l.SectionCount = BitConverter.ToInt32(memory, readerpos);
                t1.Nodes.Add(new TreeNode("Section Count : " + l.SectionCount.ToString()));
                l.Sections = new List<Section>();
                readerpos += 4;
                TreeNode t2 = new TreeNode("Sections");
                for (int j = 0; j < l.SectionCount; j++)
                {
                    Section s = new Section();
                    string q = "Section [" + j.ToString() + "] : {(Name)";
                    //s.Name = BitConverter.ToInt32(memory, readerpos);
                    //if (s.Name > 0)
                    //    m.MatInst.Add(new MaterialInstanceConstant(pcc, s.Name - 1));
                    //    q += pcc.getObjectName(s.Name) + " ; ";
                    //else
                    q += s.Name.ToString() + " ; ";
                    s.Unk1 = BitConverter.ToInt32(memory, readerpos + 4);
                    q += s.Unk1.ToString() + " ; ";
                    s.Unk2 = BitConverter.ToInt32(memory, readerpos + 8);
                    q += s.Unk2.ToString() + " ; ";
                    s.Unk3 = BitConverter.ToInt32(memory, readerpos + 12);
                    q += s.Unk3.ToString() + " ; (FirstIdx)";
                    s.FirstIdx1 = BitConverter.ToInt32(memory, readerpos + 16);
                    q += s.FirstIdx1.ToString() + " ; (NumFaces)";
                    s.NumFaces1 = BitConverter.ToInt32(memory, readerpos + 20);
                    q += s.NumFaces1.ToString() + " ; (VertexMin)";
                    s.MatEff1 = BitConverter.ToInt32(memory, readerpos + 24);
                    q += s.MatEff1.ToString() + " ; (VertexMax)";
                    s.MatEff2 = BitConverter.ToInt32(memory, readerpos + 28);
                    q += s.MatEff2.ToString() + " ; ";
                    s.Unk4 = BitConverter.ToInt32(memory, readerpos + 32);
                    q += s.Unk4.ToString() + " ; ";
                    s.Unk5 = BitConverter.ToInt32(memory, readerpos + 36);
                    q += s.Unk5.ToString() + " ; (FirstIdx)";
                    if (s.Unk5 == 1)
                    {
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 40);
                        q += s.FirstIdx2.ToString() + " ; (NumFaces)";
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 44);
                        q += s.NumFaces2.ToString() + " ; ";
                        s.Unk6 = memory[readerpos + 48];
                        q += s.Unk6.ToString() + "}";
                    }
                    else
                    {
                        s.Unk6 = memory[readerpos + 40];
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 41);
                        q += s.FirstIdx2.ToString() + " ; (NumFaces)";
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 45);
                        q += s.NumFaces2.ToString() + " ; ";                        
                        q += s.Unk6.ToString() + "}";
                    }
                    t2.Nodes.Add(q);
                    readerpos += 49;
                    l.Sections.Add(s);
                }
                t1.Nodes.Add(t2);
                l.SizeVert = BitConverter.ToInt32(memory, readerpos);
                t1.Nodes.Add(new TreeNode("Size Verts : " + l.SizeVert.ToString()));
                l.NumVert = BitConverter.ToInt32(memory, readerpos + 4);
                t1.Nodes.Add(new TreeNode("Num Vert : " + l.NumVert.ToString()));
                l.LodCount = BitConverter.ToInt32(memory, readerpos + 8);
                t1.Nodes.Add(new TreeNode("Lod Count : " + l.LodCount.ToString()));
                if(l.Sections[0].Unk5 == 1)
                    readerpos += 12;
                else
                    readerpos += 4;
                m.Lods.Add(l);
            }
            res.Nodes.Add(t1);
            TreeNode t3 = new TreeNode("Materials");
            for (int i = 0; i < m.MatInst.Count(); i++)
                t3.Nodes.Add(m.MatInst[i].ToTree());
            res.Nodes.Add(t3);
            m.t = res;
            Mesh.Mat = m;
        }

        public void ReadVerts(byte[] memory)
        {
            TreeNode res = new TreeNode("Vertices pos: 0x" + readerpos.ToString("X4"));
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode("Size : " + l.size.ToString()));
            res.Nodes.Add(new TreeNode("Count : " + l.count.ToString()));
            TreeNode t = new TreeNode("Data");
            Vertices = new List<Vector3>();
            for (int i = 0; i < l.count; i++)
            {
                float f1 = BitConverter.ToSingle(memory, readerpos);
                float f2 = BitConverter.ToSingle(memory, readerpos + 4);
                float f3 = BitConverter.ToSingle(memory, readerpos + 8);
                Vertices.Add(new Vector3(f1, f2, f3));
                string s = f1 + " " + f2 + " " + f3;
                readerpos += l.size;
                t.Nodes.Add(new TreeNode("#" + i.ToString("D4") + " : " + s));
            }
            res.Nodes.Add(t);
            Verts v = new Verts();
            v.Points = Vertices;
            v.t = res;
            Mesh.Vertices = v;
        }

        public void ReadBuffers(byte[] memory)
        {
            Buffers X = new Buffers();
            X.Wireframe1 = new byte[4];

            TreeNode res = new TreeNode("Buffers (?), Position: 0x" + readerpos.ToString("X4"));
            byte[] buffer = new byte[20];
            int[] output = new int[3];
            for (int i = 0; i < 20; i++)
            {
                buffer[i] = memory[readerpos];
                readerpos += 1;
            }
            /// Here using struct Buffers
            output[0] = BitConverter.ToInt32(buffer, 0);
            X.UV1 = output[0];
            output[1] = BitConverter.ToInt32(buffer, 4);
            X.UV2 = output[1];
            output[2] = BitConverter.ToInt32(buffer, 8);
            X.IndexBuffer = output[2];
            int counter = 0;

            for (int i = 12; i < 16; i++)
            {
                X.Wireframe1[counter] = buffer[i];
                counter += 1;
            };
            X.Wireframe2 = new byte[4];
            counter = 0;
            for (int i = 16; i < 20; i++)
            {
                X.Wireframe2[counter] = buffer[i];
                counter += 1;
            }
            /// Here displaying all data
            res.Nodes.Add(new TreeNode("UV Buffer 1 [INT32]: " + output[0].ToString()));
            res.Nodes.Add(new TreeNode("UV Buffer 2 [INT32]: " + output[1].ToString()));
            res.Nodes.Add(new TreeNode("Index Buffer [INT32]: " + output[2].ToString()));
            res.Nodes.Add(new TreeNode("Wireframe buffer 1[four bytes] : " + buffer[12].ToString("X4") + " " + buffer[14].ToString("X4")));
            res.Nodes.Add(new TreeNode("Wireframe buffer 2 [four bytes] : " + buffer[16].ToString("X4") + " " + buffer[18].ToString("X4")));

            Mesh.Buffers = X;
            Mesh.Buffers.t = res;

        }

        public string packedNorm(int off)
        {
            string s = "(x ";
            s += String.Format("{0:0.0000}", memory[off] / 256f) + " ; y ";
            s += String.Format("{0:0.0000}", memory[off + 1] / 256f) + " ; z ";
            s += String.Format("{0:0.0000}", memory[off + 2] / 256f) + " ; w ";
            s += String.Format("{0:0.0000}", memory[off + 3] / 256f) + ")";
            return s;
        }

        public void ReadEdges(byte[] memory)
        {
            TreeNode res = new TreeNode("Edges list, start: 0x" + readerpos.ToString("X4"));
            UnknownList edges = new UnknownList(); //here using struct unknown list, later we're just filling up data array byte by byte
            Edges e = new Edges();
            edges.size = BitConverter.ToInt32(memory, readerpos);
            edges.count = BitConverter.ToInt32(memory, readerpos + 4);
            e.size = edges.size;
            e.count = edges.count;
            //quick'n'dirty fix above, need work! <--------------
            readerpos += 8;
            int len = edges.size * edges.count;
            edges.data = new byte[len];
            res.Nodes.Add(new TreeNode("Size : " + edges.size.ToString()));
            res.Nodes.Add(new TreeNode("Count : " + edges.count.ToString()));
            TreeNode data = new TreeNode("Data");
            int datacounter = 0;

            e.UVSet = new List<UVSet>();
            for (int i = 0; i < edges.count; i++)
            {
                UVSet uv = new UVSet();
                uv.UVs = new List<Vector2>();

                //here adding packed normals
                uv.x1 = memory[readerpos];
                uv.y1 = memory[readerpos + 1];
                uv.z1 = memory[readerpos + 2];
                uv.w1 = memory[readerpos + 3];
                uv.x2 = memory[readerpos + 4];
                uv.y2 = memory[readerpos + 5];
                uv.z2 = memory[readerpos + 6];
                uv.w2 = memory[readerpos + 7];

                //string rawdata = "";
                //string n1 = packedNorm(readerpos);
                //string n2 = packedNorm(readerpos + 4);
                //rawdata = n1 + " " + n2 + " UV Sets: "; //
                //          memory[readerpos].ToString("X2") + " " +
                //          memory[readerpos + 1].ToString("X2") + " " +
                //          memory[readerpos + 2].ToString("X2") + " " +
                //          memory[readerpos + 3].ToString("X2") + " " +
                //          memory[readerpos + 4].ToString("X2") + " " +
                //          memory[readerpos + 5].ToString("X2") + " " +
                //          memory[readerpos + 6].ToString("X2") + " " +
                //          memory[readerpos + 7].ToString("X2") + " " + 
                readerpos += 8;


                for (int row = 0; row < (edges.size - 8) / 4; row++)
                {
                    float u = HalfToFloat(BitConverter.ToUInt16(memory, readerpos));
                    float v = HalfToFloat(BitConverter.ToUInt16(memory, readerpos + 2));
                    uv.UVs.Add(new Vector2(u, v));
                    //rawdata += "uv(" + u + " ; " + v + ") ";
                    edges.data[datacounter] = memory[readerpos];
                    readerpos += 4;
                    datacounter += 1;
                }
                e.UVSet.Add(uv);
                data.Nodes.Add(new TreeNode(i.ToString("d4") + ": " ));
            }

            res.Nodes.Add(data);
            e.t = res;
            Mesh.Edges = e;
        }

        public void UnknownPart(byte[] memory)
        {
            TreeNode res = new TreeNode("Unknown 28 bytes, start: 0x" + readerpos.ToString("X4"));
            TreeNode unknowndata = new TreeNode("Data");
            UnknownP p = new UnknownP();
            p.data = new byte[28];

            int tempReader = readerpos;

            for (int i = 0; i < 28; i++)
            { //don't mind me, just savin' some data here.
                p.data[i] = memory[tempReader];
                tempReader++;
            }


            string s = "";
            for (int i = 0; i < 8; i++)
            {
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            unknowndata.Nodes.Add("Block 1 [8]: " + s);

            s = "";
            for (int i = 0; i < 8; i++)
            {
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add("Block 2 [8]: " + s);

            s = "";
            for (int i = 0; i < 8; i++)
            {
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add("Block 3 [8]: " + s);

            s = "";
            for (int i = 0; i < 4; i++)
            {
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add("Block 4 [4]: " + s);

            res.Nodes.Add(unknowndata);

            p.t = res;
            Mesh.UnknownPart = p;
        }

        public void ReadIndexBuffer(byte[] memory)
        {
            TreeNode res = new TreeNode("Index data, start: 0x" + readerpos.ToString("X4"));
            IndexBuffer idx = new IndexBuffer();
            idx.Indexes = new List<UInt16>();
            idx.size = BitConverter.ToInt32(memory, readerpos);
            idx.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            res.Nodes.Add(new TreeNode("Size :" + idx.size.ToString()));
            res.Nodes.Add(new TreeNode("Count :" + idx.count.ToString()));
            TreeNode data = new TreeNode();

            for (int count = 0; count < idx.count; count++)
            {
                UInt16 v = BitConverter.ToUInt16(memory, readerpos);
                idx.Indexes.Add(v);
                string s = v.ToString("X2");
                readerpos += 2;
                data.Nodes.Add("Data row " + count + ": " + s);
            }
            res.Nodes.Add(data);
            idx.t = res;
            Mesh.IdxBuf = idx;

        }

        public void ReadEnd(byte[] memory)
        {
            #region End
            EndOfStruct endChunk = new EndOfStruct();
            endChunk.data = new byte[memory.Length - readerpos];

            TreeNode res = new TreeNode("Last chunk, 0x" + readerpos.ToString("X4"));

            TreeNode End = new TreeNode("Data");
            string s = ""; //here start some unknown chunks

            // unknown 8 bytes, probably integer
            for (int i = 0; i < 8; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 1 [6]: " + s);


            // same, 8 byte int
            s = "";
            for (int i = 8; i < 16; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 2 [8]: " + s);

            // 4 bytes, always zero
            s = "";
            for (int i = 16; i < 20; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 3 [4]: " + s);

            //4 bytes, always 0x1
            s = "";
            for (int i = 20; i < 24; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 4 [4]: " + s);

            //then some 8 bytes of unknown data
            s = "";
            for (int i = 24; i < 32; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 5 [8]: " + s);

            // 6 bytes, always zero
            s = "";
            for (int i = 32; i < 38; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 6 [6]: " + s);

            //4 bytes data
            s = "";
            for (int i = 38; i < 42; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 7 [4]: " + s);

            //6 bytes zeroes
            s = "";
            for (int i = 42; i < 48; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 8 [6]: " + s);


            //4 bytes zeroes
            s = "";
            for (int i = 48; i < 52; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 9 [4]: " + s);

            //and finally 16 bytes of some data
            s = "";
            for (int i = 52; i < 68; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += memory[readerpos].ToString("X2") + " ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 10 [16]: " + s);
            Mesh.End = endChunk;
            res.Nodes.Add(End);
            Mesh.End.t = End;

            #endregion
        }


        #endregion

        #region Serialize Data

        public void SerializeToFile(string path)
        {
            BitConverter.IsLittleEndian = true;
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            WriteBoundaries(fs);
            Write_kDOP(fs);
            WriteTriangles(fs);
            WriteMaterials(fs);
            WriteVerts(fs);
            WriteBuffers(fs);
            WriteEdges(fs);
            WriteUnknownPart(fs);
            WriteIndexBuffers(fs);
            WriteEnd(fs);

            fs.Close();
        }

        public byte[] SerializeToBuffer()
        {
            BitConverter.IsLittleEndian = true;
            MemoryStream fs = new MemoryStream();
            WriteProperties(fs);
            WriteBoundaries(fs);
            Write_kDOP(fs);
            WriteTriangles(fs);
            WriteMaterials(fs);
            WriteVerts(fs);
            WriteBuffers(fs);
            WriteEdges(fs);
            WriteUnknownPart(fs);
            WriteIndexBuffers(fs);
            WriteEnd(fs);
            return fs.ToArray();
        }
        #region FileStreamed
        public void WriteBoundaries(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.X), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.Y), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.Z), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.X), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.Y), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.Z), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.R), 0, 4);
            for (int i = 0; i < 7; i++)
                fs.Write(BitConverter.GetBytes(Mesh.Bounds.unk[i]), 0, 4);
        }

        public void Write_kDOP(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.count), 0, 4);
            fs.Write(Mesh.kDOPTree.data, 0, Mesh.kDOPTree.size * Mesh.kDOPTree.count);


        }

        public void WriteTriangles(FileStream fs)
        {
            byte[] size = new byte[4];
            size[0] = (byte)8;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles.Count()), 0, 4);

            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v0), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v1), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v2), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].mat), 0, 2);
            }
        }

        public void WriteMaterials(FileStream fs)
        {

            fs.Write(BitConverter.GetBytes(Mesh.InternalVersion), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Mat.LodCount), 0, 4);

            for (int i = 0; i < Mesh.Mat.LodCount; i++)
            {
                fs.Write(Mesh.Mat.Lods[i].Guid, 0, 16);
                fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].SectionCount), 0, 4);

                for (int j = 0; j < Mesh.Mat.Lods[i].SectionCount; j++)
                {
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Name), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk2), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk3), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].MatEff1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].MatEff2), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk4), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk5), 0, 4);
                    if (Mesh.Mat.Lods[i].Sections[j].Unk5 == 1)
                    {
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk6), 0, 1);
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk6), 0, 1);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces2), 0, 4);                        
                    }
                        
                }
                if (Mesh.Mat.Lods[i].Sections[0].Unk5 == 1)
                {
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].SizeVert), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].NumVert), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].LodCount), 0, 4);
                }
            }     
        }

        public void WriteVerts(FileStream fs)
        {
            byte[] size = new byte[4];
            size[0] = 12;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points.Count), 0, 4);

            for (int i = 0; i < Mesh.Vertices.Points.Count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].X), 0, 4);
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].Y), 0, 4);
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].Z), 0, 4);
            }
            //that was easy ^_^
        }

        public void WriteBuffers(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.UV1), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.UV2), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.IndexBuffer), 0, 4);
            fs.Write(Mesh.Buffers.Wireframe1, 0, 4);
            fs.Write(Mesh.Buffers.Wireframe2, 0, 4);

        }

        public void WriteEdges(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Edges.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Edges.count), 0, 4);

            for (int i = 0; i < Mesh.Edges.count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].x1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].y1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].z1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].w1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].x2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].y2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].z2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].w2), 0, 1);

                for (int j = 0; j < Mesh.Edges.UVSet[i].UVs.Count; j++)
                {

                    fs.Write(BitConverter.GetBytes(FloatToHalf(Mesh.Edges.UVSet[i].UVs[j].X)), 0, 2);
                    fs.Write(BitConverter.GetBytes(FloatToHalf(Mesh.Edges.UVSet[i].UVs[j].Y)), 0, 2);
                }
            }
            // You thought you had me there, didn't you? HaHA! 
        }

        public void WriteUnknownPart(FileStream fs)
        {
            fs.Write(Mesh.UnknownPart.data, 0, 28);
            //I hope that this works...
        }

        public void WriteIndexBuffers(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.count), 0, 4);

            for (int i = 0; i < Mesh.IdxBuf.count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.Indexes[i]), 0, 2);
            }
        }

        public void WriteEnd(FileStream fs)
        {
            fs.Write(Mesh.End.data, 0, Mesh.End.data.Length);
        }
        #endregion

        #region InMemory
        public void WriteProperties(MemoryStream fs)

        {
            int len = props[props.Count() - 1].offend;            
            byte[] buffer = new byte[len];
            for (int i = 0; i < len; i++)
                buffer[i] = memory[i];
            fs.Write(buffer, 0, len);
        }

        public void WriteBoundaries(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.X), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.Y), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Origin.Z), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.X), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.Y), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.Box.Z), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.R), 0, 4);
            for (int i = 0; i < 7; i++)
                fs.Write(BitConverter.GetBytes(Mesh.Bounds.unk[i]), 0, 4);
        }

        public void Write_kDOP(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.count), 0, 4);
            fs.Write(Mesh.kDOPTree.data, 0, Mesh.kDOPTree.size * Mesh.kDOPTree.count);


        }

        public void WriteTriangles(MemoryStream fs)
        {
            byte[] size = new byte[4];
            size[0] = (byte)8;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles.Count()), 0, 4);

            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v0), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v1), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].v2), 0, 2);
                fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles[i].mat), 0, 2);
            }
        }

        public void WriteMaterials(MemoryStream fs)
        {

            fs.Write(BitConverter.GetBytes(Mesh.InternalVersion), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Mat.LodCount), 0, 4);

            for (int i = 0; i < Mesh.Mat.LodCount; i++)
            {
                fs.Write(Mesh.Mat.Lods[i].Guid, 0, 16);
                fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].SectionCount), 0, 4);

                for (int j = 0; j < Mesh.Mat.Lods[i].SectionCount; j++)
                {
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Name), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk2), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk3), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].MatEff1), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].MatEff2), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk4), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk5), 0, 4);
                    if (Mesh.Mat.Lods[i].Sections[j].Unk5 == 1)
                    {
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk6), 0, 1);
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].Unk6), 0, 1);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].FirstIdx2), 0, 4);
                        fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].Sections[j].NumFaces2), 0, 4);
                    }

                }
                if (Mesh.Mat.Lods[i].Sections[0].Unk5 == 1)
                {
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].SizeVert), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].NumVert), 0, 4);
                    fs.Write(BitConverter.GetBytes(Mesh.Mat.Lods[i].LodCount), 0, 4);
                }
            } 
        }

        public void WriteVerts(MemoryStream fs)
        {
            byte[] size = new byte[4];
            size[0] = 12;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points.Count()), 0, 4);

            for (int i = 0; i < Mesh.Vertices.Points.Count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].X), 0, 4);
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].Y), 0, 4);
                fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points[i].Z), 0, 4);
            }
            //that was easy ^_^
        }

        public void WriteBuffers(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.UV1), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.UV2), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Buffers.IndexBuffer), 0, 4);
            fs.Write(Mesh.Buffers.Wireframe1, 0, 4);
            fs.Write(Mesh.Buffers.Wireframe2, 0, 4);

        }

        public void WriteEdges(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.Edges.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Edges.count), 0, 4);

            for (int i = 0; i < Mesh.Edges.count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].x1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].y1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].z1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].w1), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].x2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].y2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].z2), 0, 1);
                fs.Write(BitConverter.GetBytes(Mesh.Edges.UVSet[i].w2), 0, 1);

                for (int j = 0; j < Mesh.Edges.UVSet[i].UVs.Count; j++)
                {

                    fs.Write(BitConverter.GetBytes(FloatToHalf(Mesh.Edges.UVSet[i].UVs[j].X)), 0, 2);
                    fs.Write(BitConverter.GetBytes(FloatToHalf(Mesh.Edges.UVSet[i].UVs[j].Y)), 0, 2);
                }
            }
        }

        public void WriteUnknownPart(MemoryStream fs)
        {
            fs.Write(Mesh.UnknownPart.data, 0, 28);
            //I hope that this works...
        }

        public void WriteIndexBuffers(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.count), 0, 4);

            for (int i = 0; i < Mesh.IdxBuf.count; i++)
            {
                fs.Write(BitConverter.GetBytes(Mesh.IdxBuf.Indexes[i]), 0, 2);
            }
        }

        public void WriteEnd(MemoryStream fs)
        {
            fs.Write(Mesh.End.data, 0, Mesh.End.data.Length);
        }
        #endregion


        #endregion

        #region DirectX

        public void DrawMesh(Device device)
        {
            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
            device.RenderState.Lighting = false;
            if(DirectXGlobal.DrawWireFrame)
                device.RenderState.FillMode = FillMode.WireFrame;
            else
                device.RenderState.FillMode = FillMode.Solid;
            if (Mesh.Mat.Lods != null) 
            for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
            {
                Section sec = Mesh.Mat.Lods[0].Sections[i];
                if (DirectXGlobal.Tex_Default != null)
                    device.SetTexture(0, DirectXGlobal.Tex_Default);
                else
                    DirectXGlobal.Tex_Default = TextureLoader.FromFile(device, Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\Default.bmp");
                if (sec.RawTriangles == null)
                {
                    sec.RawTriangles = new CustomVertex.PositionNormalTextured[sec.NumFaces1 * 3];
                    if (Mesh.IdxBuf.Indexes.Count() != 0)
                        for (int j = 0; j < sec.NumFaces1; j++)
                        {
                            int Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3];
                            Vector3 pos = Mesh.Vertices.Points[Idx];
                            Vector2 UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0,0,0), UV.X, UV.Y);
                            Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3 + 1];
                            pos = Mesh.Vertices.Points[Idx];
                            UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3 + 1] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                            Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3 + 2];
                            pos = Mesh.Vertices.Points[Idx];
                            UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3 + 2] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                        }
                    else
                        for (int j = 0; j < sec.NumFaces1; j++)
                        {
                            int Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v0;
                            Vector3 pos = Mesh.Vertices.Points[Idx];
                            Vector2 UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                            Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v1;
                            pos = Mesh.Vertices.Points[Idx];
                            UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3 + 1] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                            Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v2;
                            pos = Mesh.Vertices.Points[Idx];
                            UV = Mesh.Edges.UVSet[Idx].UVs[0];
                            sec.RawTriangles[j * 3 + 2] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                        }
                    for (int j = 0; j < sec.RawTriangles.Length; j+=3)
                    {
                        Vector3 p0 = sec.RawTriangles[j].Position - sec.RawTriangles[j + 1].Position;
                        Vector3 p1 = sec.RawTriangles[j].Position - sec.RawTriangles[j + 2].Position;
                        p0.Normalize();
                        p1.Normalize();
                        Vector3 n = Vector3.Cross(p0, p1);
                        sec.RawTriangles[j].Normal = n;
                        sec.RawTriangles[j + 1].Normal = n;
                        sec.RawTriangles[j + 2].Normal = n;
                    }

                }
                device.DrawUserPrimitives(PrimitiveType.TriangleList, sec.NumFaces1, sec.RawTriangles);
            }
        }

        public void Render(Device device, Matrix m)
        {
            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
            device.RenderState.Lighting = true;
            if (DirectXGlobal.DrawWireFrame || isVolumetric)
                device.RenderState.FillMode = FillMode.WireFrame;
            else
                device.RenderState.FillMode = FillMode.Solid;
            device.Transform.World = m;
            device.RenderState.CullMode = Cull.None;
            try
            {
                for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
                {
                    Section sec = Mesh.Mat.Lods[0].Sections[i];
                    if (isSelected)
                        device.SetTexture(0, DirectXGlobal.Tex_Select);
                    else
                        device.SetTexture(0, DirectXGlobal.Tex_Default);
                    #region CreateDXMesh
                    if (sec.RawTriangles == null)
                    {
                        sec.RawTriangles = new CustomVertex.PositionNormalTextured[sec.NumFaces1 * 3];
                        try
                        {
                            if (Mesh.IdxBuf.Indexes.Count() != 0)
                                for (int j = 0; j < sec.NumFaces1; j++)
                                {
                                    int Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3];
                                    Vector3 pos = Mesh.Vertices.Points[Idx];
                                    Vector2 UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                    Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3 + 1];
                                    pos = Mesh.Vertices.Points[Idx];
                                    UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3 + 1] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                    Idx = Mesh.IdxBuf.Indexes[sec.FirstIdx1 + j * 3 + 2];
                                    pos = Mesh.Vertices.Points[Idx];
                                    UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3 + 2] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                }
                            else
                                for (int j = 0; j < sec.NumFaces1; j++)
                                {
                                    int Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v0;
                                    Vector3 pos = Mesh.Vertices.Points[Idx];
                                    Vector2 UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                    Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v1;
                                    pos = Mesh.Vertices.Points[Idx];
                                    UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3 + 1] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                    Idx = Mesh.RawTris.RawTriangles[sec.FirstIdx1 / 3 + j].v2;
                                    pos = Mesh.Vertices.Points[Idx];
                                    UV = Mesh.Edges.UVSet[Idx].UVs[0];
                                    sec.RawTriangles[j * 3 + 2] = new CustomVertex.PositionNormalTextured(pos, new Vector3(0, 0, 0), UV.X, UV.Y);
                                }
                        }
                        catch (Exception e)
                        {
                            DebugOutput.PrintLn("Static Mesh error on reading dx mesh: " + e.Message);
                            sec.RawTriangles = new CustomVertex.PositionNormalTextured[0];
                        }

                        for (int j = 0; j < sec.RawTriangles.Length; j += 3)
                        {
                            Vector3 p0 = sec.RawTriangles[j].Position - sec.RawTriangles[j + 1].Position;
                            Vector3 p1 = sec.RawTriangles[j].Position - sec.RawTriangles[j + 2].Position;
                            p0.Normalize();
                            p1.Normalize();
                            Vector3 n = Vector3.Cross(p0, p1);
                            sec.RawTriangles[j].Normal = n;
                            sec.RawTriangles[j + 1].Normal = n;
                            sec.RawTriangles[j + 2].Normal = n;
                        }
                        Mesh.Mat.Lods[0].Sections[i] = sec;
                    }
                    #endregion
                    if (sec.RawTriangles != null && sec.RawTriangles.Length != 0)
                        device.DrawUserPrimitives(PrimitiveType.TriangleList, sec.RawTriangles.Length / 3, sec.RawTriangles);
                }
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Static Mesh ERROR: " + e.Message);
            }
        }

        public void Focus(Matrix m)
        {
            Vector3 center = Vector3.TransformCoordinate(Mesh.Bounds.Origin, m);
            Vector3 dis = center - DirectXGlobal.Cam.pos;
            dis.Normalize();
            DirectXGlobal.Cam.dir = dis;
            DirectXGlobal.Cam.pos = center - dis * Mesh.Bounds.R * 2;
        }

        public float Process3DClick(Vector3 org, Vector3 dir, Matrix m)
        {
            float dist = -1f;
                for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
                {
                    Section sec = Mesh.Mat.Lods[0].Sections[i];
                    float d = -1f;
                    for (int j = 0; j < sec.RawTriangles.Length / 3; j++)
                        if (RayIntersectTriangle(org,
                                                dir,
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3].Position, m),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 1].Position, m),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 2].Position, m),
                                                out d))
                            if ((d < dist && d > 0) || (dist == -1f && d > 0))
                                dist = d;
                }
            return dist;
        }

        Vector2 RaySphereIntersect(Vector3 p, Vector3 d, float r)
        {
            float det, b;
            b = -Vector3.Dot(p, d);
            det = b * b - Vector3.Dot(p, p) + r * r;
            if (det < 0) return new Vector2(-1, -1);
            det = (float)Math.Sqrt(det);
            Vector2 v = new Vector2(b - det, b + det);
            if (v.Y < 0) return new Vector2(-1, -1);
            if (v.X < 0) v.X = 0;
            return v;
        }

        public bool RayIntersectTriangle(Vector3 rayPosition, Vector3 rayDirection, Vector3 tri0, Vector3 tri1, Vector3 tri2, out float pickDistance)
        {
            pickDistance = -1f;
            // Find vectors for two edges sharing vert0
            Vector3 edge1 = tri1 - tri0;
            Vector3 edge2 = tri2 - tri0;
            // Begin calculating determinant - also used to calculate barycentricU parameter
            Vector3 pvec = Vector3.Cross(rayDirection, edge2);
            // If determinant is near zero, ray lies in plane of triangle
            float det = Vector3.Dot(edge1, pvec);
            if (det < 0.0001f)
                return false;
            // Calculate distance from vert0 to ray origin
            Vector3 tvec = rayPosition - tri0;
            // Calculate barycentricU parameter and test bounds
            float barycentricU = Vector3.Dot(tvec, pvec);
            if (barycentricU < 0.0f || barycentricU > det)
                return false;
            // Prepare to test barycentricV parameter
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            // Calculate barycentricV parameter and test bounds
            float barycentricV = Vector3.Dot(rayDirection, qvec);
            if (barycentricV < 0.0f || barycentricU + barycentricV > det)
                return false;
            // Calculate pickDistance
            pickDistance = Vector3.Dot(edge2, qvec);
            float fInvDet = 1.0f / det;
            pickDistance *= fInvDet;
            return true;
        }

        public void GenerateMesh()
        {
            if (Mesh.IdxBuf.Indexes.Count() != 0)
            {
                Meshplorer.Preview3D.RawTriangles = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured[Mesh.IdxBuf.Indexes.Count()];
                for (int i = 0; i < Mesh.IdxBuf.Indexes.Count(); i++)
                {
                    int idx = Mesh.IdxBuf.Indexes[i];
                    Vector3 pos = Mesh.Vertices.Points[idx];
                    Vector2 UV = Mesh.Edges.UVSet[idx].UVs[0];
                    Meshplorer.Preview3D.RawTriangles[i] = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured(pos, UV.X, UV.Y);
                }
            }
            else
            {
                Meshplorer.Preview3D.RawTriangles = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured[Mesh.RawTris.RawTriangles.Count() * 3];
                for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
                {
                    int idx = Mesh.RawTris.RawTriangles[i].v0;
                    Vector3 pos = Mesh.Vertices.Points[idx];
                    Vector2 UV = Mesh.Edges.UVSet[idx].UVs[0];
                    Meshplorer.Preview3D.RawTriangles[i * 3] = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured(pos, UV.X, UV.Y);
                    idx = Mesh.RawTris.RawTriangles[i].v1;
                    pos = Mesh.Vertices.Points[idx];
                    UV = Mesh.Edges.UVSet[idx].UVs[0];
                    Meshplorer.Preview3D.RawTriangles[i * 3 + 1] = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured(pos, UV.X, UV.Y);
                    idx = Mesh.RawTris.RawTriangles[i].v2;
                    pos = Mesh.Vertices.Points[idx];
                    UV = Mesh.Edges.UVSet[idx].UVs[0];
                    Meshplorer.Preview3D.RawTriangles[i * 3 + 2] = new Microsoft.DirectX.Direct3D.CustomVertex.PositionTextured(pos, UV.X, UV.Y);
                }
            }
            if (Mesh.Mat.MatInst.Count() != 0)
            {
                MaterialInstanceConstant m = Mesh.Mat.MatInst[0];
                Meshplorer.Preview3D.CurrentTex = null;
                for (int i = 0; i < m.Textures.Count(); i++)
                    if (m.Textures[i].Desc.ToLower().Contains("diffuse") && Meshplorer.Preview3D.device != null)
                    {
                        Texture2D t = new Texture2D(pcc, m.Textures[i].TexIndex - 1);
                        string loc = Path.GetDirectoryName(Application.ExecutablePath);
                        Texture2D.ImageInfo inf = new Texture2D.ImageInfo();
                        for (int j = 0; j < t.imgList.Count(); j++)
                            if (t.imgList[j].storageType != Texture2D.storage.empty)
                            {
                                inf = t.imgList[j];
                                break;
                            }
                        if (File.Exists(loc + "\\exec\\TempTex.dds"))
                            File.Delete(loc + "\\exec\\TempTex.dds");
                        t.extractImage(inf, ME3Directory.cookedPath, loc + "\\exec\\TempTex.dds");
                        if (File.Exists(loc + "\\exec\\TempTex.dds"))
                        {
                            Texture t2 = TextureLoader.FromFile(Meshplorer.Preview3D.device, loc + "\\exec\\TempTex.dds");
                            Meshplorer.Preview3D.CurrentTex = t2;
                        }
                        break;
                    }
            }
        }

        #endregion

        #region Helpers

        public void CalcTangentSpace()
        {
            int vertexCount = Mesh.Vertices.Points.Count();
            Vector3[] vertices = ToVec3(Mesh.Vertices.Points);
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] texcoords = new Vector2[vertexCount];
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                RawTriangle raw = Mesh.RawTris.RawTriangles[i];
                Vector3 v1 = Mesh.Vertices.Points[raw.v0];
                Vector3 v2 = Mesh.Vertices.Points[raw.v1];
                Vector3 v3 = Mesh.Vertices.Points[raw.v2];
                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                normals[raw.v0] += Vector3.Cross(edge1, edge2);
                edge1 = v3 - v2;
                edge2 = v1 - v2;
                normals[raw.v1] += Vector3.Cross(edge1, edge2);
                edge1 = v1 - v3;
                edge2 = v2 - v3;
                normals[raw.v2] += Vector3.Cross(edge1, edge2);
                texcoords[raw.v0] = Mesh.Edges.UVSet[raw.v0].UVs[0];
                texcoords[raw.v1] = Mesh.Edges.UVSet[raw.v1].UVs[0];
                texcoords[raw.v2] = Mesh.Edges.UVSet[raw.v2].UVs[0];
            }
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                RawTriangle raw = Mesh.RawTris.RawTriangles[i];
                normals[raw.v0].Normalize();
                normals[raw.v1].Normalize();
                normals[raw.v2].Normalize();
            }
            Vector4[] tangents = new Vector4[vertexCount];
            Vector4[] bitangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                RawTriangle raw = Mesh.RawTris.RawTriangles[i];
                Vector3 v1 = vertices[raw.v0];
                Vector3 v2 = vertices[raw.v1];
                Vector3 v3 = vertices[raw.v2];

                Vector2 w1 = texcoords[raw.v0];
                Vector2 w2 = texcoords[raw.v1];
                Vector2 w3 = texcoords[raw.v2];

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

                tan1[raw.v0] += sdir;
                tan1[raw.v1] += sdir;
                tan1[raw.v2] += sdir;

                tan2[raw.v0] += tdir;
                tan2[raw.v1] += tdir;
                tan2[raw.v2] += tdir;
            }

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan1[i];
                Vector3 tmp = (t - n * Vector3.Dot(n, t));
                tmp.Normalize();
                tangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z,0);
                tangents[i].W = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;
            }
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan2[i];
                Vector3 tmp = (t - n * Vector3.Dot(n, t));
                tmp.Normalize();
                bitangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z, 0);
                bitangents[i].W = (Vector3.Dot(Vector3.Cross(n, t), tan1[i]) < 0.0f) ? -1.0f : 1.0f;
            }
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
            {
                RawTriangle raw = Mesh.RawTris.RawTriangles[i];
                ApplyTangents(raw.v0, tangents[raw.v0] * -1f, bitangents[raw.v0] * -1f);
                ApplyTangents(raw.v1, tangents[raw.v1] * -1f, bitangents[raw.v1] * -1f);
                ApplyTangents(raw.v2, tangents[raw.v2] * -1f, bitangents[raw.v2] * -1f);
            }
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
                v.Add(new Vector3(p.x,p.y,p.z));
            return v;
        }

        public Vector3[] ToVec3(PSKFile.PSKPoint[] points)
        {
            Vector3[] v = new Vector3[points.Count()];
            int count = 0;
            foreach (PSKFile.PSKPoint p in points)
            {
                v[count] = new Vector3(p.x, p.y, p.z);
                count++;
            }
            return v;
        }

        public Vector3[] ToVec3(List<Vector3> points)
        {
            Vector3[] v = new Vector3[points.Count];
            int count = 0;
            foreach (Vector3 p in points)
            {
                v[count] = p;
                count++;
            }
            return v;
        }

        public void ApplyTangents(int edge, Vector4 tan, Vector4 bitan)
        {
            UVSet us = Mesh.Edges.UVSet[edge];
            us.x2 = Convert.ToByte((tan.X * 0.5f + 0.5f) * 0xFF);
            us.y2 = Convert.ToByte((tan.Y * 0.5f + 0.5f) * 0xFF);
            us.z2 = Convert.ToByte((tan.Z * 0.5f + 0.5f) * 0xFF);
            us.w2 = Convert.ToByte((tan.W * 0.5f + 0.5f) * 0xFF);
            us.x1 = Convert.ToByte((bitan.X * 0.5f + 0.5f) * 0xFF);
            us.y1 = Convert.ToByte((bitan.Y * 0.5f + 0.5f) * 0xFF);
            us.z1 = Convert.ToByte((bitan.Z * 0.5f + 0.5f) * 0xFF);
            us.w1 = Convert.ToByte((bitan.W * 0.5f + 0.5f) * 0xFF);
            Mesh.Edges.UVSet[edge] = us;
        }

        public void RecalculateBoundings()
        {
            Vector3 org = Mesh.Vertices.Points[0];
            Vector3 extends = Mesh.Vertices.Points[0];
            foreach (Vector3 v in Mesh.Vertices.Points)
            {
                if (v.X < org.X)
                    org.X = v.X;
                if (v.Y < org.Y)
                    org.Y = v.Y;
                if (v.Z < org.Z)
                    org.Z = v.Z;
                if (v.X > extends.X)
                    extends.X = v.X;
                if (v.Y > extends.Y)
                    extends.Y = v.Y;
                if (v.Z > extends.Z)
                    extends.Z = v.Z;
            }
            
            Mesh.Bounds.Box = (extends - org) * 0.5f;
            Mesh.Bounds.Origin = org + Mesh.Bounds.Box;
            Mesh.Bounds.R = (float)Math.Sqrt(sq(Mesh.Bounds.Box.X) + sq(Mesh.Bounds.Box.Y) + sq(Mesh.Bounds.Box.Z));
        }

        public float sq(float f)
        {
            return f * f;
        }

        public void SetSelection(bool Selected)
        {
            isSelected = Selected;
        }

        public bool GetSelection()
        {
            return isSelected;
        }

        #endregion

        #region Export
        
        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("#" + index + " : Static Mesh");
            if (Mesh.Bounds.t != null)
            {
                res.Nodes.Add(Mesh.Bounds.t);
                res.Nodes.Add(Mesh.kDOPTree.t);
                res.Nodes.Add(Mesh.RawTris.t);
                res.Nodes.Add(Mesh.Mat.t);
                res.Nodes.Add(Mesh.Vertices.t);
                res.Nodes.Add(Mesh.Buffers.t);
                res.Nodes.Add(Mesh.Edges.t);
                res.Nodes.Add(Mesh.UnknownPart.t);
                res.Nodes.Add(Mesh.IdxBuf.t);
                res.Nodes.Add(Mesh.End.t);
            }
            return res;
        }

        public TreeNode ToTreeShort()
        {
            TreeNode res = new TreeNode("#" + index + " : Static Mesh");
            return res;
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

        public byte GetMaterial(int index)
        {
            byte b = 0;
            if (Mesh.Mat.MatInst != null)
                for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
                    if (index >= Mesh.Mat.Lods[0].Sections[i].FirstIdx1 &&
                        index < Mesh.Mat.Lods[0].Sections[i].FirstIdx1 + Mesh.Mat.Lods[0].Sections[i].NumFaces1 * 3)
                        b = (byte)i;
            return b;
        }

        public void ExportToPsk(string path)
        {
            PSKFile p = ExportToPsk();
            p.Export(path);
        }

        public PSKFile ExportToPsk()
        {
            PSKFile psk = new PSKFile();
            PSKFile.PSKContainer pskc = new PSKFile.PSKContainer();
            pskc.points = new List<PSKFile.PSKPoint>();
            foreach (Vector3 v in Mesh.Vertices.Points)
                pskc.points.Add(new PSKFile.PSKPoint(v));
            pskc.edges = new List<PSKFile.PSKEdge>();
            for (int i = 0; i < Mesh.Edges.UVSet.Count(); i++)
            {
                UVSet s = Mesh.Edges.UVSet[i];
                pskc.edges.Add(new PSKFile.PSKEdge((short)i, s.UVs[0], GetMaterial(i)));
            }
            pskc.faces = new List<PSKFile.PSKFace>();
            if (Mesh.IdxBuf.Indexes != null && Mesh.IdxBuf.Indexes.Count() != 0)
            {
                for (int i = 0; i < Mesh.IdxBuf.Indexes.Count() / 3; i++)
                {
                    int v0 = Mesh.IdxBuf.Indexes[i * 3];
                    int v1 = Mesh.IdxBuf.Indexes[i * 3 + 1];
                    int v2 = Mesh.IdxBuf.Indexes[i * 3 + 2];
                    byte material = GetMaterial(i * 3);
                    pskc.faces.Add(new PSKFile.PSKFace(v0, v1, v2, material));
                    PSKFile.PSKEdge e = pskc.edges[v0];
                    e.material = material;
                    pskc.edges[v0] = e;
                    e = pskc.edges[v1];
                    e.material = material;
                    pskc.edges[v1] = e;
                    e = pskc.edges[v2];
                    e.material = material;
                    pskc.edges[v2] = e;
                }
            }

            {
                for (int i = 0; i < Mesh.RawTris.RawTriangles.Count(); i++)
                {
                    RawTriangle r = Mesh.RawTris.RawTriangles[i];
                    byte material = GetMaterial(i * 3);
                    pskc.faces.Add(new PSKFile.PSKFace(r.v0, r.v1, r.v2, material));
                    PSKFile.PSKEdge e = pskc.edges[r.v0];
                    e.material = material;
                    pskc.edges[r.v0] = e;
                    e = pskc.edges[r.v1];
                    e.material = material;
                    pskc.edges[r.v1] = e;
                    e = pskc.edges[r.v2];
                    e.material = material;
                    pskc.edges[r.v2] = e;
                }
            }
            pskc.materials = new List<PSKFile.PSKMaterial>();
            foreach (Section s in Mesh.Mat.Lods[0].Sections)
                pskc.materials.Add(new PSKFile.PSKMaterial(pcc.getObjectName(s.Name), 0));
            pskc.bones = new List<PSKFile.PSKBone>();
            pskc.weights = new List<PSKFile.PSKWeight>();
            psk = new PSKFile();
            psk.psk = pskc;
            return psk;
        }

        public void Export3DS(Lib3dsFile f, Matrix m)
        {
            try
            {
                PSKFile p = ExportToPsk();
                Helper3DS.AddMeshTo3DS(f, p, m);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Export to 3ds ERROR: in\"" + MyName + "\" " + e.Message);
            }
        }

        #endregion

        #region Import

        public void ImportFromPsk(string path)
        {
            psk = new PSKFile();
            psk.ImportPSK(path);
#region Vertices
            Mesh.Vertices = new Verts();
            Mesh.Vertices.Points = new List<Vector3>();
            for (int i = 0; i < psk.psk.edges.Count; i++)
            {
                int idx = psk.psk.edges[i].index;
                float valueX = psk.psk.points[idx].x;
                float valueY = psk.psk.points[idx].y;
                float valueZ = psk.psk.points[idx].z;
                Mesh.Vertices.Points.Add(new Vector3(valueX, valueY, valueZ));

            }
            Mesh.Buffers.IndexBuffer = Mesh.Vertices.Points.Count();
            byte[] buff = BitConverter.GetBytes((int)Mesh.Vertices.Points.Count());
            for (int i = 0; i < 4; i++)
                Mesh.UnknownPart.data[24 + i] = buff[i];
            #endregion
#region materials
            Lod l = Mesh.Mat.Lods[0];
            l.Sections = new List<Section>();
            for (int i = 0; i < psk.psk.faces.Count; i++)
            {
                PSKFile.PSKFace e = psk.psk.faces[i];
                int mat = e.material;
                
                if (mat >= l.Sections.Count())
                {
                    int min = i * 3;
                    int minv = e.v0;
                    if (e.v1 < minv)
                        minv = e.v1;
                    if (e.v2 < minv)
                        minv = e.v2;
                    int maxv = e.v0;
                    if (e.v1 > maxv)
                        maxv = e.v1;
                    if (e.v2 > maxv)
                        maxv = e.v2;
                    Section s = new Section();
                    s.FirstIdx1 = min;
                    s.FirstIdx2 = min;
                    s.NumFaces1 = 1;
                    s.NumFaces2 = 1;
                    s.MatEff1 = minv;
                    s.MatEff2 = maxv;
                    s.Unk1 = 1;
                    s.Unk2 = 1;
                    s.Unk3 = 1;
                    s.Unk4 = 0;
                    s.Unk5 = 1;
                    s.Unk6 = 0;
                    l.Sections.Add(s);
                }
                else
                {
                    Section s = l.Sections[mat];
                    int min = s.FirstIdx1 / 3;
                    int max = s.NumFaces1;
                    int minv = s.MatEff1;
                    if (e.v1 < minv)
                        minv = e.v1;
                    if (e.v2 < minv)
                        minv = e.v2;
                    int maxv = s.MatEff2;
                    if (e.v1 > maxv)
                        maxv = e.v1;
                    if (e.v2 > maxv)
                        maxv = e.v2;
                    if (i - s.FirstIdx1 / 3  + 1> max)
                        max = i - s.FirstIdx1 / 3 + 1;
                    if (i < min)
                        min = i;
                    s.FirstIdx1 = min * 3;
                    s.FirstIdx2 = min * 3;
                    s.NumFaces1 = max;
                    s.NumFaces2 = max;
                    s.MatEff1 = minv;
                    s.MatEff2 = maxv;
                    l.Sections[mat] = s;
                }
            }
            l.SectionCount = l.Sections.Count();
            for (int i = 0; i < l.SectionCount; i++)
            {
                Select_Material selm = new Select_Material();
                selm.hasSelected = false;
                selm.listBox1.Items.Clear();
                selm.Objects = new List<int>();
                for(int j =0;j<pcc.Exports.Count;j++)
                {
                    PCCObject.ExportEntry e =pcc.Exports[j];
                    if (e.ClassName == "Material" || e.ClassName == "MaterialInstanceConstant")
                    {
                        selm.listBox1.Items.Add(j + "\t" + e.ClassName + " : " + e.ObjectName);
                        selm.Objects.Add(j);
                    }
                }
                selm.Show();
                while (selm != null && !selm.hasSelected)
                {
                    Application.DoEvents();
                }
                Section s = l.Sections[i];
                s.Name = selm.SelIndex + 1;
                l.Sections[i] = s;
                selm.Close();
            }
            l.NumVert = psk.psk.points.Count();
            Mesh.Mat.Lods[0] = l;
#endregion
#region Edges
            int oldcount = Mesh.Edges.UVSet[0].UVs.Count();
            Mesh.Buffers.UV1 = oldcount;
            Mesh.Buffers.UV2 = oldcount * 4 + 8;
            Mesh.Edges = new Edges();
            Mesh.Edges.UVSet = new List<UVSet>();
            for (int i = 0; i < psk.psk.edges.Count; i++)
            {
                UVSet newSet = new UVSet();
                newSet.UVs = new List<Vector2>();
                for (int j = 0; j < oldcount; j++)
                    newSet.UVs.Add(new Vector2(psk.psk.edges[i].U, psk.psk.edges[i].V));
                newSet.x1 = 0;
                newSet.x2 = 0;
                newSet.y1 = 0;
                newSet.y2 = 0;
                newSet.z1 = 0;
                newSet.z2 = 0;
                newSet.w1 = 0;
                newSet.w2 = 0;
                Mesh.Edges.UVSet.Add(newSet);
            }            
            Mesh.Edges.count = psk.psk.edges.Count;
            Mesh.Edges.size = 8 + 4 * oldcount;
#endregion
#region Faces
            Mesh.RawTris.RawTriangles = new List<RawTriangle>();
            bool WithIndex = (Mesh.IdxBuf.Indexes.Count != 0);
            if (WithIndex)
                Mesh.IdxBuf.Indexes = new List<ushort>();
            for (int i = 0; i < psk.psk.faces.Count; i++)
            {
                RawTriangle r = new RawTriangle();
                PSKFile.PSKFace f = psk.psk.faces[i];
                r.v0 = (Int16)f.v0;
                r.v1 = (Int16)f.v1;
                r.v2 = (Int16)f.v2;
                r.mat = f.material;
                Mesh.RawTris.RawTriangles.Add(r);
                if (WithIndex)
                {
                    Mesh.IdxBuf.Indexes.Add((UInt16)f.v0);
                    Mesh.IdxBuf.Indexes.Add((UInt16)f.v1);
                    Mesh.IdxBuf.Indexes.Add((UInt16)f.v2);
                    Mesh.IdxBuf.count = Mesh.IdxBuf.Indexes.Count();
                }               
            }
            CalcTangentSpace();
#endregion
            RecalculateBoundings();
            GenerateMesh();

        }

        #endregion

    }
}