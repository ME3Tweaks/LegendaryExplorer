using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ionic.Zip;

namespace ME1Explorer
{
    public class SaveGame
    {
        public struct SaveFileEntry
        {
            public MemoryStream memory;
            public string FileName;
        }
        public List<SaveFileEntry> Files;
        public MemoryStream complete;
        public MemoryStream zipfile;
        public ZipFile zip;
        public bool Loaded = false;
        public string MyFilename;

        public SaveGame(string path)
        {
            try
            {
                MyFilename = path;
                MemoryStream m = new MemoryStream();
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                int len = (int)fs.Length;
                byte[] buff = new byte[len];
                fs.Read(buff, 0, len);
                m.Write(buff, 0, len);
                fs.Close();
                complete = m;
                BitConverter.IsLittleEndian = true;
                m.Seek(8, SeekOrigin.Begin);
                int off = ReadInt(m);
                int len2 = len - off;
                m.Seek(off, SeekOrigin.Begin);
                buff = new byte[len2];
                m.Read(buff, 0, len2);
                zipfile = new MemoryStream(buff);
                zipfile.Seek(0, SeekOrigin.Begin);
                ZipFile zipin = ZipFile.Read(zipfile);
                Files = new List<SaveFileEntry>();
                foreach (ZipEntry file in zipin)
                {
                    SaveFileEntry ent = new SaveFileEntry();
                    ent.FileName = file.FileName;
                    ent.memory = new MemoryStream();
                    file.Extract(ent.memory);
                    Files.Add(ent);
                }
                zip = zipin;
            }
            catch (Exception)
            {
                return;
            }
            Loaded = true;
        }

        private int ReadInt(MemoryStream m)
        {
            byte[] buff = new byte[4];
            m.Read(buff, 0, 4);
            return BitConverter.ToInt32(buff, 0);
        }

        public void ExtractME1Package(int n, string path)
        {
            if (!(n == 0 || n == 2))
                return;
            MemoryStream m = Files[n].memory;
            m.Seek(8, SeekOrigin.Begin);
            int off = ReadInt(m);
            m.Seek(off, SeekOrigin.Begin);
            MemoryStream m2 = new MemoryStream();
            m2.Write(Files[n].memory.ToArray(), off, (int)Files[n].memory.Length - off);
            m2.Seek(0x1D, SeekOrigin.Begin);
            int realoff = ReadInt(m2);
            m = new MemoryStream();
            m.Write(m2.ToArray(), 0, 0x89);
            int len = realoff - 0x89;
            for (int i = 0; i < len; i++)
                m.WriteByte(0);
            m.Write(m2.ToArray(), 0x89, (int)m2.Length - 0x89);
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(m.ToArray(), 0, (int)m.Length);
            fs.Close();
        }

        public void ImportME1Package(int n, string path)
        {
            if (!(n == 0 || n == 2))
                return;
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] buff = new byte[(int)fs.Length];
            fs.Read(buff, 0, (int)fs.Length);
            fs.Close();
            MemoryStream m = new MemoryStream(buff);
            MemoryStream m2 = new MemoryStream();
            m2.Write(m.ToArray(), 0, 0x89);
            m.Seek(0x1D, SeekOrigin.Begin);
            int off = ReadInt(m);
            m2.Write(m.ToArray(), off, (int)m.Length - off);
            m = new MemoryStream();
            Files[n].memory.Seek(8, SeekOrigin.Begin);
            int off2 = ReadInt(Files[n].memory);
            m.Write(Files[n].memory.ToArray(), 0, off2);
            m.Write(m2.ToArray(), 0, (int)m2.Length);
            SaveFileEntry ent = Files[n];
            ent.memory = m;
            Files[n] = ent;
        }

        public void Save()
        {
            FileStream fs = new FileStream(MyFilename, FileMode.Create, FileAccess.Write);
            byte[] buff;
            ZipFile zipin = new ZipFile();
            foreach (SaveFileEntry ent in Files)
            {
                FileStream fs2 = new FileStream(ent.FileName, FileMode.Create, FileAccess.Write);
                fs2.Write(ent.memory.ToArray(), 0, (int)ent.memory.Length);
                fs2.Close();
                if (File.Exists(ent.FileName))
                {
                    zipin.AddFile(ent.FileName);
                    zipin.Save("temp.zip");
                }
                File.Delete(ent.FileName);
            }
            zip = zipin;
            FileStream fs3 = new FileStream("temp.zip", FileMode.Open, FileAccess.Read);
            int len = (int)fs3.Length;
            buff = new byte[len];
            fs3.Read(buff, 0, len);
            fs3.Close();
            zipfile = new MemoryStream(buff);
            File.Delete("temp.zip");
            BitConverter.IsLittleEndian = true;
            MemoryStream m = new MemoryStream();
            complete.Seek(8, SeekOrigin.Begin);
            int off = ReadInt(complete);
            complete.Seek(0, SeekOrigin.Begin);
            buff = new byte[off];
            complete.Read(buff, 0, off);
            m.Write(buff, 0, off);
            m.Write(zipfile.ToArray(), 0, (int)zipfile.Length);
            fs.Write(m.ToArray(), 0, (int)m.Length);
            fs.Close();
        }

        public void Save(string path)
        {
            string temp = MyFilename;
            MyFilename = path;
            Save();
            MyFilename = temp;
        }
    }
}
