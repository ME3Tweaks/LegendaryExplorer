using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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

        public const string COMPRESSION_WRAPPER_NAME = "CompressionWrappers.dll";

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

        private const int CHUNK_HEADER_MAGIC = -1641380927;


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
            public Block[] blocks;
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
            public ArraySegment<byte> uncompressedData;
            public byte[] compressedData;
        }


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
        /// Decompresses a compressed package. Works with ME1/ME2/ME3/LE1/LE2/LE3/UDK
        /// </summary>
        /// <param name="raw"></param>
        /// <param name="compressionInfoOffset"></param>
        /// <param name="compressionType"></param>
        /// <param name="numChunks"></param>
        /// <param name="game"></param>
        /// <param name="platform"></param>
        /// <param name="canUseLazyDecompression"></param>
        /// <returns></returns>
        public static Stream DecompressPackage(EndianReader raw, long compressionInfoOffset, UnrealPackageFile.CompressionType compressionType = UnrealPackageFile.CompressionType.None,
                                               int numChunks = 0, MEGame game = MEGame.Unknown, GamePlatform platform = GamePlatform.PC, bool canUseLazyDecompression = false)
        {
            raw.BaseStream.JumpTo(compressionInfoOffset);
            if (compressionType == UnrealPackageFile.CompressionType.None)
                compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();

            if (numChunks == 0)
                numChunks = raw.ReadInt32();

            if (raw.Endian.IsNative && canUseLazyDecompression && raw.BaseStream is FileStream fs && (game.IsLEGame() || game.IsOTGame()) && platform is GamePlatform.PC && compressionType is not UnrealPackageFile.CompressionType.None)
            {
                return new PackageDecompressionFileStream(fs, game.IsOTGame() ? MAX_BLOCK_SIZE_OT : MAX_BLOCK_SIZE_LE, compressionType, numChunks);
            }

            var chunks = new List<Chunk>(numChunks);

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
                rentedArrays = new List<byte[]>(numChunks);
            }

            for (int i = 0; i < numChunks; i++)
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

                ReadOnlySpan<byte> compressedSpan = c.Compressed.Span;
                c.header = new ChunkHeader
                {
                    magic = EndianReader.ToInt32(compressedSpan, 0, raw.Endian),
                    // must force block size for ME1 xbox cause in place of block size it seems to list package tag again which breaks loads of things
                    blocksize = (platform == GamePlatform.Xenon && game == MEGame.ME1) ? 0x20000 : EndianReader.ToInt32(compressedSpan, 4, raw.Endian),
                    compressedsize = EndianReader.ToInt32(compressedSpan, 8, raw.Endian),
                    uncompressedsize = EndianReader.ToInt32(compressedSpan, 12, raw.Endian)
                };

                // Parse block table
                if (c.header.magic != CHUNK_HEADER_MAGIC)
                    throw new FormatException("Chunk magic number incorrect");
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                int pos = 16;
                int blockCount = c.header.uncompressedsize / c.header.blocksize;
                if (c.header.uncompressedsize % c.header.blocksize != 0) blockCount++;

                c.blocks = new Block[blockCount];
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                int blockUncompressedOffset = c.uncompressedOffset;
                for (int j = 0; j < blockCount; j++)
                {
                    var b = new Block
                    {
                        compressedsize = EndianReader.ToInt32(compressedSpan, pos, raw.Endian),
                        uncompressedsize = EndianReader.ToInt32(compressedSpan, pos + 4, raw.Endian),
                        uncompressedOffset = blockUncompressedOffset
                    };
                    blockUncompressedOffset += b.uncompressedsize;
                    maxUncompressedBlockSize = Math.Max(b.uncompressedsize, maxUncompressedBlockSize); // find the max size to reduce allocations
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    c.blocks[j] = b;
                }

                //c.Uncompressed = new byte[c.uncompressedSize];
                chunks.Add(c);

                raw.Seek(nextChunkPos, SeekOrigin.Begin);
            }

            if (raw.Endian.IsNative && canUseLazyDecompression && isMemoryStream)
            {
                return new PackageDecompressionStream(chunks, maxUncompressedBlockSize, compressionType);
            }

            var firstChunkOffset = chunks.MinBy(x => x.uncompressedOffset).uncompressedOffset;
            var fullUncompressedSize = chunks.Sum(x => x.uncompressedSize);
            var result = MemoryManager.GetMemoryStream(fullUncompressedSize + firstChunkOffset);

            //DebugOutput.PrintLn("\tRead Chunks...");
            int count = 0;

            // Just re-use the dataout buffer to prevent memory allocations. 
            // We will allocate the largest uncompressed size block we can find

            var dataout = MemoryManager.GetByteArray(maxUncompressedBlockSize);


            for (int i = 0; i < chunks.Count; i++)
            {
                int pos = 16 + 8 * chunks[i].blocks.Length;

                int currentUncompChunkOffset = 0;
                int blocknum = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in chunks[i].blocks)
                {
                    //Debug.WriteLine("Decompressing block " + blocknum);
                    var datain = chunks[i].Compressed.Span.Slice(pos, b.compressedsize);
                    //Buffer.BlockCopy(Chunks[i].Compressed, pos, datain, 0, b.compressedsize);
                    pos += b.compressedsize;
                    if (b.compressedsize == b.uncompressedsize)
                    {
                        // WiiU and PS3 files sometimes have weird case where one single block has same sizes and does not have LZMA compression flag for some reason
                        // These are very uncommon
                        // WiiU: SFXGame
                        // PS3: BIOA_NOR_04_DS1
                        datain.CopyTo(dataout.AsSpan(0, b.uncompressedsize));
                    }
                    else
                    {
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
                    }

                    result.Seek(chunks[i].uncompressedOffset + currentUncompChunkOffset, SeekOrigin.Begin);
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

        internal abstract class PackageDecompressionStreamBase : Stream
        {
            protected List<Chunk> Chunks;
            protected UnrealPackageFile.CompressionType CompressionType;
            protected long _position;
            protected int firstChunkOffset;
            protected int segStartPos;
            protected byte[] Segment;
            protected int SegmentLength;
            protected int chunkIdx;
            protected int blockIdx = -1;

            private int SegmentPosition => (int)(_position - segStartPos);

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;

            protected long _length;
            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set => Seek(value, SeekOrigin.Begin);
            }

            public override int Read(Span<byte> buffer)
            {
                var initialPosition = _position;
                while (true)
                {
                    if (_position == _length)
                    {
                        //at end
                        return 0;
                    }
                    if (_position < segStartPos)
                    {
                        //go to previous block (hopefully never happens)
                        chunkIdx = 0;
                        blockIdx = -1;
                        MoveToNextBlockAndDecompress();
                        continue;
                    }
                    int segEndPos = segStartPos + SegmentLength;
                    if (_position >= segEndPos)
                    {
                        //go to next block
                        MoveToNextBlockAndDecompress();
                        continue;
                    }
                    int count = buffer.Length;
                    if (_position + count > segEndPos)
                    {
                        //multi-segment read
                        int bytesRemainingInBlock = SegmentLength - SegmentPosition;
                        Segment.AsSpan(SegmentPosition, bytesRemainingInBlock).CopyTo(buffer);
                        Seek(bytesRemainingInBlock, SeekOrigin.Current);
                        buffer = buffer[bytesRemainingInBlock..];
                        MoveToNextBlockAndDecompress();
                        continue;
                    }
                    Segment.AsSpan(SegmentPosition, count).CopyTo(buffer);
                    _position += count; //Inlined Seek(count, SeekOrigin.Current); Can't be < firstChunkOffset since we're adding, can't be > length since that would go into the if above
                    return (int)(_position - initialPosition);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }

            protected abstract void MoveToNextBlockAndDecompress();
            protected abstract ReadOnlySpan<byte> GetCompressedData(Chunk chunk, int blockstart, int compressedSize);

            protected void DecompressBlock()
            {
                Chunk chunk = Chunks[chunkIdx];
                Block b = chunk.blocks[blockIdx];
                segStartPos = b.uncompressedOffset;
                int blockstart = 16 + 8 * chunk.blocks.Length;
                for (int i = 0; i < blockIdx; i++)
                {
                    blockstart += chunk.blocks[i].compressedsize;
                }
                SegmentLength = b.uncompressedsize;
                ReadOnlySpan<byte> datain = GetCompressedData(chunk, blockstart, b.compressedsize);
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
                switch (origin)
                {
                    case SeekOrigin.Current:
                        _position += offset;
                        break;
                    case SeekOrigin.Begin:
                        _position = offset;
                        break;
                    case SeekOrigin.End:
                        _position = _length - offset;
                        break;
                }
                if (_position > _length)
                {
                    _position = _length;
                }
                else if (_position < firstChunkOffset)
                {
                    throw new Exception("PackageDecompressionStream cannot read header!");
                }
                return _position;
            }

            protected override void Dispose(bool disposing)
            {
                // Segment is not rented from Memory Manager
                // Disabled 06/18/2022 by Mgamerz
                //if (disposing)
                //{
                //    MemoryManager.ReturnByteArray(Segment);
                //}
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("This stream is read-only!");
            public override void Flush() { }
            public override void SetLength(long value) { }

            public unsafe string ReadUnrealStringLittleEndianFast()
            {
                fixed (byte* bytePtr = Segment)
                {
                    byte* lengthPtr = bytePtr + (_position - segStartPos);
                    byte* end = bytePtr + SegmentLength;
                    if (lengthPtr + 4 >= end)
                    {
                        goto Fallback;
                    }
                    int len = Unsafe.ReadUnaligned<int>(lengthPtr);
                    if (len == 0)
                    {
                        _position += 4;
                        return "";
                    }
                    if (len > 0)
                    {
                        goto Latin1;
                    }
                    len *= -2;
                    if (lengthPtr + len + 4 >= end)
                    {
                        goto Fallback;
                    }
                    _position += 4 + len;
                    return Encoding.Unicode.GetString(lengthPtr + 4, len - 2);

                Latin1:
                    if (lengthPtr + len + 4 >= end)
                    {
                        goto Fallback;
                    }
                    _position += 4 + len;
                    return Encoding.Latin1.GetString(lengthPtr + 4, len - 1);
                }

            Fallback:
                //If we're here, it means this string is either in the next segment, or straddles the segment boundary
                //This should happen < 1% of the time
                return ReadUnrealStringLittleEndianSlow();
            }

            private string ReadUnrealStringLittleEndianSlow()
            {
                Span<byte> bytes = stackalloc byte[256];
                Span<byte> lenBytes = bytes[..4];
                Read(lenBytes);
                int len = MemoryMarshal.Read<int>(lenBytes);
                if (len == 0)
                {
                    return "";
                }
                if (len > 0)
                {
                    Span<byte> strBytes = len > 256 ? new byte[len] : bytes[..len];
                    Read(strBytes);
                    return Encoding.Latin1.GetString(strBytes).Trim('\0');
                }
                else
                {
                    len *= -2;
                    Span<byte> strBytes = len > 256 ? new byte[len] : bytes[..len];
                    Read(strBytes);
                    return Encoding.Unicode.GetString(strBytes).Trim('\0');
                }
            }
        }

        private sealed class PackageDecompressionStream : PackageDecompressionStreamBase
        {
            public PackageDecompressionStream(IEnumerable<Chunk> chunks, int maxBlockSize, UnrealPackageFile.CompressionType compressionType)
            {
                CompressionType = compressionType;
                Chunks = chunks.OrderBy(c => c.uncompressedOffset).ToList();
                segStartPos = firstChunkOffset = Chunks.MinBy(x => x.uncompressedOffset).uncompressedOffset;
                var fullUncompressedSize = Chunks.Sum(x => x.uncompressedSize);
                _length = fullUncompressedSize + firstChunkOffset;
                Segment = new byte[maxBlockSize];
            }

            protected override void MoveToNextBlockAndDecompress()
            {
                for (; chunkIdx < Chunks.Count; chunkIdx++)
                {
                    Chunk chunk = Chunks[chunkIdx];
                    if (chunk.uncompressedOffset <= _position && chunk.uncompressedOffset + chunk.uncompressedSize > _position)
                    {
                        for (blockIdx++; blockIdx < chunk.blocks.Length; blockIdx++)
                        {
                            Block block = chunk.blocks[blockIdx];
                            if (block.uncompressedOffset <= _position && block.uncompressedOffset + block.uncompressedsize > _position)
                            {
                                DecompressBlock();
                                return;
                            }
                        }
                    }
                    blockIdx = -1;
                }
            }

            protected override ReadOnlySpan<byte> GetCompressedData(Chunk chunk, int blockstart, int compressedSize)
            {
                return chunk.Compressed.Span.Slice(blockstart, compressedSize);
            }
        }

        private sealed class PackageDecompressionFileStream : PackageDecompressionStreamBase
        {
            private readonly FileStream BaseStream;
            private readonly byte[] compressedBlock;


            //filestream position must be immediately after numchunks. Should only be used for PC ME games. No console or UDK
            public PackageDecompressionFileStream(FileStream fs, int maxUncompressedBlockSize, UnrealPackageFile.CompressionType compressionType, int numChunks)
            {
                CompressionType = compressionType;
                BaseStream = fs;
                int maxCompressedBlockSize = compressionType switch
                {
                    UnrealPackageFile.CompressionType.Zlib => Zlib.GetCompressionBound(maxUncompressedBlockSize),
                    UnrealPackageFile.CompressionType.LZO => LZO2.GetCompressionBound(maxUncompressedBlockSize),
                    UnrealPackageFile.CompressionType.OodleLeviathan => OodleHelper.GetCompressionBound(maxUncompressedBlockSize),
                    //no idea how to get a compression bound for these two algos. 2x should be more than enough though. 
                    UnrealPackageFile.CompressionType.LZX => maxUncompressedBlockSize * 2,
                    UnrealPackageFile.CompressionType.LZMA => maxUncompressedBlockSize * 2,
                    _ => throw new ArgumentOutOfRangeException(nameof(compressionType), compressionType, null)
                };
                compressedBlock = new byte[maxCompressedBlockSize];
                Segment = new byte[maxUncompressedBlockSize];

                Chunks = new List<Chunk>(numChunks);
                for (int i = 0; i < numChunks; i++)
                {
                    Chunks.Add(new Chunk
                    {
                        uncompressedOffset = fs.ReadInt32(),
                        uncompressedSize = fs.ReadInt32(),
                        compressedOffset = fs.ReadInt32(),
                        compressedSize = fs.ReadInt32(),
                    });
                }
                segStartPos = firstChunkOffset = Chunks.MinBy(x => x.uncompressedOffset).uncompressedOffset;
                var fullUncompressedSize = Chunks.Sum(x => x.uncompressedSize);
                _length = fullUncompressedSize + firstChunkOffset;
            }

            protected override void MoveToNextBlockAndDecompress()
            {
                for (; chunkIdx < Chunks.Count; chunkIdx++)
                {
                    Chunk chunk = Chunks[chunkIdx];
                    if (chunk.uncompressedOffset <= _position && chunk.uncompressedOffset + chunk.uncompressedSize > _position)
                    {
                        if (chunk.blocks is null)
                        {
                            BaseStream.JumpTo(chunk.compressedOffset);
                            chunk.header = new ChunkHeader
                            {
                                magic = BaseStream.ReadInt32(),
                                blocksize = BaseStream.ReadInt32(),
                                compressedsize = BaseStream.ReadInt32(),
                                uncompressedsize = BaseStream.ReadInt32(),
                            };
                            if (chunk.header.magic != CHUNK_HEADER_MAGIC) throw new FormatException("Chunk magic number incorrect");
                            int blockCount = chunk.header.uncompressedsize / chunk.header.blocksize;
                            if (chunk.header.uncompressedsize % chunk.header.blocksize != 0) blockCount++;

                            chunk.blocks = new Block[blockCount];
                            int blockUncompressedOffset = chunk.uncompressedOffset;
                            for (int j = 0; j < blockCount; j++)
                            {
                                var b = new Block
                                {
                                    compressedsize = BaseStream.ReadInt32(),
                                    uncompressedsize = BaseStream.ReadInt32(),
                                    uncompressedOffset = blockUncompressedOffset
                                };
                                blockUncompressedOffset += b.uncompressedsize;
                                chunk.blocks[j] = b;
                            }
                        }
                        for (blockIdx++; blockIdx < chunk.blocks.Length; blockIdx++)
                        {
                            Block block = chunk.blocks[blockIdx];
                            if (block.uncompressedOffset <= _position && block.uncompressedOffset + block.uncompressedsize > _position)
                            {
                                DecompressBlock();
                                return;
                            }
                        }
                    }
                    blockIdx = -1;
                }
            }

            protected override ReadOnlySpan<byte> GetCompressedData(Chunk chunk, int blockstart, int compressedSize)
            {
                Span<byte> datain = compressedBlock.AsSpan(0, compressedSize);
                BaseStream.JumpTo(chunk.compressedOffset + blockstart);
                BaseStream.Read(datain);
                return datain;
            }
        }
    }
}
