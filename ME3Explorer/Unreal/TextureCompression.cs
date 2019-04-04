/*
 * Copyright (C) 2019 Pawel Kolodziejski
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System.IO;
using StreamHelpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static ME3Explorer.BinaryInterpreter;

namespace ME3Explorer
{
    static public class TextureCompression
    {
        const uint textureTag = 0x9E2A83C1;
        const uint maxBlockSize = 0x20000; // 128KB

        const int SizeOfChunkBlock = 8;
        public struct ChunkBlock
        {
            public uint comprSize;
            public uint uncomprSize;
            public byte[] compressedBuffer;
            public byte[] uncompressedBuffer;
        }

        const int SizeOfChunk = 16;
        public struct Chunk
        {
            public uint uncomprOffset;
            public uint uncomprSize;
            public uint comprOffset;
            public uint comprSize;
            public List<ChunkBlock> blocks;
        }

        static public void DecompressTexture(byte[] DecompressedBuffer, MemoryStream stream, StorageTypes type, int uncompressedSize, int compressedSize)
        {
            uint blockTag = stream.ReadUInt32();
            if (blockTag != textureTag)
                throw new Exception("Texture tag wrong");
            uint blockSize = stream.ReadUInt32();
            if (blockSize != maxBlockSize)
                throw new Exception("Texture header broken");
            uint compressedChunkSize = stream.ReadUInt32();
            uint uncompressedChunkSize = stream.ReadUInt32();
            if (uncompressedChunkSize != uncompressedSize)
                throw new Exception("Texture header broken");

            uint blocksCount = (uncompressedChunkSize + maxBlockSize - 1) / maxBlockSize;
            if ((compressedChunkSize + SizeOfChunk + SizeOfChunkBlock * blocksCount) != compressedSize)
                throw new Exception("Texture header broken");

            List<ChunkBlock> blocks = new List<ChunkBlock>();
            for (uint b = 0; b < blocksCount; b++)
            {
                ChunkBlock block = new ChunkBlock();
                block.comprSize = stream.ReadUInt32();
                block.uncomprSize = stream.ReadUInt32();
                blocks.Add(block);
            }

            for (int b = 0; b < blocks.Count; b++)
            {
                ChunkBlock block = blocks[b];
                block.compressedBuffer = stream.ReadToBuffer(blocks[b].comprSize);
                block.uncompressedBuffer = new byte[maxBlockSize * 2];
                blocks[b] = block;
            }

            Parallel.For(0, blocks.Count, b =>
            {
                uint dstLen = 0;
                ChunkBlock block = blocks[b];
                if (type == StorageTypes.extLZO || type == StorageTypes.pccLZO)
                    dstLen = LZO2Helper.LZO2.Decompress(block.compressedBuffer, block.comprSize, block.uncompressedBuffer);
                else if (type == StorageTypes.extZlib || type == StorageTypes.pccZlib)
                    dstLen = ZlibHelper.Zlib.Decompress(block.compressedBuffer, block.comprSize, block.uncompressedBuffer);
                else
                    throw new Exception("Compression type not expected!");
                if (dstLen != block.uncomprSize)
                    throw new Exception("Decompressed data size not expected!");
            });

            int dstPos = 0;
            for (int b = 0; b < blocks.Count; b++)
            {
                Buffer.BlockCopy(blocks[b].uncompressedBuffer, 0, DecompressedBuffer, dstPos, (int)blocks[b].uncomprSize);
                dstPos += (int)blocks[b].uncomprSize;
            }
        }
    }
}
