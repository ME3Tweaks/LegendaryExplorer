using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using static LegendaryExplorerCore.Packages.MEPackage;

namespace LegendaryExplorerCore.Packages
{
    public static class CompressionHelper
    {

#if AZURE
        public const string OODLE_DLL_NAME = @"C:\Users\Public\LEDC.dll";
#else
        public const string OODLE_DLL_NAME = @"oo2core_8_win64.dll";
#endif

#if WINDOWS
        public const string COMPRESSION_WRAPPER_NAME = "CompressionWrappers.dll";
#elif MACOS
        public const string COMPRESSION_WRAPPER_NAME = "IDK";
#elif LINUX
        public const string COMPRESSION_WRAPPER_NAME = "libCompressionWrappers.so";
#endif

        /// <summary>
        /// Maximum size of a compressed chunk. This is not relevant for the table chunk or if an export is larger than the max chunk size
        /// </summary>
        public const int MAX_CHUNK_SIZE = 0x100000; //1 Mebibyte

        /// <summary>
        /// Maximum size of a block within a chunk
        /// </summary>
        public const int MAX_BLOCK_SIZE_OT = 0x20000; //128 Kibibytes
        public const int MAX_BLOCK_SIZE_LE = 0x40000; //256 Kibibytes

        public const int SIZE_OF_CHUNK_HEADER = 16;
        public const int SIZE_OF_CHUNK_BLOCK_HEADER = 8;


        /// <summary>
        /// Represents an item in the Chunk table of a package
        /// </summary>
        public class Chunk
        {
            public int uncompressedOffset;
            public int uncompressedSize;
            public int compressedOffset;
            public int compressedSize;
            public Memory<byte> Compressed;
            //public byte[] Uncompressed;
            public ChunkHeader header;
            public List<Block> blocks;
        }

        /// <summary>
        /// Represents the header of chunk (that is pointed to by the chunk table)
        /// </summary>

        public struct ChunkHeader
        {
            public int magic;
            public int blocksize;
            public int compressedsize;
            public int uncompressedsize;
        }

        /// <summary>
        /// Represents a block in the block table of a chunk
        /// </summary>
        public class Block
        {
            public int compressedsize;
            public int uncompressedsize;
            public int uncompressedOffset;
            public Memory<byte> uncompressedData;
            public byte[] compressedData;
        }


        //        #region Decompression


        //        // REMOVE THIS METHOD AND USE THE STANDARDIZED ONE
        //        /// <summary>
        //        ///     decompress an entire ME3, 2, or 1 package file.
        //        /// </summary>
        //        /// <param name="pccFileName">pcc file's name to open.</param>
        //        /// <returns>a decompressed array of bytes.</returns>
        //        //public static Stream Decompress(string pccFileName)
        //        //{
        //        //    using (FileStream input = File.OpenRead(pccFileName))
        //        //    {
        //        //        EndianReader packageReader = EndianReader.SetupForPackageReading(input);
        //        //        packageReader.SkipInt32(); //skip package tag
        //        //        var versionLicenseePacked = packageReader.ReadUInt32();
        //        //        var unrealVersion = (ushort)(versionLicenseePacked & 0xFFFF);
        //        //        var licenseeVersion = (ushort)(versionLicenseePacked >> 16);

        //        //        //ME3
        //        //        if ((unrealVersion == MEPackage.ME3UnrealVersion || unrealVersion == MEPackage.ME3WiiUUnrealVersion) && licenseeVersion == MEPackage.ME3LicenseeVersion)
        //        //        {
        //        //            return DecompressME3(packageReader);
        //        //        }
        //        //        //Support other platforms
        //        //        //ME2 || ME1
        //        //        else if (unrealVersion == 512 && licenseeVersion == 130 || unrealVersion == 491 && licenseeVersion == 1008)
        //        //        {
        //        //            return DecompressME1orME2(input);
        //        //        }
        //        //        else
        //        //        {
        //        //            throw new FormatException("Not an ME1, ME2, or ME3 package file.");
        //        //        }
        //        //    }
        //        //}

        //        // REMOVE THIS METHOD AND USE THE STANDARDIZED ONE
        //        /// <summary>
        //        ///     decompress an entire ME1 or 2 pcc file.
        //        /// </summary>
        //        /// <param name="raw">pcc file passed in stream format</param>
        //        /// <returns>a decompressed stream.</returns>
        //        //public static MemoryStream DecompressME1orME2(Stream raw)
        //        //{
        //        //    raw.Seek(4, SeekOrigin.Begin);
        //        //    ushort versionLo = raw.ReadUInt16();
        //        //    ushort versionHi = raw.ReadUInt16();
        //        //    raw.Seek(12, SeekOrigin.Begin);
        //        //    int tempNameSize = raw.ReadInt32();
        //        //    raw.Seek(64 + tempNameSize, SeekOrigin.Begin);
        //        //    int tempGenerations = raw.ReadInt32();
        //        //    raw.Seek(32 + tempGenerations * 12, SeekOrigin.Current);

        //        //    //if ME1
        //        //    if (versionLo == 491 && versionHi == 1008)
        //        //    {
        //        //        raw.Seek(4, SeekOrigin.Current);
        //        //    }
        //        //    UnrealPackageFile.CompressionType compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();


        //        //    int pos = 4;
        //        //    int NumChunks = raw.ReadInt32();
        //        //    var Chunks = new List<Chunk>();

        //        //    //DebugOutput.PrintLn("Reading chunk headers...");
        //        //    for (int i = 0; i < NumChunks; i++)
        //        //    {
        //        //        Chunk c = new Chunk
        //        //        {
        //        //            uncompressedOffset = raw.ReadInt32(),
        //        //            uncompressedSize = raw.ReadInt32(),
        //        //            compressedOffset = raw.ReadInt32(),
        //        //            compressedSize = raw.ReadInt32()
        //        //        };
        //        //        c.Compressed = new byte[c.compressedSize];
        //        //        c.Uncompressed = new byte[c.uncompressedSize];
        //        //        //DebugOutput.PrintLn("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
        //        //        //DebugOutput.PrintLn("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
        //        //        Chunks.Add(c);
        //        //    }

        //        //    //DebugOutput.PrintLn("\tRead Chunks...");
        //        //    int count = 0;
        //        //    for (int i = 0; i < Chunks.Count; i++)
        //        //    {
        //        //        Chunk c = Chunks[i];
        //        //        raw.Seek(c.compressedOffset, SeekOrigin.Begin);
        //        //        c.Compressed = raw.ReadToBuffer(c.compressedSize);

        //        //        ChunkHeader h = new ChunkHeader
        //        //        {
        //        //            magic = BitConverter.ToInt32(c.Compressed, 0),
        //        //            blocksize = BitConverter.ToInt32(c.Compressed, 4),
        //        //            compressedsize = BitConverter.ToInt32(c.Compressed, 8),
        //        //            uncompressedsize = BitConverter.ToInt32(c.Compressed, 12)
        //        //        };
        //        //        if (h.magic != -1641380927)
        //        //            throw new FormatException("Chunk magic number incorrect");
        //        //        //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
        //        //        pos = 16;
        //        //        int blockCount = (h.uncompressedsize % h.blocksize == 0)
        //        //            ?
        //        //            h.uncompressedsize / h.blocksize
        //        //            :
        //        //            h.uncompressedsize / h.blocksize + 1;
        //        //        var BlockList = new List<Block>();
        //        //        //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
        //        //        for (int j = 0; j < blockCount; j++)
        //        //        {
        //        //            Block b = new Block
        //        //            {
        //        //                compressedsize = BitConverter.ToInt32(c.Compressed, pos),
        //        //                uncompressedsize = BitConverter.ToInt32(c.Compressed, pos + 4)
        //        //            };
        //        //            //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
        //        //            pos += 8;
        //        //            BlockList.Add(b);
        //        //        }
        //        //        int outpos = 0;
        //        //        //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
        //        //        foreach (Block b in BlockList)
        //        //        {
        //        //            var datain = new byte[b.compressedsize];
        //        //            var dataout = new byte[b.uncompressedsize];
        //        //            for (int j = 0; j < b.compressedsize; j++)
        //        //                datain[j] = c.Compressed[pos + j];
        //        //            pos += b.compressedsize;

        //        //            switch (compressionType)
        //        //            {
        //        //                case UnrealPackageFile.CompressionType.LZO:
        //        //                    {
        //        //                        if (
        //        //                                LZO2.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
        //        //                            throw new Exception("LZO decompression failed!");
        //        //                        break;
        //        //                    }
        //        //                case UnrealPackageFile.CompressionType.Zlib:
        //        //                    {
        //        //                        if (ZlibHelper.Zlib.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
        //        //                            throw new Exception("Zlib decompression failed!");
        //        //                        break;
        //        //                    }
        //        //                default:
        //        //                    throw new Exception("Unknown compression type for this package.");
        //        //            }
        //        //            for (int j = 0; j < b.uncompressedsize; j++)
        //        //                c.Uncompressed[outpos + j] = dataout[j];
        //        //            outpos += b.uncompressedsize;
        //        //        }
        //        //        c.header = h;
        //        //        c.blocks = BlockList;
        //        //        count++;
        //        //        Chunks[i] = c;
        //        //    }

        //        //    MemoryStream result = new MemoryStream();
        //        //    foreach (Chunk c in Chunks)
        //        //    {
        //        //        result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
        //        //        result.WriteFromBuffer(c.Uncompressed);
        //        //    }

        //        //    return result;
        //        //}

        //        /// <summary>
        //        /// Reads 
        //        /// </summary>
        //        /// <param name="input"></param>
        //        /// <param name="compressionType"></param>
        //        /// <returns></returns>
        //        public static byte[] DecompressChunks(EndianReader input, List<Chunk> chunkTable, UnrealPackageFile.CompressionType compressionType)
        //        {
        //            var fullUncompressedSize = chunkTable.Sum(x => x.uncompressedSize);
        //            byte[] decompressedBuffer = new byte[fullUncompressedSize];
        //            foreach (var chunk in chunkTable)
        //            {
        //                //Header of individual chunk
        //                input.Seek(chunk.compressedOffset, SeekOrigin.Begin);
        //                var uncompressedOffset = input.ReadUInt32(); //where to write to into the decompressed buffer
        //                var uncompressedSize = input.ReadUInt32(); //how much to write
        //                var compressedOffset = input.ReadUInt32(); //where to read from
        //                var compressedSize = input.ReadUInt32(); //how much to read
        //                var firstBlockInfoOffset = (int)input.Position;

        //                var buff = new byte[compressedSize];
        //                input.Seek(compressedOffset, SeekOrigin.Begin);
        //                input.Read(buff, 0, buff.Length);
        //                if (compressionType == UnrealPackageFile.CompressionType.Zlib)
        //                {

        //                }
        //                else if (compressionType == UnrealPackageFile.CompressionType.LZMA)
        //                {

        //                }
        //                //AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
        //                //tasks[i] = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
        //            }

        //            return decompressedBuffer;
        //        }

        //        #region Block decompression
        //        public static readonly uint zlibmagic = 0x9E2A83C1;
        //        public static readonly uint zlibmaxsegmentsize = 0x20000;


        //        public static byte[] DecompressZLibBlock(byte[] buffer, int num = 0)
        //        {
        //            if (buffer == null)
        //                throw new ArgumentNullException();
        //            using (MemoryStream buffStream = new MemoryStream(buffer))
        //            {
        //                EndianReader reader = EndianReader.SetupForReading(buffStream, (int)zlibmagic, out int zlibBlockMagic);
        //                if ((uint)zlibBlockMagic != zlibmagic)
        //                {
        //                    throw new InvalidDataException("found an invalid zlib block, wrong magic");
        //                }

        //                uint buffMaxSegmentSize = reader.ReadUInt32();
        //                if (buffMaxSegmentSize != zlibmaxsegmentsize)
        //                {
        //                    throw new FormatException("Wrong segment size for ZLIB!");
        //                }

        //                uint totComprSize = reader.ReadUInt32();
        //                uint totUncomprSize = reader.ReadUInt32();

        //                byte[] outputBuffer = new byte[totUncomprSize];
        //                int numOfSegm = (int)Math.Ceiling(totUncomprSize / (double)zlibmaxsegmentsize);
        //                int headSegm = 16;
        //                int dataSegm = headSegm + (numOfSegm * 8);
        //                int buffOff = 0;

        //                for (int i = 0; i < numOfSegm; i++)
        //                {
        //                    reader.Seek(headSegm, SeekOrigin.Begin);
        //                    int comprSegm = reader.ReadInt32();
        //                    int uncomprSegm = reader.ReadInt32();
        //                    headSegm = (int)reader.Position;

        //                    reader.Seek(dataSegm, SeekOrigin.Begin);
        //                    //Console.WriteLine("compr size: {0}, uncompr size: {1}, data offset: 0x{2:X8}", comprSegm, uncomprSegm, dataSegm);
        //                    byte[] src = reader.ReadBytes(comprSegm);
        //                    byte[] dst = new byte[uncomprSegm];
        //                    if (Zlib.Decompress(src, (uint)src.Length, dst) != uncomprSegm)
        //                        throw new Exception("Zlib decompression failed!");

        //                    Buffer.BlockCopy(dst, 0, outputBuffer, buffOff, uncomprSegm);

        //                    buffOff += uncomprSegm;
        //                    dataSegm += comprSegm;
        //                }

        //                reader.Close();
        //                return outputBuffer;
        //            }
        //        }
        //        #endregion

        //        /// <summary>
        //        ///     decompress an entire ME3 pcc file into a new stream
        //        /// </summary>
        //        /// <param name="input">pcc file passed in stream format (wrapped in endianreader)</param>
        //        /// <returns>a decompressed array of bytes</returns>
        //        //public static MemoryStream DecompressME3(EndianReader input)
        //        //{
        //        //    input.Seek(0, SeekOrigin.Begin);
        //        //    var magic = input.ReadUInt32();
        //        //    if (magic != 0x9E2A83C1)
        //        //    {
        //        //        throw new FormatException("not a pcc file");
        //        //    }

        //        //    var versionLo = input.ReadUInt16();
        //        //    var versionHi = input.ReadUInt16();

        //        //    //if (versionLo != 684 &&
        //        //    //    versionHi != 194)
        //        //    //{
        //        //    //    throw new FormatException("unsupported pcc version");
        //        //    //}

        //        //    long headerSize = 8;

        //        //    input.Seek(4, SeekOrigin.Current);
        //        //    headerSize += 4;

        //        //    var folderNameLength = input.ReadInt32();
        //        //    headerSize += 4;

        //        //    var folderNameByteLength =
        //        //        folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
        //        //    input.Seek(folderNameByteLength, SeekOrigin.Current);
        //        //    headerSize += folderNameByteLength;

        //        //    var packageFlagsOffset = input.Position;
        //        //    var packageFlags = input.ReadUInt32();
        //        //    headerSize += 4;

        //        //    if ((packageFlags & 0x02000000u) == 0)
        //        //    {
        //        //        throw new FormatException("pcc file is already decompressed");
        //        //    }

        //        //    if ((packageFlags & 8) != 0)
        //        //    {
        //        //        input.Seek(4, SeekOrigin.Current);
        //        //        headerSize += 4;
        //        //    }

        //        //    uint nameCount = input.ReadUInt32();
        //        //    uint nameOffset = input.ReadUInt32();

        //        //    input.Seek(52, SeekOrigin.Current);
        //        //    headerSize += 60;

        //        //    var generationsCount = input.ReadUInt32();
        //        //    input.Seek(generationsCount * 12, SeekOrigin.Current);
        //        //    headerSize += generationsCount * 12;

        //        //    input.Seek(20, SeekOrigin.Current);
        //        //    headerSize += 24;

        //        //    var blockCount = input.ReadUInt32();
        //        //    int headBlockOff = (int)input.Position;
        //        //    var afterBlockTableOffset = headBlockOff + (blockCount * 16);
        //        //    var indataOffset = afterBlockTableOffset + 8;

        //        //    input.Seek(0, SeekOrigin.Begin);
        //        //    MemoryStream output = new MemoryStream();
        //        //    output.Seek(0, SeekOrigin.Begin);

        //        //    output.WriteFromStream(input.BaseStream, headerSize);
        //        //    output.WriteUInt32(0);// block count

        //        //    input.Seek(afterBlockTableOffset, SeekOrigin.Begin);
        //        //    output.WriteFromStream(input.BaseStream, 8);

        //        //    //check if has extra name list (don't know it's usage...)
        //        //    if ((packageFlags & 0x10000000) != 0)
        //        //    {
        //        //        long curPos = output.Position;
        //        //        output.WriteFromStream(input.BaseStream, nameOffset - curPos);
        //        //    }

        //        //    //decompress blocks in parallel
        //        //    var tasks = new Task<byte[]>[blockCount];
        //        //    var uncompressedOffsets = new uint[blockCount];
        //        //    for (int i = 0; i < blockCount; i++)
        //        //    {
        //        //        input.Seek(headBlockOff, SeekOrigin.Begin);
        //        //        uncompressedOffsets[i] = input.ReadUInt32();
        //        //        var uncompressedSize = input.ReadUInt32();
        //        //        var compressedOffset = input.ReadUInt32();
        //        //        var compressedSize = input.ReadUInt32();
        //        //        headBlockOff = (int)input.Position;

        //        //        var buff = new byte[compressedSize];
        //        //        input.Seek(compressedOffset, SeekOrigin.Begin);
        //        //        input.Read(buff, 0, buff.Length);
        //        //        AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
        //        //        //tasks[i] = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
        //        //    }
        //        //    Task.WaitAll(tasks);
        //        //    for (int i = 0; i < blockCount; i++)
        //        //    {
        //        //        output.Seek(uncompressedOffsets[i], SeekOrigin.Begin);
        //        //        output.WriteFromBuffer(tasks[i].Result);
        //        //    }

        //        //    //Do not change the IsCompressed bit as it will not accurately reflect the state of the file on disk.
        //        //    //output.Seek(packageFlagsOffset, SeekOrigin.Begin);
        //        //    //output.WriteUInt32(packageFlags & ~0x02000000u, ); //Mark file as decompressed.
        //        //    return output;
        //        //}


        /// <summary>
        /// Decompresses a fully compressed package file. These only occur on console platforms. 
        /// </summary>
        /// <param name="rawInput">Input stream to read from</param>
        /// <param name="compressionType">Known compression type of package. If this is not known, it will attempt to be determined, and this variable will be updated.</param>
        /// <returns></returns>
        public static MemoryStream DecompressFullyCompressedPackage(EndianReader rawInput, ref UnrealPackageFile.CompressionType compressionType)
        {
            rawInput.Position = 0;
            var magic = rawInput.ReadInt32();
            var blockSize = rawInput.ReadInt32();
            var compressedSize = rawInput.ReadInt32();
            var decompressedSize = rawInput.ReadInt32();

            var blockCount = 0;
            if (decompressedSize < blockSize)
            {
                blockCount = 1;
            }
            else
            {
                // Calculate the number of blocks
                blockCount = decompressedSize / blockSize;
                if (decompressedSize % blockSize != 0) blockCount++; //Add one to decompress the final data
            }


            MemoryStream outStream = MemoryManager.GetMemoryStream();
            List<(int blockCompressedSize, int blockDecompressedSize)> blockTable = new List<(int blockCompressedSize, int blockDecompressedSize)>();

            // Read Block Table
            int i = 0;
            while (i < blockCount)
            {
                blockTable.Add((rawInput.ReadInt32(), rawInput.ReadInt32()));
                i++;
            }

            int index = 0;
            foreach (var btInfo in blockTable)
            {
                // Decompress
                //Debug.WriteLine($"Decompressing data at 0x{raw.Position:X8}");
                var datain = rawInput.ReadToBuffer(btInfo.blockCompressedSize); // This is kinda poor performance. But fully compressed packages aren't on PC
                if (compressionType == UnrealPackageFile.CompressionType.None)
                {
                    // We have to determine if it's LZMA or LZX based on first few bytes
                    if (datain[0] == 0x5D && BitConverter.ToInt32(datain, 1) == 0x10000)
                    {
                        // This is LZMA header
                        Debug.WriteLine("Fully compressed package: Detected LZMA compression");
                        compressionType = UnrealPackageFile.CompressionType.LZMA;
                    }
                    else
                    {
                        Debug.WriteLine("Fully compressed package: Detected LZX compression");
                        compressionType = UnrealPackageFile.CompressionType.LZX;
                    }
                }

                var dataout = MemoryManager.GetByteArray(btInfo.blockDecompressedSize);
                //var dataout = new byte[btInfo.blockDecompressedSize];
                if (dataout.Length == datain.Length)
                {
                    // WiiU SFXGame has weird case where one single block has same sizes and does not have LZMA compression flag for some reason
                    dataout = datain;
                }
                else
                {
                    switch (compressionType)
                    {
                        case UnrealPackageFile.CompressionType.LZO:
                            if (LZO2.Decompress(datain, dataout) != btInfo.blockDecompressedSize)
                                throw new Exception("LZO decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.Zlib:
                            if (Zlib.Decompress(datain, dataout) != btInfo.blockDecompressedSize)
                                throw new Exception("Zlib decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.LZMA:
                            // Todo: This needs to use MemoryManager system. But it's internally different from others so will take 
                            // a bit more work to do
                            // Mgamerz 2/23/2021
                            dataout = LZMA.Decompress(datain, (uint)btInfo.blockDecompressedSize);
                            if (dataout.Length != btInfo.blockDecompressedSize)
                                throw new Exception("LZMA decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.LZX:
                            if (LZX.Decompress(datain, (uint)datain.Length, dataout, (uint)btInfo.blockDecompressedSize) != 0)
                                throw new Exception("LZX decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.OodleLeviathan:
                            OodleHelper.Decompress(datain, dataout);
                            if (dataout.Length != btInfo.blockDecompressedSize)
                                throw new Exception("Oodle-Leviathan decompression failed!");
                            break;
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }
                }

                index++;
                outStream.Write(dataout, 0, btInfo.blockDecompressedSize); // write from start to decomp size as the amount we receive may not be the actual size of the buffer we got from memory manager
                MemoryManager.ReturnByteArray(dataout);
            }

            outStream.Position = 0;
            return outStream;
        }

        /// <summary>
        /// Decompresses a compressed package. Works with ME1/ME2/ME3/UDK
        /// </summary>
        /// <param name="raw"></param>
        /// <param name="compressionInfoOffset"></param>
        /// <param name="compressionType"></param>
        /// <param name="NumChunks"></param>
        /// <param name="game"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public static Stream DecompressPackage(EndianReader raw, long compressionInfoOffset, UnrealPackageFile.CompressionType compressionType = UnrealPackageFile.CompressionType.None,
                                               int NumChunks = 0, MEGame game = MEGame.Unknown, GamePlatform platform = GamePlatform.PC, bool canUseLazyDecompression = false)
        {
            raw.BaseStream.JumpTo(compressionInfoOffset);
            if (compressionType == UnrealPackageFile.CompressionType.None)
                compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();

            if (NumChunks == 0)
                NumChunks = raw.ReadInt32();
            var Chunks = new List<Chunk>(NumChunks);
            var chunkTableStart = raw.Position;

            //DebugOutput.PrintLn("Reading chunk headers...");
            int maxUncompressedBlockSize = 0;
            List<byte[]> rentedArrays = null;
            bool isMemoryStream = false;
            if (raw.BaseStream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> seg))
            {
                isMemoryStream = true;
            }
            else
            {
                seg = default;
                rentedArrays = new List<byte[]>(NumChunks);
            }

            for (int i = 0; i < NumChunks; i++)
            {
                var c = new Chunk
                {
                    uncompressedOffset = raw.ReadInt32(),
                    uncompressedSize = raw.ReadInt32(),
                    compressedOffset = raw.ReadInt32(),
                    compressedSize = raw.ReadInt32(),
                };

                var nextChunkPos = raw.Position;
                if (isMemoryStream)
                {
                    c.Compressed = seg.AsMemory(c.compressedOffset, c.compressedSize);
                }
                else
                {
                    byte[] arr = MemoryManager.GetByteArray(c.compressedSize);
                    raw.Seek(c.compressedOffset, SeekOrigin.Begin);
                    raw.Read(arr, 0, c.compressedSize);
                    c.Compressed = arr.AsMemory();
                    rentedArrays.Add(arr);
                }

                c.header = new ChunkHeader
                {
                    magic = EndianReader.ToInt32(c.Compressed.Span, 0, raw.Endian),
                    // must force block size for ME1 xbox cause in place of block size it seems to list package tag again which breaks loads of things
                    blocksize = (platform == GamePlatform.Xenon && game == MEGame.ME1) ? 0x20000 : EndianReader.ToInt32(c.Compressed.Span, 4, raw.Endian),
                    compressedsize = EndianReader.ToInt32(c.Compressed.Span, 8, raw.Endian),
                    uncompressedsize = EndianReader.ToInt32(c.Compressed.Span, 12, raw.Endian)
                };

                // Parse block table
                if (c.header.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                int pos = 16;
                int blockCount = c.header.uncompressedsize / c.header.blocksize;
                if (c.header.uncompressedsize % c.header.blocksize != 0) blockCount++;

                c.blocks = new List<Block>(blockCount);
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                int blockUncompressedOffset = c.uncompressedOffset;
                for (int j = 0; j < blockCount; j++)
                {
                    var b = new Block
                    {
                        compressedsize = EndianReader.ToInt32(c.Compressed.Span, pos, raw.Endian),
                        uncompressedsize = EndianReader.ToInt32(c.Compressed.Span, pos + 4, raw.Endian),
                        uncompressedOffset = blockUncompressedOffset
                    };
                    blockUncompressedOffset += b.uncompressedsize;
                    maxUncompressedBlockSize = Math.Max(b.uncompressedsize, maxUncompressedBlockSize); // find the max size to reduce allocations
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    c.blocks.Add(b);
                }

                //c.Uncompressed = new byte[c.uncompressedSize];
                Chunks.Add(c);

                raw.Seek(nextChunkPos, SeekOrigin.Begin);
            }

            if (canUseLazyDecompression && isMemoryStream)
            {
                return new PackageDecompressionStream(Chunks, maxUncompressedBlockSize, compressionType);
            }

            var firstChunkOffset = Chunks.MinBy(x => x.uncompressedOffset).uncompressedOffset;
            var fullUncompressedSize = Chunks.Sum(x => x.uncompressedSize);
            var result = MemoryManager.GetMemoryStream(fullUncompressedSize + firstChunkOffset);

            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;

            // Just re-use the dataout buffer to prevent memory allocations. 
            // We will allocate the largest uncompressed size block we can find

            var dataout = MemoryManager.GetByteArray(maxUncompressedBlockSize);


            for (int i = 0; i < Chunks.Count; i++)
            {
                int pos = 16 + 8 * Chunks[i].blocks.Count;

                int currentUncompChunkOffset = 0;
                int blocknum = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in Chunks[i].blocks)
                {
                    //Debug.WriteLine("Decompressing block " + blocknum);
                    var datain = Chunks[i].Compressed.Span.Slice(pos, b.compressedsize);
                    //Buffer.BlockCopy(Chunks[i].Compressed, pos, datain, 0, b.compressedsize);
                    pos += b.compressedsize;
                    switch (compressionType)
                    {
                        case UnrealPackageFile.CompressionType.LZO:
                            if (LZO2.Decompress(datain, dataout.AsSpan(0, b.uncompressedsize)) != b.uncompressedsize)
                                throw new Exception("LZO decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.Zlib:
                            if (Zlib.Decompress(datain, dataout.AsSpan(0, b.uncompressedsize)) != b.uncompressedsize)
                                throw new Exception("Zlib decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.LZMA:
                            if (LZMA.Decompress(datain, dataout.AsSpan(0, b.uncompressedsize)) != 0)
                                throw new Exception("LZMA decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.LZX:
                            if (LZX.Decompress(datain, (uint)datain.Length, dataout, (uint)b.uncompressedsize) != 0)
                                throw new Exception("LZX decompression failed!");
                            break;
                        case UnrealPackageFile.CompressionType.OodleLeviathan:
                            // Error decompressing exception is thrown in decompress method itself
                            OodleHelper.Decompress(datain, dataout.AsSpan(0, b.uncompressedsize));
                            break;
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }

                    result.Seek(Chunks[i].uncompressedOffset + currentUncompChunkOffset, SeekOrigin.Begin);
                    result.Write(dataout, 0, b.uncompressedsize); //cannot trust the length of the array as it's rented
                    currentUncompChunkOffset += b.uncompressedsize;
                    blocknum++;
                }

                // end of chunk 
                count++;
                if (!isMemoryStream)
                {
                    MemoryManager.ReturnByteArray(rentedArrays[i]);
                }
            }

            // Reattach the original header
            result.Position = 0;
            raw.Position = 0;
            raw.BaseStream.CopyToEx(result, firstChunkOffset); // Copy the header in
                                                               // Does header need adjusted here to be accurate? 
                                                               // Do we change it to show decompressed, as the actual state, or the state of what it was on disk?


            // Cleanup memory
            MemoryManager.ReturnByteArray(dataout);
            return result;
        }


        //        #endregion

        //        #region Compression
        //        /// <summary>
        //        ///     compress an entire ME3 pcc into a byte array.
        //        /// </summary>
        //        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        //        /// <returns>a compressed array of bytes.</returns>
        //        public static byte[] Compress(byte[] uncompressedPcc)
        //        {
        //            MemoryStream uncPccStream = new MemoryStream(uncompressedPcc);
        //            return ((MemoryStream)Compress(uncPccStream)).ToArray();
        //        }

        //        /// <summary>
        //        ///     compress an entire ME3 pcc into a stream.
        //        /// </summary>
        //        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        //        /// <returns>compressed pcc stream</returns>
        //        public static Stream Compress(Stream uncompressedPcc)
        //        {
        //            uncompressedPcc.Position = 0;

        //            var magic = uncompressedPcc.ReadUInt32();
        //            if (magic != 0x9E2A83C1)
        //            {
        //                throw new FormatException("not a pcc package");
        //            }

        //            var versionLo = uncompressedPcc.ReadUInt16();
        //            var versionHi = uncompressedPcc.ReadUInt16();

        //            if (versionLo != 684 &&
        //                versionHi != 194)
        //            {
        //                throw new FormatException("unsupported version");
        //            }

        //            uncompressedPcc.Seek(4, SeekOrigin.Current);

        //            var folderNameLength = uncompressedPcc.ReadInt32();
        //            var folderNameByteLength =
        //                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
        //            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);

        //            var packageFlagsOffset = uncompressedPcc.Position;
        //            var packageFlags = uncompressedPcc.ReadUInt32();

        //            if ((packageFlags & 8) != 0)
        //            {
        //                uncompressedPcc.Seek(4, SeekOrigin.Current);
        //            }

        //            var nameCount = uncompressedPcc.ReadUInt32();
        //            var namesOffset = uncompressedPcc.ReadUInt32();
        //            var exportCount = uncompressedPcc.ReadUInt32();
        //            var exportInfosOffset = uncompressedPcc.ReadUInt32();
        //            var exportDataOffsets = new SortedDictionary<uint, uint>();

        //            Stream data;
        //            if ((packageFlags & 0x02000000) == 0)
        //            {
        //                data = uncompressedPcc;
        //            }
        //            else
        //            {
        //                throw new FormatException("pcc data is compressed");
        //            }

        //            // get info about export data, sizes and offsets
        //            data.Seek(exportInfosOffset, SeekOrigin.Begin);
        //            for (uint i = 0; i < exportCount; i++)
        //            {
        //                var classIndex = data.ReadInt32();
        //                data.Seek(4, SeekOrigin.Current);
        //                var outerIndex = data.ReadInt32();
        //                var objectNameIndex = data.ReadInt32();
        //                data.Seek(16, SeekOrigin.Current);

        //                uint exportDataSize = data.ReadUInt32();
        //                uint exportDataOffset = data.ReadUInt32();
        //                exportDataOffsets.Add(exportDataOffset, exportDataSize);

        //                data.Seek(4, SeekOrigin.Current);
        //                var count = data.ReadUInt32();
        //                data.Seek(count * 4, SeekOrigin.Current);
        //                data.Seek(20, SeekOrigin.Current);
        //            }

        //            const uint maxBlockSize = 0x100000;
        //            Stream outputStream = new MemoryStream();
        //            // copying pcc header
        //            byte[] buffer = new byte[130];
        //            uncompressedPcc.Seek(0, SeekOrigin.Begin);
        //            uncompressedPcc.Read(buffer, 0, 130);
        //            outputStream.Write(buffer, 0, buffer.Length);

        //            //add compressed pcc flag
        //            uncompressedPcc.Seek(12, SeekOrigin.Begin);
        //            folderNameLength = uncompressedPcc.ReadInt32();
        //            folderNameByteLength =
        //                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
        //            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);
        //            outputStream.Seek(uncompressedPcc.Position, SeekOrigin.Begin);

        //            packageFlags = uncompressedPcc.ReadUInt32();
        //            packageFlags |= 0x02000000; // add compression flag
        //            outputStream.WriteUInt32(packageFlags);

        //            outputStream.Seek(buffer.Length, SeekOrigin.Begin);

        //            long inOffsetData = namesOffset;
        //            var blockSizes = new List<int>();
        //            int countSize = (int)(exportDataOffsets.Min(obj => obj.Key) - namesOffset);

        //            //count the number of blocks and relative sizes
        //            uint lastOffset = exportDataOffsets.Min(obj => obj.Key);
        //            foreach (KeyValuePair<uint, uint> exportInfo in exportDataOffsets)
        //            {
        //                // part that adds empty spaces (leaved when editing export data and moved to the end of pcc) into the count
        //                if (exportInfo.Key != lastOffset)
        //                {
        //                    int emptySpace = (int)(exportInfo.Key - lastOffset);
        //                    if (countSize + emptySpace > maxBlockSize)
        //                    {
        //                        blockSizes.Add(countSize);
        //                        countSize = 0;
        //                    }
        //                    else
        //                        countSize += emptySpace;
        //                }

        //                // adds export data into the count
        //                if (countSize + exportInfo.Value > maxBlockSize)
        //                {
        //                    blockSizes.Add(countSize);
        //                    countSize = (int)exportInfo.Value;
        //                }
        //                else
        //                {
        //                    countSize += (int)exportInfo.Value;
        //                }

        //                lastOffset = exportInfo.Key + exportInfo.Value;
        //            }
        //            blockSizes.Add(countSize);

        //            outputStream.WriteInt32(blockSizes.Count);
        //            long outOffsetBlockInfo = outputStream.Position;
        //            long outOffsetData = namesOffset + (blockSizes.Count * 16);

        //            uncompressedPcc.Seek(namesOffset, SeekOrigin.Begin);
        //            //divide the block in segments
        //            foreach (int currentUncBlockSize in blockSizes)
        //            {
        //                outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
        //                outputStream.WriteUInt32((uint)uncompressedPcc.Position);
        //                outputStream.WriteInt32(currentUncBlockSize);
        //                outputStream.WriteUInt32((uint)outOffsetData);

        //                byte[] inputBlock = new byte[currentUncBlockSize];
        //                uncompressedPcc.Read(inputBlock, 0, currentUncBlockSize);
        //                byte[] compressedBlock = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Compress(inputBlock);

        //                outputStream.WriteInt32(compressedBlock.Length);
        //                outOffsetBlockInfo = outputStream.Position;

        //                outputStream.Seek(outOffsetData, SeekOrigin.Begin);
        //                outputStream.Write(compressedBlock, 0, compressedBlock.Length);
        //                outOffsetData = outputStream.Position;
        //            }

        //            //copying some unknown values + extra names list
        //            int bufferSize = (int)namesOffset - 0x86;
        //            buffer = new byte[bufferSize];
        //            uncompressedPcc.Seek(0x86, SeekOrigin.Begin);
        //            uncompressedPcc.Read(buffer, 0, buffer.Length);
        //            outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
        //            outputStream.Write(buffer, 0, buffer.Length);

        //            outputStream.Seek(0, SeekOrigin.Begin);

        //            return outputStream;
        //        }

        //        /// <summary>
        //        ///     compress an entire ME3 pcc into a file.
        //        /// </summary>
        //        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        //        /// <param name="pccFileName">pcc file name to save.</param>
        //        /// <returns>a compressed pcc file.</returns>
        //        public static void CompressAndSave(Stream uncompressedPcc, string pccFileName)
        //        {
        //            using (FileStream outputStream = new FileStream(pccFileName, FileMode.Create, FileAccess.Write))
        //            {
        //                Compress(uncompressedPcc).CopyTo(outputStream);
        //            }
        //        }

        //        /// <summary>
        //        ///     compress an entire ME3 pcc into a file.
        //        /// </summary>
        //        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        //        /// <param name="pccFileName">pcc file name to save.</param>
        //        /// <returns>a compressed pcc file.</returns>
        //        public static void CompressAndSave(byte[] uncompressedPcc, string pccFileName)
        //        {
        //            CompressAndSave(new MemoryStream(uncompressedPcc), pccFileName);
        //        }
        //        #endregion

        public sealed class PackageDecompressionStream : Stream
        {
            private readonly List<Chunk> Chunks;
            private readonly UnrealPackageFile.CompressionType CompressionType;
            private long _position;
            private readonly int firstChunkOffset;
            private int segStartPos;
            private readonly byte[] Segment;
            private int SegmentLength;
            private int chunkIdx;
            private int blockIdx;

            private int SegmentPosition => (int)(_position - segStartPos);

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;

            private readonly long _length;
            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set => Seek(value, SeekOrigin.Begin);
            }

            public PackageDecompressionStream(IEnumerable<Chunk> chunks, int maxBlockSize, UnrealPackageFile.CompressionType compressionType)
            {
                CompressionType = compressionType;
                Chunks = chunks.OrderBy(c => c.uncompressedOffset).ToList();
                segStartPos = firstChunkOffset = Chunks.MinBy(x => x.uncompressedOffset).uncompressedOffset;
                var fullUncompressedSize = Chunks.Sum(x => x.uncompressedSize);
                _length = fullUncompressedSize + firstChunkOffset;
                Segment = MemoryManager.GetByteArray(maxBlockSize);
                for (chunkIdx = 0; chunkIdx < Chunks.Count; chunkIdx++)
                {
                    for (blockIdx = 0; blockIdx < Chunks[chunkIdx].blocks.Count;)
                    {
                        DecompressBlock();
                        return;
                    }
                }
            }

            public override int Read(Span<byte> buffer)
            {
                if (_position == _length)
                {
                    //at end
                    return 0;
                }
                int segEndPos = segStartPos + SegmentLength;
                if (_position < segStartPos)
                {
                    //go to previous block (hopefully never happens)
                    chunkIdx = 0;
                    blockIdx = -1;
                    return DecompressNewBlockAndRead(buffer);
                }
                if (_position >= segEndPos)
                {
                    //go to next block
                    return DecompressNewBlockAndRead(buffer);
                }
                int count = buffer.Length;
                if (_position + count > segEndPos)
                {
                    //multi-segment read
                    int bytesRemainingInBlock = SegmentLength - SegmentPosition;
                    Segment.AsSpan(SegmentPosition, bytesRemainingInBlock).CopyTo(buffer);
                    Seek(bytesRemainingInBlock, SeekOrigin.Current);
                    return bytesRemainingInBlock + Read(buffer[bytesRemainingInBlock..]);
                }
                Segment.AsSpan(SegmentPosition, count).CopyTo(buffer);
                Seek(count, SeekOrigin.Current);
                return count;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }

            private int DecompressNewBlockAndRead(Span<byte> buffer)
            {
                for (; chunkIdx < Chunks.Count; chunkIdx++)
                {
                    Chunk chunk = Chunks[chunkIdx];
                    if (chunk.uncompressedOffset <= _position && chunk.uncompressedOffset + chunk.uncompressedSize > _position)
                    {
                        for (blockIdx++; blockIdx < chunk.blocks.Count; blockIdx++)
                        {
                            Block block = chunk.blocks[blockIdx];
                            if (block.uncompressedOffset <= _position && block.uncompressedOffset + block.uncompressedsize > _position)
                            {
                                DecompressBlock();
                                return Read(buffer);
                            }
                        }
                    }
                    blockIdx = -1;
                }

                return 0;
            }

            private void DecompressBlock()
            {
                Chunk chunk = Chunks[chunkIdx];
                Block b = chunk.blocks[blockIdx];
                segStartPos = b.uncompressedOffset;
                int blockstart = 16 + 8 * chunk.blocks.Count;
                for (int i = 0; i < blockIdx; i++)
                {
                    blockstart += chunk.blocks[i].compressedsize;
                }
                SegmentLength = b.uncompressedsize;
                ReadOnlySpan<byte> datain = chunk.Compressed.Span.Slice(blockstart, b.compressedsize);
                Span<byte> dataOut = Segment.AsSpan(0, b.uncompressedsize);
                switch (CompressionType)
                {
                    case UnrealPackageFile.CompressionType.LZO:
                        if (LZO2.Decompress(datain, dataOut) != b.uncompressedsize)
                            throw new Exception("LZO decompression failed!");
                        break;
                    case UnrealPackageFile.CompressionType.Zlib:
                        if (Zlib.Decompress(datain, dataOut) != b.uncompressedsize)
                            throw new Exception("Zlib decompression failed!");
                        break;
                    case UnrealPackageFile.CompressionType.LZMA:
                        if (LZMA.Decompress(datain, dataOut) != 0)
                            throw new Exception("LZMA decompression failed!");
                        break;
                    case UnrealPackageFile.CompressionType.LZX:
                        if (LZX.Decompress(datain, (uint)datain.Length, Segment, (uint)b.uncompressedsize) != 0)
                            throw new Exception("LZX decompression failed!");
                        break;
                    case UnrealPackageFile.CompressionType.OodleLeviathan:
                        // Error decompressing exception is thrown in decompress method itself
                        OodleHelper.Decompress(datain, dataOut);
                        break;
                    default:
                        throw new Exception("Unknown compression type for this package.");
                }

            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                var len = _length;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        _position = offset;
                        break;
                    case SeekOrigin.Current:
                        _position += offset;
                        break;
                    case SeekOrigin.End:
                        _position = len - offset;
                        break;
                }
                if (_position > len)
                {
                    _position = len;
                }
                else if (_position < firstChunkOffset)
                {
                    throw new Exception("PackageDecompressionStream cannot read header!");
                }
                return _position;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    MemoryManager.ReturnByteArray(Segment);
                }
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("This stream is read-only!");
            public override void Flush() { }
            public override void SetLength(long value) { }
        }
    }
}
