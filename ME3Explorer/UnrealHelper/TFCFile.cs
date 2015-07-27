using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gibbed.IO;

namespace ME3Explorer.UnrealHelper
{
    public class TFCFile
    {
        private string filename;

        public struct ChunkHeader
        {
            public uint magic;
            public uint blocksize;
            public uint compsize;
            public uint uncompsize;
        }
        public struct ChunkBlock
        {
            public uint compsize;
            public uint uncompsize;
        }

        public TFCFile(int source)
        {
            filename = "";
            if (source == 0)
            {
                OpenFileDialog Dialog1 = new OpenFileDialog();
                Dialog1.Filter = "Textures.tfc|Textures.tfc";
                if (Dialog1.ShowDialog() == DialogResult.OK)
                    filename = Dialog1.FileName;
            }
            if (source == 1)
            {
                OpenFileDialog Dialog1 = new OpenFileDialog();
                Dialog1.Filter = "CharTextures.tfc|CharTextures.tfc";
                if (Dialog1.ShowDialog() == DialogResult.OK)
                    filename = Dialog1.FileName;
            }
        }

        public TFCFile(string path)
        {
            if(!path.EndsWith(".tfc"))
            {
              string tfcname = path + ".tfc";
              OpenFileDialog Dialog1 = new OpenFileDialog();
              Dialog1.Filter = tfcname + "|" + tfcname + "|All files|*.*";
              if (Dialog1.ShowDialog() == DialogResult.OK)
                  filename = Dialog1.FileName;
            }
            else
                filename = path;
        }

        public bool isTFCCompressed()
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            fs.Seek(16, SeekOrigin.Begin);
            uint magic = fs.ReadValueU32();
            fs.Close();
            return (magic == 0x9E2A83C1) || (magic.Swap() == 0x9E2A83C1);
        }

        public bool CheckTFC(uint pos)
        {
            if(!File.Exists(filename))
                return false;
            FileStream fs = new FileStream(filename,FileMode.Open, FileAccess.Read);
            if (pos > fs.Length)
            {
                fs.Close();
                return false;
            }
            fs.Seek(pos, SeekOrigin.Begin);
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            uint magic = BitConverter.ToUInt32(buff, 0);
            fs.Close();
            if (magic == 0x9E2A83C1)
                return true;
            return false;
        }

        public byte[] getRawTFCComp(uint pos)
        {
            MemoryStream ret = new MemoryStream();
            if (!File.Exists(filename))
                return ret.ToArray();
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            if (pos > fs.Length)
            {
                fs.Close();
                return ret.ToArray();
            }
            fs.Seek(pos, SeekOrigin.Begin);
            ChunkHeader ch = new ChunkHeader();
            List<ChunkBlock> cb = new List<ChunkBlock>();
            byte[] buff = new byte[4];
            fs.Read(buff, 0, 4);
            uint magic = BitConverter.ToUInt32(buff, 0);
            if (magic == 0x9E2A83C1)
            {
                ch.magic = magic;
                fs.Read(buff, 0, 4);
                ch.blocksize = BitConverter.ToUInt32(buff, 0);
                fs.Read(buff, 0, 4);
                ch.compsize = BitConverter.ToUInt32(buff, 0);
                fs.Read(buff, 0, 4);
                ch.uncompsize = BitConverter.ToUInt32(buff, 0);
            }
            int n = (int)(ch.uncompsize / ch.blocksize);
            if (ch.uncompsize < ch.blocksize)
                n = 1;
            for (int i = 0; i < n; i++)
            {
                ChunkBlock t = new ChunkBlock();
                fs.Read(buff, 0, 4);
                t.compsize = BitConverter.ToUInt32(buff, 0);
                fs.Read(buff, 0, 4);
                t.uncompsize = BitConverter.ToUInt32(buff, 0);
                cb.Add(t);
            }
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            for (int i = 0; i < n; i++)
            {
                if (File.Exists(loc + "\\exec\\temp.dat"))
                    File.Delete(loc + "\\exec\\temp.dat");
                if (File.Exists(loc + "\\exec\\out.dat"))
                    File.Delete(loc + "\\exec\\out.dat");
                FileStream fs2 = new FileStream(loc + "\\exec\\temp.dat", FileMode.Create, FileAccess.Write);
                for (int j = 0; j < cb[i].compsize; j++)
                    fs2.WriteByte((byte)fs.ReadByte());
                fs2.Close();
                RunShell(loc + "\\exec\\zlibber.exe", "-sdc temp.dat out.dat");
                fs2 = new FileStream(loc + "\\exec\\out.dat", FileMode.Open, FileAccess.Read);
                buff = new byte[fs2.Length];
                for (int j = 0; j < fs2.Length; j++)
                    buff[j] = (byte)fs2.ReadByte();
                fs2.Close();
                StreamAppend(ret, buff);
            }
            fs.Close();
            return ret.ToArray();
        }

        public byte[] getRawTFC(uint pos,int size)
        {
            MemoryStream ret = new MemoryStream();
            if (!File.Exists(filename))
                return ret.ToArray();
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            if (pos + size >= fs.Length)
            {
                fs.Close();
                return ret.ToArray();
            }
            byte[] buff = new byte[size]; 
            fs.Seek(pos, SeekOrigin.Begin);
            fs.Read(buff, 0, size);
            fs.Close();
            ret.Write(buff, 0, size);
            return ret.ToArray();
        }

        public byte[] makeRawTFCComp(byte[] input)
        {
            MemoryStream ms = new MemoryStream();
            byte[] buff = BitConverter.GetBytes(0x9E2A83C1);
            ms.Write(buff, 0, 4);
            buff = BitConverter.GetBytes((Int32)0x20000);
            ms.Write(buff, 0, 4);
            int n = input.Length / 0x20000;
            if (input.Length < 0x20000)
                n = 1;
            int pos = 0;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(loc + "\\exec\\temp.dat"))
                File.Delete(loc + "\\exec\\temp.dat");
            FileStream fs = new FileStream(loc + "\\exec\\temp.dat", FileMode.Create, FileAccess.Write);
            List<ChunkBlock> cb = new List<ChunkBlock>();
            for (int i = 0; i < n; i++)
            {
                int len = 0x20000;
                if (input.Length - pos < 0x20000)
                    len = input.Length - pos;
                if (File.Exists(loc + "\\exec\\out0.dat"))
                    File.Delete(loc + "\\exec\\out0.dat");
                if (File.Exists(loc + "\\exec\\out1.dat"))
                    File.Delete(loc + "\\exec\\out1.dat");
                FileStream fs2 = new FileStream(loc + "\\exec\\out0.dat",FileMode.Create,FileAccess.Write);
                for (int j = pos; j<pos+len; j++)
                    fs2.WriteByte((byte)input[j]);
                fs2.Close();
                RunShell(loc + "\\exec\\zlibber", "-sc out0.dat out1.dat");
                fs2 = new FileStream(loc + "\\exec\\out1.dat",FileMode.Open,FileAccess.Read);
                for (int j = 0; j < fs2.Length; j++)
                    fs.WriteByte((byte)fs2.ReadByte());
                ChunkBlock t = new ChunkBlock();
                t.compsize = (uint)fs2.Length;
                t.uncompsize = (uint)len;
                cb.Add(t);
                fs2.Close();
                pos += 0x20000;
            }
            buff = BitConverter.GetBytes((Int32)fs.Length);
            ms.Write(buff, 0, 4);
            buff = BitConverter.GetBytes((Int32)input.Length);
            ms.Write(buff, 0, 4);
            for (int i = 0; i < n; i++)
            {
                buff = BitConverter.GetBytes((Int32)cb[i].compsize);
                ms.Write(buff, 0, 4);
                buff = BitConverter.GetBytes((Int32)cb[i].uncompsize);
                ms.Write(buff, 0, 4);
            }
            fs.Close();
            fs = new FileStream(loc + "\\exec\\temp.dat", FileMode.Open, FileAccess.Read);
            for (int i = 0; i < fs.Length; i++)
                ms.WriteByte((byte)fs.ReadByte());
            fs.Close();
            if (File.Exists(loc + "\\exec\\temp.dat"))
                File.Delete(loc + "\\exec\\temp.dat");
            if (File.Exists(loc + "\\exec\\out0.dat"))
                File.Delete(loc + "\\exec\\out0.dat");
            if (File.Exists(loc + "\\exec\\out1.dat"))
                File.Delete(loc + "\\exec\\out1.dat");
            return ms.ToArray();
        }

        public void AppendToTFC(byte[] input)
        {
            if (!File.Exists(filename))
                return;
            FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write);
            fs.Write(input, 0, input.Length);
            fs.Close();
        }

        public int getFileSize()
        {
            if (File.Exists(filename))
            {
                FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                int size = (int)fs.Length;
                fs.Close();
                return size;
            }
            else
                return -1;

        }

        private MemoryStream StreamAppend(MemoryStream s, byte[] buff)
        {
            MemoryStream m = s;
            for (int i = 0; i < buff.Length; i++)
                m.WriteByte((byte)buff[i]);
            return m;
        }

        private void RunShell(string cmd, string args)
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
        }
        
    }
}
