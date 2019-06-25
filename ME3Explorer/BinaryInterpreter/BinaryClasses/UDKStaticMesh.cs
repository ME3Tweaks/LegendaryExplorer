using System;
using System.IO;
using ME3Explorer.Packages;

namespace ME3Explorer
{
    public class UDKStaticMesh : IUDKImportable
    {
        public UDKPackage Owner;
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

        public UDKStaticMesh(UDKPackage udk, int Index)
        {
            MyIndex = Index;
            Owner = udk;
            ReadEnd = GetPropertyEnd(Index);
            byte[] buff = udk.Exports[Index].Data;
            MemoryStream m = new MemoryStream((byte[])buff.Clone());
            Read(m);
        }

        public void PortToME1Export(ExportEntry destExport)
        {
            MemoryStream m = GetSTMBinaryData(destExport);
            destExport.setBinaryData(m.ToArray());
        }

        public void PortToME2Export(ExportEntry destExport)
        {
            MemoryStream m = GetSTMBinaryData(destExport);
            destExport.setBinaryData(m.ToArray());
        }

        public void PortToME3Export(ExportEntry destExport)
        {
            MemoryStream m = GetSTMBinaryData(destExport);
            destExport.setBinaryData(m.ToArray());
        }

        private MemoryStream GetSTMBinaryData(ExportEntry destExport)
        {
            MemoryStream m = new MemoryStream();
            //Properties
            //m.Write(BitConverter.GetBytes(0), 0, 4);
            //WriteName(m, destExport.FileRef.FindNameOrAdd("BodySetup"));
            //WriteName(m, destExport.FileRef.FindNameOrAdd("ObjectProperty"));
            //WriteInt(m, 4);
            //WriteInt(m, 0);
            //WriteInt(m, 0);
            //WriteName(m, destExport.FileRef.FindNameOrAdd("UseSimpleBoxCollision"));
            //WriteName(m, destExport.FileRef.FindNameOrAdd("BoolProperty"));
            //WriteInt(m, 0);
            //WriteInt(m, 0);
            //m.WriteByte(1);
            //WriteName(m, destExport.FileRef.FindNameOrAdd("None"));

            //Start of binary data
            m.Write(Bounds1, 0, Bounds1.Length);
            WriteInt(m, 0);
            m.Write(Bounds2, 0, Bounds2.Length);
            m.Write(Surfs, 0, Surfs.Length);
            m.Write(Faces, 0, Faces.Length);
            WriteInt(m, 18);
            WriteInt(m, 1);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            m.Write(Mats, 0, Mats.Length);
            m.Write(Tris, 0, 8);
            WriteInt(m, 1);
            m.Write(Tris, 0, Tris.Length);
            m.Write(Unk3, 0, Unk3.Length);
            WriteInt(m, 0);
            m.Write(UVs, 0, UVs.Length);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 4);
            WriteInt(m, 0);
            WriteInt(m, 4);
            m.Write(Unk4, 4, 8);
            m.Write(Indexes1, 0, Indexes1.Length);
            WriteInt(m, 2);
            WriteInt(m, 0);
            WriteInt(m, 0x10);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 1);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            WriteInt(m, 0);
            return m;
        }

        private void WriteInt(MemoryStream m, int idx)
        {
            m.Write(BitConverter.GetBytes(idx), 0, 4);
        }

        private void WriteName(MemoryStream m, int idx)
        {
            WriteInt(m, idx);
            m.Write(BitConverter.GetBytes(0), 0, 4);
        }

        public void Read(MemoryStream m)
        {
            //Read binary data
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

        private int GetPropertyEnd(int n)
        {
            return Owner.Exports[n].propsEnd();
        }
    }
}
