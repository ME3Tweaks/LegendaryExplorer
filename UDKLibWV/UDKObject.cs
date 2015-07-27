using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace UDKLibWV
{
    public class UDKObject
    {

        public struct NameEntry
        {
            public string name;
            public int unk;
            public int flags;
        }
        public struct ExportEntry
        {
            public int clas;
            public int link;
            public int name;
            public int flags;
            public int size;
            public int offset;
            public byte[] raw;
            public byte[] data;
            public byte[] olddata;
            public bool IsChanged;
            public int _off;
        }
        public struct ImportEntry
        {
            public int Package;
            public int link;
            public int name;
            public byte[] raw;
        }
        public struct FreeZone
        {
            public int start;
            public int end;
            public byte[] raw;
        }

        public int NameCount, NameOffset;
        public int ImportCount, ImportOffset;
        public int ExportCount, ExportOffset;
        public List<NameEntry> Names;
        public List<ImportEntry> Imports;
        public List<ExportEntry> Exports;
        public byte[] Header;
        public int _HeaderOff;
        public FreeZone fz;        

        public UDKObject(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            ReadFile(fs);
            fs.Close();
        }

        public UDKObject()
        {
        }

        public void SaveToFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Create , FileAccess.Write);
            WriteFile(fs);
            fs.Close();
        }

        private void WriteFile(FileStream file)
        {
            MemoryStream m = new MemoryStream();
            NameOffset = (int)m.Length;
            foreach(NameEntry e in Names)
            {
                m.Write(BitConverter.GetBytes((int)(e.name.Length + 1)), 0, 4);
                for (int i = 0; i < e.name.Length; i++)
                    m.WriteByte((byte)e.name[i]);
                m.WriteByte(0);
                m.Write(BitConverter.GetBytes(e.unk), 0, 4);
                m.Write(BitConverter.GetBytes(e.flags), 0, 4);
            }
            ImportOffset = (int)m.Length;
            foreach (ImportEntry e in Imports)
            {
                m.Write(e.raw, 0, e.raw.Length);
            }            
            ExportOffset = (int)m.Length;
            for (int i = 0; i < ExportCount; i++)//add export list
            {
                int off = (int)m.Length;
                ExportEntry e = Exports[i];
                e._off = off;
                Exports[i] = e;
                m.Write(e.raw, 0, e.raw.Length);
            }
            fz.start = (int)m.Length;
            fz.end = fz.start + fz.raw.Length;
            m.Write(fz.raw, 0, fz.raw.Length);
            for (int i = 0; i < ExportCount; i++)//add unchanged stuff
                if (!Exports[i].IsChanged)
                {
                    ExportEntry e = Exports[i];
                    e.offset = (int)m.Length;
                    m.Write(e.data, 0, e.data.Length);
                    Exports[i] = e;
                }
                else
                {
                    ExportEntry e = Exports[i];
                    m.Write(e.olddata, 0, e.olddata.Length);
                }
            for (int i = 0; i < ExportCount; i++)//add new stuff
                if (Exports[i].IsChanged)
                {
                    ExportEntry e = Exports[i];
                    e.offset = (int)m.Length;
                    m.Write(e.data, 0, e.data.Length);
                    Exports[i] = e;
                }
            for (int i = 0; i < ExportCount; i++)//patch lists
            {
                ExportEntry e = Exports[i];
                m.Seek(e._off + 32, SeekOrigin.Begin);
                m.Write(BitConverter.GetBytes(e.data.Length), 0, 4);
                m.Write(BitConverter.GetBytes(e.offset), 0, 4);
            }
            m.Seek(_HeaderOff, SeekOrigin.Begin);
            m.Write(BitConverter.GetBytes(NameCount), 0, 4);
            m.Write(BitConverter.GetBytes(NameOffset), 0, 4);
            m.Write(BitConverter.GetBytes(ExportCount), 0, 4);
            m.Write(BitConverter.GetBytes(ExportOffset), 0, 4);
            m.Write(BitConverter.GetBytes(ImportCount), 0, 4);
            m.Write(BitConverter.GetBytes(ImportOffset), 0, 4);
            m.Write(BitConverter.GetBytes(fz.start), 0, 4);
            m.Write(BitConverter.GetBytes(fz.end), 0, 4);
            m.Seek(8, SeekOrigin.Begin);
            m.Write(BitConverter.GetBytes(fz.end), 0, 4);
            m.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < m.Length; i++)
                file.WriteByte((byte)m.ReadByte());
        }

        private void ReadFile(FileStream file)
        {
            file.Seek(0, SeekOrigin.Begin);
            ReadHeader(file);
            ReadNames(file);
            ReadImports(file);
            ReadFreeZone(file);
            ReadExports(file);
        }

        private void ReadHeader(FileStream fs)
        {
            fz = new FreeZone();
            int pos = 20;
            int len = ReadInt32(fs, 12);
            pos += len;
            _HeaderOff = pos;
            NameCount = ReadInt32(fs, pos);
            pos += 4;
            NameOffset= ReadInt32(fs, pos);
            pos += 4;
            ExportCount = ReadInt32(fs, pos);
            pos += 4;
            ExportOffset = ReadInt32(fs, pos);
            pos += 4;
            ImportCount = ReadInt32(fs, pos);
            pos += 4;
            ImportOffset = ReadInt32(fs, pos);
            pos += 4;
            fz.start = ReadInt32(fs, pos);
            pos += 4;
            fz.end = ReadInt32(fs, pos);
            pos = NameOffset;
            Header = new byte[pos];
            fs.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < pos; i++)
                Header[i] = (byte)fs.ReadByte();
        }
        private void ReadNames(FileStream fs)
        {
            int pos = NameOffset;
            Names = new List<NameEntry>();
            for (int i = 0; i < NameCount; i++)
            {
                int len = ReadInt32(fs, pos);
                pos +=4;
                fs.Seek(pos, SeekOrigin.Begin);
                string s = "";
                for (int j = 0; j < len - 1; j++)
                {
                    s += (char)fs.ReadByte();
                    pos++;
                }
                pos++;
                int unk1 = ReadInt32(fs, pos);
                int flags = ReadInt32(fs, pos + 4);
                pos += 8;
                NameEntry e = new NameEntry();
                e.name = s;
                e.unk = unk1;
                e.flags = flags;
                Names.Add(e);
            }            
        }
        private void ReadImports(FileStream fs)
        {
            int pos = ImportOffset;
            Imports = new List<ImportEntry>();
            for (int i = 0; i < ImportCount; i++)
            {
                int start = pos;
                int package = ReadInt32(fs, pos);
                pos += 16;
                int link = ReadInt32(fs, pos);
                pos += 4;
                int name = ReadInt32(fs, pos);
                pos += 8;
                int len = pos - start;
                byte[] buff = new byte[len];
                fs.Seek(start, SeekOrigin.Begin);
                for (int j = 0; j < len; j++)
                    buff[j] = (byte)fs.ReadByte();
                ImportEntry e = new ImportEntry();
                e.Package = package;
                e.link = link;
                e.name = name;
                e.raw = buff;
                Imports.Add(e);
            }
        }
        private void ReadExports(FileStream fs)
        {
            fs.Seek(ExportOffset, SeekOrigin.Begin);
            int pos = ExportOffset;
            Exports = new List<ExportEntry>();
            for (int i = 0; i < ExportCount; i++)
            {
                int start = pos;
                int clas = ReadInt32(fs, pos);
                pos += 8;
                int link = ReadInt32(fs, pos);
                pos += 4;
                int name = ReadInt32(fs, pos);
                pos += 16;
                int flags = ReadInt32(fs, pos);
                pos += 4;
                int size = ReadInt32(fs, pos);
                pos += 4;
                int offset = ReadInt32(fs, pos);
                pos += 8;
                int count = ReadInt32(fs, pos);
                pos += 24 + count * 4;
                int len = pos - start;
                byte[] buff = new byte[len];
                fs.Seek(start, SeekOrigin.Begin);
                for (int j = 0; j < len; j++)
                    buff[j] = (byte)fs.ReadByte();
                byte[] buff2 = new byte[size];
                fs.Seek(offset, SeekOrigin.Begin);
                for (int j = 0; j < size; j++)
                    buff2[j] = (byte)fs.ReadByte();
                byte[] buff3 = new byte[size];
                for (int j = 0; j < size; j++)
                    buff3[j] = buff2[j];
                ExportEntry e = new ExportEntry();
                e.clas = clas;
                e.link = link;
                e.name = name;
                e.flags = flags;
                e.size = size;
                e.offset = offset;
                e.raw = buff;
                e.data = buff2;
                e.olddata = buff3;
                e.IsChanged = false;
                Exports.Add(e);
            }
        }
        private void ReadFreeZone(FileStream fs)
        {
            fs.Seek(fz.start, SeekOrigin.Begin);
            int len = fz.end - fz.start;
            fz.raw = new byte[len];
            for (int i = 0; i < len; i++)
                fz.raw[i] = (byte)fs.ReadByte();
        }

        public bool isName(int Index)
        {
            return (Index >= 0 && Index < NameCount);
        }

        public bool isImport(int Index)
        {
            return (Index >= 0 && Index < ImportCount);
        }

        public bool isExport(int Index)
        {
            return (Index >= 0 && Index < ExportCount);
        }

        public string GetClass(int Index)
        {
            if (Index > 0 && isExport(Index - 1))
                return GetName(Exports[Index - 1].name);
            if (Index < 0 && isImport(Index * -1 - 1))
                return GetName(Imports[Index * -1 - 1].name);
            return "Class";
        }

        public string FollowLink(int Link)
        {
            string s = "";
            if (Link > 0 && isExport(Link - 1))
            {
                s = GetName(Exports[Link - 1].name) + ".";
                s = FollowLink(Exports[Link - 1].link) + s;
            }
            if (Link < 0 && isImport(Link * -1 - 1))
            {
                s = GetName(Imports[Link * -1 - 1].name) + ".";
                s = FollowLink(Imports[Link * -1 - 1].link) + s;
            }
            return s;
        }

        public string GetName(int Index)
        {
            string s = "";
            if (isName(Index))
                s = Names[Index].name;
            return s;
        }

        public int ReadInt32(FileStream fs, int pos)
        {
            fs.Seek(pos, SeekOrigin.Begin);
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }
        
    }
}
