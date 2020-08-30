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

using System.Runtime.InteropServices;

namespace ME3ExplorerCore.Compression
{
    public static class LZX
    {
        [DllImport("lzxdhelper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int LZXDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        /// <summary>
        /// Decompresses LZX data. The return value will be 0 if the data decompressed OK
        /// </summary>
        /// <param name="src"></param>
        /// <param name="srcLen"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        public static int Decompress(byte[] src, uint srcLen, byte[] dst)
        {
            uint dstLen = (uint)dst.Length;
            return LZXDecompress(src, srcLen, dst, ref dstLen);
        }
    }
}
