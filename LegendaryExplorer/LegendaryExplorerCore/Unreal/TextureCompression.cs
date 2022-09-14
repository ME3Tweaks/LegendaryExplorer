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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    /// Storage Types for Texture2D
    /// </summary>
    public enum StorageTypes
    {
        pccUnc = StorageFlags.noFlags,                                     // ME1 (Compressed PCC), ME2 (Compressed PCC)
        pccLZO = StorageFlags.compressedLZO,                               // ME1 (Uncompressed PCC)
        pccZlib = StorageFlags.compressedZlib,                             // ME1 (Uncompressed PCC)
        pccOodle = StorageFlags.compressedOodle,                           // LE (Uncompressed PCC) - NOT SURE WHERE THIS IS YET
        extUnc = StorageFlags.externalFile,                                // ME3 (DLC TFC archive)
        extLZO = StorageFlags.externalFile | StorageFlags.compressedLZO,   // ME1 (Reference to PCC), ME2 (TFC archive)
        extZlib = StorageFlags.externalFile | StorageFlags.compressedZlib, // ME3 (non-DLC TFC archive)
        extLZMA = StorageFlags.externalFile | StorageFlags.compressedLZMA, // ME3 WiiU, PS3 (non-DLC TFC archive)
        extOodle = StorageFlags.externalFile | StorageFlags.compressedOodle, // LE TFC
        empty = StorageFlags.externalFile | StorageFlags.unused,           // ME1, ME2, ME3
    }

    public static class StorageTypeExtensions
    {
        public static bool IsExternal(this StorageTypes type)
        {
            return type is StorageTypes.extUnc or StorageTypes.extLZMA or StorageTypes.extLZO or StorageTypes.extZlib; // Could probably bitmask this
        }
    }

    /// <summary>
    /// Storage type flags for Texture2D
    /// </summary>
    [Flags]
    public enum StorageFlags
    {
        noFlags = 0,
        externalFile = 1 << 0,
        compressedZlib = 1 << 1,
        compressedLZO = 1 << 4,
        compressedLZMA = 1 << 8,
        compressedOodle = 1 << 12,
        unused = 1 << 5,
    }

    public static class TextureCompression
    {
        const uint textureTag = 0x9E2A83C1;
        const int maxBlockSizeOT = 0x20000; // 128KB
        const int maxBlockSizeOodle = 0x40000; // 256KB

        const int SizeOfChunkBlock = 8;
        public struct ChunkBlock
        {
            public uint comprSize;
            public int uncomprSize;
            public int uncompressedOffset;
            public byte[] compressedBuffer;
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

        public static byte[] CompressTexture(byte[] inputData, StorageTypes type)
        {
            int maxBlockSize = type is StorageTypes.extOodle or StorageTypes.pccOodle ? maxBlockSizeOodle : maxBlockSizeOT;
            using MemoryStream ouputStream = MemoryManager.GetMemoryStream(inputData.Length);
            uint compressedSize = 0;
            int dataBlockLeft = inputData.Length;
            int newNumBlocks = (inputData.Length + maxBlockSize - 1) / maxBlockSize;
            var blocks = new ChunkBlock[newNumBlocks];
            // skip blocks header and table - filled later
            ouputStream.Seek(SizeOfChunk + SizeOfChunkBlock * newNumBlocks, SeekOrigin.Begin);

            for (int b = 0; b < newNumBlocks; b++)
            {
                var block = new ChunkBlock
                {
                    uncomprSize = Math.Min(maxBlockSize, dataBlockLeft),
                    uncompressedOffset = inputData.Length - dataBlockLeft
                };
                dataBlockLeft -= block.uncomprSize;
                blocks[b] = block;
            }

            Parallel.For(0, blocks.Length, b =>
            {
                ref ChunkBlock block = ref blocks[b];
                ReadOnlySpan<byte> uncompressedSpan = inputData.AsSpan(block.uncompressedOffset, block.uncomprSize);
                switch (type)
                {
                    case StorageTypes.extLZO:
                    case StorageTypes.pccLZO:
                        block.compressedBuffer = LZO2.Compress(uncompressedSpan);
                        break;
                    case StorageTypes.extZlib:
                    case StorageTypes.pccZlib:
                        block.compressedBuffer = Zlib.Compress(uncompressedSpan);
                        break;
                    case StorageTypes.extOodle:
                    case StorageTypes.pccOodle:
                        block.compressedBuffer = OodleHelper.Compress(uncompressedSpan);
                        break;
                    default:
                        throw new Exception("Compression type not expected!");
                }
                if (block.compressedBuffer.Length == 0)
                    throw new Exception("Compression failed!");
                block.comprSize = (uint)block.compressedBuffer.Length;
            });

            for (int b = 0; b < blocks.Length; b++)
            {
                ChunkBlock block = blocks[b];
                ouputStream.Write(block.compressedBuffer, 0, (int)block.comprSize);
                compressedSize += block.comprSize;
            }

            ouputStream.SeekBegin();
            ouputStream.WriteUInt32(textureTag);
            ouputStream.WriteInt32(maxBlockSize);
            ouputStream.WriteUInt32(compressedSize);
            ouputStream.WriteInt32(inputData.Length);
            foreach (ChunkBlock block in blocks)
            {
                ouputStream.WriteUInt32(block.comprSize);
                ouputStream.WriteInt32(block.uncomprSize);
            }

            return ouputStream.ToArray();
        }

        public static void DecompressTexture(byte[] DecompressedBuffer, Stream stream, StorageTypes type, int uncompressedSize, int compressedSize)
        {
            int maxBlockSize = type is StorageTypes.extOodle or StorageTypes.pccOodle ? maxBlockSizeOodle : maxBlockSizeOT;
            uint blockTag = stream.ReadUInt32();
            if (blockTag != textureTag)
                throw new Exception("Texture magic wrong");
            int blockSize = stream.ReadInt32();
            if (blockSize != maxBlockSize)
                throw new Exception("Texture header broken");
            int compressedChunkSize = stream.ReadInt32();
            int uncompressedChunkSize = stream.ReadInt32();
            if (uncompressedChunkSize != uncompressedSize)
                throw new Exception("Texture header broken");

            int blocksCount = (uncompressedChunkSize + maxBlockSize - 1) / maxBlockSize;
            if ((compressedChunkSize + SizeOfChunk + SizeOfChunkBlock * blocksCount) != compressedSize)
                throw new Exception("Texture header broken");

            var blocks = new List<ChunkBlock>();
            int uncompressedOffset = 0;
            for (uint b = 0; b < blocksCount; b++)
            {
                var block = new ChunkBlock
                {
                    comprSize = stream.ReadUInt32(),
                    uncomprSize = stream.ReadInt32(),
                    uncompressedOffset = uncompressedOffset
                };
                blocks.Add(block);
                uncompressedOffset += block.uncomprSize;
            }

            for (int b = 0; b < blocks.Count; b++)
            {
                ChunkBlock block = blocks[b];
                block.compressedBuffer = stream.ReadToBuffer(blocks[b].comprSize);
                blocks[b] = block;
            }

            Parallel.For(0, blocks.Count, b =>
            {
                uint dstLen;
                ChunkBlock block = blocks[b];
                Span<byte> uncompressedBuff = DecompressedBuffer.AsSpan(block.uncompressedOffset, block.uncomprSize);
                switch (type)
                {
                    case StorageTypes.extLZO:
                    case StorageTypes.pccLZO:
                        dstLen = LZO2.Decompress(block.compressedBuffer, uncompressedBuff);
                        break;
                    case StorageTypes.extZlib:
                    case StorageTypes.pccZlib:
                        dstLen = Zlib.Decompress(block.compressedBuffer, uncompressedBuff);
                        break;
                    case StorageTypes.extOodle:
                    case StorageTypes.pccOodle:
                        dstLen = (uint)OodleHelper.Decompress(block.compressedBuffer, uncompressedBuff);
                        break;
                    case StorageTypes.extLZMA:
                        dstLen = (uint)LZMA.Decompress(block.compressedBuffer, uncompressedBuff);
                        break;
                    default:
                        throw new Exception("Compression type not expected!");
                }
                if (dstLen != block.uncomprSize)
                    throw new Exception("Decompressed data size not expected!");
            });
        }

        public static byte[] ConvertTextureCompression(byte[] textureCompressed, int decompressedSize, ref StorageTypes storageType, MEGame newGame, bool forceInternal)
        {
            // Unsure if when calling convert on compression types, unc should be changed
            //if (storageType == StorageTypes.pccUnc || storageType == StorageTypes.extUnc)
            //    return textureCompressed;

            // todo: optimize this
            byte[] decompressed = new byte[decompressedSize];
            TextureCompression.DecompressTexture(decompressed, new MemoryStream(textureCompressed), storageType, decompressedSize, textureCompressed.Length);

            bool external = !forceInternal && storageType.IsExternal(); // Should this be stored externally?
            storageType = TextureCompression.GetStorageTypeForGame(newGame, external);

            if (storageType != StorageTypes.pccUnc)
            {
                textureCompressed = TextureCompression.CompressTexture(decompressed, storageType);
            }
            else
            {
                textureCompressed = decompressed;
            }

            return textureCompressed;

        }

        public static StorageTypes GetStorageTypeForGame(MEGame game, bool isExternal)
        {
            switch (game)
            {
                case MEGame.ME1 or MEGame.ME2 when isExternal: return StorageTypes.extLZO;
                case MEGame.ME1 or MEGame.ME2: return StorageTypes.pccLZO;
                case MEGame.ME3 when isExternal: return StorageTypes.extZlib;
                case MEGame.ME3: return StorageTypes.pccZlib;
                case MEGame.LE1 or MEGame.LE2 or MEGame.LE3 when isExternal: return StorageTypes.extOodle;
                case MEGame.LE1 or MEGame.LE2 or MEGame.LE3: return StorageTypes.pccUnc; // LE game packages are always compressed. Do not compress pcc stored textures
                default: throw new Exception($"{game} is not a supported game for getting texture storage types");
            }
        }
    }
}
