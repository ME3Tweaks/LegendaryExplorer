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
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Compression
{
    /// <summary>
    /// This helper class works by copying the game's oodle dll to the native libs folder of LEC and pulling it in from there.
    /// </summary>
    public static class OodleHelper
    {
        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern unsafe int OodleLZ_Compress(OodleFormat format, byte* buffer, long bufferSize, byte* outputBuffer, OodleCompressionLevel level,
            ulong unused1, ulong unused2, ulong unused3,
            ulong unused4, ulong unused);

        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern unsafe int OodleLZ_Decompress(byte* buffer, long bufferSize, byte* outputBuffer, long outputBufferSize,
            uint a, uint b, uint c, ulong d, ulong e, ulong f, ulong g, ulong h, ulong i, ulong threadModule);

        [DllImport(CompressionHelper.OODLE_DLL_NAME)]
        private static extern long OodleLZ_GetCompressedBufferSizeNeeded(byte format, long bufferSize);

        private static bool dllLoaded;
        /// <summary>
        /// Loads the oodle dll into memory. It should not load out of game dir or it will lock the game directory in use, which
        /// will prevent deletion of the directory.
        /// </summary>
        /// <param name="dllPath"></param>
        public static void LoadOodleDll(string dllPath)
        {
            // This call is for this solution https://stackoverflow.com/a/8836934/800318 merged with native version https://stackoverflow.com/a/14967825/800318
            // Not sure there is a point to unloading dll since it will likely need to be used again later
            if (!dllLoaded)
            {
                LECLog.Information($@"Loading oodle library into memory from {dllPath}");
                NativeLibrary.Load(dllPath);
                dllLoaded = true;
            }
        }

        public enum OodleCompressionLevel : uint
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

        /// <summary>
        /// Ensures the oodle dll is available and loads the image into memory if it is found, so DllImports to oodle calls will work.
        /// </summary>
        /// <param name="gameRootPath"></param>
        /// <param name="storagePath"></param>
        /// <returns></returns>
        public static bool EnsureOodleDll(string gameRootPath = null, string storagePath = null)
        {
            if (dllLoaded)
            {
                return true;
            }
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
            LoadOodleDll(@"C:\Users\Public\LEDC.dll");
            return true;
#else
            LECLog.Information($@"Attempting to source oodle dll from filesystem.");
            if (storagePath != null && Directory.Exists(storagePath))
            {
                var fullStoragePath = Path.Combine(storagePath, CompressionHelper.OODLE_DLL_NAME);
                if (File.Exists(fullStoragePath) && new FileInfo(fullStoragePath).Length > 0)
                {
                    LoadOodleDll(fullStoragePath);
                    return true; // OK
                }

                string oodleGamePath = null;
                if (gameRootPath != null)
                {
                    oodleGamePath = Path.Combine(gameRootPath, @"Binaries", @"Win64", CompressionHelper.OODLE_DLL_NAME);
                    if (!File.Exists(oodleGamePath))
                        oodleGamePath = null;
                }
                else if (oodleGamePath == null && LE1Directory.ExecutableFolder != null)
                {
                    oodleGamePath = Path.Combine(LE1Directory.ExecutableFolder, CompressionHelper.OODLE_DLL_NAME);
                }

                if (oodleGamePath != null && File.Exists(oodleGamePath))
                {
                    LECLog.Information($@"Caching oodle dll: {oodleGamePath} -> {fullStoragePath}");
                    File.Copy(oodleGamePath, fullStoragePath, true);
                    LoadOodleDll(fullStoragePath);
                    return true;
                }
            }

            // check native search directories
            var t = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES");
            if (t is string str && !string.IsNullOrWhiteSpace(str))
            {
                var paths = str.Split(';');
                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string tpath = null;
                    try
                    {
                        tpath = Path.Combine(path, CompressionHelper.OODLE_DLL_NAME);
                        if (File.Exists(tpath))
                        {
                            LoadOodleDll(tpath);
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        LECLog.Warning($@"Error looking up native search directory {tpath}: {e.Message}, skipping");
                    }
                }

                //possible that someone might have deleted one or two of the LE games to save disk space
                string anLEExecutableFolder = LE1Directory.ExecutableFolder ?? LE2Directory.ExecutableFolder ?? LE3Directory.ExecutableFolder;
                if (anLEExecutableFolder is not null)
                {
                    string oodPath = Path.Combine(anLEExecutableFolder, CompressionHelper.OODLE_DLL_NAME);
                    if (File.Exists(oodPath) && paths.Length > 0)
                    {
                        // Todo: FIX: CANNOT RUN IN TEST MODE
                        // Access denied to directory
                        try
                        {
                            string destPath = storagePath != null ? Path.Combine(storagePath, CompressionHelper.OODLE_DLL_NAME) : Path.Combine(paths.First(), CompressionHelper.OODLE_DLL_NAME);
                            LECLog.Information($@"Caching oodle dll: {oodPath} -> {destPath}");
                            File.Copy(oodPath, destPath, true);
                            LoadOodleDll(destPath);
                        }
                        catch (UnauthorizedAccessException e)
                        {
                            // I guess just try to load it... might lock the folder :(
                            LECLog.Error($@"Could not copy Oodle dll to native dll directory, loading directly out of game dir instead: {oodPath}");
                            LoadOodleDll(oodPath);
                        }

                        return true;
                    }
                }
            }
#endif

            LECLog.Warning(@"Failed to source oodle dll from filesystem");
            return false;
        }

        public static int Compress(ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
        {
            int compressedCount;
            unsafe
            {
                fixed (byte* inPtr = inputBuffer)
                fixed (byte* outPtr = outputBuffer)
                {
                    compressedCount = OodleLZ_Compress(OodleFormat.Leviathan, inPtr, inputBuffer.Length, outPtr, OodleCompressionLevel.Normal,
                                                       0, 0, 0, 0, 0);
                }
            }
            return compressedCount;
        }

        public static byte[] Compress(ReadOnlySpan<byte> buffer)
        {
            int compressedBufferSize = GetCompressionBound(buffer.Length);
            byte[] compressedBuffer = MemoryManager.GetByteArray(compressedBufferSize); // we will not use all of this. someday we will want to improve this i think

            int compressedCount = Compress(buffer, compressedBuffer.AsSpan());

            byte[] outputBuffer = new byte[compressedCount];
            Buffer.BlockCopy(compressedBuffer, 0, outputBuffer, 0, compressedCount);

            MemoryManager.ReturnByteArray(compressedBuffer);
            return outputBuffer;
        }

        public static int GetCompressionBound(int bufferSize) => (int)OodleLZ_GetCompressedBufferSizeNeeded((byte)OodleFormat.Leviathan, bufferSize);

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
