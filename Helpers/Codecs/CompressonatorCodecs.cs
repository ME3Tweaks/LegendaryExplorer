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

namespace CompressonatorCodecs
{
    public static class Codecs
    {
        public const int BLOCK_SIZE_4X4X4 = 64;
        public const int BLOCK_SIZE_4X4BPP4 = 8;
        public const int BLOCK_SIZE_4X4BPP8 = 16;

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBABlock([In] byte[] rgbBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressRGBABlock([Out] byte[] rgbBlock, [In] uint[] compressedBlock);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBABlock_ExplicitAlpha([In] byte[] rgbBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressRGBABlock_ExplicitAlpha([Out] byte[] rgbBlock, [In] uint[] compressedBlock);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressRGBBlock([In] byte[] rgbBlock, [Out] uint[] compressedBlock, int bDXT1 = 0,
            int bDXT1UseAlpha = 0, byte nDXT1AlphaThreshold = 128);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressRGBBlock([Out] byte[] rgbBlock, [In] uint[] compressedBlock, int bDXT1);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CompressAlphaBlock([In] byte[] alphaBlock, [Out] uint[] compressedBlock);

        [DllImport("CompressonatorCodecs.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DecompressAlphaBlock([Out] byte[] alphaBlock, [In] uint[] compressedBlock);


        public static uint[] CompressRGBABlock(byte[] rgbBlock)
        {
            if (rgbBlock.Length != BLOCK_SIZE_4X4X4)
                throw new Exception();
            uint[] compressedBlock = new uint[4];
            CompressRGBABlock(rgbBlock, compressedBlock);
            return compressedBlock;
        }

        public static byte[] DecompressRGBABlock(uint[] compressedBlock)
        {
            if (compressedBlock.Length != 4)
                throw new Exception();
            byte[] rgbBlock = new byte[BLOCK_SIZE_4X4X4];
            DecompressRGBABlock(rgbBlock, compressedBlock);
            return rgbBlock;
        }

        public static uint[] CompressRGBABlock_ExplicitAlpha(byte[] rgbBlock)
        {
            if (rgbBlock.Length != BLOCK_SIZE_4X4X4)
                throw new Exception();
            uint[] compressedBlock = new uint[4];
            CompressRGBABlock_ExplicitAlpha(rgbBlock, compressedBlock);
            return compressedBlock;
        }

        public static byte[] DecompressRGBABlock_ExplicitAlpha(uint[] compressedBlock)
        {
            if (compressedBlock.Length != 4)
                throw new Exception();
            byte[] rgbBlock = new byte[BLOCK_SIZE_4X4X4];
            DecompressRGBABlock_ExplicitAlpha(rgbBlock, compressedBlock);
            return rgbBlock;
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

        public static byte[] DecompressRGBBlock(uint[] compressedBlock, bool bDXT1)
        {
            if (compressedBlock.Length != 2)
                throw new Exception();
            byte[] rgbBlock = new byte[BLOCK_SIZE_4X4X4];
            DecompressRGBBlock(rgbBlock, compressedBlock, bDXT1 ? 1 : 0);
            return rgbBlock;
        }

        public static uint[] CompressAlphaBlock(byte[] alphaBlock)
        {
            if (alphaBlock.Length != BLOCK_SIZE_4X4BPP8)
                throw new Exception();
            uint[] compressedBlock = new uint[2];
            CompressAlphaBlock(alphaBlock, compressedBlock);
            return compressedBlock;
        }

        public static byte[] DecompressAlphaBlock(uint[] compressedBlock)
        {
            if (compressedBlock.Length != 2)
                throw new Exception();
            byte[] alphaBlock = new byte[BLOCK_SIZE_4X4BPP8];
            DecompressAlphaBlock(alphaBlock, compressedBlock);
            return alphaBlock;
        }
    }
}
