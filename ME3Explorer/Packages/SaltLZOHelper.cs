using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagedLZO;
using System.IO;
using Gibbed.IO;

namespace ME3Explorer.Packages
{
    public static class SaltLZOHelper
    {
        public struct CompressedChunkBlock
        {
            public int cprSize;
            public int uncSize;
            public byte[] rawData;
        }

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

        const int maxChunkSize = 1048576;
        const int maxBlockSize = 131072;

        public static MemoryStream DecompressPCC(Stream raw, long headerLength)
        {
            raw.Seek(headerLength, SeekOrigin.Begin);
            int pos = 4;
            int NumChunks = raw.ReadValueS32();
            List<Chunk> Chunks = new List<Chunk>();

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < NumChunks; i++)
            {
                Chunk c = new Chunk();
                c.uncompressedOffset = raw.ReadValueS32();
                c.uncompressedSize = raw.ReadValueS32();
                c.compressedOffset = raw.ReadValueS32();
                c.compressedSize = raw.ReadValueS32();
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
                c.Compressed = raw.ReadBytes(c.compressedSize);

                ChunkHeader h = new ChunkHeader();
                h.magic = BitConverter.ToInt32(c.Compressed, 0);
                if (h.magic != -1641380927)
                    throw new FormatException("Chunk magic number incorrect");
                h.blocksize = BitConverter.ToInt32(c.Compressed, 4);
                h.compressedsize = BitConverter.ToInt32(c.Compressed, 8);
                h.uncompressedsize = BitConverter.ToInt32(c.Compressed, 12);
                //DebugOutput.PrintLn("Chunkheader read: Magic = " + h.magic + ", Blocksize = " + h.blocksize + ", Compressed Size = " + h.compressedsize + ", Uncompressed size = " + h.uncompressedsize);
                pos = 16;
                int blockCount = (h.uncompressedsize % h.blocksize == 0)
                    ?
                    h.uncompressedsize / h.blocksize
                    :
                    h.uncompressedsize / h.blocksize + 1;
                List<Block> BlockList = new List<Block>();
                //DebugOutput.PrintLn("\t\t" + count + " Read Blockheaders...");
                for (int j = 0; j < blockCount; j++)
                {
                    Block b = new Block();
                    b.compressedsize = BitConverter.ToInt32(c.Compressed, pos);
                    b.uncompressedsize = BitConverter.ToInt32(c.Compressed, pos + 4);
                    //DebugOutput.PrintLn("Block " + j + ", compressed size = " + b.compressedsize + ", uncompressed size = " + b.uncompressedsize);
                    pos += 8;
                    BlockList.Add(b);
                }
                int outpos = 0;
                //DebugOutput.PrintLn("\t\t" + count + " Read and decompress Blocks...");
                foreach (Block b in BlockList)
                {
                    byte[] datain = new byte[b.compressedsize];
                    byte[] dataout = new byte[b.uncompressedsize];
                    for (int j = 0; j < b.compressedsize; j++)
                        datain[j] = c.Compressed[pos + j];
                    pos += b.compressedsize;

                    try
                    {
                        LZO1X.Decompress(datain, dataout);
                    }
                    catch
                    {
                        throw new Exception("LZO decompression failed!");
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
                result.WriteBytes(c.Uncompressed);
            }
            
            return result;
        }
    }
}
