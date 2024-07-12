/*
 * LZX Helper
 *
 * Decompresion helper for Xbox LZX compression
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
 * The dll is created using code from UEViewer:
 * https://github.com/gildor2/UEViewer
 */

using System;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    public static class LZX
    {
        [DllImport(CompressionHelper.COMPRESSION_WRAPPER_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int LZXDecompress(byte* srcBuf, uint srcLen, byte* dstBuf, uint* dstLen);

        /// <summary>
        /// Decompresses LZX data. The return value will be 0 if the data decompressed OK
        /// </summary>
        /// <param name="src"></param>
        /// <param name="srcLen"></param>
        /// <param name="dest"></param>
        /// <param name="dstLen"></param>
        /// <returns></returns>
        public static int Decompress(ReadOnlySpan<byte> src, uint srcLen, Span<byte> dest, uint dstLen = 0)
        {
            if (dstLen == 0)
                dstLen = (uint)dest.Length;
            unsafe
            {
                fixed (byte* srcPtr = &MemoryMarshal.GetReference(src))
                fixed (byte* destPtr = &MemoryMarshal.GetReference(dest))
                {
                    return LZXDecompress(srcPtr, srcLen, destPtr, &dstLen);
                }
            }
        }

        /// <summary>
        /// Decompresses LZX data. The return value will be 0 if the data decompressed OK
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        public static int Decompress(ReadOnlySpan<byte> src, Span<byte> dest)
        {
            uint srcLen = (uint)src.Length;
            uint dstLen = (uint)dest.Length;
            unsafe
            {
                fixed (byte* srcPtr = &MemoryMarshal.GetReference(src))
                fixed (byte* destPtr = &MemoryMarshal.GetReference(dest))
                {
                    return LZXDecompress(srcPtr, srcLen, destPtr, &dstLen);
                }
            }
        }
    }
}
