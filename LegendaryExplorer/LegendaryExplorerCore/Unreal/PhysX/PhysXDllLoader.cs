using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;

namespace LegendaryExplorerCore.Unreal.PhysX
{
    internal static class PhysXDllLoader
    {
        internal const string PHYSXCOOKING64_DLL = "PhysXCooking64.dll";
        internal const string PHYSXCORE64_DLL = "PhysXCore64.dll";
        internal const string PHYSXLOADER64_DLL = "PhysXLoader64.dll";

        private static bool cookingDllLoaded;
        private static bool coreDllLoaded;
        private static bool loaderDllLoaded;

        public static bool EnsureCookingDll() => EnsureDll(PHYSXCOOKING64_DLL, ref cookingDllLoaded);

        public static bool EnsureCoreDll() => EnsureDll(PHYSXCORE64_DLL, ref coreDllLoaded);

        public static bool EnsureLoaderDll() => EnsureDll(PHYSXLOADER64_DLL, ref loaderDllLoaded, "PhysXLocal");

        private static void LoadDll(string dllPath, ref bool dllLoaded)
        {
            if (!dllLoaded)
            {
                LECLog.Information($@"Loading physx library into memory from {dllPath}");
                NativeLibrary.Load(dllPath);
                dllLoaded = true;
            }
        }

        private static bool EnsureDll(string filename, ref bool dllLoaded, string srcRelativePath = null)
        {
            if (cookingDllLoaded)
            {
                return true;
            }

            // check native search directories
            object t = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES");
            if (t is string str && !string.IsNullOrWhiteSpace(str))
            {
                string[] paths = str.Split(';');
                foreach (string path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    string tpath = null;
                    try
                    {
                        tpath = Path.Combine(path, filename);
                        if (File.Exists(tpath))
                        {
                            LoadDll(tpath, ref dllLoaded);
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        LECLog.Warning($@"Error looking up native search directory {tpath}: {e.Message}, skipping");
                    }
                }

                //possible that someone might have deleted one or two of the LE games to save disk space
                string anLEExecutableFolder = LE3Directory.ExecutableFolder ?? LE2Directory.ExecutableFolder ?? LE1Directory.ExecutableFolder;
                if (anLEExecutableFolder is not null)
                {
                    string dllPath = srcRelativePath is null ? Path.Combine(anLEExecutableFolder, filename) : Path.Combine(anLEExecutableFolder, srcRelativePath, filename);
                    if (File.Exists(dllPath) && paths.Length > 0)
                    {
                        try
                        {
                            string destPath = Path.Combine(paths.First(), filename);
                            LECLog.Information($@"Caching {filename}: {dllPath} -> {destPath}");
                            File.Copy(dllPath, destPath, true);
                            LoadDll(destPath, ref dllLoaded);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // I guess just try to load it... might lock the folder :(
                            LECLog.Error($@"Could not copy {filename} to native dll directory, loading directly out of game dir instead: {dllPath}");
                            LoadDll(dllPath, ref dllLoaded);
                        }

                        return true;
                    }
                }
            }

            LECLog.Warning($@"Failed to source {filename} from filesystem");
            return false;
        }
    }
}
