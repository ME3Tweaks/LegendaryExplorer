/*
 * LZO2 Helper
 *
 * Copyright (C) 2014-2018 Pawel Kolodziejski
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

/*
 * This code use lzo2wrapper.dll copied from MassEffectModder:
 * https://github.com/MassEffectModder/MassEffectModderLegacy/tree/master/MassEffectModder/Dlls
 *
 * The dll is created using LZO2 library and MassEffectModder wrapper code:
 * https://github.com/MassEffectModder/MassEffectModderLegacy/tree/master/MassEffectModder/Helpers/LZO2
 *
 */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    public static class LZO2
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int LZODecompress(byte* srcBuf, uint srcLen, byte* dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int LZOCompress(byte* srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int LZOCompress(in byte srcBuf, uint srcLen, in byte dstBuf, ref uint dstLen);

        public static uint Decompress(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            uint srcLen = (uint)src.Length;
            uint dstLen = (uint)dst.Length;
            int status;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(src))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(dst))
                {
                    status = LZODecompress(inPtr, srcLen, outPtr, ref dstLen);
                }
            }
            if (status != 0)
                return 0;

            return dstLen;
        }

        public static byte[] Compress(ReadOnlySpan<byte> src)
        {
            var bufLen = GetCompressionBound(src.Length);
            byte[] tmpBuf = MemoryManager.GetByteArray(bufLen);
            uint dstLen = 0;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(src))
                {
                    int status = LZOCompress(inPtr, (uint)src.Length, tmpBuf, ref dstLen);
                    if (status != 0)
                        return Array.Empty<byte>();
                }
            }

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpBuf, dst, dstLen);

            MemoryManager.ReturnByteArray(tmpBuf);
            return dst;
        }

        public static int Compress(ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
        {
            uint compressedCount = (uint)outputBuffer.Length;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(inputBuffer))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(outputBuffer))
                {
                    int status = LZOCompress(Unsafe.AsRef<byte>(inPtr), (uint)inputBuffer.Length, Unsafe.AsRef<byte>(outPtr), ref compressedCount);
                    if (status != 0)
                    {
                        return 0;
                    }
                }
            }
            return (int)compressedCount;
        }

        public static int GetCompressionBound(int length)
        {
            return length + length / 16 + 64 + 3;
        }
    }
}
