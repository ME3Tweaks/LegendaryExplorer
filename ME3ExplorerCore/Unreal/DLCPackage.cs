using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Compression;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3ExplorerCore.Unreal
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
            public void Serialize(SerializingFile con)
            {
                //Magic = magic;
                Version = con + Version;
                DataOffset = con + DataOffset;
                EntryOffset = con + EntryOffset;
                FileCount = con + FileCount;
                BlockTableOffset = con + BlockTableOffset;
                MaxBlockSize = con + MaxBlockSize;
                if (con.isLoading)
                    CompressionScheme = con.Memory.BaseStream.ReadStringASCII(4).Trim();
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
            public long RealDataOffset { get; set; }
            public long BlockTableOffset;
            public long[] BlockOffsets;
            public ushort[] BlockSizes;
            public string FileName { get; set; }
            public bool isActualFile;

            public void Serialize(SerializingFile con, HeaderStruct header)
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
                        con.Seek((int)getBlockOffset((int)BlockSizeTableIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
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

            private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
            {
                return entryOffset + (numEntries * 0x1E) + (blockIndex * 2);
            }
        }

        public static readonly byte[] TOCHash = { 0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00, 0x19, 0x69, 0x7C };

        public HeaderStruct Header;
        public ObservableCollectionExtended<FileEntryStruct> Files { get; } = new ObservableCollectionExtended<FileEntryStruct>();

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

        public void Serialize(SerializingFile con)
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


        public void ReadFileNames()
        {
            FileEntryStruct e;
            int f = -1;
            for (int i = 0; i < Header.FileCount; i++)
            {
                e = Files[i];
                e.FileName = "UNKNOWN";
                Files[i] = e;
                if (Files[i].Hash.SequenceEqual(TOCHash))
                    f = i;
            }
            if (f == -1)
                return;
            var fFile = Files[f];
            fFile.FileName = "Filenames.txt (this file has no real name)";
            fFile.isActualFile = false;
            Files[f] = fFile;
            MemoryStream m = DecompressEntry(f);
            m.Seek(0, 0);
            StreamReader r = new StreamReader(m);
            while (!r.EndOfStream)
            {
                string line = r.ReadLine();
                byte[] hash = ComputeHash(line);
                f = -1;
                for (int i = 0; i < Header.FileCount; i++)
                    if (Files[i].Hash.SequenceEqual(hash))
                        f = i;
                if (f != -1)
                {
                    e = Files[f];
                    e.FileName = line;
                    Files[f] = e;
                }
            }
        }

        public List<byte[]> GetBlocks(int Index)
        {
            List<byte[]> res = new List<byte[]>();
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
            Debug.WriteLine("Decompressing " + e.FileName);
            MemoryStream result = new MemoryStream();
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
                        if (Header.CompressionScheme == "amzl")
                        {
                            var outputBlock = LZMA.Decompress(inputBlock, actualUncompressedBlockSize);
                            if (outputBlock.Length != actualUncompressedBlockSize)
                                throw new Exception("SFAR LZMA Decompression Error");
                            result.Write(outputBlock, 0, (int)actualUncompressedBlockSize);
                            left -= uncompressedBlockSize;
                            //continue;
                        }

                        if (Header.CompressionScheme == "lzx")
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
            MemoryStream result = new MemoryStream();
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
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
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
                if (Files[i].Hash.SequenceEqual(TOCHash))
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
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
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
                List<FileEntryStruct> l = new List<FileEntryStruct>();
                l.AddRange(Files);
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
            FileEntryStruct e = new FileEntryStruct();
            e.FileName = path;
            e.BlockOffsets = new long[0];
            e.Hash = ComputeHash(path);
            e.BlockSizeTableIndex = 0xFFFFFFFF;
            e.UncompressedSize = (uint)FileIN.Length;
            e.UncompressedSizeAdder = 0;
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


        public void ReplaceEntry(string filein, int Index)
        {
            byte[] FileIN = File.ReadAllBytes(filein);
            ReplaceEntry(FileIN, Index);
        }

        public void ReplaceEntry(byte[] FileIN, int Index)
        {

            string DLCPath = FileName;
            FileStream fs = new FileStream(DLCPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs.Seek(0, SeekOrigin.End);
            uint offset = (uint)fs.Length;
            fs.Write(FileIN, 0, FileIN.Length);
            FileEntryStruct e = Files[Index];
            e.BlockSizes = new ushort[0];
            e.BlockOffsets = new long[1];
            e.BlockOffsets[0] = offset;
            e.BlockSizeTableIndex = 0xFFFFFFFF;
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

        public List<TOCBinFile.Entry> UpdateTOCbin(bool Rebuild = false)
        {
            Debug.WriteLine("File opened\nSearching TOCbin...");
            int f = -1;
            for (int i = 0; i < Files.Count; i++)
                if (Files[i].FileName.Contains("PCConsoleTOC.bin"))
                    f = i;
            if (f == -1)
            {
                Debug.WriteLine("Couldnt Find PCConsoleTOC.bin");
                return null;
            }
            int IndexTOC = f;
            Debug.WriteLine("Found PCConsoleTOC.bin(" + f + ")!\nLoading Entries...");
            TOCBinFile TOC = new TOCBinFile(new MemoryStream(DecompressEntry(f).ToArray()));
            Debug.WriteLine("Checking Entries...");
            int count = 0;
            if (TOC.Entries == null)
                Debug.WriteLine("No TOC entries found. Oh dear...");
            for (int i = 0; i < TOC.Entries.Count; i++)
            {
                TOCBinFile.Entry e = TOC.Entries[i];
                f = -1;
                for (int j = 0; j < Files.Count; j++)
                    if (Files[j].FileName.Replace('/', '\\').Contains(e.name))
                        f = j;

                ////////////////////////////// KFREON TEMPORARY STUFF WV :)
                if (f == -1)
                {
                    List<string> parts = new List<string>(this.FileName.Split('\\'));
                    parts.RemoveAt(parts.Count - 1);
                    parts.RemoveAt(parts.Count - 1);
                    string path = string.Join("\\", parts) + "\\" + e.name;
                    if (File.Exists(path))
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Length == e.size)
                            Debug.WriteLine((count++) + " : Entry is correct " + e.name);
                        else
                        {
                            e.size = (int)fi.Length;
                            Debug.WriteLine((count++) + " : Entry will be updated " + e.name);
                            TOC.Entries[i] = e;
                        }
                    }
                    else
                        Debug.WriteLine((count++) + " : Entry not found " + e.name);
                }  /////////////////////////////// END KFREON BLATHER
                else
                {
                    if (Files[f].UncompressedSize == e.size)
                        Debug.WriteLine((count++) + " : Entry is correct " + e.name);
                    else if (Files[f].UncompressedSize != e.size)
                    {
                        e.size = (int)Files[f].UncompressedSize;
                        Debug.WriteLine((count++) + " : Entry will be updated " + e.name);
                        TOC.Entries[i] = e;
                    }
                }
            }
            Debug.WriteLine("Replacing TOC back...");
            ReplaceEntry(TOC.Save().ToArray(), IndexTOC);
            if (Rebuild)
            {
                Debug.WriteLine("Reopening SFAR...");
                Load(FileName);
                Debug.WriteLine("Rebuild...");
                ReBuild();
            }
            return TOC.Entries;
        }

        public int FindFileEntry(string fileName)
        {
            return Files.IndexOf(Files.FirstOrDefault(x => x.FileName.Contains(fileName)));
        }
    }
}
