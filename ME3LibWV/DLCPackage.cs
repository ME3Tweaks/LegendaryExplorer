using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ME3LibWV
{
    public class DLCPackage
    {
        public string MyFileName;
        public struct HeaderStruct
        {
            public uint Magic;
            public uint Version;
            public uint DataOffset;
            public uint EntryOffset;
            public uint FileCount;
            public uint BlockTableOffset;
            public uint MaxBlockSize;
            public char[] CompressionScheme;
            public void Serialize(SerializingContainer con)
            {
                Magic = con + Magic;
                Version = con + Version;
                DataOffset = con + DataOffset;
                EntryOffset = con + EntryOffset;
                FileCount = con + FileCount;
                BlockTableOffset = con + BlockTableOffset;
                MaxBlockSize = con + MaxBlockSize;
                if (con.isLoading)
                    CompressionScheme = new char[4];
                for (int i = 0; i < 4; i++)
                    CompressionScheme[i] = con + CompressionScheme[i];
                if (Magic != 0x53464152 ||
                    Version != 0x00010000 ||
                    MaxBlockSize != 0x00010000)
                {
                    DebugLog.PrintLn("DLCPACKAGE::HEADERSTRUCT::SERIALIZE ERROR: Not supported DLC file!");
                    return;
                }
                DebugLog.PrintLn("Magic: \t0x" + Magic.ToString("X8"));
                DebugLog.PrintLn("Version: \t0x" + Version.ToString("X8"));
                DebugLog.PrintLn("DataOffset: \t0x" + DataOffset.ToString("X8"));
                DebugLog.PrintLn("EntryOffset: 0x" + EntryOffset.ToString("X8"));
                DebugLog.PrintLn("FileCount: \t0x" + FileCount.ToString("X8"));
                DebugLog.PrintLn("MaxBlockSize:0x" + MaxBlockSize.ToString("X8"));
            }

            public TreeNode ToTree()
            {
                TreeNode result = new TreeNode("Header");
                result.Nodes.Add("Magic : " + Magic.ToString("X8"));
                result.Nodes.Add("Version : " + Version.ToString("X8"));
                result.Nodes.Add("DataOffset : " + DataOffset.ToString("X8"));
                result.Nodes.Add("EntryOffset : " + EntryOffset.ToString("X8"));
                result.Nodes.Add("FileCount : " + FileCount.ToString("X8"));
                result.Nodes.Add("BlockTableOffset : " + BlockTableOffset.ToString("X8"));
                result.Nodes.Add("MaxBlockSize : " + MaxBlockSize.ToString("X8"));
                string Scheme = "";
                for (int i = 3; i >= 0; i--)
                    Scheme += CompressionScheme[i];
                result.Nodes.Add("Scheme : " + Scheme);
                return result;
            }
        }
        public struct FileEntryStruct
        {
            public HeaderStruct Header;
            public uint MyOffset;
            public byte[] Hash;
            public uint BlockSizeIndex;
            public uint UncompressedSize;
            public byte UncompressedSizeAdder;
            public long RealUncompressedSize;
            public uint DataOffset;
            public byte DataOffsetAdder;
            public long RealDataOffset;
            public long BlockTableOffset;
            public long[] BlockOffsets;
            public ushort[] BlockSizes;
            public string FileName;

            public void Serialize(SerializingContainer con, HeaderStruct header)
            {
                Header = header;
                MyOffset = (uint)con.GetPos();
                if (con.isLoading)
                    Hash = new byte[16];
                for (int i = 0; i < 16; i++)
                    Hash[i] = con + Hash[i];
                BlockSizeIndex = con + BlockSizeIndex;
                UncompressedSize = con + UncompressedSize;
                UncompressedSizeAdder = con + UncompressedSizeAdder;
                RealUncompressedSize = UncompressedSize + UncompressedSizeAdder << 32;
                DataOffset = con + DataOffset;
                DataOffsetAdder = con + DataOffsetAdder;
                RealDataOffset = DataOffset + DataOffsetAdder << 32;
                if (BlockSizeIndex == 0xFFFFFFFF)
                {
                    BlockOffsets = new long[1];
                    BlockOffsets[0] = RealDataOffset;
                    BlockSizes = new ushort[1];
                    BlockSizes[0] = (ushort)UncompressedSize;
                    BlockTableOffset = 0;
                }
                else
                {
                    int numBlocks = (int)Math.Ceiling((double)UncompressedSize / (double)header.MaxBlockSize);
                    if (con.isLoading)
                    {
                        BlockOffsets = new long[numBlocks];
                        BlockSizes = new ushort[numBlocks];
                    }
                    BlockOffsets[0] = RealDataOffset;
                    long pos = con.Memory.Position;
                    con.Seek((int)getBlockOffset((int)BlockSizeIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
                    BlockTableOffset = con.Memory.Position;
                    BlockSizes[0] = con + BlockSizes[0];
                    for (int i = 1; i < numBlocks; i++)
                    {
                        BlockSizes[i] = con + BlockSizes[i];
                        BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
                    }
                    con.Seek((int)pos, 0);
                }
            }
            private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
            {
                return (long)(entryOffset + (numEntries * 0x1E) + (blockIndex * 2));
            }
            public TreeNode ToTree(int MyIndex)
            {
                TreeNode result = new TreeNode(MyIndex + " : @0x" + MyOffset.ToString("X8") + " Filename: " + FileName);
                string h = "Hash : ";
                foreach (byte b in Hash)
                    h += b.ToString("X2");
                result.Nodes.Add("Hash : " + h);
                result.Nodes.Add("BlockSizeIndex : " + BlockSizeIndex.ToString("X8"));
                result.Nodes.Add("UncompressedSize : " + UncompressedSize.ToString("X8"));
                result.Nodes.Add("UncompressedSizeAdder : " + UncompressedSizeAdder.ToString("X2"));
                result.Nodes.Add("RealUncompressedSize : " + RealUncompressedSize.ToString("X8"));
                result.Nodes.Add("DataOffset : " + DataOffset.ToString("X8"));
                result.Nodes.Add("DataOffsetAdder : " + DataOffsetAdder.ToString("X2"));
                result.Nodes.Add("RealDataOffset : " + RealDataOffset.ToString("X8"));
                result.Nodes.Add("BlockTableOffset : " + BlockTableOffset.ToString("X8"));
                TreeNode t = new TreeNode("Blocks : " + BlockOffsets.Length);
                for (int i = 0; i < BlockOffsets.Length; i++)
                    t.Nodes.Add(i + " : Offset: 0x" + BlockOffsets[i].ToString("X8") + " Size: 0x" + BlockSizes[i].ToString("X8"));
                result.Nodes.Add(t);
                return result;
            }
        }
        public static readonly byte[] TOCHash = { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C };

        public HeaderStruct Header;
        public FileEntryStruct[] Files;

        public DLCPackage(string FileName)
        {
            Load(FileName);
        }
        public void Load(string FileName)
        {
            MyFileName = FileName;
            DebugLog.PrintLn("Opening file : " + FileName);
            SerializingContainer con = new SerializingContainer(new FileStream(FileName, FileMode.Open, FileAccess.Read));
            Serialize(con);
            con.Memory.Close();
        }        
        public void ReBuild()
        {
            string path = Path.GetDirectoryName(MyFileName) + "\\" + Path.GetFileNameWithoutExtension(MyFileName) + ".tmp";
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            DebugLog.PrintLn("Creating Header Dummy...");
            for (int i = 0; i < 8; i++)
                fs.Write(BitConverter.GetBytes((int)0), 0, 4);
            Header.EntryOffset = 0x20;
            DebugLog.PrintLn("Creating File Table...");
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                fs.Write(BitConverter.GetBytes(e.BlockSizeIndex), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
            }
            Header.BlockTableOffset = (uint)fs.Position;
            DebugLog.PrintLn("Creating Block Table...");
            for (int i = 0; i < Header.FileCount; i++)
                if (Files[i].BlockSizeIndex != 0xFFFFFFFF)
                    foreach (ushort u in Files[i].BlockSizes)
                        fs.Write(BitConverter.GetBytes(u), 0, 2);
            Header.DataOffset = (uint)fs.Position;
            DebugLog.PrintLn("Appending Files...");
            uint pos = (uint)fs.Position;
            for (int i = 0; i < Header.FileCount; i++)
            {
                List<byte[]> blocks = GetBlocks(i);
                FileEntryStruct e = Files[i];
                DebugLog.PrintLn("Rebuilding \"" + e.FileName + "\" (" + (i + 1) + "/" + Header.FileCount + ") " + BytesToString(e.UncompressedSize) + " ...");
                e.DataOffset = pos;
                e.DataOffsetAdder = 0;
                for (int j = 0; j < blocks.Count; j++)
                {
                    MemoryStream m = new MemoryStream(blocks[j]);
                    fs.Write(m.ToArray(), 0, (int)m.Length);
                    pos += (uint)m.Length;
                }
                Files[i] = e;
            }
            DebugLog.PrintLn("Updating FileTable...");
            fs.Seek(0x20, 0);
            pos = (uint)fs.Position;
            uint blocksizeindex = 0;
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                if (e.BlockSizeIndex != 0xFFFFFFFF)
                {
                    fs.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                    e.BlockSizeIndex = blocksizeindex;
                    blocksizeindex += (uint)e.BlockSizes.Length;
                }
                else
                    fs.Write(BitConverter.GetBytes(0xFFFFFFFF), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
                e.MyOffset = pos;
                Files[i] = e;
                pos += 0x1E;
            }
            fs.Seek(0, 0);
            DebugLog.PrintLn("Rebuilding Header...");
            fs.Write(BitConverter.GetBytes(Header.Magic), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.Version), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.DataOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.EntryOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.FileCount), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.BlockTableOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.MaxBlockSize), 0, 4);
            foreach (char c in Header.CompressionScheme)
                fs.WriteByte((byte)c);
            fs.Close();
            File.Delete(MyFileName);
            File.Move(path, MyFileName);
        }
        private int FindTOC()
        {
            int f = -1;
            for (int i = 0; i < Header.FileCount; i++)
            {
                if (CompareByteArray(Files[i].Hash, TOCHash))
                    f = i;
            }
            return f;
        }
        public void DeleteEntry(int Index)
        {
            try
            {
                FileStream fs = new FileStream(MyFileName, FileMode.Open, FileAccess.Read);
                DebugLog.PrintLn("Searching TOC...");
                int f = FindTOC();
                if (f == -1)
                    return;
                DebugLog.PrintLn("Found TOC, deleting line...");
                MemoryStream m = DecompressEntry(f, fs);
                fs.Close();
                FileEntryStruct e = Files[Index];
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                string file = e.FileName + "\r\n";
                toc = toc.Replace(file, "");
                DebugLog.PrintLn("Replacing TOC...");
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), f);
                DebugLog.PrintLn("Deleting Entry from Filelist...");
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
                l.RemoveAt(Index);
                Files = l.ToArray();
                Header.FileCount--;
                DebugLog.PrintLn("Rebuilding...");
                ReBuild();
                DebugLog.PrintLn("Done.");
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("ERROR\n" + ex.Message);
            }
        }
        public void DeleteEntry(List<int> Index)
        {
            try
            {
                Index.Sort();
                Index.Reverse();
                FileStream fs = new FileStream(MyFileName, FileMode.Open, FileAccess.Read);
                DebugLog.PrintLn("Searching TOC...");
                int f = FindTOC();
                if (f == -1)
                    return;
                DebugLog.PrintLn("Found TOC, deleting lines...");
                MemoryStream m = DecompressEntry(f, fs);
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                for (int i = 0; i < Index.Count; i++)
                {
                    FileEntryStruct e = Files[Index[i]];
                    string file = e.FileName + "\r\n";
                    toc = toc.Replace(file, "");
                }
                DebugLog.PrintLn("Replacing TOC...");
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), f);
                DebugLog.PrintLn("Deleting Entry from Filelist...");
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
                for (int i = 0; i < Index.Count; i++)
                {
                    l.RemoveAt(Index[i]);
                    Header.FileCount--;
                }
                Files = l.ToArray();
                DebugLog.PrintLn("Rebuilding...");
                ReBuild();
                DebugLog.PrintLn("Done.");
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("ERROR\n" + ex.Message);
            }
        }
        public void AddFileQuick(string filein, string path)
        {
            try
            {
                string DLCPath = MyFileName;
                FileStream fs = new FileStream(DLCPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                byte[] FileIN = File.ReadAllBytes(filein);
                //Create Entry
                List<FileEntryStruct> tmp = new List<FileEntryStruct>(Files);
                FileEntryStruct e = new FileEntryStruct();
                e.FileName = path;
                e.BlockOffsets = new long[0];
                e.Hash = ComputeHash(path);
                e.BlockSizeIndex = 0xFFFFFFFF;
                e.UncompressedSize = (uint)FileIN.Length;
                e.UncompressedSizeAdder = 0;
                tmp.Add(e);
                e = new FileEntryStruct();
                Files = tmp.ToArray();
                //
                //Find TOC
                DebugLog.PrintLn("Searching TOC...");
                int f = FindTOC();
                if (f == -1)
                    return;
                DebugLog.PrintLn("Found TOC, adding line...");
                MemoryStream m = DecompressEntry(f, fs);
                //
                //Update TOC
                WriteString(m, path);
                m.WriteByte(0xD);
                m.WriteByte(0xA);
                //
                //Append new FileTable
                int count = (int)Header.FileCount + 1;
                long oldsize = fs.Length;
                long offset = oldsize;
                DebugLog.PrintLn("File End Offset : 0x" + offset.ToString("X10"));
                fs.Seek(oldsize, 0);
                Header.EntryOffset = (uint)offset;
                for (int i = 0; i < count; i++)
                {
                    e = Files[i];
                    fs.Write(e.Hash, 0, 16);
                    fs.Write(BitConverter.GetBytes(e.BlockSizeIndex), 0, 4);
                    fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                    fs.WriteByte(e.UncompressedSizeAdder);
                    fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                    fs.WriteByte(e.DataOffsetAdder);
                }
                offset += count * 0x1E;
                DebugLog.PrintLn("Table End Offset : 0x" + offset.ToString("X10"));
                Header.BlockTableOffset = (uint)offset;
                //
                //Append blocktable
                for (int i = 0; i < count; i++)
                {
                    e = Files[i];
                    if (e.BlockSizeIndex != 0xFFFFFFFF && i != f)
                        foreach (ushort u in e.BlockSizes)
                            fs.Write(BitConverter.GetBytes(u), 0, 2);
                }
                offset = fs.Length;
                DebugLog.PrintLn("Block Table End Offset : 0x" + offset.ToString("X10"));
                long dataoffset = offset;
                fs.Write(FileIN, 0, FileIN.Length);
                offset += FileIN.Length;
                DebugLog.PrintLn("New Data End Offset : 0x" + offset.ToString("X10"));
                //
                //Append TOC
                long tocoffset = offset;
                fs.Write(m.ToArray(), 0, (int)m.Length);
                offset = fs.Length;
                DebugLog.PrintLn("New TOC Data End Offset : 0x" + offset.ToString("X10"));
                //update filetable
                fs.Seek(oldsize, 0);
                uint blocksizeindex = 0;
                for (int i = 0; i < count; i++)
                {
                    e = Files[i];
                    fs.Write(e.Hash, 0, 16);
                    if (e.BlockSizeIndex == 0xFFFFFFFF || i == f)
                        fs.Write(BitConverter.GetBytes((int)-1), 0, 4);
                    else
                    {
                        fs.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                        e.BlockSizeIndex = (uint)blocksizeindex;
                        blocksizeindex += (uint)e.BlockSizes.Length;
                        Files[i] = e;
                    }
                    if (i == f)
                    {
                        fs.Write(BitConverter.GetBytes(m.Length), 0, 4);
                        fs.WriteByte(0);
                        fs.Write(BitConverter.GetBytes(tocoffset), 0, 4);
                        byte b = (byte)((tocoffset & 0xFF00000000) >> 32);
                        fs.WriteByte(b);
                    }
                    else if (i == count - 1)
                    {
                        fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                        fs.WriteByte(0);
                        fs.Write(BitConverter.GetBytes(dataoffset), 0, 4);
                        byte b = (byte)((dataoffset & 0xFF00000000) >> 32);
                        fs.WriteByte(b);
                    }
                    else
                    {
                        fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                        fs.WriteByte(e.UncompressedSizeAdder);
                        fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                        fs.WriteByte(e.DataOffsetAdder);
                    }
                }
                //Update Header
                fs.Seek(0xC, 0);
                fs.Write(BitConverter.GetBytes(Header.EntryOffset), 0, 4);
                fs.Write(BitConverter.GetBytes(count), 0, 4);
                fs.Write(BitConverter.GetBytes(Header.BlockTableOffset), 0, 4);
                //
                fs.Close();
            }
            catch (Exception ex)
            {
                DebugLog.PrintLn("DLCPACKAGE::ADDFILEQUICK ERROR:\n" + ex.Message);
            }
        }
        public void ReplaceEntry(string filein, int Index)
        {
            byte[] FileIN = File.ReadAllBytes(filein);
            ReplaceEntry(FileIN, Index);
        }
        public void ReplaceEntry(byte[] FileIN, int Index)
        {
            string DLCPath = MyFileName;
            FileStream fs = new FileStream(DLCPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs.Seek(0, SeekOrigin.End);
            uint offset = (uint)fs.Length;
            fs.Write(FileIN, 0, FileIN.Length);
            FileEntryStruct e = Files[Index];
            e.BlockSizes = new ushort[0];
            e.BlockOffsets = new long[1];
            e.BlockOffsets[0] = offset;
            e.BlockSizeIndex = 0xFFFFFFFF;
            e.DataOffset = offset;
            e.UncompressedSize = (uint)FileIN.Length;
            fs.Seek(e.MyOffset, 0);
            fs.Write(e.Hash, 0, 16);
            fs.Write(BitConverter.GetBytes(0xFFFFFFFF), 0, 4);
            fs.Write(BitConverter.GetBytes(FileIN.Length), 0, 4);
            fs.WriteByte(e.UncompressedSizeAdder);
            fs.Write(BitConverter.GetBytes(offset), 0, 4);
            fs.WriteByte(0);
            Files[Index] = e;
            fs.Close();
        }
        public void UpdateTOCbin(bool Rebuild = false)
        {
            DebugLog.PrintLn("File opened\nSearching TOCbin...");
            int f = -1;
            for (int i = 0; i < Files.Length; i++)
                if (Files[i].FileName.Contains("PCConsoleTOC.bin"))
                    f = i;
            if (f == -1)
            {
                DebugLog.PrintLn("Couldnt Find PCConsoleTOC.bin");
                return;
            }
            int IndexTOC = f;
            DebugLog.PrintLn("Found PCConsoleTOC.bin(" + f + ")!\nLoading Entries...");
            TOCBinFile TOC = new TOCBinFile(new MemoryStream(DecompressEntry(f).ToArray()));
            DebugLog.PrintLn("Checking Entries...");
            int count = 0;
            if (TOC.Entries == null)
                DebugLog.PrintLn("No TOC entries found. Oh dear...");
            for (int i = 0; i < TOC.Entries.Count; i++)
            {
                TOCBinFile.Entry e = TOC.Entries[i];
                f = -1;
                for (int j = 0; j < Files.Length; j++)
                    if (Files[j].FileName.Replace('/', '\\').Contains(e.name))
                        f = j;

                ////////////////////////////// KFREON TEMPORARY STUFF WV :)
                if (f == -1)
                {
                    List<string> parts = new List<string>(this.MyFileName.Split('\\'));
                    parts.RemoveAt(parts.Count - 1);
                    parts.RemoveAt(parts.Count - 1);
                    string path = String.Join("\\", parts) + "\\" + e.name;
                    if (File.Exists(path))
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length == e.size)
                            DebugLog.PrintLn((count++) + " : Entry is correct " + e.name);
                        else
                        {
                            e.size = (int)fi.Length;
                            DebugLog.PrintLn((count++) + " : Entry will be updated " + e.name);
                            TOC.Entries[i] = e;
                        }
                    }
                    else
                        DebugLog.PrintLn((count++) + " : Entry not found " + e.name);
                }  /////////////////////////////// END KFREON BLATHER
                else
                {
                    if (Files[f].UncompressedSize == e.size)
                        DebugLog.PrintLn((count++) + " : Entry is correct " + e.name);
                    else if (Files[f].UncompressedSize != e.size)
                    {
                        e.size = (int)Files[f].UncompressedSize;
                        DebugLog.PrintLn((count++) + " : Entry will be updated " + e.name);
                        TOC.Entries[i] = e;
                    }
                }
            }
            DebugLog.PrintLn("Replacing TOC back...");
            ReplaceEntry(TOC.Save().ToArray(), IndexTOC);
            if (Rebuild)
            {
                DebugLog.PrintLn("Reopening SFAR...");
                Load(MyFileName);
                DebugLog.PrintLn("Rebuild...");
                ReBuild();
            }
        }
        public TreeNode ToTree()
        {
            TreeNode result = new TreeNode(MyFileName);
            result.Nodes.Add(Header.ToTree());
            TreeNode t = new TreeNode("FileEntries");
            for (int i = 0; i < Header.FileCount; i++)
                t.Nodes.Add(Files[i].ToTree(i));
            result.Nodes.Add(t);
            return result;
        }

        public void Serialize(SerializingContainer con)
        {
            if (con.isLoading)
                Header = new HeaderStruct();
            DebugLog.PrintLn("Header...");
            Header.Serialize(con);
            con.Seek((int)Header.EntryOffset, SeekOrigin.Begin);
            if (con.isLoading)
                Files = new FileEntryStruct[Header.FileCount];
            DebugLog.PrintLn("Files...");
            for (int i = 0; i < Header.FileCount; i++)
                Files[i].Serialize(con, Header);
            if (con.isLoading)
                ReadFileNames(con);
        }
        public List<byte[]> GetBlocks(int Index)
        {
            List<byte[]> res = new List<byte[]>();
            FileEntryStruct e = Files[Index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            FileStream fs = new FileStream(MyFileName, FileMode.Open, FileAccess.Read);
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                res.Add(buff);
                fs.Close();
                return res;
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = (uint)e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        buff = new byte[compressedBlockSize];
                        fs.Read(buff, 0, buff.Length);
                        res.Add(buff);
                        left -= compressedBlockSize;
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(left, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            DebugLog.PrintLn("DLCPACKAGE::GETBLOCKS ERROR: compressed block size smaller than 5");
                            break;
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        res.Add(inputBlock);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            fs.Close();
            return res;
        } 
        private bool CompareByteArray(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;
            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;
            return true;
        }
        public void ReadFileNames(SerializingContainer con)
        {
            FileEntryStruct e;
            int f = -1;
            for (int i = 0; i < Header.FileCount; i++)
            {
                e = Files[i];
                e.FileName = "UNKNOWN";
                Files[i] = e;
                if (CompareByteArray(Files[i].Hash, TOCHash))
                    f = i;
            }
            if (f == -1)
                return;
            MemoryStream m = DecompressEntry(f);
            m.Seek(0, 0);
            StreamReader r = new StreamReader(m);
            while (!r.EndOfStream)
            {
                string line = r.ReadLine();
                byte[] hash = ComputeHash(line);
                f = -1;
                for (int i = 0; i < Header.FileCount; i++)
                    if (CompareByteArray(Files[i].Hash, hash))
                        f = i;
                if (f != -1)
                {
                    e = Files[f];
                    e.FileName = line;
                    Files[f] = e;
                }
            }
        }
        public MemoryStream DecompressEntry(int Index)
        {
            FileStream fs = new FileStream(MyFileName, FileMode.Open, FileAccess.Read);
            return DecompressEntry(Index, fs);
        }
        public MemoryStream DecompressEntry(int Index, FileStream fs)
        {
            MemoryStream result = new MemoryStream();
            FileEntryStruct e = Files[Index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                result.Write(buff, 0, buff.Length);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = (uint)e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        buff = new byte[compressedBlockSize];
                        fs.Read(buff, 0, buff.Length);
                        result.Write(buff, 0, buff.Length);
                        left -= compressedBlockSize;
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(left, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            DebugLog.PrintLn("DLCPACKAGE::DECOMPRESSENTRY ERROR:compressed block size smaller than 5");
                            break;
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        uint actualUncompressedBlockSize = uncompressedBlockSize;
                        uint actualCompressedBlockSize = compressedBlockSize;
                        outputBlock = SevenZipHelper.Decompress(inputBlock, (int)actualUncompressedBlockSize);
                        if (outputBlock.Length != actualUncompressedBlockSize)
                        {
                            DebugLog.PrintLn("DLCPACKAGE::DECOMPRESSENTRY ERROR:Decompression Error");
                            break;
                        }
                        result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            fs.Close();
            return result;
        }
        public static byte[] ComputeHash(string input)
        {
            byte[] bytes = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
                bytes[i] = (byte)Sanitize(input[i]);
            var md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(bytes);
        }
        public static char Sanitize(char c)
        {
            switch ((ushort)c)
            {
                case 0x008C: return (char)0x9C;
                case 0x009F: return (char)0xFF;
                case 0x00D0:
                case 0x00DF:
                case 0x00F0:
                case 0x00F7: return c;
            }
            if ((c >= 'A' && c <= 'Z') || (c >= 'À' && c <= 'Þ'))
                return char.ToLowerInvariant(c);
            return c;
        }
        public void WriteString(MemoryStream m, string s)
        {
            foreach (char c in s)
                m.WriteByte((byte)c);
        }
        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}
