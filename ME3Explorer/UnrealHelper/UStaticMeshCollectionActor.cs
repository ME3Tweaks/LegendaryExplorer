using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class UStaticMeshCollectionActor
    {
        public byte[] memory;
        public int memsize;
        public string[] names;
        public string name;
        public List<UPropertyReader.Property> props;
        public UPropertyReader UPR;
        public PCCFile pcc;
        public List<int> Entries;
        public List<Matrix> Matrices;
        public ULevel.LevelProperties LP;
        public bool CHANGED = false;

        public UStaticMeshCollectionActor(byte[] mem, PCCFile Pcc)
        {
            memory = mem;
            memsize = mem.Length;
            names = Pcc.names;
            pcc = Pcc;
            props = new List<UPropertyReader.Property>();
        }

        public void Deserialize()
        {
            int pos = 4;
            if (UPR == null)
                return;
            props = UPR.readProperties(memory, "StaticMeshCollectionActor", names, pos);
            ReadObjects();
            ReadMatrices();
        }

        public void ReadObjects()
        {
            int entry = -1;
            for (int i = 0; i < props.Count; i++)
                if (props[i].name == "StaticMeshComponents")
                    entry = i + 1;
            if(entry == -1)
                return;
            int count = (props[entry].raw.Length - 20) / 4;
            Entries = new List<int>();
            for (int i = 0; i < count; i++)
                Entries.Add(BitConverter.ToInt32(props[entry].raw, i * 4 + 20) -1);
        }

        public void ReadMatrices()
        {
            int pos = props[props.Count - 1].offset + props[props.Count - 1].raw.Length;
            Matrices = new List<Matrix>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Matrix m = new Matrix();
                float[,] buff =  new float[4,4];
                for(int y =0;y<4;y++)
                    for (int x = 0; x < 4; x++)
                    {
                        buff[x, y] = BitConverter.ToSingle(memory, pos);
                        pos += 4;
                    }
                m.M11 = buff[0, 0];
                m.M12 = buff[1, 0];
                m.M13 = buff[2, 0];
                m.M14 = buff[3, 0];

                m.M21 = buff[0, 1];
                m.M22 = buff[1, 1];
                m.M23 = buff[2, 1];
                m.M24 = buff[3, 1];

                m.M31 = buff[0, 2];
                m.M32 = buff[1, 2];
                m.M33 = buff[2, 2];
                m.M34 = buff[3, 2];

                m.M41 = buff[0, 3];
                m.M42 = buff[1, 3];
                m.M43 = buff[2, 3];
                m.M44 = buff[3, 3];
                Matrices.Add(m);
            }
        }

        public TreeNode ExportToTree()
        {
            TreeNode t = new TreeNode("StaticMeshCollectionActor");
            t.Nodes.Add(PropsToTree());
            return t;
        }

        public TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            for (int i = 0; i < props.Count; i++)
            {
                TreeNode t = new TreeNode(props[i].name);
                TreeNode t2 = new TreeNode(props[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public MemoryStream MatrixToStream(Matrix m)
        {
            MemoryStream mem = new MemoryStream();
            mem.Write(BitConverter.GetBytes(m.M11), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M12), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M13), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M14), 0, 4);

            mem.Write(BitConverter.GetBytes(m.M21), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M22), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M23), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M24), 0, 4);

            mem.Write(BitConverter.GetBytes(m.M31), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M32), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M33), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M34), 0, 4);

            mem.Write(BitConverter.GetBytes(m.M41), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M42), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M43), 0, 4);
            mem.Write(BitConverter.GetBytes(m.M44), 0, 4);
            return mem;
        }

        public MemoryStream StreamAppend(MemoryStream m1, MemoryStream m2)
        {
            MemoryStream m = m1;
            m2.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < m2.Length; i++)
                m.WriteByte((byte)m2.ReadByte());
            return m;
        }

        public void CloneObject(int index)
        {
            int n = Entries[index];
            Matrix m = Matrices[index];
            Entries.Add(n);
            Matrices.Add(m);
            MemoryStream mem = new MemoryStream();
            int ent = UPR.FindProperty("StaticMeshComponents", props) + 1;
            int off = props[ent].offset + 8;
            int offend = props[ent].offset + props[ent].raw.Length;
            mem.Write(memory, 0, off);
            mem.Write(BitConverter.GetBytes(Entries.Count * 4 + 4), 0, 4);
            mem.Write(BitConverter.GetBytes((Int32)0), 0, 4);
            mem.Write(BitConverter.GetBytes(Entries.Count), 0, 4);
            for (int i = 0; i < Entries.Count; i++)
                mem.Write(BitConverter.GetBytes(Entries[i] + 1), 0, 4);
            if (ent + 1 < props.Count)
            {
                int offnextend = props[props.Count - 1].offset + props[props.Count-1].raw.Length;
                mem.Write(memory, offend, offnextend - offend);
            }
            for (int i = 0; i < Matrices.Count; i++)
                mem = StreamAppend(mem, MatrixToStream(Matrices[i]));
            memory = mem.ToArray();
            memsize = memory.Length;
            CHANGED = true;
        }
    }
}
