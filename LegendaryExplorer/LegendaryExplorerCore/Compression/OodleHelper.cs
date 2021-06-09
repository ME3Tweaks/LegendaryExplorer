using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    public class OodleHelper
    {
        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern int OodleLZ_Compress(OodleFormat format, in byte buffer, long bufferSize, byte[] outputBuffer, OodleCompressionLevel level,
            uint unused1, uint unused2, uint unused3,
            uint unused4, uint unused, uint unused6);
        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern int OodleLZ_Compress(OodleFormat format, in byte buffer, long bufferSize, in byte outputBuffer, OodleCompressionLevel level,
            uint unused1, uint unused2, uint unused3,
            uint unused4, uint unused, uint unused6);


        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern unsafe int OodleLZ_Decompress(byte* buffer, long bufferSize, byte* outputBuffer, long outputBufferSize,
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
#if AZURE
            string supportZip = null;
            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, "support");
                if (Directory.Exists(testDataPath))
                {
                    supportZip = Path.Combine(testDataPath, "LEDC.zip");
                    break;
                }
            }

            if (supportZip == null)
                throw new Exception("Could not find support directory!");


            ZipFile.ExtractToDirectory(supportZip, @"C:\Users\Public", true);
            return true;
#else
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
#endif

            return false;
        }

        public static int Compress(ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
        {
            int compressedCount;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(inputBuffer))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(outputBuffer))
                {
                    compressedCount = OodleLZ_Compress(OodleFormat.Leviathan, Unsafe.AsRef<byte>(inPtr), inputBuffer.Length, Unsafe.AsRef<byte>(outPtr), OodleCompressionLevel.Normal,
                                                       0, 0, 0, 0, 0, 0);
                }
            }
            return compressedCount;
        }

        public static byte[] Compress(ReadOnlySpan<byte> buffer, int size, OodleFormat format, OodleCompressionLevel level)
        {
            int compressedBufferSize = GetCompressionBound(size);
            byte[] compressedBuffer = MemoryManager.GetByteArray(compressedBufferSize); // we will not use all of this. someday we will want to improve this i think

            int compressedCount;
            unsafe
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
                {
                    compressedCount = OodleLZ_Compress(format, Unsafe.AsRef<byte>(ptr), buffer.Length, compressedBuffer, level, 0, 0, 0, 0, 0, 0);
                }
            }
            

            byte[] outputBuffer = new byte[compressedCount];
            Buffer.BlockCopy(compressedBuffer, 0, outputBuffer, 0, compressedCount);

            MemoryManager.ReturnByteArray(compressedBuffer);
            return outputBuffer;
        }

        public static int GetCompressionBound(int bufferSize)
        {
            // Not sure how to do this
            return bufferSize + 274 * ((bufferSize + 0x3FFFF) / 0x40000);
        }

        public static int Decompress(ReadOnlySpan<byte> buffer, Span<byte> decompressedBuffer)
        {
            long size = buffer.Length;
            long uncompressedSize = decompressedBuffer.Length;
            int decompressedCount;
            unsafe
            {
                fixed (byte* inPtr = &MemoryMarshal.GetReference(buffer))
                fixed (byte* outPtr = &MemoryMarshal.GetReference(decompressedBuffer))
                {
                    decompressedCount = OodleLZ_Decompress(inPtr, size, outPtr, uncompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
                }
            }
            if (decompressedCount != uncompressedSize)
            {
                throw new Exception("Error decompressing Oodle data!");
            }

            return decompressedCount;
        }
    }
}
