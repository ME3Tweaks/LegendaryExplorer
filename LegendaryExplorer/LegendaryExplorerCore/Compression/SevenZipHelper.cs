/*
 * SevenZip Helper
 *
 * Copyright (C) 2015-2018 Pawel Kolodziejski
 * Copyright (C) 2019 Michael Perez
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
 * This code use sevenzipwrapper.dll copied from MassEffectModder:
 * https://github.com/MassEffectModder/MassEffectModderLegacy/tree/master/MassEffectModder/Dlls
 *
 * The dll is created using LZMA SDK and MassEffectModder wrapper code:
 * https://github.com/MassEffectModder/MassEffectModderLegacy/tree/master/MassEffectModder/Helpers/7Zip
 *
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    // This class has additional methods that are used by external libraries
    // Do not remove them
    public static class LZMA
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int SevenZipDecompress(byte* srcBuf, uint srcLen, byte* dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SevenZipUnpackFile([In] string archive, [In] string outputpath, [In] int keepArchivePaths);

        public static int Decompress(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            uint srcLen = (uint)src.Length;
            uint dstLen = (uint)dst.Length;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(src))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(dst))
                {
                    return SevenZipDecompress(inPtr, srcLen, outPtr, ref dstLen);
                }
            }
        }

        /// <summary>
        /// Decompresses the input block (as a span) to the specified output block
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        public static int Decompress(ReadOnlySpan<byte> src, byte[] dst)
        {
            var dstLen = (uint) dst.Length;
            uint srcLen = (uint)src.Length;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(src))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(dst.AsSpan()))
                {
                    return SevenZipDecompress(inPtr, srcLen, outPtr, ref dstLen);
                }
            }
        }

        public static byte[] Decompress(byte[] src, uint dstLen)
        {
            /*
             Return codes are as follows:
            This library (CompressionWrappers) is built from aquadran's MassEffectModder repo in the libs folder.
             *#define SZ_OK 0
               
               #define SZ_ERROR_DATA 1
               #define SZ_ERROR_MEM 2
               #define SZ_ERROR_CRC 3
               #define SZ_ERROR_UNSUPPORTED 4
               #define SZ_ERROR_PARAM 5
               #define SZ_ERROR_INPUT_EOF 6
               #define SZ_ERROR_OUTPUT_EOF 7
               #define SZ_ERROR_READ 8
               #define SZ_ERROR_WRITE 9
               #define SZ_ERROR_PROGRESS 10
               #define SZ_ERROR_FAIL 11
               #define SZ_ERROR_THREAD 12
               
               #define SZ_ERROR_ARCHIVE 16
               #define SZ_ERROR_NO_ARCHIVE 17
             */
            uint len = dstLen;
            byte[] dst = new byte[dstLen];

            int status = SevenZipDecompress(src, (uint)src.Length, dst, ref len);
            if (status != 0)
                return Array.Empty<byte>();

            return dst;
        }

        public static byte[] Compress(byte[] src, int compressionLevel = 9)
        {
            uint dstLen = (uint)(src.Length * 2 + 8);
            byte[] tmpbuf = new byte[dstLen];

            int status = SevenZipCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
            if (status != 0)
                return Array.Empty<byte>();

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

            return dst;
        }

        public static bool ExtractSevenZipArchive(string archive, string outputpath, bool keepArchivePath = true)
        {
            Directory.CreateDirectory(outputpath); //must exist
            var result = SevenZipUnpackFile(archive, outputpath, keepArchivePath ? 1 : 0);
            return result == 0;
        }

        /// <summary>
        /// Compresses the input data and returns LZMA compressed data, with the proper header for an LZMA file.
        /// </summary>
        /// <param name="src">Source data</param>
        /// <returns>Byte array of compressed data</returns>
        public static byte[] CompressToLZMAFile(byte[] src)
        {
            var compressedBytes = LZMA.Compress(src);
            byte[] fixedBytes = new byte[compressedBytes.Length + 8]; //needs 8 byte header written into it (only mem version needs this)
            
            // Copy LZMA header info and write the full length of the data
            Buffer.BlockCopy(compressedBytes, 0, fixedBytes, 0, 5);
            fixedBytes.OverwriteRange(5, BitConverter.GetBytes(src.Length));
            // Copy the remaining data
            Buffer.BlockCopy(compressedBytes, 5, fixedBytes, 13, compressedBytes.Length - 5);
            return fixedBytes;
        }

        public static byte[] DecompressLZMAFile(byte[] lzmaFile)
        {
            int len = (int)BitConverter.ToInt32(lzmaFile, 5); //this is technically a 64-bit value, but since MEM code can't handle 64 bit sizes we are just going to use 32bit. We aren't going to have a 2GB+ single LZMA file

            if (len >= 0)
            {
                byte[] strippedData = new byte[lzmaFile.Length - 8];
                //Non-Streamed (made from disk)
                Buffer.BlockCopy(lzmaFile, 0, strippedData, 0, 5);
                Buffer.BlockCopy(lzmaFile, 13, strippedData, 5, lzmaFile.Length - 13);
                return Decompress(strippedData, (uint)len);
            }
            else if (len == -1)
            {
                throw new InvalidOperationException("Cannot decompress streamed LZMA with this implementation!");
            }
            else
            {
                Debug.WriteLine(@"Cannot decompress LZMA array: Length is not positive or -1 (" + len + @")! This is not an LZMA array");
                return null; //Not LZMA!
            }
        }

        public static void DecompressLZMAStream(Stream compressedStream, MemoryStream decompressedStream)
        {
            compressedStream.Seek(5, SeekOrigin.Begin);
            int len = compressedStream.ReadInt32();
            compressedStream.Seek(0, SeekOrigin.Begin);

            if (len >= 0)
            {
                byte[] strippedData = new byte[compressedStream.Length - 8];
                compressedStream.Read(strippedData, 0, 5);
                compressedStream.Seek(8, SeekOrigin.Current); //Skip 8 bytes for length.
                compressedStream.Read(strippedData, 5, (int)compressedStream.Length - 13);
                var decompressed = Decompress(strippedData, (uint)len);
                decompressedStream.WriteFromBuffer(decompressed);
            }
            else if (len == -1)
            {
                throw new Exception("Cannot decompress streamed LZMA with this implementation!");
            }
            else
            {
                Debug.WriteLine(@"LZMA Stream to decompress has wrong length: " + len);
            }
        }
    }
}