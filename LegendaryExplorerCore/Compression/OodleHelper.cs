using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    /// <summary>
    /// This helper class works by copying the game's oodle dll to the native libs folder of LEC and pulling it in from there.
    /// </summary>
    class OodleHelper
    {
        [DllImport(CompressionHelper.OODLE_DLL_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private static extern int OodleLeviathanDecompress([In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        //[DllImport(CompressionHelper.OODLE_DLL_NAME, CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        //private static extern int OodleLeviathanCompress(int compressionLevel, [In] byte[] srcBuf, uint srcLen, [Out] byte[] dstBuf, ref uint dstLen);

        public static uint Decompress(byte[] src, uint srcLen, byte[] dst, uint dstLen = 0)
        {
            if (dstLen == 0)
                dstLen = (uint)dst.Length;

            int status = OodleLeviathanDecompress(src, srcLen, dst, ref dstLen);
            if (status != 0)
                return 0;

            return dstLen;
        }

        //public static byte[] Compress(byte[] src, int compressionLevel = -1)
        //{
        //    byte[] tmpbuf = new byte[(src.Length * 2) + 128];
        //    uint dstLen = (uint)tmpbuf.Length;

        //    int status = ZlibCompress(compressionLevel, src, (uint)src.Length, tmpbuf, ref dstLen);
        //    if (status != 0)
        //        return new byte[0];

        //    byte[] dst = new byte[dstLen];
        //    Array.Copy(tmpbuf, dst, (int)dstLen);

        //    return dst;
        //}
    }
}
