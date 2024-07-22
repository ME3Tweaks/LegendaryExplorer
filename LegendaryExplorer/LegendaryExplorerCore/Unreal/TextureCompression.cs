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

        private struct CompressionChunkBlock
        {
            public uint CompressedSize;
            public int UncompressedSize;
            public int UncompressedOffset;
            public byte[] CompressedBuffer;
        }

        private struct DecompressionChunkBlock
        {
            public uint CompressedSize;
            public int UncompressedSize;
            public ArraySegment<byte> CompressedBuffer;
            public ArraySegment<byte> DecompressedBuffer;
        }

        const int SizeOfChunk = 16;

        public static byte[] CompressTexture(byte[] inputData, StorageTypes type)
        {
            int maxBlockSize = type is StorageTypes.extOodle or StorageTypes.pccOodle ? maxBlockSizeOodle : maxBlockSizeOT;
            using MemoryStream ouputStream = MemoryManager.GetMemoryStream(inputData.Length);
            uint compressedSize = 0;
            int dataBlockLeft = inputData.Length;
            int newNumBlocks = (inputData.Length + maxBlockSize - 1) / maxBlockSize;
            var blocks = new CompressionChunkBlock[newNumBlocks];
            // skip blocks header and table - filled later
            ouputStream.Seek(SizeOfChunk + SizeOfChunkBlock * newNumBlocks, SeekOrigin.Begin);

            for (int b = 0; b < newNumBlocks; b++)
            {
                var block = new CompressionChunkBlock
                {
                    UncompressedSize = Math.Min(maxBlockSize, dataBlockLeft),
                    UncompressedOffset = inputData.Length - dataBlockLeft
                };
                dataBlockLeft -= block.UncompressedSize;
                blocks[b] = block;
            }

            Parallel.For(0, blocks.Length, b =>
            {
                ref CompressionChunkBlock block = ref blocks[b];
                ReadOnlySpan<byte> uncompressedSpan = inputData.AsSpan(block.UncompressedOffset, block.UncompressedSize);
                switch (type)
                {
                    case StorageTypes.extLZO:
                    case StorageTypes.pccLZO:
                        block.CompressedBuffer = LZO2.Compress(uncompressedSpan);
                        break;
                    case StorageTypes.extZlib:
                    case StorageTypes.pccZlib:
                        block.CompressedBuffer = Zlib.Compress(uncompressedSpan);
                        break;
                    case StorageTypes.extOodle:
                    case StorageTypes.pccOodle:
                        block.CompressedBuffer = OodleHelper.Compress(uncompressedSpan);
                        break;
                    default:
                        throw new Exception("Compression type not expected!");
                }
                if (block.CompressedBuffer.Length == 0)
                    throw new Exception("Compression failed!");
                block.CompressedSize = (uint)block.CompressedBuffer.Length;
            });

            foreach (CompressionChunkBlock block in blocks)
            {
                ouputStream.Write(block.CompressedBuffer, 0, (int)block.CompressedSize);
                compressedSize += block.CompressedSize;
            }

            ouputStream.SeekBegin();
            ouputStream.WriteUInt32(textureTag);
            ouputStream.WriteInt32(maxBlockSize);
            ouputStream.WriteUInt32(compressedSize);
            ouputStream.WriteInt32(inputData.Length);
            foreach (CompressionChunkBlock block in blocks)
            {
                ouputStream.WriteUInt32(block.CompressedSize);
                ouputStream.WriteInt32(block.UncompressedSize);
            }

            return ouputStream.ToArray();
        }

        public static void DecompressTexture(byte[] decompressedBuffer, Stream stream, StorageTypes type, int uncompressedSize, int compressedSize)
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

            var blocks = new DecompressionChunkBlock[blocksCount];
            int uncompressedOffset = 0;
            for (uint b = 0; b < blocksCount; b++)
            {
                var block = new DecompressionChunkBlock
                {
                    CompressedSize = stream.ReadUInt32(),
                    UncompressedSize = stream.ReadInt32(),
                };
                block.DecompressedBuffer = new ArraySegment<byte>(decompressedBuffer, uncompressedOffset, block.UncompressedSize);
                blocks[b] = block;
                uncompressedOffset += block.UncompressedSize;
            }

            long compressedLength = 0;
            foreach (DecompressionChunkBlock b in blocks)
            {
                compressedLength += b.CompressedSize;
            }
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> compressedBuffer))
            {
                compressedBuffer = new ArraySegment<byte>(compressedBuffer.Array!, (int)stream.Position, (int)compressedLength);
                stream.Position += compressedLength;
            }
            else
            {
                compressedBuffer = stream.ReadToBuffer(compressedLength);
            }

            int blockOffset = 0;
            for (int b = 0; b < blocks.Length; b++)
            {
                int size = (int)blocks[b].CompressedSize;
                blocks[b].CompressedBuffer = compressedBuffer.Slice(blockOffset, size);
                blockOffset += size;
            }

            switch (type)
            {
                case StorageTypes.pccLZO:
                case StorageTypes.extLZO:
                    Parallel.ForEach(blocks, static block =>
                    {
                        if (LZO2.Decompress(block.CompressedBuffer, block.DecompressedBuffer) != block.UncompressedSize)
                            throw new Exception("Decompressed data size not expected!");
                    });
                    break;
                case StorageTypes.pccZlib:
                case StorageTypes.extZlib:
                    Parallel.ForEach(blocks, static block =>
                    {
                        if (Zlib.Decompress(block.CompressedBuffer, block.DecompressedBuffer) != block.UncompressedSize)
                            throw new Exception("Decompressed data size not expected!");
                    });
                    break;
                case StorageTypes.pccOodle:
                case StorageTypes.extOodle:
                    Parallel.ForEach(blocks, static block =>
                    {
                        if ((uint)OodleHelper.Decompress(block.CompressedBuffer, block.DecompressedBuffer) != block.UncompressedSize)
                            throw new Exception("Decompressed data size not expected!");
                    });
                    break;
                case StorageTypes.extLZMA:
                    Parallel.ForEach(blocks, static block =>
                    {
                        if ((uint)LZMA.Decompress(block.CompressedBuffer, block.DecompressedBuffer) != block.UncompressedSize)
                            throw new Exception("Decompressed data size not expected!");
                    });
                    break;
                default:
                    throw new Exception("Compression type not expected!");
            }
        }

        public static byte[] ConvertTextureCompression(byte[] textureCompressed, int decompressedSize, ref StorageTypes storageType, MEGame newGame, bool forceInternal)
        {
            // Unsure if when calling convert on compression types, unc should be changed
            //if (storageType == StorageTypes.pccUnc || storageType == StorageTypes.extUnc)
            //    return textureCompressed;

            // todo: optimize this
            byte[] decompressed = new byte[decompressedSize];
            DecompressTexture(decompressed, new MemoryStream(textureCompressed), storageType, decompressedSize, textureCompressed.Length);

            bool external = !forceInternal && storageType.IsExternal(); // Should this be stored externally?
            storageType = GetStorageTypeForGame(newGame, external);

            if (storageType != StorageTypes.pccUnc)
            {
                return CompressTexture(decompressed, storageType);
            }
            return decompressed;
        }

        public static StorageTypes GetStorageTypeForGame(MEGame game, bool isExternal)
        {
            return game switch
            {
                MEGame.ME1 or MEGame.ME2 when isExternal => StorageTypes.extLZO,
                MEGame.ME1 or MEGame.ME2 => StorageTypes.pccLZO,

                MEGame.ME3 when isExternal => StorageTypes.extZlib,
                MEGame.ME3 => StorageTypes.pccZlib,

                MEGame.LE1 or MEGame.LE2 or MEGame.LE3 when isExternal => StorageTypes.extOodle,
                MEGame.LE1 or MEGame.LE2 or MEGame.LE3 => StorageTypes.pccUnc, // LE game packages are always compressed. Do not compress pcc stored textures
                
                MEGame.UDK when isExternal => StorageTypes.extLZO,
                MEGame.UDK => StorageTypes.pccLZO,
                _ => throw new Exception($"{game} is not a supported game for getting texture storage types")
            };
        }
    }
}
