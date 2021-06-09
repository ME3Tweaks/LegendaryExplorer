/*
 * Zlib Helper
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski
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
 * The dll is created using Zlib, MiniZlib, FastZlib libraries and MassEffectModder wrapper code:
 * https://github.com/MassEffectModder/MassEffectModderLegacy/tree/master/MassEffectModder/Helpers/Zlib
 *
 */

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    public static class Zlib
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int ZlibDecompress(byte* srcBuf, uint srcLen, byte* dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int ZlibCompress(int compressionLevel, byte* srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZlibCompress(int compressionLevel, in byte srcBuf, uint srcLen, in byte dstBuf, ref uint dstLen);

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
                    status = ZlibDecompress(inPtr, srcLen, outPtr, ref dstLen);
                }
            }
            if (status != 0)
                return 0;

            return dstLen;
        }

        public static byte[] Compress(ReadOnlySpan<byte> src, int compressionLevel = -1)
        {
            byte[] tmpbuf = new byte[GetCompressionBound(src.Length)];
            uint dstLen = (uint)tmpbuf.Length;

            unsafe
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(src))
                {
                    int status = ZlibCompress(compressionLevel, ptr, (uint)src.Length, tmpbuf, ref dstLen);
                    if (status != 0)
                        return new byte[0];
                }
            }

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

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
                    int status = ZlibCompress(-1, Unsafe.AsRef<byte>(inPtr), (uint)inputBuffer.Length, Unsafe.AsRef<byte>(outPtr), ref compressedCount);
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
            return length + ((length + 7) >> 3) + ((length + 63) >> 6) + 11;
        }
    }
}
