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
    internal class ISACTHelperExtended
    {

        [DllImport(@"ISACTTools.dll")]
        private static extern int CreateIPSOgg(byte[] wavedata, uint waveDataLen, byte[] dstBuf, uint dstLen, float quality);

        // Encodes a .wav file to .ogg
        public static byte[] ConvertWaveToOgg(byte[] wavData, float quality)
        {
            byte[] outputBuffer = new byte[wavData.Length]; // It will always be smaller than this
            var result = CreateIPSOgg(wavData, (uint)wavData.Length, outputBuffer, (uint)outputBuffer.Length, quality);
            return outputBuffer.Take(result).ToArray();
        }
    }
}
