using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class PCCFile
    {
        public struct header
        {
            public int v1;
            public int v2;
            public uint PackFlags;
            public uint NameCount;
            public uint NameOffset;
            public uint ExportCount;
            public uint ExportOffset;
            public uint ImportCount;
            public uint ImportOffset;
            public uint offinfo;
        }
        public struct imports
        {
            public int Link;
            public uint Name;
            public uint Package;
        }
        public struct exports
        {
            public int Class;
            public int Link;
            public uint Name;
            public uint DataSize;
            public uint DataOffset;
            public uint off;
            public uint start;
            public uint end;
        }

        public header Header;
        public imports[] Import;
        public exports[] Export;
        public byte[] memory = null;
        public string[] names;
        public string loadedFilename;
        public int memsize { get { if (memory == null) return -1; else return memory.Length; } }
        public bool isXBox = false;

        public PCCFile()
        {
        }

#region Loading
        public int LoadFile(string path)
        {
            return Load(path, null);
        }

        public int LoadFile(string path, ToolStripProgressBar pb)
        {
            return Load(path, pb);
        }

        public int Load(string path,ToolStripProgressBar pb = null)
        {
            //returns   0=Ok
            //         -1=Fail
            //         -2=Compressed
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            try
            {
                memory = new byte[(int)fileStream.Length];
                int count;
                int sum = 0;
                while ((count = fileStream.Read(memory, sum, memsize - sum)) > 0) sum += count;
            }
            finally
            {
                fileStream.Close();
            }
            if (memsize > 0)
            {
                BitConverter.IsLittleEndian = true;
                uint magic = getUInt(0);
                if (magic != 0x9E2A83C1)
                {
                    if (magic == 0xC1832A9E)
                    {
                        BitConverter.IsLittleEndian = false;
                        isXBox = true;
                    }
                    else
                        return -1;
                }
                Header.v1 = getVersion(4);
                Header.v2 = getVersion(6);
                Header.PackFlags = getUInt(26);
                uint test = getUInt(30);
                int reloff = 0;
                if (test != 0) reloff = 4;
                if ((Header.PackFlags & 0x02000000u) != 0)
                    return -2;//Compressed
                loadedFilename = Path.GetFileName(path);
                reloff = 0;
                Header.offinfo =(uint)(34 - reloff);
                Header.NameCount = getUInt(34 - reloff);
                Header.NameOffset = getUInt(38 - reloff);
                Header.ExportCount = getUInt(42 - reloff);
                Header.ExportOffset = getUInt(46 - reloff);
                Header.ImportCount = getUInt(50 - reloff);
                Header.ImportOffset = getUInt(54 - reloff);
                names = new string[Header.NameCount];
                Import = new imports[Header.ImportCount];
                Export = new exports[Header.ExportCount];
                ReadNames((int)Header.NameOffset, pb);
                ReadImports((int)Header.ImportOffset, pb);
                ReadExports((int)Header.ExportOffset, pb);
                return 0;
            }
            return -1;
        }

        public void ReadNames(int off, ToolStripProgressBar pb)
        {
            if(pb!=null) pb.Maximum = (int)Header.NameCount;
            int pos = off;
            for (int i = 0; i < Header.NameCount; i++)
            {
                if (pb != null)
                {
                    pb.Value = i;
                    Application.DoEvents();
                }
                int l = getInt(pos);
                l *= -1;
                pos += 4;
                names[i] = "";
                for (int j = 0; j < l; j++)
                {
                    names[i] += (char)memory[pos];
                    pos += 2;
                }
            }
        }

        public void ReadImports(int off, ToolStripProgressBar pb)
        {
            if(pb !=null) pb.Maximum = (int)Header.ImportCount;
            int pos = off;
            for (int i = 0; i < Header.ImportCount; i++)
            {
                if (pb != null)
                {
                    pb.Value = i;
                    Application.DoEvents();
                }
                Import[i].Package = getUInt(pos);
                pos += 16;
                Import[i].Link = getInt(pos);
                pos += 4;
                Import[i].Name = getUInt(pos);
                pos += 8;
            }
        }

        public void ReadExports(int off, ToolStripProgressBar pb)
        {
            int count;
            if(pb!=null) pb.Maximum = (int)Header.ExportCount;
            int pos = off;
            for (int i = 0; i < Header.ExportCount; i++)
            {
                Export[i].start = (uint)pos;
                if (pb != null)
                {
                    pb.Value = i;
                    Application.DoEvents();
                }
                Export[i].Class = getInt(pos);
                pos += 8;
                Export[i].Link = getInt(pos);
                pos += 4;
                Export[i].Name = getUInt(pos);
                pos += 20;
                Export[i].off = (uint)pos;
                Export[i].DataSize = getUInt(pos);
                pos += 4;
                Export[i].DataOffset = getUInt(pos);
                pos += 8;
                count = getInt(pos);
                if (count != 0)
                {
                }
                pos += 24 + count * 4;
                Export[i].end = (uint)pos;
            }
        }
#endregion


#region Helperz

        public void EditEntryMeta(int idx, int name,int link,int classname,int datasize,int dataoffset)
        {
            Export[idx].Name = (uint)name;
            Export[idx].Link = link;
            Export[idx].Class = classname;
            Export[idx].DataSize = (uint)datasize;
            Export[idx].DataOffset = (uint)dataoffset;
            int off = (int)Export[idx].start;
            byte[] buff = BitConverter.GetBytes(classname);
            for (int i = 0; i < 4; i++)
                memory[off + i] = buff[i];
            buff = BitConverter.GetBytes(link);
            for (int i = 0; i < 4; i++)
                memory[off + i + 8] = buff[i];
            buff = BitConverter.GetBytes(name);
            for (int i = 0; i < 4; i++)
                memory[off + i + 12] = buff[i];
            buff = BitConverter.GetBytes(datasize);
            for (int i = 0; i < 4; i++)
                memory[off + i + 32] = buff[i];
            buff = BitConverter.GetBytes(dataoffset);
            for (int i = 0; i < 4; i++)
                memory[off + i + 36] = buff[i];
        }
        public void CloneEntry(int entry)
        {
            byte[] buffdata;
            DialogResult r = MessageBox.Show("Clone with new data?","", MessageBoxButtons.YesNo);
            if (r == DialogResult.No)
            {
                buffdata = EntryToBuff(entry);                
            }
            else
            {
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "bin files (*.bin)|*.bin|all files|*.*";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    FileStream f = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                    int size = (int)f.Length;
                    buffdata = new byte[size];
                    int count;
                    int sum = 0;
                    while ((count = f.Read(buffdata, sum, size - sum)) > 0) sum += count;
                }
                else
                    return;
            }
            int sizebefore = memsize;
            MemoryStream m = new MemoryStream();
            m.Write(memory, 0, (int)Header.offinfo + 8);
            byte[] buff = BitConverter.GetBytes(Header.ExportCount + 1);
            m.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(sizebefore);
            m.Write(buff, 0, 4);
            m.Write(memory, (int)Header.offinfo + 16,sizebefore - ((int)Header.offinfo + 16));
            for (int i = 0; i < Export.Length; i++)
            {
                uint len = Export[i].end - Export[i].start;
                m.Write(memory, (int)Export[i].start, (int)len);
            }
            int pos =(int)m.Length;
            uint len2 = Export[entry].end - Export[entry].start;
            m.Write(memory, (int)Export[entry].start, 32);
            buff = BitConverter.GetBytes(buffdata.Length);
            m.Write(buff, 0, 4);
            buff = BitConverter.GetBytes(pos + (int)len2);
            m.Write(buff, 0, 4);
            m.Write(memory, (int)Export[entry].start + 40, (int)len2- 40);
            m.Write(buffdata, 0, buffdata.Length);
            Header.ExportCount++;
            Header.ExportOffset = (uint)sizebefore;
            exports ex = new exports();
            ex.Class = Export[entry].Class;
            ex.DataOffset = (uint)pos + len2;
            ex.DataSize = (uint)buffdata.Length;
            ex.end = (uint)pos + len2;
            ex.Link  = Export[entry].Link;
            ex.Name = Export[entry].Name;
            ex.off = Export[entry].off;
            ex.start = (uint)pos;
            exports[] t = new exports[Header.ExportCount];
            for (int i = 0; i < Header.ExportCount - 1; i++)
                t[i] = Export[i];
            t[Header.ExportCount - 1] = ex;
            Export = t;
            memory = m.ToArray();
            TOCeditor tc = new TOCeditor();
            if (!tc.UpdateFile(loadedFilename, (uint)memsize))
                MessageBox.Show("Didn't found Entry");
        }
        public void RedirectEntry(int entry, int offset, int size)
        {
            int off = (int)Export[entry].off;
            byte[] buff = BitConverter.GetBytes(size);
            for (int i = 0; i < 4; i++)
                memory[off + i] = buff[i];
            buff = BitConverter.GetBytes(offset);
            for (int i = 0; i < 4; i++)
                memory[off + i + 4] = buff[i];
            Export[entry].DataOffset = (uint)offset;
            Export[entry].DataSize = (uint)size;
        }
        public uint getUInt(int index)
        {
            return BitConverter.ToUInt32(memory,index);
        }
        public int getInt(int index)
        {
            return BitConverter.ToInt32(memory, index);
        }
        public int getVersion(int index)
        {
            return BitConverter.ToUInt16(memory, index);
        }
        public byte[] EntryToBuff(int Entry)
        {
            byte[] ret = new byte[0];
            if (Entry < 0 || Entry >= Header.ExportCount)
                return ret;
            exports ex = Export[Entry];
            ret = new byte[ex.DataSize];
            for (int i = 0; i < ex.DataSize; i++)
                ret[i] = memory[ex.DataOffset + i];
            return ret;
        }
        public string getClassName(int value)
        {
            string s = "";
            if (value > 0)
            {
                s = names[Export[value - 1].Name].Substring(0, names[Export[value - 1].Name].Length - 1);
            }
            if (value < 0)
            {
                s = names[Import[value * -1 - 1].Name].Substring(0, names[Import[value * -1 - 1].Name].Length - 1);
            }
            if (value == 0)
            {
                s = "Class";
            }
            return s;
        }
        public void appendStream(byte[] buff)
        {
            MemoryStream m = new MemoryStream();
            m.Write(memory, 0, memsize);
            m.Write(buff, 0, buff.Length);
            memory = m.ToArray();
        }
        public void appendStream(MemoryStream buff)
        {
            MemoryStream m = new MemoryStream();
            m.Write(memory, 0, memsize);
            buff.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < buff.Length; i++)
                m.WriteByte((byte)buff.ReadByte());
            memory = m.ToArray();
        }
#endregion


    }
}
