using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Gibbed.IO;

namespace AmaroK86.MassEffect3.ZlibBlock
{
    public static class ZBlock
    {
        public static readonly uint magic = 0x9E2A83C1;
        public static readonly uint maxSegmentSize = 0x20000;

        /*
         * Name function: Compress
         * Purpose: compress a part of the byte array into a Zlib Block
         * Input: - buffer: byte array
         *        - offset: starting offset inside the array
         *        - count: num of bytes to compress starting from the offset
         * Output: compressed byte array block, the structure is:
         *         - magic word
         *         - max segment size
         *         - total compressed size
         *         - total uncompressed size
         *         - segment list
         *         - compressed data list
         */
        public static byte[] Compress(byte[] buffer, int offset, int count)
        {
            if(buffer == null)
                throw new ArgumentNullException();
            if (count < 0)
                throw new FormatException();
            if (offset + count > buffer.Length)
                throw new IndexOutOfRangeException();

            MemoryStream headBlock = new MemoryStream();
            MemoryStream dataBlock = new MemoryStream();
            DeflaterOutputStream zipStream;

            int numSeg = (int)Math.Ceiling((double)count / (double)maxSegmentSize);

            headBlock.WriteValueU32(magic);
            headBlock.WriteValueU32(maxSegmentSize);
            headBlock.WriteValueU32(0x0);            //total compressed size, still to calculate
            headBlock.WriteValueS32(count);          //total uncompressed size

            for (int i = count; i > 0; i -= (int)maxSegmentSize)
            {
                int copyBytes = Math.Min(i, (int)maxSegmentSize);
                uint precCompSize = (uint)dataBlock.Length;
                zipStream = new DeflaterOutputStream(dataBlock);
                zipStream.Write(buffer, offset + (count - i), copyBytes);
                zipStream.Flush();
                zipStream.Finish();
                headBlock.WriteValueU32((uint)dataBlock.Length - precCompSize); //compressed segment size
                headBlock.WriteValueS32(copyBytes); //uncompressed segment size
                //Console.WriteLine("  Segment size: {0}, total read: {1}, compr size: {2}", maxSegmentSize, copyBytes, (uint)dataBlock.Length - precCompSize);
            }

            headBlock.Seek(8, SeekOrigin.Begin);
            headBlock.WriteValueS32((int)dataBlock.Length); // total compressed size

            byte[] finalBlock = new byte[headBlock.Length + dataBlock.Length];
            Buffer.BlockCopy(headBlock.ToArray(), 0, finalBlock, 0, (int)headBlock.Length);
            Buffer.BlockCopy(dataBlock.ToArray(), 0, finalBlock, (int)headBlock.Length, (int)dataBlock.Length);
            headBlock.Close();
            dataBlock.Close();

            return finalBlock;
        }

        /*
         * Name function: Compress
         * Purpose: compress a part of the byte array into a Zlib Block
         * Input: - buffer: byte array
         *        - offset: starting offset inside the array
         *        - count: num of bytes to compress starting from the offset
         * Output: compressed byte array block, the structure is:
         *         - magic word
         *         - max segment size
         *         - total compressed size
         *         - total uncompressed size
         *         - segment list
         *         - compressed data list
         */
        public static byte[] Compress(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            return Compress(buffer, 0, buffer.Length);
        }

        /*
         * Name function: Compress
         * Purpose: compress a part of the stream into a Zlib Block
         * Input: - inStream: input Stream
         *        - count: num of bytes to compress starting from the Stream position
         * Output: compressed byte array block, the structure is:
         *         - magic word
         *         - max segment size
         *         - total compressed size
         *         - total uncompressed size
         *         - segment list
         *         - compressed data list
         */
        public static byte[] Compress(Stream inStream, int count)
        {
            if (count < 0)
                throw new FormatException();
            if (inStream.Position + count > inStream.Length)
                throw new ArgumentOutOfRangeException();
            byte[] buffer = new byte[count];
            inStream.Read(buffer, 0, count);
            return Compress(buffer,0,count);
        }

        public static byte[] Decompress(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            return Decompress(buffer, 0, buffer.Length);
        }

        public static byte[] Decompress(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            if (count < 0)
                throw new FormatException();
            if (offset + count > buffer.Length)
                throw new IndexOutOfRangeException();

            using (MemoryStream buffStream = new MemoryStream(buffer, offset, count))
            {
                InflaterInputStream zipStream;
                uint magicStream = buffStream.ReadValueU32();
                if (magicStream != magic && magicStream.Swap() != magic)
                {
                    throw new InvalidDataException("found an invalid zlib block");
                }

                uint buffMaxSegmentSize = buffStream.ReadValueU32();
                if (buffMaxSegmentSize != maxSegmentSize)
                {
                    throw new FormatException();
                }

                uint totComprSize = buffStream.ReadValueU32();
                uint totUncomprSize = buffStream.ReadValueU32();

                byte[] outputBuffer = new byte[totUncomprSize];
                int numOfSegm = (int)Math.Ceiling((double)totUncomprSize / (double)maxSegmentSize);
                int headSegm = 16;
                int dataSegm = headSegm + (numOfSegm * 8);
                int buffOff = 0;

                for (int i = 0; i < numOfSegm; i++)
                {
                    buffStream.Seek(headSegm, SeekOrigin.Begin);
                    int comprSegm = buffStream.ReadValueS32();
                    int uncomprSegm = buffStream.ReadValueS32();
                    headSegm = (int)buffStream.Position;

                    buffStream.Seek(dataSegm, SeekOrigin.Begin);
                    //Console.WriteLine("compr size: {0}, uncompr size: {1}, data offset: 0x{2:X8}", comprSegm, uncomprSegm, dataSegm);
                    zipStream = new InflaterInputStream(buffStream);
                    zipStream.Read(outputBuffer, buffOff, uncomprSegm);
                    zipStream.Flush();
                    buffOff += uncomprSegm;
                    dataSegm += comprSegm;
                }
                buffStream.Close();
                return outputBuffer;
            }
        }

        public static byte[] Decompress(Stream inStream, int count)
        {
            if (count < 0)
                throw new FormatException();
            if (inStream.Position + count > inStream.Length)
                return new byte[count];
            byte[] buffer = new byte[count];
            inStream.Read(buffer, 0, count);
            return Decompress(buffer, 0, count);
        }
    }
}
