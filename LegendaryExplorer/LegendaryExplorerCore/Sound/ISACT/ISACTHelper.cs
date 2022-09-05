using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Sound.ISACT
{
    /// <summary>
    /// Contains methods related to working with ISACT content
    /// </summary>
    public static class ISACTHelper
    {
        [DllImport(@"ISACTTools.dll", CharSet = CharSet.Ansi)]
        private static extern int CreateBioSoundNodeWaveStreamingData([In] string icbPath, [In] string isbPath,
             byte[] dstBuf, ulong dstLen);

        public static string GenerateSoundNodeWaveStreamingData(ExportEntry wsdExport, string icbPath, string isbPath)
        {
            if (icbPath is null || isbPath is null || wsdExport is null)
                throw new Exception("No arguments can be null");

            if (!File.Exists(icbPath))
                throw new Exception($"ICB path not available: {icbPath}");

            if (!File.Exists(isbPath))
                throw new Exception($"ISB path not available: {isbPath}");

            byte[] outputBuf = new byte[8 * FileSize.MebiByte];
            var numBytesWritten =
                CreateBioSoundNodeWaveStreamingData(icbPath, isbPath, outputBuf, (ulong)outputBuf.Length);
            if (numBytesWritten < 0)
            {
                // Handle errors here
                return $"Error creating data, library returned code {numBytesWritten}";
            }

            MemoryStream ms = new MemoryStream();
            ms.WriteInt32(0);
            ms.Write(outputBuf, 0, numBytesWritten);
            ms.Seek(0, SeekOrigin.Begin);
            ms.WriteInt32((int)ms.Length - 4);
            wsdExport.WriteBinary(ms.ToArray());

            return null;
        }
    }
}
