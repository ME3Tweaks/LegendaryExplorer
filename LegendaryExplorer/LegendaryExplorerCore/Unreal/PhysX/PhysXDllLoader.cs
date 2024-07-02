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

        private static bool cookingDllLoaded;

        private static void LoadCookingDll(string dllPath)
        {
            if (!cookingDllLoaded)
            {
                LECLog.Information($@"Loading physx cooking library into memory from {dllPath}");
                NativeLibrary.Load(dllPath);
                cookingDllLoaded = true;
            }
        }
        public static bool EnsureCookingDll()
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
                        tpath = Path.Combine(path, PHYSXCOOKING64_DLL);
                        if (File.Exists(tpath))
                        {
                            LoadCookingDll(tpath);
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
                    string dllPath = Path.Combine(anLEExecutableFolder, PHYSXCOOKING64_DLL);
                    if (File.Exists(dllPath) && paths.Length > 0)
                    {
                        try
                        {
                            string destPath = Path.Combine(paths.First(), PHYSXCOOKING64_DLL);
                            LECLog.Information($@"Caching physx cooking dll: {dllPath} -> {destPath}");
                            File.Copy(dllPath, destPath, true);
                            LoadCookingDll(destPath);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // I guess just try to load it... might lock the folder :(
                            LECLog.Error($@"Could not copy physx cooking dll to native dll directory, loading directly out of game dir instead: {dllPath}");
                            LoadCookingDll(dllPath);
                        }

                        return true;
                    }
                }
            }

            LECLog.Warning(@"Failed to source physx cooking dll from filesystem");
            return false;
        }
    }
}
