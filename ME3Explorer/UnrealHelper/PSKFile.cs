using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using System.Text;

namespace ME3Explorer.UnrealHelper
{
    public class PSKFile
    {

#region Decleration
        public struct PSKHeader
        {
            public string name;
            public int size;
            public int count;
        }

        public struct PSKObject
        {
            public Vector[] Points;
            public Edge[] Edges;
            public Face[] Faces;
            public Material[] Mats;
            public PSKBone[] Bones;
            public Weight[] Weights;
        }

        public struct Quad
        {
            public float w;
            public float x;
            public float y;
            public float z;
        }

        public struct Vector
        {
            public float x;
            public float y;
            public float z;
        }

        public struct Edge
        {
            public int index;
            public float U;
            public float V;
            public int mat;
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

        public struct Material
        {
            public string name;
            public int texid;
            public int flags;
            public int matid;
            public int matflags;
            public int LODBias;
            public int LODStyle;
        }

        public struct PSKBone
        {
            public string name;
            public int flags;
            public int childcount;
            public int parent;
            public Quad orientation;
            public Vector position;
            public float length;
            public Vector size;
            public int index;
        }

        public struct Weight
        {
            public float w;
            public int pointid;
            public int boneid;
        }

        public PSKObject PSK;
        public int bonecount;
#endregion

        public PSKFile()
        {
        }

        public PSKFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            PSK = new PSKObject();
            ReadPSK(fileStream);
            fileStream.Close();
        }

#region PSKImport
        private void ReadPSK(FileStream fileStream)
        {
            while (fileStream.Position < fileStream.Length)
            {
                PSKHeader PSKh = PSKFGetHeader(fileStream);
                switch (PSKh.name)
                {
                    case "ACTRHEAD":
                        continue;
                    case "PNTS0000":
                        PSKFReadPoints(fileStream, PSKh);
                        break;
                    case "VTXW0000":
                        PSKFReadEdges(fileStream, PSKh);
                        break;
                    case "FACE0000":
                        PSKFReadFaces(fileStream, PSKh);
                        break;
                    case "MATT0000":
                        PSKFReadMats(fileStream, PSKh);
                        break;
                    case "REFSKELT":
                        PSKFReadBones(fileStream, PSKh);
                        break;
                    case "RAWWEIGHTS":
                        PSKFReadWeights(fileStream, PSKh);
                        break;
                    default:
                        fileStream.Position = fileStream.Length;
                        break;
                }
            }
        }

        private PSKHeader PSKFGetHeader(FileStream fs)
        {
            PSKHeader ret = new PSKHeader();
            ret.name = "";
            byte b;
            for (int i = 0; i < 20; i++)
            {
                b = (byte)fs.ReadByte();
                if (b != 0) ret.name += (char)b;
            }
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            fs.Read(buff, 0, 4);
            ret.size = BitConverter.ToInt32(buff, 0);
            fs.Read(buff, 0, 4);
            ret.count = BitConverter.ToInt32(buff, 0);
            return ret;
        }

        private void PSKFReadPoints(FileStream fs, PSKHeader PSKh)
        {
            PSK.Points = new Vector[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Points[i].x = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Points[i].y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Points[i].z = BitConverter.ToSingle(buff, 0);
            }
        }

        private void PSKFReadEdges(FileStream fs, PSKHeader PSKh)
        {
            PSK.Edges = new Edge[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                byte[] buff = new byte[2];
                fs.Read(buff, 0, 2);
                PSK.Edges[i].index = BitConverter.ToInt16(buff, 0);
                fs.Read(buff, 0, 2);
                buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Edges[i].U = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Edges[i].V = BitConverter.ToSingle(buff, 0);
                PSK.Edges[i].mat = fs.ReadByte();
                fs.ReadByte();
                fs.ReadByte();
                fs.ReadByte();
            }
        }

        private void PSKFReadFaces(FileStream fs, PSKHeader PSKh)
        {
            PSK.Faces = new Face[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                byte[] buff = new byte[2];
                fs.Read(buff, 0, 2);
                PSK.Faces[i].e0 = BitConverter.ToUInt16(buff, 0);
                fs.Read(buff, 0, 2);
                PSK.Faces[i].e1 = BitConverter.ToUInt16(buff, 0);
                fs.Read(buff, 0, 2);
                PSK.Faces[i].e2 = BitConverter.ToUInt16(buff, 0);
                PSK.Faces[i].mat = (byte)fs.ReadByte();
                fs.ReadByte();
                buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Faces[i].smooth = BitConverter.ToInt32(buff, 0);
            }
        }

        private void PSKFReadMats(FileStream fs, PSKHeader PSKh)
        {
            PSK.Mats = new Material[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                PSK.Mats[i].name = "";
                byte b;
                for (int j = 0; j < 64; j++)
                {
                    b = (byte)fs.ReadByte();
                    if (b != 0) PSK.Mats[i].name += (char)b;
                }
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Mats[i].texid = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Mats[i].flags = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Mats[i].matid = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Mats[i].matflags = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Mats[i].LODBias = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Mats[i].LODStyle = BitConverter.ToInt32(buff, 0);
            }
        }

        private void PSKFReadBones(FileStream fs, PSKHeader PSKh)
        {
            PSK.Bones = new PSKBone[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                PSK.Bones[i].name = "";
                byte b;
                for (int j = 0; j < 64; j++)
                {
                    b = (byte)fs.ReadByte();
                    if (b != 0) PSK.Bones[i].name += (char)b;
                }
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Bones[i].flags = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].childcount = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].parent = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].orientation.x = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].orientation.y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].orientation.z = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].orientation.w = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].position.x = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].position.y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].position.z = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].length = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].size.x = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].size.y = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Bones[i].size.z = BitConverter.ToSingle(buff, 0);
            }
        }

        private void PSKFReadWeights(FileStream fs, PSKHeader PSKh)
        {
            PSK.Weights = new Weight[PSKh.count];
            for (int i = 0; i < PSKh.count; i++)
            {
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                PSK.Weights[i].w = BitConverter.ToSingle(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Weights[i].pointid = BitConverter.ToInt32(buff, 0);
                fs.Read(buff, 0, 4);
                PSK.Weights[i].boneid = BitConverter.ToInt32(buff, 0);
            }
        }
#endregion

#region Export
        public TreeNode PSKtoTreeView()
        {
            TreeNode ret = new TreeNode("PSKFile");
            TreeNode t = new TreeNode("ACTRHEAD");
            ret.Nodes.Add(t);
            t = PSKPoints2Node();
            ret.Nodes.Add(t);
            t = PSKEdges2Node();
            ret.Nodes.Add(t);
            t = PSKFaces2Node();
            ret.Nodes.Add(t);
            t = PSKMats2Node();
            ret.Nodes.Add(t);
            t = PSKBones2Node();
            ret.Nodes.Add(t);
            t = PSKWeights2Node();
            ret.Nodes.Add(t);
            return ret;
        }

        private TreeNode PSKPoints2Node()
        {
            TreeNode t = new TreeNode("PNTS0000");
            for (int i = 0; i < PSK.Points.Length; i++)
                t.Nodes.Add("Point " + i.ToString() +
                            " X=" + PSK.Points[i].x.ToString() +
                            " Y=" + PSK.Points[i].y.ToString() +
                            " Z=" + PSK.Points[i].z.ToString());
            return t;
        }

        private TreeNode PSKEdges2Node()
        {
            TreeNode t = new TreeNode("VTXW0000");
            for (int i = 0; i < PSK.Edges.Length; i++)
                t.Nodes.Add("Edge " + i.ToString() +
                            " PId=" + PSK.Edges[i].index.ToString() +
                            " U=" + PSK.Edges[i].U.ToString() +
                            " V=" + PSK.Edges[i].V.ToString());
            return t;
        }

        private TreeNode PSKFaces2Node()
        {
            TreeNode t = new TreeNode("FACE0000");
            for (int i = 0; i < PSK.Faces.Length; i++)
                t.Nodes.Add("Face " + i.ToString() +
                            " PId1=" + PSK.Faces[i].e0.ToString() +
                            " PId2=" + PSK.Faces[i].e1.ToString() +
                            " PId3=" + PSK.Faces[i].e2.ToString() +
                            " MatId=" + PSK.Faces[i].mat.ToString() +
                            " Smooth=" + PSK.Faces[i].smooth.ToString());
            return t;
        }

        private TreeNode PSKMats2Node()
        {
            TreeNode t = new TreeNode("MATT0000");
            for (int i = 0; i < PSK.Mats.Length; i++)
                t.Nodes.Add("Material " + i.ToString() +
                            " Name=" + PSK.Mats[i].name +
                            " Id=" + PSK.Mats[i].texid.ToString() +
                            " Flags=" + PSK.Mats[i].flags.ToString() +
                            " MatId=" + PSK.Mats[i].matid.ToString() +
                            " MatFlags=" + PSK.Mats[i].matflags.ToString() +
                            " LODBias=" + PSK.Mats[i].LODBias.ToString() +
                            " LODStyle=" + PSK.Mats[i].LODStyle.ToString());
            return t;
        }

        private TreeNode PSKBones2Node()
        {
            TreeNode t = new TreeNode("REFSKELT");
            for (int i = 0; i < PSK.Bones.Length; i++)
                t.Nodes.Add("Bone " + i.ToString() +
                            " Name=" + PSK.Bones[i].name +
                            " Childs=" + PSK.Bones[i].childcount.ToString() +
                            " Parent=" + PSK.Bones[i].parent.ToString() +
                            " Orientation=(X:" + PSK.Bones[i].orientation.x.ToString() +
                            " Y:" + PSK.Bones[i].orientation.y.ToString() +
                            " Z:" + PSK.Bones[i].orientation.z.ToString() +
                            " W:" + PSK.Bones[i].orientation.w.ToString() +
                            ") Postion=(X:" + PSK.Bones[i].position.x.ToString() +
                            " Y:" + PSK.Bones[i].position.y.ToString() +
                            " Z:" + PSK.Bones[i].position.z.ToString() +
                            ") Length=" + PSK.Bones[i].length.ToString() +
                            " Flags=" + PSK.Bones[i].flags);
            return t;
        }

        private TreeNode PSKWeights2Node()
        {
            TreeNode t = new TreeNode("RAWWEIGHTS");
            for (int i = 0; i < PSK.Weights.Length; i++)
                t.Nodes.Add("Weight " + i.ToString() +
                            " w=" + PSK.Weights[i].w.ToString() +
                            " PointId=" + PSK.Weights[i].pointid +
                            " BoneID=" + PSK.Weights[i].boneid);
            return t;
        }

        public void SaveToFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            PSKWriteHeader(fileStream);
            PSKWritePoints(fileStream);
            PSKWriteEdges(fileStream);
            PSKWriteFaces(fileStream);
            PSKWriteMaterial(fileStream);
            PSKWriteBones(fileStream);
            PSKWriteWeights(fileStream);
            fileStream.Close();
        }

        private byte[] PSKChunkHeader(string id, int type, int size, int count)
        {
            byte[] h = new byte[32];
            for (int i = 0; i < 32 && i < id.Length; i++)
                h[i] = (byte)id[i];
            byte[] buff = BitConverter.GetBytes(type);
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

        private void PSKWriteHeader(FileStream fs)
        {
            fs.Write(PSKChunkHeader("ACTRHEAD", 0x1E83B9, 0, 0), 0, 32);
        }

        private void PSKWritePoints(FileStream fs)
        {
            int count = PSK.Points.Length;
            fs.Write(PSKChunkHeader("PNTS0000", 0x1E83B9, 0xC, count), 0, 32);
            for (int i = 0; i < count; i++)
            {
                Vector t = PSK.Points[i];
                byte[] buff = BitConverter.GetBytes(t.x);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(t.y);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(t.z);
                fs.Write(buff, 0, 4);
            }
        }

        private void PSKWriteEdges(FileStream fs)
        {
            int count = PSK.Edges.Length;
            fs.Write(PSKChunkHeader("VTXW0000", 0x1E83B9, 0x10, count), 0, 32);
            for (int i = 0; i < count; i++)
            {
                Edge t = PSK.Edges[i];
                byte[] buff = new byte[2];
                buff = BitConverter.GetBytes((UInt16)t.index);
                fs.Write(buff, 0, 2);
                buff = BitConverter.GetBytes((Int16)0);
                fs.Write(buff, 0, 2);
                buff = BitConverter.GetBytes(t.U);
                fs.Write(buff, 0, 4);
                buff = BitConverter.GetBytes(t.V);
                fs.Write(buff, 0, 4);
                fs.WriteByte((byte)0);
                fs.WriteByte((byte)0);
                buff = BitConverter.GetBytes((Int16)0);
                fs.Write(buff, 0, 2);
            }
        }

        private void PSKWriteFaces(FileStream fs)
        {
            int count = PSK.Faces.Length;
            fs.Write(PSKChunkHeader("FACE0000", 0x1E83B9, 0xC, count), 0, 32);
            for (int i = 0; i < count; i++)
            {
                Face f = PSK.Faces[i];
                byte[] buff = BitConverter.GetBytes(f.e0);
                fs.Write(buff, 0, 2);
                buff = BitConverter.GetBytes(f.e1);
                fs.Write(buff, 0, 2);
                buff = BitConverter.GetBytes(f.e2);
                fs.Write(buff, 0, 2);
                fs.WriteByte((byte)0);
                fs.WriteByte((byte)0);
                buff = BitConverter.GetBytes((Int32)0);
                fs.Write(buff, 0, 4);
            }
        }

        private void PSKWriteMaterial(FileStream fs)
        {
            int count = PSK.Mats.Length;
            fs.Write(PSKChunkHeader("MATT0000", 0x1E83B9, 0x58, count), 0, 32);
            for (int i = 0; i < count; i++)
            {
                string name = PSK.Mats[i].name;
                byte[] h = new byte[64];
                for (int j = 0; j < 64 && j < name.Length; j++)
                    h[j] = (byte)name[j];
                fs.Write(h, 0, 64);
                h = BitConverter.GetBytes(i);
                fs.Write(h, 0, 4);
                h = BitConverter.GetBytes(PSK.Mats[i].flags);
                fs.Write(h, 0, 4);
                h = BitConverter.GetBytes(PSK.Mats[i].matid);
                fs.Write(h, 0, 4);
                h = BitConverter.GetBytes(PSK.Mats[i].matflags);
                fs.Write(h, 0, 4);
                h = BitConverter.GetBytes(PSK.Mats[i].LODBias);
                fs.Write(h, 0, 4);
                h = BitConverter.GetBytes(PSK.Mats[i].LODStyle);
                fs.Write(h, 0, 4);
            }
        }

        private TreeNode GetChild(TreeNode tn)
        {
            TreeNode ret = tn;
            int n = Convert.ToInt32(tn.Text);
            for (int i = 0; i < PSK.Bones.Length; i++)
                if (i != n && PSK.Bones[i].parent == n)
                {
                    TreeNode t = new TreeNode(i.ToString());
                    t = GetChild(t);
                    ret.Nodes.Add(t);
                }
            return ret;
        }

        private Quad RecalcOrientation(Quad q)
        {
            Quad t = q;
            t.y *= -1;
            return t;
        }

        private void WriteBone(FileStream fs, TreeNode t, int index)
        {
            int bn = Convert.ToInt32(t.Text);
            string name = PSK.Bones[bn].name;
            byte[] h = new byte[64];
            for (int j = 0; j < 64 && j < name.Length; j++)
                h[j] = (byte)name[j];
            fs.Write(h, 0, 64);
            h = new byte[4];
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(PSK.Bones[bn].childcount);
            fs.Write(h, 0, 4);
            int p = 0;
            if (t.Parent != null)
                p = Convert.ToInt32(t.Parent.Text);
            if (bn != 0)
                h = BitConverter.GetBytes(PSK.Bones[p].index);
            else
                h = BitConverter.GetBytes((Int32)0);
            fs.Write(h, 0, 4);
            PSKBone bon = PSK.Bones[bn];
            bon.index = index;
            PSK.Bones[bn] = bon;
            if (bn != 0)
                bon.orientation.y *= -1;
            else
            {
                bon.orientation.y *= -1;
                bon.orientation.w *= -1;
            }
            h = BitConverter.GetBytes(bon.orientation.x);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.orientation.y);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.orientation.z);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.orientation.w);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.position.x);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.position.y * -1);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes(bon.position.z);
            fs.Write(h, 0, 4);
            h = BitConverter.GetBytes((Int32)0);
            fs.Write(h, 0, 4);
            fs.Write(h, 0, 4);
            fs.Write(h, 0, 4);
            fs.Write(h, 0, 4);
            bonecount++;
        }

        private void WriteBone(FileStream fs, TreeNode t)
        {
            int bn = Convert.ToInt32(t.Text);
            WriteBone(fs, t, bonecount);
            for (int i = 0; i < t.Nodes.Count; i++)
                WriteBone(fs, t.Nodes[i]);
        }

        private void PSKWriteBones(FileStream fs)
        {
            int count = PSK.Bones.Length;
            fs.Write(PSKChunkHeader("REFSKELT", 0x1E83B9, 0x78, count), 0, 32);
            if (count == 0)
                return;
            TreeNode skel = new TreeNode("0");
            skel = GetChild(skel);
            bonecount = 0;
            WriteBone(fs, skel);
        }

        private void PSKWriteWeights(FileStream fs)
        {

                int count = PSK.Weights.Length;
                fs.Write(PSKChunkHeader("RAWWEIGHTS", 0x1E83B9, 0xC, count), 0, 32);
                for (int i = 0; i < count; i++)
                {
                    float w = PSK.Weights[i].w;
                    int b = PSK.Weights[i].boneid;
                    w /= 255f;
                    byte[] buff = BitConverter.GetBytes(w);
                    fs.Write(buff, 0, 4);
                    buff = BitConverter.GetBytes(i);
                    fs.Write(buff, 0, 4);
                    buff = BitConverter.GetBytes(b);
                    fs.Write(buff, 0, 4);
                }
        }
#endregion

    }
}
