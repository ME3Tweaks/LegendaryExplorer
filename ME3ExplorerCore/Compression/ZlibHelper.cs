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
using System.Runtime.InteropServices;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Compression
{
    public static class Zlib
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZlibDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ZlibCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        public static uint Decompress(byte[] src, uint srcLen, byte[] dst)
        {
            uint dstLen = (uint)dst.Length;

            int status = ZlibDecompress(src, srcLen, dst, ref dstLen);
            if (status != 0)
                return 0;

            return dstLen;
        }

        public static byte[] Compress(byte[] src, int compressionLevel = -1)
        {
            byte[] tmpbuf = new byte[(src.Length * 2) + 128];
            uint dstLen = (uint)tmpbuf.Length;

            int status = ZlibCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
            if (status != 0)
                return new byte[0];

            byte[] dst = new byte[dstLen];
            Array.Copy(tmpbuf, dst, (int)dstLen);

            return dst;
        }
    }
}
