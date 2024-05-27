/*
 * C# CompressonatorCodecs Helper for wrapper
 *
 * Copyright (C) 2017 Pawel Kolodziejski
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
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Textures.Codecs
{
    public static class Codecs
    {
        public const int BLOCK_SIZE_4X4X4 = 64;
        public const int BLOCK_SIZE_4X4BPP4 = 8;
        public const int BLOCK_SIZE_4X4BPP8 = 16;

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBABlock([In] byte[] rgbBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void DecompressRGBABlock([Out] byte* rgbBlock, [In] uint* compressedBlock);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBABlock_ExplicitAlpha([In] byte[] rgbBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void DecompressRGBABlock_ExplicitAlpha([Out] byte* rgbBlock, [In] uint* compressedBlock);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBBlock([In] byte[] rgbBlock, [Out] uint[] compressedBlock, int bDXT1 = 0,
            int bDXT1UseAlpha = 0, byte nDXT1AlphaThreshold = 128);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void DecompressRGBBlock([Out] byte* rgbBlock, [In] uint* compressedBlock, int bDXT1);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressAlphaBlock([In] byte[] alphaBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressionWrappers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void DecompressAlphaBlock([Out] byte* alphaBlock, [In] uint* compressedBlock);

        public static uint[] CompressRGBABlock(byte[] rgbBlock)
        {
            if (rgbBlock.Length != BLOCK_SIZE_4X4X4)
                throw new Exception();
            uint[] compressedBlock = new uint[4];
            CompressRGBABlock(rgbBlock, compressedBlock);
            return compressedBlock;
        }

        public static unsafe void DecompressRGBABlock(ReadOnlySpan<uint> compressedBlock, Span<byte> output64bytes)
        {
            if (compressedBlock.Length != 4 || output64bytes.Length != 64)
                throw new Exception();
            fixed (byte* rgbBlock = &MemoryMarshal.GetReference(output64bytes))
            fixed (uint* compressedBlockPtr = &MemoryMarshal.GetReference(compressedBlock))
            {
                DecompressRGBABlock(rgbBlock, compressedBlockPtr);
            }
        }

        public static uint[] CompressRGBABlock_ExplicitAlpha(byte[] rgbBlock)
        {
            if (rgbBlock.Length != BLOCK_SIZE_4X4X4)
                throw new Exception();
            uint[] compressedBlock = new uint[4];
            CompressRGBABlock_ExplicitAlpha(rgbBlock, compressedBlock);
            return compressedBlock;
        }

        public static unsafe void DecompressRGBABlock_ExplicitAlpha(ReadOnlySpan<uint> compressedBlock, Span<byte> output64bytes)
        {
            if (compressedBlock.Length != 4 || output64bytes.Length != 64)
                throw new Exception();
            fixed (byte* rgbBlock = &MemoryMarshal.GetReference(output64bytes))
            fixed (uint* compressedBlockPtr = &MemoryMarshal.GetReference(compressedBlock))
            {
                DecompressRGBABlock_ExplicitAlpha(rgbBlock, compressedBlockPtr);
            }
        }

        public static uint[] CompressRGBBlock(byte[] rgbBlock, bool bDXT1 = false,
            bool bDXT1UseAlpha = false, byte nDXT1AlphaThreshold = 128)
        {
            if (rgbBlock.Length != BLOCK_SIZE_4X4X4)
                throw new Exception();
            uint[] compressedBlock = new uint[2];
            CompressRGBBlock(rgbBlock, compressedBlock, bDXT1 ? 1 : 0, bDXT1UseAlpha ? 1 : 0, nDXT1AlphaThreshold);
            return compressedBlock;
        }

        public static unsafe void DecompressRGBBlock(ReadOnlySpan<uint> compressedBlock, Span<byte> output64bytes, bool bDXT1)
        {
            if (compressedBlock.Length != 2 || output64bytes.Length != 64)
                throw new Exception();
            fixed (byte* rgbBlock = &MemoryMarshal.GetReference(output64bytes))
            fixed (uint* compressedBlockPtr = &MemoryMarshal.GetReference(compressedBlock))
            {
                DecompressRGBBlock(rgbBlock, compressedBlockPtr, bDXT1 ? 1 : 0);
            }
        }

        public static uint[] CompressAlphaBlock(byte[] alphaBlock)
        {
            if (alphaBlock.Length != BLOCK_SIZE_4X4BPP8)
                throw new Exception();
            uint[] compressedBlock = new uint[2];
            CompressAlphaBlock(alphaBlock, compressedBlock);
            return compressedBlock;
        }

        public static unsafe void DecompressAlphaBlock(ReadOnlySpan<uint> compressedBlock, Span<byte> output16bytes)
        {
            if (compressedBlock.Length != 2 || output16bytes.Length != 16)
                throw new Exception();
            fixed (byte* alphaBlock = &MemoryMarshal.GetReference(output16bytes))
            fixed (uint* compressedBlockPtr = &MemoryMarshal.GetReference(compressedBlock))
            {
                DecompressAlphaBlock(alphaBlock, compressedBlockPtr);
            }
        }
    }
}
