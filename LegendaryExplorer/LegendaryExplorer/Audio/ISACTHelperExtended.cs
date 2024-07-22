using LegendaryExplorer.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Audio
{
    internal partial class ISACTHelperExtended
    {
        [LibraryImport(@"ISACTTools.dll")]
        private static partial int CreateIPSOgg([In] byte[] wavedata, uint waveDataLen, [Out] byte[] dstBuf, uint dstLen, float quality);

        // Encodes a .wav file to .ogg
        public static byte[] ConvertWaveToOgg(byte[] wavData, float quality)
        {
            byte[] outputBuffer = new byte[wavData.Length]; // It will always be smaller than this
            var result = CreateIPSOgg(wavData, (uint)wavData.Length, outputBuffer, (uint)outputBuffer.Length, quality);
            if (result > 0)
            {
                return outputBuffer.Take(result).ToArray();
            }

            return null; // Data segment was not found / ogg was not encoded.
        }
    }
}
