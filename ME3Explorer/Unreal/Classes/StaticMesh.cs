using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Media;
using ME3Explorer.Unreal;
using SharpDX;
using lib3ds.Net;
using ME3Explorer.Debugging;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using System.Globalization;

namespace ME3Explorer.Unreal.Classes
{
    public class StaticMesh
    {
        public int aDebuggingPosition => Export != null ? (readerpos + Export.propsEnd()) : 0;
        public byte[] memory;
        public int memsize;
        public int readerpos;
        public PSKFile psk;
        public ExportEntry Export;

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
            public List<ushort> Indexes;
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
        }

        public struct Bounding
        {
            public Vector3 Origin;
            public Vector3 Box;
            public float R;
            public int RB_BodySetup;
            public float[] MinMaxFloats;
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
            public short v0;
            public short v1;
            public short v2;
            public short mat;
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
        public bool isVolumetric = false; //draw debug meshes as wireframe
        public bool isSelected = false;
        #endregion

        public StaticMesh()
        {
        }

        public StaticMesh(ExportEntry export)
        {
            this.Export = export;
            if (Export.ObjectName.ToLower().Contains("volumetric") || Export.ObjectName.ToLower().Contains("spheremesh"))
                isVolumetric = true;
            memory = Export.Data;
            memsize = memory.Length;
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
                Debug.WriteLine($"Boundings at 0x{aDebuggingPosition:X8}");
                ReadBoundings(raw);

                // KDOP===============
                Debug.WriteLine($"kDOP at 0x{aDebuggingPosition:X8}");

                ReadkDOPTree(raw);
                Debug.WriteLine($"RawTris at 0x{aDebuggingPosition:X8}");
                ReadRawTris(raw);

                //END KDOP------------


                //LOD MODELS (only reads LOD 0) ------
                Debug.WriteLine($"Materials at 0x{aDebuggingPosition:X8}");
                ReadMaterials(raw);
                Debug.WriteLine($"Verts at 0x{aDebuggingPosition:X8}");
                ReadVerts(raw);
                Debug.WriteLine($"Buffers at 0x{aDebuggingPosition:X8}");
                ReadBuffers(raw);
                Debug.WriteLine($"Edges at 0x{aDebuggingPosition:X8}");
                ReadEdges(raw);
                Debug.WriteLine($"Unknown at 0x{aDebuggingPosition:X8}");
                UnknownPart(raw);
                Debug.WriteLine($"IndexBuffer at 0x{aDebuggingPosition:X8}");
                ReadIndexBuffer(raw);
                Debug.WriteLine($"End at 0x{aDebuggingPosition:X8}");
                ReadEnd(raw);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"StaticMesh Error: {e.Message}");
            }
        }

        public void ReadBoundings(byte[] memory)
        {
            TreeNode res = new TreeNode($"Bounding pos: 0x{readerpos:X4}");
            Bounding b = new Bounding();
            b.Origin = new Vector3
            {
                X = BitConverter.ToSingle(memory, readerpos),
                Y = BitConverter.ToSingle(memory, readerpos + 4),
                Z = BitConverter.ToSingle(memory, readerpos + 8)
            };
            b.Box = new Vector3
            {
                X = BitConverter.ToSingle(memory, readerpos + 12),
                Y = BitConverter.ToSingle(memory, readerpos + 16),
                Z = BitConverter.ToSingle(memory, readerpos + 20)
            };
            b.R = BitConverter.ToSingle(memory, readerpos + 24);
            b.RB_BodySetup = BitConverter.ToInt32(memory, readerpos + 28);
            b.MinMaxFloats = new float[6];

            string minMaxStr = "Min/Max {";
            if (Export.Game == MEGame.ME3)
            {
                int pos = readerpos + 32;
                for (int i = 0; i < 6; i++)
                {
                    b.MinMaxFloats[i] = BitConverter.ToSingle(memory, pos);
                    minMaxStr += $"{b.MinMaxFloats[i]} ";
                    pos += 4;
                }

                minMaxStr += "}";
            }

            int rawSize = Export.Game == MEGame.ME3 ? 56 : 32;
            b.raw = new byte[rawSize];
            Buffer.BlockCopy(memory, readerpos, b.raw, 0, rawSize);
            readerpos += rawSize;

            res.Nodes.Add(new TreeNode($"Origin: {{{b.Origin.X} ; {b.Origin.Y} ; {b.Origin.Z}}}"));
            res.Nodes.Add(new TreeNode($"Box: {{{b.Box.X} ; {b.Box.Y} ; {b.Box.Z}}}"));
            res.Nodes.Add(new TreeNode($"Radius: {{{b.R}}}"));
            res.Nodes.Add(new TreeNode($"RB_BodySetup: {{{Export.FileRef.getEntry(b.RB_BodySetup)?.GetFullPath ?? "None"}}}"));
            if (Export.Game == MEGame.ME3)
                res.Nodes.Add(minMaxStr);
            b.t = res;
            Mesh.Bounds = b;
        }

        public void ReadkDOPTree(byte[] memory)
        {
            TreeNode res = new TreeNode($"kDOP-tree pos: 0x{readerpos:X4}");
            UnknownList l = new UnknownList
            {
                size = BitConverter.ToInt32(memory, readerpos),
                count = BitConverter.ToInt32(memory, readerpos + 4)
            };
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode($"Size : {l.size}"));
            res.Nodes.Add(new TreeNode("Count : " + l.count));
            TreeNode t = new TreeNode("Data");
            kdNodes = new List<kDOPNode>();
            for (int i = 0; i < l.count; i++)
            {
                kDOPNode nd = new kDOPNode
                {
                    min = new Vector3(memory[readerpos] / 256f,
                                      memory[readerpos + 1] / 256f,
                                      memory[readerpos + 2] / 256f),
                    max = new Vector3(memory[readerpos + 3] / 256f,
                                      memory[readerpos + 4] / 256f,
                                      memory[readerpos + 5] / 256f)
                };
                kdNodes.Add(nd);
                string s = "";
                for (int j = 0; j < l.size; j++)
                {
                    s += $"{memory[readerpos] / 256f} ";
                    readerpos++;
                }
                t.Nodes.Add(new TreeNode($"#{i:D4} : {s}"));
            }
            //ReadkdNodes(Meshplorer.Preview3D.Cubes[0], memory);
            res.Nodes.Add(t);
            l.t = res;
            Mesh.kDOPTree = l;
        }
        /*
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
        }*/

        public void ReadRawTris(byte[] memory)
        {
            TreeNode res = new TreeNode($"Raw triangles pos: 0x{readerpos:X4}");
            UnknownList l = new UnknownList
            {
                size = BitConverter.ToInt32(memory, readerpos),
                count = BitConverter.ToInt32(memory, readerpos + 4)
            };
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode($"Size : {l.size}"));
            res.Nodes.Add(new TreeNode($"Count : {l.count}"));
            TreeNode t = new TreeNode("Data");
            RawTriangles = new List<RawTriangle>();
            for (int i = 0; i < l.count; i++)
            {
                RawTriangle r = new RawTriangle
                {
                    v0 = BitConverter.ToInt16(memory, readerpos),
                    v1 = BitConverter.ToInt16(memory, readerpos + 2),
                    v2 = BitConverter.ToInt16(memory, readerpos + 4),
                    mat = BitConverter.ToInt16(memory, readerpos + 6)
                };
                RawTriangles.Add(r);
                string s = "";
                for (int j = 0; j < l.size; j++)
                {
                    s += $"{memory[readerpos]:X2} ";
                    readerpos++;
                }
                t.Nodes.Add(new TreeNode($"#{i:D4} : {s}"));
            }
            res.Nodes.Add(t);
            Mesh.RawTris = new RawTris { RawTriangles = RawTriangles, t = res };
        }

        public void ReadMaterials(byte[] memory)
        {
            TreeNode res = new TreeNode($"Materials pos: 0x{readerpos:X4}");
            Mesh.InternalVersion = BitConverter.ToInt32(memory, readerpos);
            Materials m = new Materials
            {
                LodCount = BitConverter.ToInt32(memory, readerpos + 4),
                Lods = new List<Lod>(),
                MatInst = new List<MaterialInstanceConstant>()
            };
            res.Nodes.Add($"LodCount : {m.LodCount}");
            TreeNode t1 = new TreeNode("Lods");
            readerpos += 8;
            for (int i = 0; i < m.LodCount; i++)
            {
                //THIS IS NOT A GUID - THIS IS BULK DATA FLAGS
                Lod l = new Lod { Guid = new byte[16] };
                string t = "Guid: ";
                for (int j = 0; j < 16; j++)
                {
                    l.Guid[j] = memory[readerpos];
                    t += $"{l.Guid[j]:X2} ";
                    readerpos++;
                }


                t1.Nodes.Add(new TreeNode(t));
                l.SectionCount = BitConverter.ToInt32(memory, readerpos);
                t1.Nodes.Add(new TreeNode($"Section Count : {l.SectionCount}"));
                l.Sections = new List<Section>();
                readerpos += 4;
                TreeNode t2 = new TreeNode("Sections");
                for (int j = 0; j < l.SectionCount; j++)
                {
                    Section s = new Section();
                    string q = $"Section [{j}] : {{Name = ";
                    s.Name = BitConverter.ToInt32(memory, readerpos);
                    q += s.Name;
                    if (s.Name > 0)
                    {
                        m.MatInst.Add(new MaterialInstanceConstant(Export.FileRef.getUExport(s.Name)));
                        q += $"(\'{Export.FileRef.getObjectName(s.Name)}\'), ";
                    }

                    s.Unk1 = BitConverter.ToInt32(memory, readerpos + 4);
                    q += $"Unk1 = {s.Unk1}, ";
                    s.Unk2 = BitConverter.ToInt32(memory, readerpos + 8);
                    q += $"Unk2 = {s.Unk2}, ";
                    s.Unk3 = BitConverter.ToInt32(memory, readerpos + 12);
                    q += $"Unk3 = {s.Unk3}, ";
                    s.FirstIdx1 = BitConverter.ToInt32(memory, readerpos + 16);
                    q += $"FirstIdx1 = {s.FirstIdx1}, ";
                    s.NumFaces1 = BitConverter.ToInt32(memory, readerpos + 20);
                    q += $"NumFaces1 = {s.NumFaces1}, ";
                    s.MatEff1 = BitConverter.ToInt32(memory, readerpos + 24);
                    q += $"MatEff1 = {s.MatEff1}, ";
                    s.MatEff2 = BitConverter.ToInt32(memory, readerpos + 28);
                    q += $"MatEff2 = {s.MatEff2}, ";
                    s.Unk4 = BitConverter.ToInt32(memory, readerpos + 32);
                    q += $"Unk4 = {s.Unk4}, ";
                    s.Unk5 = BitConverter.ToInt32(memory, readerpos + 36);
                    q += $"Unk5 = {s.Unk5}, ";
                    if (s.Unk5 == 1)
                    {
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 40);
                        q += $"FirstIdx2 = {s.FirstIdx2}, ";
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 44);
                        q += $"NumFaces2 = {s.NumFaces2}, ";
                        s.Unk6 = memory[readerpos + 48];
                        q += $"Unk6 = {s.Unk6}}}";
                    }
                    else
                    {
                        s.Unk6 = memory[readerpos + 40];
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 41);
                        q += $"FirstIdx2 = {s.FirstIdx2}, ";
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 45);
                        q += $"NumFaces2 = {s.NumFaces2}, ";
                        q += "Unk6 = " + s.Unk6 + "}";
                    }
                    t2.Nodes.Add(q);
                    readerpos += 49;
                    l.Sections.Add(s);
                }
                t1.Nodes.Add(t2);
                l.SizeVert = BitConverter.ToInt32(memory, readerpos);
                t1.Nodes.Add(new TreeNode($"Size Verts : {l.SizeVert}"));
                l.NumVert = BitConverter.ToInt32(memory, readerpos + 4);
                t1.Nodes.Add(new TreeNode($"Num Vert : {l.NumVert}"));
                l.LodCount = BitConverter.ToInt32(memory, readerpos + 8);
                t1.Nodes.Add(new TreeNode($"Lod Count : {l.LodCount}"));
                if (l.Sections[0].Unk5 == 1)
                    readerpos += 12;
                else
                    readerpos += 4;
                m.Lods.Add(l);
            }
            res.Nodes.Add(t1);
            TreeNode t3 = new TreeNode("Materials");
            foreach (MaterialInstanceConstant mic in m.MatInst)
                t3.Nodes.Add(mic.ToTree());
            res.Nodes.Add(t3);
            m.t = res;
            Mesh.Mat = m;
        }

        public void ReadVerts(byte[] memory)
        {
            TreeNode res = new TreeNode($"Vertices pos: 0x{readerpos:X4}");
            UnknownList l = new UnknownList
            {
                size = BitConverter.ToInt32(memory, readerpos),
                count = BitConverter.ToInt32(memory, readerpos + 4)
            };
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            res.Nodes.Add(new TreeNode($"Size : {l.size}"));
            res.Nodes.Add(new TreeNode($"Count : {l.count}"));
            TreeNode t = new TreeNode("Data");
            Vertices = new List<Vector3>();
            for (int i = 0; i < l.count; i++)
            {
                float f1 = BitConverter.ToSingle(memory, readerpos);
                float f2 = BitConverter.ToSingle(memory, readerpos + 4);
                float f3 = BitConverter.ToSingle(memory, readerpos + 8);
                Vertices.Add(new Vector3(f1, f2, f3));
                string s = $"{f1} {f2} {f3}";
                readerpos += l.size;
                t.Nodes.Add(new TreeNode($"#{i:D4} : {s}"));
            }
            res.Nodes.Add(t);
            Mesh.Vertices = new Verts { Points = Vertices, t = res };
        }

        public void ReadBuffers(byte[] memory)
        {
            Buffers X = new Buffers { Wireframe1 = new byte[4] };

            var res = new TreeNode($"Buffers (?), Position: 0x{readerpos:X4}");
            var buffer = new byte[20];
            var output = new int[3];
            for (int i = 0; i < 20; i++)
            {
                buffer[i] = memory[readerpos];
                readerpos += 1;
            }
            // Here using struct Buffers
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
            }
            X.Wireframe2 = new byte[4];
            counter = 0;
            for (int i = 16; i < 20; i++)
            {
                X.Wireframe2[counter] = buffer[i];
                counter += 1;
            }
            // Here displaying all data
            res.Nodes.Add(new TreeNode($"UV Buffer 1 [INT32]: {output[0]}"));
            res.Nodes.Add(new TreeNode($"UV Buffer 2 [INT32]: {output[1]}"));
            res.Nodes.Add(new TreeNode($"Index Buffer [INT32]: {output[2]}"));
            res.Nodes.Add(new TreeNode($"Wireframe buffer 1[four bytes] : {buffer[12]:X4} {buffer[14]:X4}"));
            res.Nodes.Add(new TreeNode($"Wireframe buffer 2 [four bytes] : {buffer[16]:X4} {buffer[18]:X4}"));

            Mesh.Buffers = X;
            Mesh.Buffers.t = res;

        }

        public string packedNorm(int off)
        {
            string s = "(x ";
            s += $"{memory[off] / 256f:0.0000}" + " ; y ";
            s += $"{memory[off + 1] / 256f:0.0000}" + " ; z ";
            s += $"{memory[off + 2] / 256f:0.0000}" + " ; w ";
            s += $"{memory[off + 3] / 256f:0.0000}" + ")";
            return s;
        }

        public void ReadEdges(byte[] memory)
        {
            TreeNode res = new TreeNode($"Edges list, start: 0x{readerpos:X4}");
            UnknownList edges = new UnknownList //here using struct unknown list, later we're just filling up data array byte by byte
            {
                size = BitConverter.ToInt32(memory, readerpos),
                count = BitConverter.ToInt32(memory, readerpos + 4)
            };
            Edges e = new Edges { size = edges.size, count = edges.count };
            //quick'n'dirty fix above, need work! <--------------
            readerpos += 8;
            int len = edges.size * edges.count;
            edges.data = new byte[len];
            res.Nodes.Add(new TreeNode($"Size : {edges.size}"));
            res.Nodes.Add(new TreeNode($"Count : {edges.count}"));
            TreeNode data = new TreeNode("Data");
            int datacounter = 0;

            e.UVSet = new List<UVSet>();
            for (int i = 0; i < edges.count; i++)
            {
                UVSet uv = new UVSet
                {
                    UVs = new List<Vector2>(),

                    //here adding packed normals
                    x1 = memory[readerpos],
                    y1 = memory[readerpos + 1],
                    z1 = memory[readerpos + 2],
                    w1 = memory[readerpos + 3],
                    x2 = memory[readerpos + 4],
                    y2 = memory[readerpos + 5],
                    z2 = memory[readerpos + 6],
                    w2 = memory[readerpos + 7]
                };

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
                data.Nodes.Add(new TreeNode($"{i:d4}: "));
            }

            res.Nodes.Add(data);
            e.t = res;
            Mesh.Edges = e;
        }

        public void UnknownPart(byte[] memory)
        {
            TreeNode res = new TreeNode($"Unknown 28 bytes, start: 0x{readerpos:X4}");
            TreeNode unknowndata = new TreeNode("Data");
            UnknownP p = new UnknownP
            {
                data = new byte[28]
            };

            int tempReader = readerpos;

            for (int i = 0; i < 28; i++)
            { //don't mind me, just savin' some data here.
                p.data[i] = memory[tempReader];
                tempReader++;
            }


            string s = "";
            for (int i = 0; i < 8; i++)
            {
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            unknowndata.Nodes.Add($"Block 1 [8]: {s}");

            s = "";
            for (int i = 0; i < 8; i++)
            {
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add($"Block 2 [8]: {s}");

            s = "";
            for (int i = 0; i < 8; i++)
            {
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add($"Block 3 [8]: {s}");

            s = "";
            for (int i = 0; i < 4; i++)
            {
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;

            }
            unknowndata.Nodes.Add($"Block 4 [4]: {s}");

            res.Nodes.Add(unknowndata);

            p.t = res;
            Mesh.UnknownPart = p;
        }

        public void ReadIndexBuffer(byte[] memory)
        {
            TreeNode res = new TreeNode($"Index data, start: 0x{readerpos:X4}");
            IndexBuffer idx = new IndexBuffer
            {
                Indexes = new List<ushort>(),
                size = BitConverter.ToInt32(memory, readerpos),
                count = BitConverter.ToInt32(memory, readerpos + 4)
            };
            readerpos += 8;
            res.Nodes.Add(new TreeNode($"Size :{idx.size}"));
            res.Nodes.Add(new TreeNode($"Count :{idx.count}"));
            TreeNode data = new TreeNode();

            for (int count = 0; count < idx.count; count++)
            {
                ushort v = BitConverter.ToUInt16(memory, readerpos);
                idx.Indexes.Add(v);
                string s = $"{v:X2}";
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

            EndOfStruct endChunk = new EndOfStruct { data = new byte[memory.Length - readerpos] };

            TreeNode res = new TreeNode($"Last chunk, 0x{readerpos:X4}");

            TreeNode End = new TreeNode("Data");
            string s = ""; //here start some unknown chunks

            // unknown 8 bytes, probably integer
            for (int i = 0; i < 8; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 1 [6]: {s}");


            // same, 8 byte int
            s = "";
            for (int i = 8; i < 16; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 2 [8]: {s}");

            // 4 bytes, always zero
            s = "";
            for (int i = 16; i < 20; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 3 [4]: " + s);

            //4 bytes, always 0x1
            s = "";
            for (int i = 20; i < 24; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 4 [4]: {s}");

            //then some 8 bytes of unknown data
            s = "";
            for (int i = 24; i < 32; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 5 [8]: {s}");

            // 6 bytes, always zero
            s = "";
            for (int i = 32; i < 38; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 6 [6]: {s}");

            //4 bytes data
            s = "";
            for (int i = 38; i < 42; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 7 [4]: {s}");

            //6 bytes zeroes
            s = "";
            for (int i = 42; i < 48; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 8 [6]: {s}");


            //4 bytes zeroes
            s = "";
            for (int i = 48; i < 52; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add("Unknown 9 [4]: " + s);

            //and finally 16 bytes of some data
            s = "";
            for (int i = 52; i < 68; i++)
            {
                endChunk.data[i] = memory[readerpos];
                s += $"{memory[readerpos]:X2} ";
                readerpos += 1;
            }
            End.Nodes.Add($"Unknown 10 [16]: {s}");
            Mesh.End = endChunk;
            res.Nodes.Add(End);
            Mesh.End.t = End;

            #endregion
        }


        #endregion

        #region Serialize Data

        public void SerializeToFile(string path)
        {

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
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.RB_BodySetup), 0, 4);
            for (int i = 0; i < 6; i++)
                fs.Write(BitConverter.GetBytes(Mesh.Bounds.MinMaxFloats[i]), 0, 4);
        }

        public void Write_kDOP(FileStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.count), 0, 4);
            fs.Write(Mesh.kDOPTree.data, 0, Mesh.kDOPTree.size * Mesh.kDOPTree.count);


        }

        public void WriteTriangles(FileStream fs)
        {
            var size = new byte[4];
            size[0] = 8;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles.Count), 0, 4);

            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count; i++)
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
            var size = new byte[4];
            size[0] = 12;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points.Count), 0, 4);

            foreach (Vector3 v in Mesh.Vertices.Points)
            {
                fs.Write(BitConverter.GetBytes(v.X), 0, 4);
                fs.Write(BitConverter.GetBytes(v.Y), 0, 4);
                fs.Write(BitConverter.GetBytes(v.Z), 0, 4);
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

                foreach (Vector2 v in Mesh.Edges.UVSet[i].UVs)
                {

                    fs.Write(BitConverter.GetBytes(FloatToHalf(v.X)), 0, 2);
                    fs.Write(BitConverter.GetBytes(FloatToHalf(v.Y)), 0, 2);
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
            //This really needs optimized. This is disgusting
            int len = Export.propsEnd();
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
            fs.Write(BitConverter.GetBytes(Mesh.Bounds.RB_BodySetup), 0, 4);
            for (int i = 0; i < 6; i++)
                fs.Write(BitConverter.GetBytes(Mesh.Bounds.MinMaxFloats[i]), 0, 4);
        }

        public void Write_kDOP(MemoryStream fs)
        {
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.size), 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.kDOPTree.count), 0, 4);
            fs.Write(Mesh.kDOPTree.data, 0, Mesh.kDOPTree.size * Mesh.kDOPTree.count);


        }

        public void WriteTriangles(MemoryStream fs)
        {
            var size = new byte[4];
            size[0] = 8;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.RawTris.RawTriangles.Count), 0, 4);

            foreach (RawTriangle rawTri in Mesh.RawTris.RawTriangles)
            {
                fs.Write(BitConverter.GetBytes(rawTri.v0), 0, 2);
                fs.Write(BitConverter.GetBytes(rawTri.v1), 0, 2);
                fs.Write(BitConverter.GetBytes(rawTri.v2), 0, 2);
                fs.Write(BitConverter.GetBytes(rawTri.mat), 0, 2);
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
            var size = new byte[4];
            size[0] = 12;

            fs.Write(size, 0, 4);
            fs.Write(BitConverter.GetBytes(Mesh.Vertices.Points.Count), 0, 4);

            foreach (Vector3 v in Mesh.Vertices.Points)
            {
                fs.Write(BitConverter.GetBytes(v.X), 0, 4);
                fs.Write(BitConverter.GetBytes(v.Y), 0, 4);
                fs.Write(BitConverter.GetBytes(v.Z), 0, 4);
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

                foreach (Vector2 v in Mesh.Edges.UVSet[i].UVs)
                {

                    fs.Write(BitConverter.GetBytes(FloatToHalf(v.X)), 0, 2);
                    fs.Write(BitConverter.GetBytes(FloatToHalf(v.Y)), 0, 2);
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

        #region Helpers

        public void CalcTangentSpace()
        {
            int vertexCount = Mesh.Vertices.Points.Count;
            Vector3[] vertices = ToVec3(Mesh.Vertices.Points);
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] texcoords = new Vector2[vertexCount];
            foreach (RawTriangle raw in Mesh.RawTris.RawTriangles)
            {
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
            foreach (RawTriangle raw in Mesh.RawTris.RawTriangles)
            {
                normals[raw.v0].Normalize();
                normals[raw.v1].Normalize();
                normals[raw.v2].Normalize();
            }
            Vector4[] tangents = new Vector4[vertexCount];
            Vector4[] bitangents = new Vector4[vertexCount];
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count; i++)
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
                Vector3 tmp = t - n * Vector3.Dot(n, t);
                tmp.Normalize();
                tangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z, 0)
                {
                    W = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f
                };
            }
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = tan2[i];
                Vector3 tmp = (t - n * Vector3.Dot(n, t));
                tmp.Normalize();
                bitangents[i] = new Vector4(tmp.X, tmp.Y, tmp.Z, 0)
                {
                    W = (Vector3.Dot(Vector3.Cross(n, t), tan1[i]) < 0.0f) ? -1.0f : 1.0f
                };
            }
            for (int i = 0; i < Mesh.RawTris.RawTriangles.Count; i++)
            {
                RawTriangle raw = Mesh.RawTris.RawTriangles[i];
                ApplyTangents(raw.v0, tangents[raw.v0] * -1f, bitangents[raw.v0] * -1f);
                ApplyTangents(raw.v1, tangents[raw.v1] * -1f, bitangents[raw.v1] * -1f);
                ApplyTangents(raw.v2, tangents[raw.v2] * -1f, bitangents[raw.v2] * -1f);
            }
        }

        public float HalfToFloat(ushort val)
        {
            ushort u = val;
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            if (exp == 0)
            {
                return 0f;
            }
            if (exp == 31)
            {
                return 65504f;
            }
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }

        public ushort FloatToHalf(float f)
        {
            byte[] bytes = BitConverter.GetBytes((double)f);
            ulong bits = BitConverter.ToUInt64(bytes, 0);
            ulong exponent = bits & 0x7ff0000000000000L;
            ulong mantissa = bits & 0x000fffffffffffffL;
            ulong sign = bits & 0x8000000000000000L;
            int placement = (int)((exponent >> 52) - 1023);
            if (placement > 15 || placement < -14)
                return 0;
            ushort exponentBits = (ushort)((15 + placement) << 10);
            ushort mantissaBits = (ushort)(mantissa >> 42);
            ushort signBits = (ushort)(sign >> 48);
            return (ushort)(exponentBits | mantissaBits | signBits);
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

        public Vector3[] ToVec3(PSKFile.PSKPoint[] points)
        {
            Vector3[] v = new Vector3[points.Length];
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

        public int GetSectionMaterialName(int lod, int section)
        {
            return Mesh.Mat.Lods[lod].Sections[section].Name;
        }

        public void SetSectionMaterial(int lod, int section, int materialname)
        {
            Section s = Mesh.Mat.Lods[lod].Sections[section];
            int oldMaterialName = s.Name;
            s.Name = materialname;
            Mesh.Mat.Lods[lod].Sections[section] = s;
            // Load the material if it isn't already loaded
            bool found = Mesh.Mat.MatInst.Any(t => t.export.UIndex == materialname);
            if (!found)
            {
                // Load the material
                Mesh.Mat.MatInst.Add(new MaterialInstanceConstant(Export.FileRef.getUExport(materialname)));
            }
            // Remove the previously assigned MaterialInstanceConstant if is isn't used anymore.
            found = false;
            foreach (Section sec in Mesh.Mat.Lods[lod].Sections)
            {
                if (sec.Name == oldMaterialName)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                for (int i = 0; i < Mesh.Mat.MatInst.Count; i++)
                {
                    if (Mesh.Mat.MatInst[i].export.UIndex == oldMaterialName)
                    {
                        Mesh.Mat.MatInst.RemoveAt(i);
                    }
                }
            }
        }

        #endregion

        #region Export

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{Export.UIndex} : Static Mesh");
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
                if (Mesh.IdxBuf.t != null) res.Nodes.Add(Mesh.IdxBuf.t);
                if (Mesh.End.t != null) res.Nodes.Add(Mesh.End.t);
            }
            return res;
        }

        public byte[] Dump()
        {
            //todo: optimize this out
            return Export.getBinaryData();
        }

        public byte GetMaterial(int index)
        {
            byte b = 0;
            if (Mesh.Mat.MatInst != null)
            {
                for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
                {
                    if (index >= Mesh.Mat.Lods[0].Sections[i].FirstIdx1 &&
                        index < Mesh.Mat.Lods[0].Sections[i].FirstIdx1 + Mesh.Mat.Lods[0].Sections[i].NumFaces1 * 3)
                    {
                        b = (byte)i;
                    }
                }
            }

            return b;
        }

        public void ExportPSK(string path)
        {
            PSKFile p = BuildPSK();
            p.Export(path);
        }

        private PSKFile BuildPSK()
        {
            PSKFile.PSKContainer pskc = new PSKFile.PSKContainer
            {
                points = new List<PSKFile.PSKPoint>(),
                edges = new List<PSKFile.PSKEdge>(),
                faces = new List<PSKFile.PSKFace>(),
                materials = new List<PSKFile.PSKMaterial>(),
                bones = new List<PSKFile.PSKBone>(),
                weights = new List<PSKFile.PSKWeight>()
            };
            foreach (Vector3 v in Mesh.Vertices.Points)
                pskc.points.Add(new PSKFile.PSKPoint(v));
            for (int i = 0; i < Mesh.Edges.UVSet.Count; i++)
            {
                UVSet s = Mesh.Edges.UVSet[i];
                pskc.edges.Add(new PSKFile.PSKEdge((short)i, s.UVs[0], GetMaterial(i)));
            }
            if (Mesh.IdxBuf.Indexes != null && Mesh.IdxBuf.Indexes.Count != 0)
            {
                for (int i = 0; i < Mesh.IdxBuf.Indexes.Count / 3; i++)
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
                for (int i = 0; i < Mesh.RawTris.RawTriangles.Count; i++)
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
            foreach (Section s in Mesh.Mat.Lods[0].Sections)
                pskc.materials.Add(new PSKFile.PSKMaterial(Export.FileRef.getObjectName(s.Name), 0));
            return new PSKFile { psk = pskc };
        }

        public void Export3DS(Lib3dsFile f, Matrix m)
        {
            try
            {
                PSKFile p = BuildPSK();
                Helper3DS.AddMeshTo3DS(f, p, m);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn($"Export to 3ds ERROR: in\"{Export.GetFullPath}\" {e.Message}");
            }
        }

        public void ExportOBJ(string path)
        {
            using (StreamWriter writer = new StreamWriter(path))
            using (StreamWriter mtlWriter = new StreamWriter(Path.ChangeExtension(path, ".mtl")))
            {
                writer.WriteLine($"mtllib {Path.GetFileNameWithoutExtension(path)}.mtl");
                // Vertices
                List<Vector3> points = null;
                List<Vector2> uvs = null;
                var LODVertexOffsets = new Dictionary<int, int>(); // offset into the OBJ vertices that each buffer starts at
                points = Mesh.Vertices.Points;
                uvs = new List<Vector2>();
                for (int i = 0; i < points.Count; i++)
                {
                    uvs.Add(Mesh.Edges.UVSet[i].UVs[0]);
                }
                foreach (var mat in Mesh.Mat.MatInst)
                {
                    mtlWriter.WriteLine($"newmtl {mat.export.ObjectName}");
                }

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 v = points[i];
                    writer.WriteLine($"v {v.X.ToString(CultureInfo.InvariantCulture)} {v.Z.ToString(CultureInfo.InvariantCulture)} {v.Y.ToString(CultureInfo.InvariantCulture)}");
                    writer.WriteLine($"vt {uvs[i].X.ToString(CultureInfo.InvariantCulture)} {uvs[i].Y.ToString(CultureInfo.InvariantCulture)}");
                }

                // Triangles
                foreach (var lod in Mesh.Mat.Lods)
                {
                    foreach (var section in lod.Sections)
                    {
                        writer.WriteLine($"usemtl {Export.FileRef.getObjectName(section.Name)}");
                        writer.WriteLine($"g {Export.FileRef.getObjectName(section.Name)}");
                        if (Mesh.IdxBuf.Indexes != null && Mesh.IdxBuf.count > 0)
                        {
                            // Use the index buffer
                            for (int i = section.FirstIdx1; i < section.FirstIdx1 + section.NumFaces1 * 3; i += 3)
                            {
                                writer.WriteLine("f " + (Mesh.IdxBuf.Indexes[i] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (Mesh.IdxBuf.Indexes[i] + 1).ToString(CultureInfo.InvariantCulture) + " "
                                    + (Mesh.IdxBuf.Indexes[i + 1] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (Mesh.IdxBuf.Indexes[i + 1] + 1).ToString(CultureInfo.InvariantCulture) + " "
                                    + (Mesh.IdxBuf.Indexes[i + 2] + 1).ToString(CultureInfo.InvariantCulture) + "/" + (Mesh.IdxBuf.Indexes[i + 2] + 1).ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        else
                        {
                            // Ad-lib our own indices by assuming that every triangle is used exactly once in order
                            for (int i = section.FirstIdx1; i < section.FirstIdx1 + section.NumFaces1 * 3; i += 3)
                            {
                                writer.WriteLine("f " + (i + 1).ToString(CultureInfo.InvariantCulture) + "/" + (i + 1) + " "
                                    + (i + 2).ToString(CultureInfo.InvariantCulture) + "/" + (i + 2).ToString(CultureInfo.InvariantCulture) + " "
                                    + (i + 3).ToString(CultureInfo.InvariantCulture) + "/" + (i + 3).ToString(CultureInfo.InvariantCulture));
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Import

        public void ImportPSK(string path)
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
            Mesh.Buffers.IndexBuffer = Mesh.Vertices.Points.Count;
            byte[] buff = BitConverter.GetBytes(Mesh.Vertices.Points.Count);
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

                if (mat >= l.Sections.Count)
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
                    if (i - s.FirstIdx1 / 3 + 1 > max)
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
            l.SectionCount = l.Sections.Count;
            for (int i = 0; i < l.SectionCount; i++)
            {
                Select_Material selm = new Select_Material();
                selm.hasSelected = false;
                selm.listBox1.Items.Clear();
                selm.Objects = new List<int>();
                foreach (ExportEntry e in Export.FileRef.Exports)
                {
                    if (e.ClassName == "Material" || e.ClassName == "MaterialInstanceConstant")
                    {
                        selm.listBox1.Items.Add(e.UIndex + "\t" + e.ClassName + " : " + e.ObjectName);
                        selm.Objects.Add(e.UIndex);
                    }
                }

                selm.Show();

                //what is this?
                while (selm != null && !selm.hasSelected)
                {
                    Application.DoEvents();
                }
                Section s = l.Sections[i];
                s.Name = selm.SelIndex + 1;
                l.Sections[i] = s;
                selm.Close();
            }
            l.NumVert = psk.psk.points.Count;
            Mesh.Mat.Lods[0] = l;
            #endregion
            #region Edges
            int oldcount = Mesh.Edges.UVSet[0].UVs.Count;
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
                r.v0 = (short)f.v0;
                r.v1 = (short)f.v1;
                r.v2 = (short)f.v2;
                r.mat = f.material;
                Mesh.RawTris.RawTriangles.Add(r);
                if (WithIndex)
                {
                    Mesh.IdxBuf.Indexes.Add((ushort)f.v0);
                    Mesh.IdxBuf.Indexes.Add((ushort)f.v1);
                    Mesh.IdxBuf.Indexes.Add((ushort)f.v2);
                    Mesh.IdxBuf.count = Mesh.IdxBuf.Indexes.Count;
                }
            }
            CalcTangentSpace();
            #endregion
            RecalculateBoundings();

        }

        private class OBJTriangle
        {
            public int[] PositionIndices = new int[3];
            public int[] UVIndices = new int[3];
            public int[] NormalIndices = new int[3];
            public int MaterialIndex;
        }

        private class WeldedTriangle
        {
            public int[] VertexIndices = new int[3];
            public int MaterialIndex;
        }

        public void ImportOBJ(string path)
        {
            // Read OBJ data
            List<Vector3> positions = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<List<OBJTriangle>> sections = new List<List<OBJTriangle>>();
            List<string> materials = new List<string>();

            using (StreamReader reader = new StreamReader(path))
            {
                int currentMaterialIndex = 0;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine().Trim();

                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts[0] == "v")
                    {
                        // Read position (Note the axis flip)
                        positions.Add(new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[3], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture)));
                    }
                    else if (parts[0] == "vt")
                    {
                        // Read UV
                        uvs.Add(new Vector2(float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture)));
                    }
                    else if (parts[0] == "vn")
                    {
                        // Read normal
                        normals.Add(new Vector3(float.Parse(parts[1], CultureInfo.InvariantCulture),
                            float.Parse(parts[2], CultureInfo.InvariantCulture),
                            float.Parse(parts[3], CultureInfo.InvariantCulture)));
                    }
                    else if (parts[0] == "f")
                    {
                        // Read face
                        OBJTriangle triangle = new OBJTriangle();
                        for (int v = 0; v < 3; v++)
                        {
                            string[] components = parts[v + 1].Split('/');
                            if (components[0].Length > 0)
                            {
                                triangle.PositionIndices[v] = int.Parse(components[0]) - 1;
                            }
                            if (components.Length > 1 && components[1].Length > 0)
                            {
                                triangle.UVIndices[v] = int.Parse(components[1]) - 1;
                            }
                            if (components.Length > 2 && components[2].Length > 0)
                            {
                                triangle.NormalIndices[v] = int.Parse(components[2]) - 1;
                            }
                        }
                        triangle.MaterialIndex = currentMaterialIndex;
                        while (triangle.MaterialIndex >= sections.Count)
                            sections.Add(new List<OBJTriangle>());
                        sections[triangle.MaterialIndex].Add(triangle);
                    }
                    else if (parts[0] == "usemtl")
                    {
                        string matName = parts[1];

                        if (materials.Contains(matName))
                        {
                            currentMaterialIndex = materials.IndexOf(matName);
                        }
                        else
                        {
                            currentMaterialIndex = materials.Count;
                            materials.Add(matName);
                        }
                    }
                }
            }

            // We need to weld the list of positions to the list of uvs to produce one list of (position, uv) pairs, and get new indices to match.
            List<Tuple<Vector3, Vector2>> weldedVertices = new List<Tuple<Vector3, Vector2>>();
            List<List<WeldedTriangle>> weldedSections = new List<List<WeldedTriangle>>();

            foreach (List<OBJTriangle> section in sections)
            {
                List<WeldedTriangle> weldedSection = new List<WeldedTriangle>();
                foreach (OBJTriangle triangle in section)
                {
                    if (triangle.PositionIndices.Length != triangle.UVIndices.Length)
                        throw new FormatException("What the heck is even going on????");

                    WeldedTriangle weldedTriangle = new WeldedTriangle();
                    for (int i = 0; i < triangle.PositionIndices.Length; i++)
                    {
                        Tuple<Vector3, Vector2> vertex = new Tuple<Vector3, Vector2>(positions[triangle.PositionIndices[i]], uvs[triangle.UVIndices[i]]);
                        int vertexIndex = weldedVertices.IndexOf(vertex);
                        if (vertexIndex == -1)
                        {
                            vertexIndex = weldedVertices.Count;
                            weldedVertices.Add(vertex);
                        }
                        weldedTriangle.VertexIndices[i] = vertexIndex;
                    }
                    weldedTriangle.MaterialIndex = triangle.MaterialIndex;
                    weldedSection.Add(weldedTriangle);
                    if (section.IndexOf(triangle) % 100 == 0)
                        System.Diagnostics.Debug.WriteLine("Weld: " + section.IndexOf(triangle) + " out of " + section.Count);
                }
                weldedSections.Add(weldedSection);
            }

            // [ ] k-DOP Tree
            // [X] Raw Tris
            List<StaticMesh.RawTriangle> rawTriangles = new List<StaticMesh.RawTriangle>();
            foreach (List<WeldedTriangle> section in weldedSections)
            {
                foreach (WeldedTriangle t in section)
                {
                    rawTriangles.Add(new StaticMesh.RawTriangle() { mat = (short)t.MaterialIndex, v0 = (short)t.VertexIndices[0], v1 = (short)t.VertexIndices[1], v2 = (short)t.VertexIndices[2] });
                }
            }
            RawTriangles = rawTriangles;
            Mesh.RawTris = new StaticMesh.RawTris() { RawTriangles = rawTriangles, t = new TreeNode("New Raw Triangles!!!!") };
            // [X] Materials
            Lod l = Mesh.Mat.Lods[0];
            l.Sections = new List<Section>();
            int indexCount = 0;
            foreach (List<WeldedTriangle> section in weldedSections)
            {
                Section newSection = new Section();
                newSection.Name = 0; // Null material
                newSection.Unk1 = 1;
                newSection.Unk2 = 1;
                newSection.Unk3 = 1;
                newSection.Unk4 = weldedSections.IndexOf(section);
                newSection.Unk5 = 1;
                newSection.Unk6 = 0;
                newSection.FirstIdx1 = newSection.FirstIdx2 = indexCount;
                newSection.NumFaces1 = newSection.NumFaces2 = section.Count;

                int minPosIndex = section[0].VertexIndices[0];
                int maxPosIndex = section[0].VertexIndices[0];
                foreach (WeldedTriangle tri in section)
                {
                    foreach (int posIndex in tri.VertexIndices)
                    {
                        if (posIndex < minPosIndex)
                            minPosIndex = posIndex;
                        if (posIndex > maxPosIndex)
                            maxPosIndex = posIndex;
                    }
                }

                newSection.MatEff1 = minPosIndex;
                newSection.MatEff2 = maxPosIndex;
                l.Sections.Add(newSection);
                indexCount += section.Count * 3;
            }
            l.SectionCount = l.Sections.Count;


            // Crusty material selection code
            for (int i = 0; i < l.SectionCount; i++)
            {
                Select_Material selm = new Select_Material();
                selm.hasSelected = false;
                selm.listBox1.Items.Clear();
                selm.Objects = new List<int>();
                foreach (ExportEntry e in Export.FileRef.Exports)
                {
                    if (e.ClassName == "Material" || e.ClassName == "MaterialInstanceConstant")
                    {
                        selm.listBox1.Items.Add(e.UIndex + "\t" + e.ClassName + " : " + e.ObjectName);
                        selm.Objects.Add(e.UIndex);
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




            l.NumVert = weldedVertices.Count;
            Mesh.Mat.Lods[0] = l;
            // [X] Verts
            Mesh.Vertices = new Verts();
            Mesh.Vertices.Points = new List<Vector3>();
            foreach (Tuple<Vector3, Vector2> pos in weldedVertices)
            {
                Mesh.Vertices.Points.Add(pos.Item1);
            }
            Mesh.Buffers.IndexBuffer = Mesh.Vertices.Points.Count;
            byte[] countBytes = BitConverter.GetBytes(Mesh.Vertices.Points.Count);
            for (int i = 0; i < 4; i++)
                Mesh.UnknownPart.data[24 + i] = countBytes[i];
            // [ ] Buffers
            // [X] Edges
            int setCount = Mesh.Edges.UVSet[0].UVs.Count;
            Mesh.Buffers.UV1 = setCount;
            Mesh.Buffers.UV2 = setCount * 4 + 8;
            Mesh.Edges = new Edges();
            Mesh.Edges.UVSet = new List<UVSet>();
            for (int i = 0; i < weldedVertices.Count; i++)
            {
                UVSet newSet = new UVSet();
                newSet.UVs = new List<Vector2>();
                for (int set = 0; set < setCount; set++)
                {
                    newSet.UVs.Add(weldedVertices[i].Item2);
                }
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
            Mesh.Edges.count = weldedVertices.Count;
            Mesh.Edges.size = 8 + 4 * setCount;
            // [ ] Unknown Part?
            // [X] Index Buffer
            if (Mesh.IdxBuf.Indexes.Count > 0)
            {
                Mesh.IdxBuf.Indexes = new List<ushort>();

                foreach (List<WeldedTriangle> section in weldedSections)
                {
                    foreach (WeldedTriangle t in section)
                    {
                        Mesh.IdxBuf.Indexes.Add((ushort)t.VertexIndices[0]);
                        Mesh.IdxBuf.Indexes.Add((ushort)t.VertexIndices[1]);
                        Mesh.IdxBuf.Indexes.Add((ushort)t.VertexIndices[2]);
                    }
                }

                Mesh.IdxBuf.count = Mesh.IdxBuf.Indexes.Count;
                // TODO: Use 32-bit indices if triangles.Count * 3 > 0xFFFF
            }
            // [ ] End
            // [X] Boundings
            RecalculateBoundings();
            //     [ ] Unk
            CalcTangentSpace();

            System.Diagnostics.Debug.WriteLine("OBJ stats: input was " + positions.Count + " positions, " + uvs.Count + " uvs, welded became " + weldedVertices + " vertices.");
        }

        #endregion

    }
}