using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal
{
    public class DLCPackage
    {
        public string FileName;
        public struct HeaderStruct
        {
            //public uint Magic;
            public uint Version;
            public uint DataOffset;
            public uint EntryOffset;
            public uint FileCount;
            public uint BlockTableOffset;
            public uint MaxBlockSize;
            public string CompressionScheme;
            internal void Serialize(SerializingFile con)
            {
                //Magic = magic;
                Version = con + Version;
                DataOffset = con + DataOffset;
                EntryOffset = con + EntryOffset;
                FileCount = con + FileCount;
                BlockTableOffset = con + BlockTableOffset;
                MaxBlockSize = con + MaxBlockSize;
                if (con.isLoading)
                    CompressionScheme = con.Memory.BaseStream.ReadStringLatin1(4).Trim();
                if (//Magic != 0x53464152 ||
                    Version != 0x00010000 ||
                    MaxBlockSize != 0x00010000)
                    throw new Exception("Not supported DLC file!");
            }
        }
        public struct FileEntryStruct
        {
            public HeaderStruct Header;
            public uint MyOffset;
            public byte[] Hash;
            public uint BlockSizeTableIndex;
            public uint UncompressedSize;
            public byte UncompressedSizeAdder;
            public long RealUncompressedSize { get; set; }
            public uint DataOffset;
            public byte DataOffsetAdder;
            /// <summary>
            /// Where the (compressed and uncompressed) data actually resides in the SFAR file
            /// </summary>
            public long RealDataOffset { get; set; }
            public long BlockTableOffset;
            public long[] BlockOffsets;
            public ushort[] BlockSizes;
            public string FileName { get; set; }
            public bool isActualFile;

            internal void Serialize(SerializingFile con, HeaderStruct header)
            {
                Header = header;
                MyOffset = (uint)con.GetPos();
                isActualFile = true; //default to true
                if (con.isLoading)
                    Hash = new byte[16];
                for (int i = 0; i < 16; i++)
                    Hash[i] = con + Hash[i];
                BlockSizeTableIndex = con + BlockSizeTableIndex;
                if (con.Memory.Endian == Endian.Big)
                {
                    UncompressedSizeAdder = con + UncompressedSizeAdder;
                    UncompressedSize = con + UncompressedSize;
                    DataOffsetAdder = con + DataOffsetAdder;
                    DataOffset = con + DataOffset;
                }
                else
                {
                    UncompressedSize = con + UncompressedSize;
                    UncompressedSizeAdder = con + UncompressedSizeAdder;
                    DataOffset = con + DataOffset;
                    DataOffsetAdder = con + DataOffsetAdder;
                }
                RealUncompressedSize = UncompressedSize + UncompressedSizeAdder; //<< 32

                RealDataOffset = DataOffset + DataOffsetAdder; // << 32
                if (BlockSizeTableIndex == 0xFFFFFFFF) //Decompressed
                {
                    BlockOffsets = new long[1];
                    BlockOffsets[0] = RealDataOffset;
                    BlockSizes = new ushort[1];
                    BlockSizes[0] = (ushort)UncompressedSize;
                    BlockTableOffset = 0;
                }
                else //Compressed
                {
                    int numBlocks = (int)Math.Ceiling(UncompressedSize / (double)header.MaxBlockSize);
                    if (con.isLoading)
                    {
                        BlockOffsets = new long[numBlocks];
                        BlockSizes = new ushort[numBlocks];
                    }

                    if (numBlocks > 0)
                    {
                        BlockOffsets[0] = RealDataOffset;
                        long pos = con.Memory.Position;
                        con.Seek((int)GetBlockOffset((int)BlockSizeTableIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
                        BlockTableOffset = con.Memory.Position;
                        BlockSizes[0] = con + BlockSizes[0];
                        for (int i = 1; i < numBlocks; i++) //read any further blocks
                        {
                            BlockSizes[i] = con + BlockSizes[i];
                            BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
                        }

                        con.Seek((int)pos, SeekOrigin.Begin);
                    }
                }
            }

            private static long GetBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
            {
                return entryOffset + (numEntries * 0x1E) + (blockIndex * 2);
            }
        }

        public static ReadOnlySpan<byte> TOCHash => [0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C];

        public HeaderStruct Header;
        public ObservableCollectionExtended<FileEntryStruct> Files { get; } = [];

        public long UncompressedSize
        {
            get
            {
                long size = 0;
                foreach (var file in Files)
                {
                    size += file.RealUncompressedSize;
                }
                return size;
            }
        }

        public DLCPackage(string FileName)
        {
            Load(FileName);
        }

        public void Load(string FileName)
        {
            this.FileName = FileName;
            var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            //var rafs = 0x52414653;
            SerializingFile con = new SerializingFile(EndianReader.SetupForReading(fs, 0x53464152, out int magic));
            Serialize(con);
            con.Memory.Close();
        }

        private void Serialize(SerializingFile con)
        {
            if (con.isLoading)
                Header = new HeaderStruct();
            Header.Serialize(con);
            con.Seek((int)Header.EntryOffset, SeekOrigin.Begin);
            for (int i = 0; i < Header.FileCount; i++)
            {
                //Debug.WriteLine($"Serialize sfar file {i} at 0x{con.Memory.Position:X8}");
                var feStruct = new FileEntryStruct();
                feStruct.Serialize(con, Header);
                Files.Add(feStruct);
                //Debug.WriteLine($"Data offset for {i}: 0x{Files[i].DataOffset:X8} (0x{Files[i].RealDataOffset:X8}), header at 0x{pos:X8}");
            }

            //var ordered = Files.OrderBy(x => x.DataOffset).ToList();
            //foreach (var f in ordered)
            //{
            //    Debug.WriteLine($"0x{f.DataOffset:X8} (0x{f.RealDataOffset:X8}), header at {f.MyOffset:X8}");
            //}
            //var firstfile = Files.MinBy(x => x.RealDataOffset);

            if (con.isLoading)
                ReadFileNames();
        }

        private const string FILENAMES_FILENAME = "Filenames.txt (this file has no real name)";

        public void ReadFileNames()
        {
            FileEntryStruct e;
            int f = -1;
            for (int i = 0; i < Header.FileCount; i++)
            {
                e = Files[i];
                e.FileName = FILENAMES_FILENAME;
                Files[i] = e;
                if (Files[i].Hash.AsSpan().SequenceEqual(TOCHash))
                    f = i;
            }
            if (f == -1)
                return;
            var fFile = Files[f];
            fFile.FileName = FILENAMES_FILENAME;
            fFile.isActualFile = false;
            Files[f] = fFile;
            try
            {
                MemoryStream m = DecompressEntry(f);
                m.Seek(0, 0);
                StreamReader r = new StreamReader(m);
                while (!r.EndOfStream)
                {
                    string line = r.ReadLine();
                    byte[] hash = ComputeHash(line);
                    f = -1;
                    for (int i = 0; i < Header.FileCount; i++)
                        if (Files[i].Hash.AsSpan().SequenceEqual(hash))
                            f = i;
                    if (f != -1)
                    {
                        e = Files[f];
                        e.FileName = line;
                        Files[f] = e;
                    }
                }
            }
            catch (Exception)
            {
                // Can't read names

            }
        }

        public List<byte[]> GetBlocks(int Index)
        {
            List<byte[]> res = [];
            FileEntryStruct e = Files[Index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeTableIndex == 0xFFFFFFFF)
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
                    uint compressedBlockSize = e.BlockSizes[count];
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
                            throw new Exception("compressed block size smaller than 5");
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

        public MemoryStream DecompressEntry(int Index)
        {
            return DecompressEntry(Files[Index]);
        }

        public MemoryStream DecompressEntry(FileEntryStruct e)
        {
            //Debug.WriteLine("Decompressing " + e.FileName);
            MemoryStream result = MemoryManager.GetMemoryStream();
            uint count = 0;
            byte[] inputBlock;
            long left = e.RealUncompressedSize;
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeTableIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                result.Write(buff, 0, buff.Length);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = e.BlockSizes[count];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == left)
                    {
                        //uncompressed?
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
                            throw new Exception("compressed block size smaller than 5");
                        }

                        inputBlock = new byte[compressedBlockSize];
                        //Debug.WriteLine($"Decompressing at 0x{fs.Position:X8}");
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        uint actualUncompressedBlockSize = uncompressedBlockSize;
                        if (Header.CompressionScheme == "amzl"  /* PC */|| Header.CompressionScheme == "lzma" /* PS3 (it doesn't appear to actually be LZMA!), WiiU */)
                        {
                            //if (Header.CompressionScheme == "lzma")
                            //{
                            //PS3 - This doesn't work. I'm not sure what kind of LZMA this uses but it has seemingly no header
                            //var attachedHeader = new byte[inputBlock.Length + 5];
                            //attachedHeader[0] = 0x5D;
                            ////attachedHeader[1] = (byte) (Header.Version >> 24);
                            ////attachedHeader[2] = (byte)(Header.Version >> 16); 
                            ////attachedHeader[3] = (byte)(Header.Version >> 8);
                            ////attachedHeader[4] = (byte) Header.Version;
                            //attachedHeader[1] = (byte)Header.Version;
                            //attachedHeader[2] = (byte)(Header.Version >> 8);
                            //attachedHeader[3] = (byte)(Header.Version >> 16);
                            //attachedHeader[4] = (byte)(Header.Version >> 24);
                            //Buffer.BlockCopy(inputBlock,0,attachedHeader,5, inputBlock.Length);
                            //inputBlock = attachedHeader;
                            //}

                            var outputBlock = LZMA.Decompress(inputBlock, actualUncompressedBlockSize);
                            if (outputBlock.Length != actualUncompressedBlockSize)
                                throw new Exception("SFAR LZMA Decompression Error");
                            result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                            left -= uncompressedBlockSize;
                            //continue;
                        }

                        if (Header.CompressionScheme == "lzx") //Xbox
                        {
                            var outputBlock = new byte[actualUncompressedBlockSize];
                            var decompResult = LZX.Decompress(inputBlock, (uint)inputBlock.Length, outputBlock);
                            if (decompResult != 0)
                                throw new Exception("SFAR LZX Decompression Error");
                            result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                            left -= uncompressedBlockSize;
                        }
                    }
                    count++;
                }
            }
            fs.Close();
            result.Position = 0;
            return result;
        }

        public SFAREntryReader GetEntryReader(int index)
        {
            return new SFAREntryReader(this, index);
        }

        internal class InputBlock
        {
            public const long Uncompressed = -1;

            public byte[] Data { get; }
            public long UncompressedSize { get; }
            public bool IsCompressed { get; }

            public InputBlock(byte[] data, long uncompressedSize)
            {
                Data = data;
                UncompressedSize = uncompressedSize;
                IsCompressed = uncompressedSize > 0;
            }
        }

        public MemoryStream DecompressEntry(int Index, FileStream fs)
        {
            MemoryStream result = MemoryManager.GetMemoryStream();
            FileEntryStruct e = Files[Index];
            uint count = 0;
            byte[] inputBlock;
            byte[] outputBlock = new byte[Header.MaxBlockSize];
            long left = e.RealUncompressedSize;
            fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
            byte[] buff;
            if (e.BlockSizeTableIndex == 0xFFFFFFFF)
            {
                buff = new byte[e.RealUncompressedSize];
                fs.Read(buff, 0, buff.Length);
                result.Write(buff, 0, buff.Length);
            }
            else
            {
                while (left > 0)
                {
                    uint compressedBlockSize = e.BlockSizes[count];
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
                            throw new Exception("compressed block size smaller than 5");
                        }
                        inputBlock = new byte[compressedBlockSize];
                        fs.Read(inputBlock, 0, (int)compressedBlockSize);
                        uint actualUncompressedBlockSize = uncompressedBlockSize;
                        uint actualCompressedBlockSize = compressedBlockSize;

                        outputBlock = LZMA.Decompress(inputBlock, actualUncompressedBlockSize);
                        if (outputBlock.Length != actualUncompressedBlockSize)
                            throw new Exception("Decompression Error");
                        result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                        left -= uncompressedBlockSize;
                    }
                    count++;
                }
            }
            return result;
        }

        public static byte[] ComputeHash(string input)
        {
            byte[] bytes = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
                bytes[i] = (byte)Sanitize(input[i]);
            return System.Security.Cryptography.MD5.HashData(bytes);
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
            ReadOnlySpan<string> suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num) + suf[place];
        }

        public void ReBuild()
        {
            string path = Path.Combine(Path.GetDirectoryName(FileName), Path.GetFileNameWithoutExtension(FileName) + ".tmp");
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            Debug.WriteLine("Creating Header Dummy...");
            for (int i = 0; i < 8; i++)
                fs.Write(BitConverter.GetBytes(0), 0, 4);
            Header.EntryOffset = 0x20;
            Debug.WriteLine("Creating File Table...");
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                fs.Write(BitConverter.GetBytes(e.BlockSizeTableIndex), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
            }
            Header.BlockTableOffset = (uint)fs.Position;
            Debug.WriteLine("Creating Block Table...");
            for (int i = 0; i < Header.FileCount; i++)
                if (Files[i].BlockSizeTableIndex != 0xFFFFFFFF)
                    foreach (ushort u in Files[i].BlockSizes)
                        fs.Write(BitConverter.GetBytes(u), 0, 2);
            Header.DataOffset = (uint)fs.Position;
            Debug.WriteLine("Appending Files...");
            uint pos = (uint)fs.Position;
            for (int i = 0; i < Header.FileCount; i++)
            {
                List<byte[]> blocks = GetBlocks(i);
                FileEntryStruct e = Files[i];
                Debug.WriteLine("Rebuilding \"" + e.FileName + "\" (" + (i + 1) + "/" + Header.FileCount + ") " + BytesToString(e.UncompressedSize) + " ...");
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
            Debug.WriteLine("Updating FileTable...");
            fs.Seek(0x20, 0);
            pos = (uint)fs.Position;
            uint blocksizeindex = 0;
            for (int i = 0; i < Header.FileCount; i++)
            {
                FileEntryStruct e = Files[i];
                fs.Write(e.Hash, 0, 16);
                if (e.BlockSizeTableIndex != 0xFFFFFFFF)
                {
                    fs.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                    e.BlockSizeTableIndex = blocksizeindex;
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
            Debug.WriteLine("Rebuilding Header...");
            //magic
            fs.Write(BitConverter.GetBytes(0x53464152), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.Version), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.DataOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.EntryOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.FileCount), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.BlockTableOffset), 0, 4);
            fs.Write(BitConverter.GetBytes(Header.MaxBlockSize), 0, 4);
            foreach (char c in Header.CompressionScheme)
                fs.WriteByte((byte)c);
            fs.Close();
            File.Delete(FileName);
            File.Move(path, FileName);
        }

        private int FindTOC()
        {
            int f = -1;
            for (int i = 0; i < Header.FileCount; i++)
            {
                if (Files[i].Hash.AsSpan().SequenceEqual(TOCHash))
                    f = i;
            }
            return f;
        }

        public void DeleteEntry(int Index)
        {
            try
            {
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                Debug.WriteLine("Searching TOC...");
                int f = FindTOC();
                if (f == -1)
                    return;
                Debug.WriteLine("Found TOC, deleting line...");
                MemoryStream m = DecompressEntry(f, fs);
                fs.Close();
                FileEntryStruct e = Files[Index];
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                string file = e.FileName + "\r\n";
                toc = toc.Replace(file, "");
                Debug.WriteLine("Replacing TOC...");
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), f);
                Debug.WriteLine("Deleting Entry from Filelist...");
                List<FileEntryStruct> l = [.. Files];
                l.RemoveAt(Index);
                Files.ReplaceAll(l);
                Header.FileCount--;
                Debug.WriteLine("Rebuilding...");
                ReBuild();
                Debug.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR\n" + ex.Message);
            }
        }

        public void DeleteEntries(List<int> Index)
        {
            try
            {
                Index.Sort();
                Index.Reverse();
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                Debug.WriteLine("Searching TOC...");
                int f = FindTOC();
                if (f == -1)
                    return;
                Debug.WriteLine("Found TOC, deleting lines...");
                MemoryStream m = DecompressEntry(f, fs);
                string toc = Encoding.UTF8.GetString(m.ToArray(), 0, (int)m.Length);
                fs.Close();
                for (int i = 0; i < Index.Count; i++)
                {
                    FileEntryStruct e = Files[Index[i]];
                    string file = e.FileName + "\r\n";
                    toc = toc.Replace(file, "");
                }
                Debug.WriteLine("Replacing TOC...");
                ReplaceEntry(Encoding.ASCII.GetBytes(toc), f);
                Debug.WriteLine("Deleting Entry from Filelist...");
                List<FileEntryStruct> l = [.. Files];
                for (int i = 0; i < Index.Count; i++)
                {
                    l.RemoveAt(Index[i]);
                    Header.FileCount--;
                }
                Files.ReplaceAll(l);
                Debug.WriteLine("Rebuilding...");
                ReBuild();
                Debug.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR\n" + ex.Message);
            }
        }

        public void AddFileQuick(string filein, string path)
        {
            string DLCPath = FileName;
            FileStream fs = new FileStream(DLCPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            byte[] FileIN = File.ReadAllBytes(filein);
            //Create Entry
            List<FileEntryStruct> tmp = new List<FileEntryStruct>(Files);
            FileEntryStruct e = new FileEntryStruct
            {
                FileName = path,
                BlockOffsets = [],
                Hash = ComputeHash(path),
                BlockSizeTableIndex = 0xFFFFFFFF,
                UncompressedSize = (uint)FileIN.Length,
                UncompressedSizeAdder = 0
            };
            tmp.Add(e);
            e = new FileEntryStruct();
            Files.ReplaceAll(tmp);
            //
            //Find TOC
            Debug.WriteLine("Searching TOC...");
            int f = FindTOC();
            if (f == -1)
                return;
            Debug.WriteLine("Found TOC, adding line...");
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
            Debug.WriteLine("File End Offset : 0x" + offset.ToString("X10"));
            fs.Seek(oldsize, 0);
            Header.EntryOffset = (uint)offset;
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                fs.Write(e.Hash, 0, 16);
                fs.Write(BitConverter.GetBytes(e.BlockSizeTableIndex), 0, 4);
                fs.Write(BitConverter.GetBytes(e.UncompressedSize), 0, 4);
                fs.WriteByte(e.UncompressedSizeAdder);
                fs.Write(BitConverter.GetBytes(e.DataOffset), 0, 4);
                fs.WriteByte(e.DataOffsetAdder);
            }
            offset += count * 0x1E;
            Debug.WriteLine("Table End Offset : 0x" + offset.ToString("X10"));
            Header.BlockTableOffset = (uint)offset;
            //
            //Append blocktable
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                if (e.BlockSizeTableIndex != 0xFFFFFFFF && i != f)
                    foreach (ushort u in e.BlockSizes)
                        fs.Write(BitConverter.GetBytes(u), 0, 2);
            }
            offset = fs.Length;
            Debug.WriteLine("Block Table End Offset : 0x" + offset.ToString("X10"));
            long dataoffset = offset;
            fs.Write(FileIN, 0, FileIN.Length);
            offset += FileIN.Length;
            Debug.WriteLine("New Data End Offset : 0x" + offset.ToString("X10"));
            //
            //Append TOC
            long tocoffset = offset;
            fs.Write(m.ToArray(), 0, (int)m.Length);
            offset = fs.Length;
            Debug.WriteLine("New TOC Data End Offset : 0x" + offset.ToString("X10"));
            //update filetable
            fs.Seek(oldsize, 0);
            uint blocksizeindex = 0;
            for (int i = 0; i < count; i++)
            {
                e = Files[i];
                fs.Write(e.Hash, 0, 16);
                if (e.BlockSizeTableIndex == 0xFFFFFFFF || i == f)
                    fs.Write(BitConverter.GetBytes(-1), 0, 4);
                else
                {
                    fs.Write(BitConverter.GetBytes(blocksizeindex), 0, 4);
                    e.BlockSizeTableIndex = blocksizeindex;
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

        public void ReplaceEntry(string sourceFileOnDisk, int entryIndex)
        {
            byte[] fileBytes;
            if (string.Equals(Path.GetExtension(sourceFileOnDisk), ".pcc", StringComparison.OrdinalIgnoreCase) && FileName.EndsWith("Patch_001.sfar", StringComparison.InvariantCultureIgnoreCase))
            {
                //if (FileName.Contains("Patch_001")) Debugger.Break();
                //Use the decompressed bytes - SFARs can't store compressed packages apparently!
                var package = MEPackageHandler.OpenMEPackage(sourceFileOnDisk);
                if (package.IsCompressed)
                {
                    fileBytes = package.SaveToStream(false).ToArray();
                }
                else
                {
                    fileBytes = File.ReadAllBytes(sourceFileOnDisk);
                }
            }
            else
            {
                fileBytes = File.ReadAllBytes(sourceFileOnDisk);
            }
            ReplaceEntry(fileBytes, entryIndex);
        }

        public void ReplaceEntry(byte[] newData, int entryIndex)
        {
            FileStream fs = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            FileEntryStruct e = Files[entryIndex];
            if (e.BlockSizeTableIndex == 0xFFFFFFFF && e.RealUncompressedSize == newData.Length)
            {
                //overwrite existing data, but only if already uncompressed!
                fs.Seek(e.RealDataOffset, SeekOrigin.Begin);
            }
            else
            {
                // It won't fit. Append it to the end instead
                fs.Seek(0, SeekOrigin.End);
            }

            uint offset = (uint)fs.Position;
            //append data
            fs.Write(newData, 0, newData.Length);

            //uncompressed entry
            e.BlockSizes = [];
            e.BlockOffsets = new long[1];
            e.BlockOffsets[0] = offset;
            e.BlockSizeTableIndex = 0xFFFFFFFF;
            e.DataOffset = offset;
            e.UncompressedSize = (uint)newData.Length;

            fs.Seek(e.MyOffset, 0);
            fs.Write(e.Hash, 0, 16);
            fs.Write(BitConverter.GetBytes(0xFFFFFFFF), 0, 4);
            fs.Write(BitConverter.GetBytes(newData.Length), 0, 4);
            fs.WriteByte(e.UncompressedSizeAdder);
            fs.Write(BitConverter.GetBytes(offset), 0, 4);
            fs.WriteByte(0);
            Files[entryIndex] = e;
            fs.Close();
        }

        public enum DLCTOCUpdateResult
        {
            RESULT_UPDATED,
            RESULT_UPDATE_NOT_NECESSARY,
            RESULT_ERROR_NO_ENTRIES,
            RESULT_ERROR_NO_TOC
        }

        public DLCTOCUpdateResult UpdateTOCbin()
        {
            int archiveFileIndex = -1;
            for (int i = 0; i < Files.Count; i++)
            {
                if (Path.GetFileName(Files[i].FileName) == @"PCConsoleTOC.bin")
                {
                    archiveFileIndex = i;
                    break;
                }
            }

            if (archiveFileIndex == -1)
            {
                Debug.WriteLine(@"Couldn't find PCConsoleTOC.bin in SFAR");
                return DLCTOCUpdateResult.RESULT_ERROR_NO_TOC;
            }

            //Collect list of information from the SFAR Header of files and their sizes
            var incomingNewEntries = new List<(string filepath, int size)>();
            foreach (var file in Files)
            {
                if (file.FileName != FILENAMES_FILENAME)
                {
                    string consoleDirFilename = file.FileName.Substring(file.FileName.IndexOf(@"DLC_", StringComparison.InvariantCultureIgnoreCase));
                    consoleDirFilename = consoleDirFilename.Substring(consoleDirFilename.IndexOf('/') + 1);
                    incomingNewEntries.Add((consoleDirFilename.Replace('/', '\\'), (int)file.UncompressedSize));
                }
            }

            //Read the current TOC and see if an update is necessary.
            bool tocNeedsUpdating = false;

            var tocMemoryStream = DecompressEntry(archiveFileIndex);
            TOCBinFile toc = new TOCBinFile(tocMemoryStream);

            var allEntries = toc.GetAllEntries();
            int actualTocEntries = allEntries.Count;
            actualTocEntries -= allEntries.Count(x => x.name.EndsWith(@"PCConsoleTOC.txt", StringComparison.InvariantCultureIgnoreCase));
            actualTocEntries -= allEntries.Count(x => x.name.EndsWith(@"GlobalPersistentCookerData.upk", StringComparison.InvariantCultureIgnoreCase));
            if (actualTocEntries != incomingNewEntries.Count)
            {
                tocNeedsUpdating = true;
            }
            else
            {
                //Check sizes to see if all of ours match.
                foreach (var existingEntry in allEntries)
                {
                    if (existingEntry.name.EndsWith(@"PCConsoleTOC.txt", StringComparison.InvariantCultureIgnoreCase) || existingEntry.name.EndsWith("GlobalPersistentCookerData.upk", StringComparison.InvariantCultureIgnoreCase)) continue; //These files don't actually exist in SFARs
                    var matchingNewEntry = incomingNewEntries.FirstOrDefault(x => x.filepath.Equals(existingEntry.name, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingNewEntry.filepath == null)
                    {
                        //same number of files but we could not find it in the list. A delete and add might have caused this.
                        tocNeedsUpdating = true;
                        break;
                    }
                    if (matchingNewEntry.size != existingEntry.size)
                    {
                        //size is different.
                        tocNeedsUpdating = true;
                        break;
                    }
                }
            }

            //DEBUG TESTING!
            if (tocNeedsUpdating/* || FileName.Contains(@"Patch_001")*/)
            {
                MemoryStream newTocStream = TOCCreator.CreateTOCForEntries(incomingNewEntries);
                byte[] newmem = newTocStream.ToArray();
                //if (tocMemoryStream.ToArray().SequenceEqual(newTocStream.ToArray()))
                //{
                //    //no update needed
                //    return DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY;
                //}

                if (newmem.Length == 0) Debugger.Break();
                ReplaceEntry(newmem, archiveFileIndex);
            }
            else
            {
                return DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY; // no update needed
            }
            return DLCTOCUpdateResult.RESULT_UPDATED;
        }

        public int FindFileEntry(string fileName)
        {
            return Files.IndexOf(Files.FirstOrDefault(x => x.FileName.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)));
        }

        private void DecompressBlock(FileStream fs, FileEntryStruct entry, int blockIndex, ref long remainingUncompSize, MemoryStream decompressedData)
        {
            var uncompressedBlockSize = (uint)Math.Min(remainingUncompSize, Header.MaxBlockSize);
            uint compressedBlockSize = entry.BlockSizes[blockIndex];
            if (compressedBlockSize == 0)
                compressedBlockSize = Header.MaxBlockSize;

            uint actualUncompressedBlockSize = uncompressedBlockSize;
            var buff = fs.ReadToBuffer((int)compressedBlockSize);
            var outputBlock = LZMA.Decompress(buff, actualUncompressedBlockSize);
            if (outputBlock.Length != actualUncompressedBlockSize)
                throw new Exception("Decompression Error");

            decompressedData.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
            remainingUncompSize -= uncompressedBlockSize;
        }

        /// <summary>
        /// Reads a specific piece of data from the listed entry. Only works on PC SFARs.
        /// </summary>
        /// <param name="entryIdx">The entry IDX to read from</param>
        /// <param name="uncompressedOffsetInEntry">The offset, as if the file was uncompressed, to read at.</param>
        /// <param name="uncompressedAmountToRead">The amount of uncompressed data to read.</param>
        /// <returns>Byte array of uncompressed data</returns>
        public byte[] ReadFromEntry(int entryIdx, int uncompressedOffsetInEntry, int uncompressedAmountToRead)
        {
            var entry = Files[entryIdx];
            using FileStream fs = File.OpenRead(FileName);
            if (entry.BlockSizeTableIndex == 0xFFFFFFFF)
            {
                // It's stored uncompressed already. Just read the data directly.
                fs.Position = entry.RealDataOffset + uncompressedOffsetInEntry;
                return fs.ReadToBuffer(uncompressedAmountToRead);
            }
            else
            {
                MemoryStream decompressedData = MemoryManager.GetMemoryStream();
                fs.Seek(entry.BlockOffsets[0], SeekOrigin.Begin);

                // Seek to the first block we must decompress that contains the data offset we are looking for
                int position = 0;
                int startUncompPosition = 0;
                var totalEntryUncompSize = entry.RealUncompressedSize;
                bool hasBegunReading = false;

                // Seek to data start
                fs.Seek(entry.BlockOffsets[0], SeekOrigin.Begin);

                for (int i = 0; i < entry.BlockSizes.Length; i++)
                {
                    uint compressedBlockSize = entry.BlockSizes[i];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = Header.MaxBlockSize;
                    if (compressedBlockSize == Header.MaxBlockSize || compressedBlockSize == entry.RealUncompressedSize)
                    {
                        // This block is actually uncompressed. How fun

                        if (!hasBegunReading && (position <= uncompressedOffsetInEntry) && (position + uncompressedAmountToRead > uncompressedOffsetInEntry))
                        {
                            // We have found the first block we must decompress
                            Debug.WriteLine($@"Begin read at 0x{position:X8}");
                            startUncompPosition = position;
                            hasBegunReading = true;
                        }

                        // If position (uncomp) > uncompressedoffset + size, we no longer need to read anything.
                        if (position >= uncompressedOffsetInEntry + uncompressedAmountToRead)
                        {
                            Debug.WriteLine($@"End read at 0x{position:X8}");
                            break;
                        }

                        position += (int)compressedBlockSize; // It's not compressed
                        if (hasBegunReading)
                        {
                            DecompressBlock(fs, entry, i, ref totalEntryUncompSize, decompressedData);
                        }
                        else
                        {
                            // Skip
                            fs.Seek(compressedBlockSize, SeekOrigin.Current);
                        }
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(totalEntryUncompSize, Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            throw new Exception("compressed block size smaller than 5");
                        }

                        // Is the offset in this block?
                        if (!hasBegunReading && (position <= uncompressedOffsetInEntry) && (position + uncompressedBlockSize > uncompressedOffsetInEntry))
                        {
                            // We have found the first block we must decompress
                            Debug.WriteLine($@"Begin read at 0x{position:X8}");
                            startUncompPosition = position;
                            hasBegunReading = true;
                        }

                        // If position (uncomp) > uncompressedoffset + size, we no longer need to read anything.
                        if (position >= uncompressedOffsetInEntry + uncompressedAmountToRead)
                        {
                            Debug.WriteLine($@"End read at 0x{position:X8}");
                            break;
                        }

                        if (hasBegunReading)
                        {
                            DecompressBlock(fs, entry, i, ref totalEntryUncompSize, decompressedData);
                        }
                        else
                        {
                            // Skip
                            fs.Seek(compressedBlockSize, SeekOrigin.Current);
                        }

                        position += (int)uncompressedBlockSize;
                    }
                }

                decompressedData.Position = uncompressedOffsetInEntry - startUncompPosition; // We may start at position > 0
                return decompressedData.ReadToBuffer(uncompressedAmountToRead);
            }
        }
    }

    /// <summary>
    /// Reads an entry from an SFAR file, but only in areas required for seeking, to minimize amount of reading.
    /// This is useful for large entries such as TFC when only a few entries need read
    /// </summary>
    public class SFAREntryReader
    {
        private readonly DLCPackage dpackage;
        private DLCPackage.FileEntryStruct entry;
        public byte[] ReadUncompressedSize(int uncompressedOffsetInEntry, int uncompressedAmountToRead)
        {
            using FileStream fs = File.OpenRead(dpackage.FileName);
            if (entry.BlockSizeTableIndex == 0xFFFFFFFF)
            {
                // It's stored uncompressed already. Just read the data directly.
                fs.Position = entry.RealDataOffset + uncompressedOffsetInEntry;
                return fs.ReadToBuffer(uncompressedAmountToRead);
            }
            else
            {
                MemoryStream decompressedData = MemoryManager.GetMemoryStream();
                fs.Seek(entry.BlockOffsets[0], SeekOrigin.Begin);

                // Seek to the first block we must decompress that contains the data offset we are looking for
                int startBlockIndex = 0; // The index of the first block we must decompress to read the file for returning
                int endBlockIndex = 0; // The index of the last block we must decompress to read the file for returning
                int position = 0;
                int startUncompPosition = 0;
                var totalEntryUncompSize = entry.RealUncompressedSize;
                for (int i = 0; i < entry.BlockSizes.Length; i++)
                {
                    uint compressedBlockSize = entry.BlockSizes[i];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = dpackage.Header.MaxBlockSize;
                    if (compressedBlockSize == dpackage.Header.MaxBlockSize || compressedBlockSize == entry.RealUncompressedSize)
                    {
                        // This block is actually uncompressed. How fun
                        if (position <= uncompressedOffsetInEntry && position + compressedBlockSize > uncompressedOffsetInEntry)
                        {
                            // We have found the first block we must decompress
                            startBlockIndex = i;
                            startUncompPosition = position;
                        }

                        if (position > uncompressedOffsetInEntry && position - compressedBlockSize >= uncompressedOffsetInEntry)
                        {
                            // We have found the last block we must decompress
                            endBlockIndex = i;
                        }

                        position += (int)entry.RealUncompressedSize; // It's not compressed
                    }
                    else
                    {
                        var uncompressedBlockSize = (uint)Math.Min(totalEntryUncompSize, dpackage.Header.MaxBlockSize);
                        if (compressedBlockSize < 5)
                        {
                            throw new Exception("compressed block size smaller than 5");
                        }

                        if (position <= uncompressedOffsetInEntry && position + compressedBlockSize > uncompressedOffsetInEntry)
                        {
                            // We have found the first block we must decompress
                            startBlockIndex = i;
                            startUncompPosition = position;
                        }

                        if (position > uncompressedOffsetInEntry && position - compressedBlockSize >= uncompressedOffsetInEntry)
                        {
                            // We have found the last block we must decompress
                            endBlockIndex = i;
                        }

                        position += (int)uncompressedBlockSize;
                    }
                }

                // Decompress the blocks
                for (int i = startBlockIndex; i < endBlockIndex; i++)
                {
                    var uncompressedBlockSize = (uint)Math.Min(totalEntryUncompSize, dpackage.Header.MaxBlockSize);
                    uint compressedBlockSize = entry.BlockSizes[i];
                    if (compressedBlockSize == 0)
                        compressedBlockSize = dpackage.Header.MaxBlockSize;

                    uint actualUncompressedBlockSize = uncompressedBlockSize;
                    var outputBlock = LZMA.Decompress(fs.ReadToBuffer((int)compressedBlockSize), actualUncompressedBlockSize);
                    if (outputBlock.Length != actualUncompressedBlockSize)
                        throw new Exception("Decompression Error");

                    decompressedData.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                    totalEntryUncompSize -= uncompressedBlockSize;
                }

                // If the data to read doesn't start on a boundary we need to strip that data out.
                decompressedData.Position = uncompressedOffsetInEntry - startUncompPosition;
                return decompressedData.ReadToBuffer(uncompressedAmountToRead);
            }
        }

        public SFAREntryReader(DLCPackage dpackage, int index)
        {
            this.dpackage = dpackage;
            entry = dpackage.Files[index];
        }
    }
}
