using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagedLZO;
using System.IO;
using Gibbed.IO;
using KFreonLib.Helpers.LiquidEngine;

namespace ME1Explorer.Helper
{
    public class SaltLZOHelper
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

        public SaltLZOHelper()
        {
        }

        /// <summary>
        /// Takes regular image data and returns it in a compressed form ready for archiving
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] CompressTex(byte[] data)
        {
            int chunkSize = 131072; //Set at this stage. Easy to change later
            int noChunks = (data.Length + chunkSize - 1) / chunkSize;
            CompressedChunkBlock[] chunks = new CompressedChunkBlock[noChunks];
            int pos = 0;
            for (int i = 0; i < noChunks; i++)
            {
                if (data.Length - (pos + chunkSize) < 0)
                {
                    chunks[i].uncSize = data.Length - pos;
                    chunks[i].rawData = new byte[chunks[i].uncSize];
                    Buffer.BlockCopy(data, pos, chunks[i].rawData, 0, chunks[i].uncSize);
                    pos += chunks[i].uncSize;
                }
                else
                {
                    chunks[i].uncSize = chunkSize;
                    chunks[i].rawData = new byte[chunkSize];
                    Buffer.BlockCopy(data, pos, chunks[i].rawData, 0, chunkSize);
                    pos += chunks[i].uncSize;
                }
            }
            pos = 0;
            CompressedChunkBlock[] newChunks = new CompressedChunkBlock[noChunks];
            for (int i = 0; i < noChunks; i++)
            {
                newChunks[i].rawData = LZO1X.Compress(chunks[i].rawData);
                newChunks[i].cprSize = newChunks[i].rawData.Length;
                newChunks[i].uncSize = chunks[i].uncSize;
                pos += newChunks[i].cprSize;
            }
            byte[] result;
            using (MemoryStream stream = new MemoryStream())
            {
                byte[] magic = new byte[] { 0xC1, 0x83, 0x2A, 0x9E, 0x00, 0x00, 0x02, 0x00 };
                pos = Gibbed.IO.NumberHelpers.LittleEndian(pos);
                BinaryWriter bin = new BinaryWriter(stream);
                bin.Write(magic);
                bin.Write(pos);
                pos = Gibbed.IO.NumberHelpers.LittleEndian(data.Length); //unc size
                bin.Write(pos);
                for (int i = 0; i < noChunks; i++)
                {
                    int uncSize = newChunks[i].uncSize;
                    int cprSize = newChunks[i].cprSize;
                    uncSize = Gibbed.IO.NumberHelpers.LittleEndian(uncSize);
                    cprSize = Gibbed.IO.NumberHelpers.LittleEndian(cprSize);
                    bin.Write(cprSize);
                    bin.Write(uncSize);
                }
                for (int i = 0; i < noChunks; i++)
                {
                    bin.Write(newChunks[i].rawData);
                }
                result = stream.ToArray();
            }

            return result;
        }

        /// <summary>
        /// Takes compressed archived image data and returns the raw image data
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] DecompressTex(Stream archiveStream, int offset, int uncSize, int cprSize)
        {
            int pos = 0;
            archiveStream.Seek(offset, SeekOrigin.Begin);
            int magicNumber = archiveStream.ReadValueS32();
            pos += 4;
            if (magicNumber != -1641380927)
            {
                throw new FormatException("Magic Number is not correct. Invalid archive data");
            }
            int blockSize = archiveStream.ReadValueS32();
            pos += 4;
            int readCprSize = archiveStream.ReadValueS32(); //Archive cprSize doesn't include header size
            pos += 4;
            int uncReadSize = archiveStream.ReadValueS32();
            if (uncReadSize != uncSize)
            {
                throw new FormatException("Uncompressed data sizes don't match. Read: " + uncReadSize + ", expected: " + uncSize);
            }
            pos += 4;
            int noChunks = (uncSize + blockSize - 1) / blockSize;

            CompressedChunkBlock[] chunks = new CompressedChunkBlock[noChunks];
            for (int i = 0; i < noChunks; i++)
            {
                CompressedChunkBlock chunk = new CompressedChunkBlock();
                chunk.cprSize = archiveStream.ReadValueS32();
                chunk.uncSize = archiveStream.ReadValueS32();
                chunk.rawData = new byte[chunk.cprSize];
                pos += 8;
                chunks[i] = chunk;
            }
            if (readCprSize + pos != cprSize)
            {
                throw new FormatException("Compressed data sizes don't match. Invalid archive data");
            }
            byte[] rawData = new byte[readCprSize];
            rawData = archiveStream.ReadBytes(readCprSize);
            archiveStream.Close();
            using (MemoryStream data = new MemoryStream(rawData))
            {
                for (int i = 0; i < noChunks; i++)
                {
                    chunks[i].rawData = data.ReadBytes(chunks[i].cprSize);
                }
            }

            byte[] imgBuffer = new byte[uncSize];
            int resultPos = 0;

            for (int i = 0; i < noChunks; i++)
            {
                CompressedChunkBlock chunk = chunks[i];
                byte[] tempResult = new byte[chunk.uncSize];
                LZO1X.Decompress(chunk.rawData, tempResult);
                Buffer.BlockCopy(tempResult, 0, imgBuffer, resultPos, chunk.uncSize);
                resultPos += chunk.uncSize;
            }

            return imgBuffer;
        }

        public MemoryTributary DecompressPCC(Stream raw, PCCObject pcc)
        {
            raw.Seek(pcc.header.Length, SeekOrigin.Begin);
            int pos = 4;
            pcc.NumChunks = raw.ReadValueS32();
            List<Chunk> Chunks = new List<Chunk>();

            //DebugOutput.PrintLn("Reading chunk headers...");
            for (int i = 0; i < pcc.NumChunks; i++)
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
                    LZO1X.Decompress(datain, dataout);
                    for (int j = 0; j < b.uncompressedsize; j++)
                        c.Uncompressed[outpos + j] = dataout[j];
                    outpos += b.uncompressedsize;
                }
                c.header = h;
                c.blocks = BlockList;
                count++;
                Chunks[i] = c;
            }

            MemoryTributary result = new MemoryTributary();
            foreach (Chunk c in Chunks)
            {
                result.Seek(c.uncompressedOffset, SeekOrigin.Begin);
                result.WriteBytes(c.Uncompressed);
            }

            return result;
        }
    }
}
