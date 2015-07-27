using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace UDKLibWV.Classes
{
    public class StaticMesh
    {
        public UDKObject Owner;
        public int MyIndex;
        private int ReadEnd;

        public byte[] Bounds1;
        public byte[] Bounds2;
        public byte[] Surfs;
        public byte[] Faces;
        public byte[] Unk1;
        public byte[] Unk2;
        public byte[] Mats;
        public byte[] Tris;
        public byte[] Unk3;
        public byte[] UVs;
        public byte[] Unk4;
        public byte[] Indexes1;
        public byte[] Indexes2;
        public byte[] Indexes3;
        public byte[] Rest;

        public StaticMesh(UDKObject udk, int Index)
        {
            MyIndex = Index;
            Owner = udk;
            ReadEnd = GetPropertyEnd(Index);
            byte[] buff = udk.Exports[Index].data;
            File.WriteAllBytes("C:\\test.bin", buff);
            MemoryStream m = new MemoryStream(buff);
            Read(m);
        }

        public void Read(MemoryStream m)
        {
            m.Seek(ReadEnd, 0);
            Bounds1 = new byte[28];
            m.Read(Bounds1, 0, 28);

            m.Seek(4, SeekOrigin.Current);
            Bounds2 = new byte[24];
            m.Read(Bounds2, 0, 24);

            int count, size;
            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Surfs = new byte[8 + count * size];
            m.Read(Surfs, 0, 8 + count * size);

            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Faces = new byte[8 + count * size];
            m.Read(Faces, 0, 8 + count * size);

            Unk1 = new byte[28];
            m.Read(Unk1, 0, 28);

            count = ReadInt(m);            
            size = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Unk2 = new byte[12 + size];
            m.Read(Unk2, 0, 12 + size);

            count = ReadInt(m);
            Mats = new byte[4 + 0x31 * count];
            m.Seek(-4, SeekOrigin.Current);
            m.Read(Mats, 0, 4 + 0x31 * count);
            
            size = ReadInt(m);
            count = ReadInt(m);
            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Tris = new byte[8 + size * count];
            m.Read(Tris, 0, 8 + size * count);

            Unk3 = new byte[16];
            m.Read(Unk3, 0, 16);

            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            UVs = new byte[8 + size * count];
            m.Read(UVs, 0, 8 + size * count);

            Unk4 = new byte[12];
            m.Read(Unk4, 0, 12);

            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Indexes1 = new byte[8 + size * count];
            m.Read(Indexes1, 0, 8 + size * count);

            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Indexes2 = new byte[8 + size * count];
            m.Read(Indexes2, 0, 8 + size * count);

            size = ReadInt(m);
            count = ReadInt(m);
            m.Seek(-8, SeekOrigin.Current);
            Indexes3 = new byte[8 + size * count];
            m.Read(Indexes3, 0, 8 + size * count);

            int len = (int)m.Length - (int)m.Position;
            Rest = new byte[len];
            m.Read(Rest, 0, len);
        }

        public int ReadInt(MemoryStream m)
        {
            byte[] buff = new byte[4];
            m.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("Skeletal Mesh");
            return res;
        }

        private int GetPropertyEnd(int n)
        {
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
    }
}
