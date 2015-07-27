using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace UDKExplorer.UDK.Classes
{
    public class StaticMesh
    {
        public struct BoundingStruct
        {
            public Vector3 origin;
            public Vector3 size;
            public float r;

            public void Serialize(SerializingContainer con)
            {
                origin = con + origin;
                size = con + size;
                r = con + r;
            }
        }

        public struct Face
        {
            public ushort v0;
            public ushort v1;
            public ushort v2;
            public ushort mat;

            public void Serialize(SerializingContainer con)
            {
                v0 = con + v0;
                v1 = con + v1;
                v2 = con + v2;
                mat = con + mat;
            }
        }
           
        public BoundingStruct Bounding;
        public int BodySetup;
        public Vector3 Min;
        public Vector3 Max;
        public List<byte[]> kDOPTree;
        public Face[] RawTriangles;
        public int InternalVersion;

        public UDKObject Owner;
        public int MyIndex;
        private int ReadEnd;

        public StaticMesh(UDKObject udk, int Index)
        {
            MyIndex = Index;
            Owner = udk;
            int start = GetPropertyEnd(Index);
            byte[] buff = new byte[udk.Exports[Index].data.Length - start];
            for (int i = 0; i < udk.Exports[Index].data.Length - start; i++)
                buff[i] = udk.Exports[Index].data[i + start];
            MemoryStream m = new MemoryStream(buff);
            SerializingContainer Container = new SerializingContainer(m);
            Container.isLoading = true;
            Serialize(Container);
        }

        public void Serialize(SerializingContainer Container)
        {
            if (Container.isLoading)
                Bounding = new BoundingStruct();
            Bounding.Serialize(Container);
            BodySetup = Container + BodySetup;
            SerializeMinMax(Container);
            SerializekDOPTree(Container);
            SerializeRawTriangles(Container);
            InternalVersion = Container + InternalVersion;
            ReadEnd = Container.GetPos();
        }

        private void SerializeMinMax(SerializingContainer Container)
        {
            Min = Container + Min;
            Max = Container + Max;
        }

        private void SerializekDOPTree(SerializingContainer Container)
        {
            int size = 0;
            int count = 0;
            if (!Container.isLoading)
            {
                size = kDOPTree[0].Length;
                count = kDOPTree.Count;
            }
            size = Container + size;
            count = Container + count;
            if (Container.isLoading)
                kDOPTree = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                if (Container.isLoading)
                    kDOPTree.Add(new byte[size]);
                for (int j = 0; j < size; j++)
                {
                    byte[] temp = kDOPTree[i];
                    temp[j] = Container + temp[j];
                    kDOPTree[i] = temp;
                }
            }
        }

        private void SerializeRawTriangles(SerializingContainer Container)
        {
            int size = 8;
            int count = 0;
            if (!Container.isLoading)
                count = RawTriangles.Length;
            size = Container + size;
            count = Container + count;
            if (Container.isLoading)
                RawTriangles = new Face[count];
            for (int i = 0; i < RawTriangles.Length; i++)
                RawTriangles[i].Serialize(Container);
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("Skeletal Mesh");
            res.Nodes.Add(GetFlags(MyIndex));
            res.Nodes.Add(GetProperties(MyIndex));
            res.Nodes.Add(BoundingsToTree());
            res.Nodes.Add("BodySetup : #" + BodySetup + " " + Owner.GetClass(BodySetup));
            res.Nodes.Add(MinMaxToTree());
            res.ExpandAll();
            res.Nodes.Add(kDOPtreeToTree());
            res.Nodes.Add(RawTrianglesToTree());
            res.Nodes.Add("Internal Version : #" + InternalVersion);
            res.Nodes.Add("Read End With Properties @0x" + (ReadEnd + GetPropertyEnd(MyIndex)).ToString("X8"));
            res.Nodes.Add("Read End Binary @0x" + ReadEnd.ToString("X8"));
            return res;
        }

        private int GetPropertyEnd(int n)
        {
            BitConverter.IsLittleEndian = true;
            int pos = 0x00;
            try
            {

                int test = BitConverter.ToInt32(Owner.Exports[n].data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((Owner.Exports[n].flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(Owner.Exports[n].data, pos);
                    if (Owner.GetName(idxname) == "None" || Owner.GetName(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(Owner.Exports[n].data, pos + 8);
                    int size = BitConverter.ToInt32(Owner.Exports[n].data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (Owner.GetName(idxtype) == "StructProperty")
                        size += 8;
                    if (Owner.GetName(idxtype) == "ByteProperty")
                        size += 8;
                    pos += 24 + size;
                    if (pos > Owner.Exports[n].data.Length)
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
            TreeNode res = new TreeNode("Flags 0x" + Owner.Exports[n].flags.ToString("X8"));
            foreach (string row in UnrealFlags.flagdesc)//0x02000000
            {
                string[] t = row.Split(',');
                long l = long.Parse(t[1].Trim(), System.Globalization.NumberStyles.HexNumber);
                l = l >> 32;
                if ((l & Owner.Exports[n].flags) != 0)
                    res.Nodes.Add(t[0].Trim());
            }
            return res;
        }

        private TreeNode GetProperties(int n)
        {
            TreeNode res = new TreeNode("Properties");
            BitConverter.IsLittleEndian = true;
            int pos = 0x00;
            try
            {

                int test = BitConverter.ToInt32(Owner.Exports[n].data, 8);
                if (test == 0)
                    pos = 0x04;
                else
                    pos = 0x08;
                if ((Owner.Exports[n].flags & 0x02000000) != 0)
                    pos = 0x1A;
                while (true)
                {
                    int idxname = BitConverter.ToInt32(Owner.Exports[n].data, pos);
                    if (Owner.GetName(idxname) == "None" || Owner.GetName(idxname) == "")
                        break;
                    int idxtype = BitConverter.ToInt32(Owner.Exports[n].data, pos + 8);
                    int size = BitConverter.ToInt32(Owner.Exports[n].data, pos + 16);
                    if (size == 0)
                        size = 1;   //boolean fix
                    if (Owner.GetName(idxtype) == "StructProperty")
                        size += 8;
                    if (Owner.GetName(idxtype) == "ByteProperty")
                        size += 8;
                    string s = pos.ToString("X8") + " " + Owner.GetName(idxname) + " (" + Owner.GetName(idxtype) + ") : ";
                    switch (Owner.GetName(idxtype))
                    {
                        case "ObjectProperty":
                        case "IntProperty":
                            int val = BitConverter.ToInt32(Owner.Exports[n].data, pos + 24);
                            s += val.ToString();
                            break;
                        case "NameProperty":
                        case "StructProperty":
                            int name = BitConverter.ToInt32(Owner.Exports[n].data, pos + 24);
                            s += Owner.GetName(name);
                            break;
                        case "FloatProperty":
                            float f = BitConverter.ToSingle(Owner.Exports[n].data, pos + 24);
                            s += f.ToString();
                            break;
                        case "BoolProperty":
                            s += (Owner.Exports[n].data[pos + 24] == 1).ToString();
                            break;
                        case "StrProperty":
                            int len = BitConverter.ToInt32(Owner.Exports[n].data, pos + 24);
                            for (int i = 0; i < len - 1; i++)
                                s += (char)Owner.Exports[n].data[pos + 28 + i];
                            break;
                    }
                    res.Nodes.Add(s);
                    pos += 24 + size;
                    if (pos > Owner.Exports[n].data.Length)
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
        
        private TreeNode MinMaxToTree()
        {
            TreeNode res = new TreeNode("Min/Max");
            res.Nodes.Add("Min : X(" + Min.X + ") Y(" + Min.Y + ") Z(" + Min.Z + ")");
            res.Nodes.Add("Max : X(" + Max.X + ") Y(" + Max.Y + ") Z(" + Max.Z + ")");
            return res;
        }

        private TreeNode kDOPtreeToTree() //lulz
        {
            TreeNode result = new TreeNode("kDOP-Tree");
            for (int i = 0; i < kDOPTree.Count; i++)
            {
                string s = i.ToString("d8") + " : ";
                foreach (byte b in kDOPTree[i])
                    s += b.ToString("X2") + " ";
                result.Nodes.Add(s);
            }
            return result;
        }

        private TreeNode RawTrianglesToTree()
        {
            TreeNode result = new TreeNode("Raw Triangles");
            for (int i = 0; i <  RawTriangles.Length; i++)
            {
                Face f = RawTriangles[i];
                string s = i.ToString("d8") + " : ";
                s += " V0(" + f.v0 + ") ";
                s += " V1(" + f.v1 + ") ";
                s += " V2(" + f.v2 + ") ";
                s += " Material(" + f.mat + ") ";
                result.Nodes.Add(s);
            }
            return result;
        }
    }
}
