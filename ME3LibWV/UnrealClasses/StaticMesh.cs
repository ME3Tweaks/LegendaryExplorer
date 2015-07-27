using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3LibWV.UnrealClasses
{
    public class StaticMesh: _DXRenderableObject
    {
        #region structs
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
        public MeshStruct Mesh;
        public List<kDOPNode> kdNodes;
        public List<RawTriangle> RawTriangles;
        public List<Vector3> Vertices;
        #endregion

        public Matrix ParentMatrix;
        public Matrix TempMatrix = Matrix.Identity;
        private int readerpos;

        public StaticMesh(PCCPackage Pcc, int index, Matrix transform)
        {
            pcc = Pcc;
            MyIndex = index;
            byte[] buff = pcc.GetObjectData(index);
            Props = PropertyReader.getPropList(pcc, buff);
            ParentMatrix = transform;
            int off = Props[Props.Count() - 1].offend;
            DeserializeDump(buff, off);
        }

        public void DeserializeDump(byte[] raw, int start)
        {
            Mesh = new MeshStruct();
            readerpos = start;
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
                DebugLog.PrintLn("StaticMesh #" + MyIndex + " Error: " + e.Message);
            }
        }
        public void ReadBoundings(byte[] memory)
        {
            Bounding b = new Bounding();
            b.Origin.X = BitConverter.ToSingle(memory, readerpos);
            b.Origin.Y = BitConverter.ToSingle(memory, readerpos + 4);
            b.Origin.Z = BitConverter.ToSingle(memory, readerpos + 8);
            b.Box.X = BitConverter.ToSingle(memory, readerpos + 12);
            b.Box.Y = BitConverter.ToSingle(memory, readerpos + 16);
            b.Box.Z = BitConverter.ToSingle(memory, readerpos + 20);
            b.R = BitConverter.ToSingle(memory, readerpos + 24);
            b.unk = new float[7];
            int pos = readerpos + 28;
            for (int i = 0; i < 7; i++)
            {
                b.unk[i] = BitConverter.ToSingle(memory, pos);
                pos += 4;
            }
            b.raw = new byte[56];
            for (int i = 0; i < 56; i++)
            {
                b.raw[i] = memory[readerpos];
                readerpos++;
            }
            Mesh.Bounds = b;
        }
        public void ReadkDOPTree(byte[] memory)
        {
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            kdNodes = new List<kDOPNode>();
            for (int i = 0; i < l.count; i++)
            {
                kDOPNode nd = new kDOPNode();
                nd.min = new Vector3(memory[readerpos] / 256f, memory[readerpos + 1] / 256f, memory[readerpos + 2] / 256f);
                nd.max = new Vector3(memory[readerpos + 3] / 256f, memory[readerpos + 4] / 256f, memory[readerpos + 5] / 256f);
                kdNodes.Add(nd);
                for (int j = 0; j < l.size; j++)
                {
                    readerpos++;
                }
            }
            Mesh.kDOPTree = l;
        }
        public void ReadRawTris(byte[] memory)
        {
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
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
            }
            RawTris rt = new RawTris();
            rt.RawTriangles = RawTriangles;
            Mesh.RawTris = rt;
        }
        public void ReadMaterials(byte[] memory)
        {
            Materials m = new Materials();
            Mesh.InternalVersion = BitConverter.ToInt32(memory, readerpos);
            m.LodCount = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            m.Lods = new List<Lod>();
            for (int i = 0; i < m.LodCount; i++)
            {
                Lod l = new Lod();
                l.Guid = new byte[16];
                for (int j = 0; j < 16; j++)
                {
                    l.Guid[j] = memory[readerpos];
                    readerpos++;
                }
                l.SectionCount = BitConverter.ToInt32(memory, readerpos);
                l.Sections = new List<Section>();
                readerpos += 4;
                for (int j = 0; j < l.SectionCount; j++)
                {
                    Section s = new Section();
                    s.Unk1 = BitConverter.ToInt32(memory, readerpos + 4);
                    s.Unk2 = BitConverter.ToInt32(memory, readerpos + 8);
                    s.Unk3 = BitConverter.ToInt32(memory, readerpos + 12);
                    s.FirstIdx1 = BitConverter.ToInt32(memory, readerpos + 16);
                    s.NumFaces1 = BitConverter.ToInt32(memory, readerpos + 20);
                    s.MatEff1 = BitConverter.ToInt32(memory, readerpos + 24);
                    s.MatEff2 = BitConverter.ToInt32(memory, readerpos + 28);
                    s.Unk4 = BitConverter.ToInt32(memory, readerpos + 32);
                    s.Unk5 = BitConverter.ToInt32(memory, readerpos + 36);
                    if (s.Unk5 == 1)
                    {
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 40);
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 44);
                        s.Unk6 = memory[readerpos + 48];
                    }
                    else
                    {
                        s.Unk6 = memory[readerpos + 40];
                        s.FirstIdx2 = BitConverter.ToInt32(memory, readerpos + 41);
                        s.NumFaces2 = BitConverter.ToInt32(memory, readerpos + 45);
                    }
                    readerpos += 49;
                    l.Sections.Add(s);
                }
                l.SizeVert = BitConverter.ToInt32(memory, readerpos);
                l.NumVert = BitConverter.ToInt32(memory, readerpos + 4);
                l.LodCount = BitConverter.ToInt32(memory, readerpos + 8);
                if (l.Sections[0].Unk5 == 1)
                    readerpos += 12;
                else
                    readerpos += 4;
                m.Lods.Add(l);
            }
            Mesh.Mat = m;
        }
        public void ReadVerts(byte[] memory)
        {
            UnknownList l = new UnknownList();
            l.size = BitConverter.ToInt32(memory, readerpos);
            l.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            int len = l.size * l.count;
            l.data = new byte[len];
            for (int i = 0; i < len; i++)
                l.data[i] = memory[readerpos + i];
            Vertices = new List<Vector3>();
            for (int i = 0; i < l.count; i++)
            {
                float f1 = BitConverter.ToSingle(memory, readerpos);
                float f2 = BitConverter.ToSingle(memory, readerpos + 4);
                float f3 = BitConverter.ToSingle(memory, readerpos + 8);
                Vertices.Add(new Vector3(f1, f2, f3));
                readerpos += l.size;
            }
            Verts v = new Verts();
            v.Points = Vertices;
            Mesh.Vertices = v;
        }
        public void ReadBuffers(byte[] memory)
        {
            Buffers X = new Buffers();
            X.Wireframe1 = new byte[4];
            byte[] buffer = new byte[20];
            int[] output = new int[3];
            for (int i = 0; i < 20; i++)
            {
                buffer[i] = memory[readerpos];
                readerpos += 1;
            }
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
            Mesh.Buffers = X;
        }
        public void ReadEdges(byte[] memory)
        {
            UnknownList edges = new UnknownList(); 
            Edges e = new Edges();
            edges.size = BitConverter.ToInt32(memory, readerpos);
            edges.count = BitConverter.ToInt32(memory, readerpos + 4);
            e.size = edges.size;
            e.count = edges.count;
            readerpos += 8;
            int len = edges.size * edges.count;
            edges.data = new byte[len];
            int datacounter = 0;
            e.UVSet = new List<UVSet>();
            for (int i = 0; i < edges.count; i++)
            {
                UVSet uv = new UVSet();
                uv.UVs = new List<Vector2>();
                uv.x1 = memory[readerpos];
                uv.y1 = memory[readerpos + 1];
                uv.z1 = memory[readerpos + 2];
                uv.w1 = memory[readerpos + 3];
                uv.x2 = memory[readerpos + 4];
                uv.y2 = memory[readerpos + 5];
                uv.z2 = memory[readerpos + 6];
                uv.w2 = memory[readerpos + 7];
                readerpos += 8;

                for (int row = 0; row < (edges.size - 8) / 4; row++)
                {
                    float u = DXHelper.HalfToFloat(BitConverter.ToUInt16(memory, readerpos));
                    float v = DXHelper.HalfToFloat(BitConverter.ToUInt16(memory, readerpos + 2));
                    uv.UVs.Add(new Vector2(u, v));
                    edges.data[datacounter] = memory[readerpos];
                    readerpos += 4;
                    datacounter += 1;
                }
                e.UVSet.Add(uv);
            }
            Mesh.Edges = e;
        }
        public void UnknownPart(byte[] memory)
        {
           UnknownP p = new UnknownP();
            p.data = new byte[28];
            for (int i = 0; i < 28; i++)
            {
                p.data[i] = memory[readerpos];
                readerpos++;
            }
            Mesh.UnknownPart = p;
        }
        public void ReadIndexBuffer(byte[] memory)
        {
            IndexBuffer idx = new IndexBuffer();
            idx.Indexes = new List<UInt16>();
            idx.size = BitConverter.ToInt32(memory, readerpos);
            idx.count = BitConverter.ToInt32(memory, readerpos + 4);
            readerpos += 8;
            for (int count = 0; count < idx.count; count++)
            {
                UInt16 v = BitConverter.ToUInt16(memory, readerpos);
                idx.Indexes.Add(v);
                readerpos += 2;
            }
            Mesh.IdxBuf = idx;
        }
        public void ReadEnd(byte[] memory)
        {
            EndOfStruct endChunk = new EndOfStruct();
            endChunk.data = new byte[memory.Length - readerpos];
            for (int i = 0; i < 68; i++)
                endChunk.data[i] = memory[readerpos + i];
            readerpos += 68;
            Mesh.End = endChunk;
        }


        public override void Render(Device device)
        {
            device.VertexFormat = CustomVertex.PositionNormalTextured.Format;
            device.RenderState.Lighting = true;
            device.RenderState.FillMode = FillMode.Solid;
            device.Transform.World = ParentMatrix * TempMatrix;
            device.RenderState.CullMode = Cull.None;
            try
            {
                for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
                {
                    Section sec = Mesh.Mat.Lods[0].Sections[i];
                    if(!Selected )
                        device.SetTexture(0, DXHelper.DefaultTex);
                    else
                        device.SetTexture(0, DXHelper.SelectTex);
                    if (sec.RawTriangles == null)
                    {
                        Mesh.Mat.Lods[0].Sections[i] = MakeMesh(sec);
                    }
                    if (sec.RawTriangles != null && sec.RawTriangles.Length != 0)
                        device.DrawUserPrimitives(PrimitiveType.TriangleList, sec.RawTriangles.Length / 3, sec.RawTriangles);
                }
            }
            catch (Exception e)
            {
                DebugLog.PrintLn("Static Mesh #" + MyIndex + " ERROR: " + e.Message);
            }
        }

        private Section MakeMesh(Section sec)
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
                DebugLog.PrintLn("Static Mesh #" + MyIndex + " error on reading dx mesh: " + e.Message);
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
            if (sec.RawTriangles.Length != 0)
                DXHelper.cam = Vector3.TransformCoordinate(sec.RawTriangles[0].Position, ParentMatrix);
            return sec;
        }
        public override TreeNode ToTree()
        {
            TreeNode t = new TreeNode("E#" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1));
            t.Name = (MyIndex + 1).ToString();
            return t;
        }

        public override void SetSelection(bool s)
        {
            Selected = s;
        }

        public override float Process3DClick(Vector3 org, Vector3 dir, float max)
        {
            float dist = max;
            for (int i = 0; i < Mesh.Mat.Lods[0].SectionCount; i++)
            {
                Section sec = Mesh.Mat.Lods[0].Sections[i];
                float d = -1f;
                if (sec.RawTriangles != null)
                    for (int j = 0; j < sec.RawTriangles.Length / 3; j++)
                        if (DXHelper.RayIntersectTriangle(org,
                                                dir,
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3].Position, ParentMatrix * TempMatrix),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 1].Position, ParentMatrix * TempMatrix),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 2].Position, ParentMatrix * TempMatrix),
                                                out d))
                        {
                            if ((d < dist && d > 0) || (dist == -1f && d > 0))
                                dist = d;
                        }
                        else
                            if (DXHelper.RayIntersectTriangle(org,
                                                dir,
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3].Position, ParentMatrix * TempMatrix),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 2].Position, ParentMatrix * TempMatrix),
                                                Vector3.TransformCoordinate(sec.RawTriangles[j * 3 + 1].Position, ParentMatrix * TempMatrix),
                                                out d))
                                if ((d < dist && d > 0) || (dist == -1f && d > 0))
                                    dist = d;
            }
            if (dist != -1 && (dist < max || max == -1))
            {
                DXHelper.level.DeSelectAll();
                SetSelection(true);
            }
            return dist;
        }
    }
}
