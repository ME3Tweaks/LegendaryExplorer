
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Gammtek.Conduit.IO;
using LZO2Helper;
using SevenZipHelper;
using StreamHelpers;
using ZlibHelper;

namespace ME3Explorer.Packages
{
    public static class CompressionHelper
    {
        /// <summary>
        /// Represents an item in the Chunk table of a package
        /// </summary>
        public struct Chunk
        {
            public int uncompressedOffset;
            public int uncompressedSize;
            public int compressedOffset;
            public int compressedSize;
            public byte[] Compressed;
            public byte[] Uncompressed;
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


        public struct Block
        {
            public int compressedsize;
            public int uncompressedsize;
        }

        #region Decompression

        /// <summary>
        ///     decompress an entire ME3, 2, or 1 package file.
        /// </summary>
        /// <param name="pccFileName">pcc file's name to open.</param>
        /// <returns>a decompressed array of bytes.</returns>
        public static Stream Decompress(string pccFileName)
        {
            using (FileStream input = File.OpenRead(pccFileName))
            {
                EndianReader packageReader = EndianReader.SetupForPackageReading(input);
                packageReader.SkipInt32(); //skip package tag
                var versionLicenseePacked = packageReader.ReadUInt32();
                var unrealVersion = (ushort)(versionLicenseePacked & 0xFFFF);
                var licenseeVersion = (ushort)(versionLicenseePacked >> 16);

                //ME3
                if ((unrealVersion == MEPackage.ME3UnrealVersion || unrealVersion == MEPackage.ME3WiiUUnrealVersion) && licenseeVersion == MEPackage.ME3LicenseeVersion)
                {
                    return DecompressME3(packageReader);
                }
                //Support other platforms
                //ME2 || ME1
                else if (unrealVersion == 512 && licenseeVersion == 130 || unrealVersion == 491 && licenseeVersion == 1008)
                {
                    return DecompressME1orME2(input);
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, or ME3 package file.");
                }
            }
        }

        /// <summary>
        ///     decompress an entire ME1 or 2 pcc file.
        /// </summary>
        /// <param name="raw">pcc file passed in stream format</param>
        /// <returns>a decompressed stream.</returns>
        public static MemoryStream DecompressME1orME2(Stream raw)
        {
            raw.Seek(4, SeekOrigin.Begin);
            ushort versionLo = raw.ReadUInt16();
            ushort versionHi = raw.ReadUInt16();
            raw.Seek(12, SeekOrigin.Begin);
            int tempNameSize = raw.ReadInt32();
            raw.Seek(64 + tempNameSize, SeekOrigin.Begin);
            int tempGenerations = raw.ReadInt32();
            raw.Seek(32 + tempGenerations * 12, SeekOrigin.Current);

            //if ME1
            if (versionLo == 491 && versionHi == 1008)
            {
                raw.Seek(4, SeekOrigin.Current);
            }
            UnrealPackageFile.CompressionType compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();


            int pos = 4;
            int NumChunks = raw.ReadInt32();
            var Chunks = new List<Chunk>();

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk
                {
                    uncompressedOffset = raw.ReadInt32(),
                    uncompressedSize = raw.ReadInt32(),
                    compressedOffset = raw.ReadInt32(),
                    compressedSize = raw.ReadInt32()
                };
                c.Compressed = new byte[c.compressedSize];
                c.Uncompressed = new byte[c.uncompressedSize];
                //DebugOutput.PrintLn("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
                //DebugOutput.PrintLn("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
                Chunks.Add(c);
            }

            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;
            for (int i = 0; i < Chunks.Count; i++)
            {
                Chunk c = Chunks[i];
                raw.Seek(c.compressedOffset, SeekOrigin.Begin);
                c.Compressed = raw.ReadToBuffer(c.compressedSize);

                ChunkHeader h = new ChunkHeader
                {
                    magic = BitConverter.ToInt32(c.Compressed, 0),
                    blocksize = BitConverter.ToInt32(c.Compressed, 4),
                    compressedsize = BitConverter.ToInt32(c.Compressed, 8),
                    uncompressedsize = BitConverter.ToInt32(c.Compressed, 12)
                };
                if (h.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                pos = 16;
                int blockCount = (h.uncompressedsize % h.blocksize == 0)
                    ?
                    h.uncompressedsize / h.blocksize
                    :
                    h.uncompressedsize / h.blocksize + 1;
                var BlockList = new List<Block>();
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block
                    {
                        compressedsize = BitConverter.ToInt32(c.Compressed, pos),
                        uncompressedsize = BitConverter.ToInt32(c.Compressed, pos + 4)
                    };
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }
                int outpos = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    var datain = new byte[b.compressedsize];
                    var dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = c.Compressed[pos + j];
                    pos += b.compressedsize;

                    switch (compressionType)
                    {
                        case UnrealPackageFile.CompressionType.LZO:
                            {
                                if (
                                        LZO2.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("LZO decompression failed!");
                                break;
                            }
                        case UnrealPackageFile.CompressionType.Zlib:
                            {
                                if (ZlibHelper.Zlib.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("Zlib decompression failed!");
                                break;
                            }
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }
                    for (int j = 0; j < b.uncompressedsize; j++)
                        c.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                }
                c.header = h;
                c.blocks = BlockList;
                count++;
                Chunks[i] = c;
            }

            MemoryStream result = new MemoryStream();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.WriteFromBuffer(c.Uncompressed);
            }

            return result;
        }

        /// <summary>
        /// Reads 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="compressionType"></param>
        /// <returns></returns>
        public static byte[] DecompressChunks(EndianReader input, List<Chunk> chunkTable, UnrealPackageFile.CompressionType compressionType)
        {
            var fullUncompressedSize = chunkTable.Sum(x => x.uncompressedSize);
            byte[] decompressedBuffer = new byte[fullUncompressedSize];
            foreach (var chunk in chunkTable)
            {
                //Header of individual chunk
                input.Seek(chunk.compressedOffset, SeekOrigin.Begin);
                var uncompressedOffset = input.ReadUInt32(); //where to write to into the decompressed buffer
                var uncompressedSize = input.ReadUInt32(); //how much to write
                var compressedOffset = input.ReadUInt32(); //where to read from
                var compressedSize = input.ReadUInt32(); //how much to read
                var firstBlockInfoOffset = (int)input.Position;

                var buff = new byte[compressedSize];
                input.Seek(compressedOffset, SeekOrigin.Begin);
                input.Read(buff, 0, buff.Length);
                if (compressionType == UnrealPackageFile.CompressionType.Zlib)
                {

                }
                else if (compressionType == UnrealPackageFile.CompressionType.LZMA)
                {

                }
                //AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
                //tasks[i] = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
            }

            return decompressedBuffer;
        }

        #region Block decompression
        public static readonly uint zlibmagic = 0x9E2A83C1;
        public static readonly uint zlibmaxsegmentsize = 0x20000;


        public static byte[] DecompressZLibBlock(byte[] buffer, int num = 0)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            using MemoryStream buffStream = new MemoryStream(buffer);
            EndianReader reader = EndianReader.SetupForReading(buffStream, (int)zlibmagic, out int zlibBlockMagic);
            if ((uint)zlibBlockMagic != zlibmagic)
            {
                throw new InvalidDataException("found an invalid zlib block, wrong magic");
            }

            uint buffMaxSegmentSize = reader.ReadUInt32();
            if (buffMaxSegmentSize != zlibmaxsegmentsize)
            {
                throw new FormatException("Wrong segment size for ZLIB!");
            }

            uint totComprSize = reader.ReadUInt32();
            uint totUncomprSize = reader.ReadUInt32();

            byte[] outputBuffer = new byte[totUncomprSize];
            int numOfSegm = (int)Math.Ceiling(totUncomprSize / (double)zlibmaxsegmentsize);
            int headSegm = 16;
            int dataSegm = headSegm + (numOfSegm * 8);
            int buffOff = 0;

            for (int i = 0; i < numOfSegm; i++)
            {
                reader.Seek(headSegm, SeekOrigin.Begin);
                int comprSegm = reader.ReadInt32();
                int uncomprSegm = reader.ReadInt32();
                headSegm = (int)reader.Position;

                reader.Seek(dataSegm, SeekOrigin.Begin);
                //Console.WriteLine("compr size: {0}, uncompr size: {1}, data offset: 0x{2:X8}", comprSegm, uncomprSegm, dataSegm);
                byte[] src = reader.ReadBytes(comprSegm);
                byte[] dst = new byte[uncomprSegm];
                if (Zlib.Decompress(src, (uint)src.Length, dst) != uncomprSegm)
                    throw new Exception("Zlib decompression failed!");

                Buffer.BlockCopy(dst, 0, outputBuffer, buffOff, uncomprSegm);

                buffOff += uncomprSegm;
                dataSegm += comprSegm;
            }
            reader.Close();
            return outputBuffer;
        }
        #endregion

        /// <summary>
        ///     decompress an entire ME3 pcc file into a new stream
        /// </summary>
        /// <param name="input">pcc file passed in stream format (wrapped in endianreader)</param>
        /// <returns>a decompressed array of bytes</returns>
        public static MemoryStream DecompressME3(EndianReader input)
        {
            input.Seek(0, SeekOrigin.Begin);
            var magic = input.ReadUInt32();
            if (magic != 0x9E2A83C1)
            {
                throw new FormatException("not a pcc file");
            }

            var versionLo = input.ReadUInt16();
            var versionHi = input.ReadUInt16();

            //if (versionLo != 684 &&
            //    versionHi != 194)
            //{
            //    throw new FormatException("unsupported pcc version");
            //}

            long headerSize = 8;

            input.Seek(4, SeekOrigin.Current);
            headerSize += 4;

            var folderNameLength = input.ReadInt32();
            headerSize += 4;

            var folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            input.Seek(folderNameByteLength, SeekOrigin.Current);
            headerSize += folderNameByteLength;

            var packageFlagsOffset = input.Position;
            var packageFlags = input.ReadUInt32();
            headerSize += 4;

            if ((packageFlags & 0x02000000u) == 0)
            {
                throw new FormatException("pcc file is already decompressed");
            }

            if ((packageFlags & 8) != 0)
            {
                input.Seek(4, SeekOrigin.Current);
                headerSize += 4;
            }

            uint nameCount = input.ReadUInt32();
            uint nameOffset = input.ReadUInt32();

            input.Seek(52, SeekOrigin.Current);
            headerSize += 60;

            var generationsCount = input.ReadUInt32();
            input.Seek(generationsCount * 12, SeekOrigin.Current);
            headerSize += generationsCount * 12;

            input.Seek(20, SeekOrigin.Current);
            headerSize += 24;

            var blockCount = input.ReadUInt32();
            int headBlockOff = (int)input.Position;
            var afterBlockTableOffset = headBlockOff + (blockCount * 16);
            var indataOffset = afterBlockTableOffset + 8;

            input.Seek(0, SeekOrigin.Begin);
            MemoryStream output = new MemoryStream();
            output.Seek(0, SeekOrigin.Begin);

            output.WriteFromStream(input.BaseStream, headerSize);
            output.WriteUInt32(0);// block count

            input.Seek(afterBlockTableOffset, SeekOrigin.Begin);
            output.WriteFromStream(input.BaseStream, 8);

            //check if has extra name list (don't know it's usage...)
            if ((packageFlags & 0x10000000) != 0)
            {
                long curPos = output.Position;
                output.WriteFromStream(input.BaseStream, nameOffset - curPos);
            }

            //decompress blocks in parallel
            var tasks = new Task<byte[]>[blockCount];
            var uncompressedOffsets = new uint[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                input.Seek(headBlockOff, SeekOrigin.Begin);
                uncompressedOffsets[i] = input.ReadUInt32();
                var uncompressedSize = input.ReadUInt32();
                var compressedOffset = input.ReadUInt32();
                var compressedSize = input.ReadUInt32();
                headBlockOff = (int)input.Position;

                var buff = new byte[compressedSize];
                input.Seek(compressedOffset, SeekOrigin.Begin);
                input.Read(buff, 0, buff.Length);
                AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
                //tasks[i] = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(buff, i);
            }
            Task.WaitAll(tasks);
            for (int i = 0; i < blockCount; i++)
            {
                output.Seek(uncompressedOffsets[i], SeekOrigin.Begin);
                output.WriteFromBuffer(tasks[i].Result);
            }

            //Do not change the IsCompressed bit as it will not accurately reflect the state of the file on disk.
            //output.Seek(packageFlagsOffset, SeekOrigin.Begin);
            //output.WriteUInt32(packageFlags & ~0x02000000u, ); //Mark file as decompressed.
            return output;
        }
        public static MemoryStream DecompressUDK(EndianReader raw, long compressionInfoOffset, UnrealPackageFile.CompressionType compressionType = UnrealPackageFile.CompressionType.None, int NumChunks = 0)
        {
            raw.BaseStream.JumpTo(compressionInfoOffset);
            if (compressionType == UnrealPackageFile.CompressionType.None)
                compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();

            if (NumChunks == 0)
                raw.ReadInt32();
            var Chunks = new List<Chunk>();
            var chunkTableStart = raw.Position;

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk
                {
                    uncompressedOffset = raw.ReadInt32(),
                    uncompressedSize = raw.ReadInt32(),
                    compressedOffset = raw.ReadInt32(),
                    compressedSize = raw.ReadInt32()
                };
                c.Compressed = new byte[c.compressedSize];
                c.Uncompressed = new byte[c.uncompressedSize];
                //DebugOutput.PrintLn("Chunk " + i + ", compressed size = " + c.compressedSize + ", uncompressed size = " + c.uncompressedSize);
                //DebugOutput.PrintLn("Compressed offset = " + c.compressedOffset + ", uncompressed offset = " + c.uncompressedOffset);
                Chunks.Add(c);
            }


            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;
            for (int i = 0; i < Chunks.Count; i++)
            {
                Chunk c = Chunks[i];
                raw.Seek(c.compressedOffset, SeekOrigin.Begin);
                raw.Read(c.Compressed, 0, c.compressedSize);

                ChunkHeader h = new ChunkHeader
                {
                    magic = EndianReader.ToInt32(c.Compressed, 0, raw.Endian),
                    blocksize = EndianReader.ToInt32(c.Compressed, 4, raw.Endian),
                    compressedsize = EndianReader.ToInt32(c.Compressed, 8, raw.Endian),
                    uncompressedsize = EndianReader.ToInt32(c.Compressed, 12, raw.Endian)
                };

                if (compressionType == UnrealPackageFile.CompressionType.LZX)
                {
                    //we use QuickBMS for this since we don't have a library for this right now
                    //it uses xmemdecompress.lib. We could make a static wrapper for this
                    //as the functions are exported
                    var nextChunkPos = raw.Position;
                    //Debug.WriteLine($"Extract 0x{datasize:X8} bytes starting from 0x{chunkTableStart:X8}");
                    var bmsDatapath = Path.GetTempPath() + $"ME3EXP_LZX_{Guid.NewGuid()}.bin";
                    File.WriteAllBytes(bmsDatapath, c.Compressed);
                    c.Uncompressed = QuickBMSDecompress(bmsDatapath, "XboxLZX", true);
                    if (c.Uncompressed.Length != c.uncompressedSize)
                        Debug.Write("WRONG LENGTH DECOMPRESSED");
                    c.header = h;
                    Chunks[i] = c;
                    continue;
                }



                if (h.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                int pos = 16;
                int blockCount = (h.uncompressedsize % h.blocksize == 0)
                    ?
                    h.uncompressedsize / h.blocksize
                    :
                    h.uncompressedsize / h.blocksize + 1;
                var BlockList = new List<Block>();
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block
                    {
                        compressedsize = EndianReader.ToInt32(c.Compressed, pos, raw.Endian),
                        uncompressedsize = EndianReader.ToInt32(c.Compressed, pos + 4, raw.Endian)
                    };
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }
                int outpos = 0;
                int blocknum = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    //Debug.WriteLine("Decompressing block " + blocknum);
                    var datain = new byte[b.compressedsize];
                    var dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = c.Compressed[pos + j];
                    pos += b.compressedsize;

                    switch (compressionType)
                    {
                        case UnrealPackageFile.CompressionType.LZO:
                            {
                                if (
                                        LZO2.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("LZO decompression failed!");
                                break;
                            }
                        case UnrealPackageFile.CompressionType.Zlib:
                            {
                                if (ZlibHelper.Zlib.Decompress(datain, (uint)datain.Length, dataout) != b.uncompressedsize)
                                    throw new Exception("Zlib decompression failed!");
                                break;
                            }
                        case UnrealPackageFile.CompressionType.LZMA:
                            dataout = LZMA.Decompress(datain, (uint)b.uncompressedsize);
                            if (dataout.Length != b.uncompressedsize)
                                throw new Exception("LZMA decompression failed!");
                            break;
                        default:
                            throw new Exception("Unknown compression type for this package.");
                    }
                    for (int j = 0; j < b.uncompressedsize; j++)
                        c.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                    blocknum++;
                }
                c.header = h;
                c.blocks = BlockList;
                count++;
                Chunks[i] = c;
            }

            MemoryStream result = new MemoryStream();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.WriteFromBuffer(c.Uncompressed);
            }

            return result;
        }

        public static byte[] QuickBMSDecompress(string bmsDataPath, string scriptFilename, bool isTemp)
        {
            var bmsDir = Path.Combine(App.ExecFolder, "quickbms");
            scriptFilename = Path.Combine(bmsDir, scriptFilename);
            var bmsPath = Path.Combine(bmsDir, "quickbms.exe");
            ProcessStartInfo procStartInfo = new ProcessStartInfo(bmsPath, $"-o \"{scriptFilename}\" \"{bmsDataPath}\" \"{Path.GetTempPath().TrimEnd('\\')}\"")
            {
                WorkingDirectory = Path.Combine(App.ExecFolder, "quickbms"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            //procStartInfo.StandardOutputEncoding = Encoding.GetEncoding(850); //standard cmd-page
            Process proc = new Process
            {
                StartInfo = procStartInfo
            };

            // Set our event handler to asynchronously read the sort output.
            Debug.WriteLine($"\"{bmsPath}\" -o \"{scriptFilename}\" \"{bmsDataPath}\" \"{Path.GetTempPath().TrimEnd('\\')}\"");
            proc.Start();
            proc.WaitForExit();
            var outputFilename = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(bmsDataPath) + ".decompressed");
            if (isTemp)
                File.Delete(bmsDataPath); //intermediate

            if (File.Exists(outputFilename))
            {
                var decompressed = File.ReadAllBytes(outputFilename);
                if (isTemp)
                    File.Delete(outputFilename);
                return decompressed;
            }

            return null;
        }

        #endregion

        #region Compression
        /// <summary>
        ///     compress an entire ME3 pcc into a byte array.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        /// <returns>a compressed array of bytes.</returns>
        public static byte[] Compress(byte[] uncompressedPcc)
        {
            MemoryStream uncPccStream = new MemoryStream(uncompressedPcc);
            return ((MemoryStream)Compress(uncPccStream)).ToArray();
        }

        /// <summary>
        ///     compress an entire ME3 pcc into a stream.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        /// <returns>compressed pcc stream</returns>
        public static Stream Compress(Stream uncompressedPcc)
        {
            uncompressedPcc.Position = 0;

            var magic = uncompressedPcc.ReadUInt32();
            if (magic != 0x9E2A83C1)
            {
                throw new FormatException("not a pcc package");
            }

            var versionLo = uncompressedPcc.ReadUInt16();
            var versionHi = uncompressedPcc.ReadUInt16();

            if (versionLo != 684 &&
                versionHi != 194)
            {
                throw new FormatException("unsupported version");
            }

            uncompressedPcc.Seek(4, SeekOrigin.Current);

            var folderNameLength = uncompressedPcc.ReadInt32();
            var folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);

            var packageFlagsOffset = uncompressedPcc.Position;
            var packageFlags = uncompressedPcc.ReadUInt32();

            if ((packageFlags & 8) != 0)
            {
                uncompressedPcc.Seek(4, SeekOrigin.Current);
            }

            var nameCount = uncompressedPcc.ReadUInt32();
            var namesOffset = uncompressedPcc.ReadUInt32();
            var exportCount = uncompressedPcc.ReadUInt32();
            var exportInfosOffset = uncompressedPcc.ReadUInt32();
            var exportDataOffsets = new SortedDictionary<uint, uint>();

            Stream data;
            if ((packageFlags & 0x02000000) == 0)
            {
                data = uncompressedPcc;
            }
            else
            {
                throw new FormatException("pcc data is compressed");
            }

            // get info about export data, sizes and offsets
            data.Seek(exportInfosOffset, SeekOrigin.Begin);
            for (uint i = 0; i < exportCount; i++)
            {
                var classIndex = data.ReadInt32();
                data.Seek(4, SeekOrigin.Current);
                var outerIndex = data.ReadInt32();
                var objectNameIndex = data.ReadInt32();
                data.Seek(16, SeekOrigin.Current);

                uint exportDataSize = data.ReadUInt32();
                uint exportDataOffset = data.ReadUInt32();
                exportDataOffsets.Add(exportDataOffset, exportDataSize);

                data.Seek(4, SeekOrigin.Current);
                var count = data.ReadUInt32();
                data.Seek(count * 4, SeekOrigin.Current);
                data.Seek(20, SeekOrigin.Current);
            }

            const uint maxBlockSize = 0x100000;
            Stream outputStream = new MemoryStream();
            // copying pcc header
            byte[] buffer = new byte[130];
            uncompressedPcc.Seek(0, SeekOrigin.Begin);
            uncompressedPcc.Read(buffer, 0, 130);
            outputStream.Write(buffer, 0, buffer.Length);

            //add compressed pcc flag
            uncompressedPcc.Seek(12, SeekOrigin.Begin);
            folderNameLength = uncompressedPcc.ReadInt32();
            folderNameByteLength =
                folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
            uncompressedPcc.Seek(folderNameByteLength, SeekOrigin.Current);
            outputStream.Seek(uncompressedPcc.Position, SeekOrigin.Begin);

            packageFlags = uncompressedPcc.ReadUInt32();
            packageFlags |= 0x02000000; // add compression flag
            outputStream.WriteUInt32(packageFlags);

            outputStream.Seek(buffer.Length, SeekOrigin.Begin);

            long inOffsetData = namesOffset;
            var blockSizes = new List<int>();
            int countSize = (int)(exportDataOffsets.Min(obj => obj.Key) - namesOffset);

            //count the number of blocks and relative sizes
            uint lastOffset = exportDataOffsets.Min(obj => obj.Key);
            foreach (KeyValuePair<uint, uint> exportInfo in exportDataOffsets)
            {
                // part that adds empty spaces (leaved when editing export data and moved to the end of pcc) into the count
                if (exportInfo.Key != lastOffset)
                {
                    int emptySpace = (int)(exportInfo.Key - lastOffset);
                    if (countSize + emptySpace > maxBlockSize)
                    {
                        blockSizes.Add(countSize);
                        countSize = 0;
                    }
                    else
                        countSize += emptySpace;
                }

                // adds export data into the count
                if (countSize + exportInfo.Value > maxBlockSize)
                {
                    blockSizes.Add(countSize);
                    countSize = (int)exportInfo.Value;
                }
                else
                {
                    countSize += (int)exportInfo.Value;
                }

                lastOffset = exportInfo.Key + exportInfo.Value;
            }
            blockSizes.Add(countSize);

            outputStream.WriteInt32(blockSizes.Count);
            long outOffsetBlockInfo = outputStream.Position;
            long outOffsetData = namesOffset + (blockSizes.Count * 16);

            uncompressedPcc.Seek(namesOffset, SeekOrigin.Begin);
            //divide the block in segments
            foreach (int currentUncBlockSize in blockSizes)
            {
                outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
                outputStream.WriteUInt32((uint)uncompressedPcc.Position);
                outputStream.WriteInt32(currentUncBlockSize);
                outputStream.WriteUInt32((uint)outOffsetData);

                byte[] inputBlock = new byte[currentUncBlockSize];
                uncompressedPcc.Read(inputBlock, 0, currentUncBlockSize);
                byte[] compressedBlock = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Compress(inputBlock);

                outputStream.WriteInt32(compressedBlock.Length);
                outOffsetBlockInfo = outputStream.Position;

                outputStream.Seek(outOffsetData, SeekOrigin.Begin);
                outputStream.Write(compressedBlock, 0, compressedBlock.Length);
                outOffsetData = outputStream.Position;
            }

            //copying some unknown values + extra names list
            int bufferSize = (int)namesOffset - 0x86;
            buffer = new byte[bufferSize];
            uncompressedPcc.Seek(0x86, SeekOrigin.Begin);
            uncompressedPcc.Read(buffer, 0, buffer.Length);
            outputStream.Seek(outOffsetBlockInfo, SeekOrigin.Begin);
            outputStream.Write(buffer, 0, buffer.Length);

            outputStream.Seek(0, SeekOrigin.Begin);

            return outputStream;
        }

        /// <summary>
        ///     compress an entire ME3 pcc into a file.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc stream.</param>
        /// <param name="pccFileName">pcc file name to save.</param>
        /// <returns>a compressed pcc file.</returns>
        public static void CompressAndSave(Stream uncompressedPcc, string pccFileName)
        {
            using (FileStream outputStream = new FileStream(pccFileName, FileMode.Create, FileAccess.Write))
            {
                Compress(uncompressedPcc).CopyTo(outputStream);
            }
        }

        /// <summary>
        ///     compress an entire ME3 pcc into a file.
        /// </summary>
        /// <param name="uncompressedPcc">uncompressed pcc file stored in a byte array.</param>
        /// <param name="pccFileName">pcc file name to save.</param>
        /// <returns>a compressed pcc file.</returns>
        public static void CompressAndSave(byte[] uncompressedPcc, string pccFileName)
        {
            CompressAndSave(new MemoryStream(uncompressedPcc), pccFileName);
        }
        #endregion
    }
}
