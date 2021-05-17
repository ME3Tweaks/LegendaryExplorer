using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    /// <summary>
    /// This helper class works by copying the game's oodle dll to the native libs folder of LEC and pulling it in from there.
    /// </summary>
    class OodleHelper
    {
        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        //private static extern int OodleLZ_Compress(OodleFormat format, byte[] buffer, long bufferSize, byte[] outputBuffer, OodleCompressionLevel level);
        //private static extern int OodleLZ_Compress(OodleFormat format, byte[] buffer, long bufferSize, byte[] outputBuffer, OodleCompressionLevel level, uint unused1, uint unused2, uint unused3,);
        private static extern int OodleLZ_Compress(OodleFormat format, byte[] buffer, long bufferSize, byte[] outputBuffer, OodleCompressionLevel level, 
            uint unused1, uint unused2, uint unused3,
            uint unused4, uint unused, uint unused6);
            

        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern int OodleLZ_Decompress(byte[] buffer, long bufferSize, byte[] outputBuffer, long outputBufferSize,
            uint a, uint b, ulong c, uint d, uint e, uint f, uint g, uint h, uint i, uint threadModule);

        public enum OodleCompressionLevel : ulong
        {
            None,
            SuperFast,
            VeryFast,
            Fast,
            Normal,
            Optimal1,
            Optimal2,
            Optimal3,
            Optimal4,
            Optimal5
        }

        // Todo: Add Leviathan
        public enum OodleFormat : uint
        {
            LZH,
            LZHLW,
            LZNIB,
            None,
            LZB16,
            LZBLW,
            LZA,
            LZNA,
            Kraken,
            Mermaid,
            BitKnit,
            Selkie,
            Akkorokamui,
            Leviathan // 13 
        }

        public static bool EnsureOodleDll()
        {
            // Ported from M3
            // Required for single file .net 5

            var t = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES");
            if (t is string str)
            {
                var paths = str.Split(';');
                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    var tpath = Path.Combine(path, CompressionHelper.OODLE_DLL_NAME);
                    if (File.Exists(tpath))
                        return true;
                }


                if (LE1Directory.ExecutableFolder != null)
                {
                    var oodPath = Path.Combine(LE1Directory.ExecutableFolder, CompressionHelper.OODLE_DLL_NAME);
                    if (File.Exists(oodPath))
                    {

                        // Todo: FIX: CANNOT RUN IN TEST MODE
                        // Access denied to directory
                        var destPath = Path.Combine(paths.First(), CompressionHelper.OODLE_DLL_NAME);
                        File.Copy(oodPath, destPath, true);
                        return true;
                    }
                }
            }

            return false;
        }

        public static byte[] Compress(byte[] buffer, int size, OodleFormat format, OodleCompressionLevel level)
        {
            uint compressedBufferSize = GetCompressionBound((uint)size);
            byte[] compressedBuffer = MemoryManager.GetByteArray((int)compressedBufferSize); // we will not use all of this. someday we will want to improve this i think
            //byte[] compressedBuffer = MemoryManager.GetByteArray(size + (64 * (int)FileSize.KibiByte)); // we will not use all of this. someday we will want to improve this i think

            // OodleLZ_Compress is in dll
            //int compressedCount = OodleLZ_Compress(format, buffer, buffer.Length, compressedBuffer, level, 0, 0, 0);
            //int compressedCount = OodleLZ_Compress(format, buffer, buffer.Length, compressedBuffer, level, 0, 0, 0,);
            int compressedCount = OodleLZ_Compress(format, buffer, buffer.Length, compressedBuffer, level, 0, 0, 0,0,0,0);

            byte[] outputBuffer = new byte[compressedCount];
            Buffer.BlockCopy(compressedBuffer, 0, outputBuffer, 0, compressedCount);

            MemoryManager.ReturnByteArray(compressedBuffer);
            return outputBuffer;
        }

        private static uint GetCompressionBound(uint bufferSize)
        {
            // Not sure how to do this
            return bufferSize + 274 * ((bufferSize + 0x3FFFF) / 0x40000);
        }

        public static int Decompress(byte[] buffer, long size, long uncompressedSize, byte[] decompressedBuffer)
        {
            decompressedBuffer ??= new byte[uncompressedSize];
            int decompressedCount = OodleLZ_Decompress(buffer, size, decompressedBuffer, uncompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
            if (decompressedCount != uncompressedSize)
            {
                throw new Exception("Error decompressing Oodle data!");
            }

            return decompressedCount;
        }
    }
}
