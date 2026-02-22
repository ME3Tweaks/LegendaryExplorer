using System;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Package cache that can load packages from SFAR archives when the physical file doesn't exist on disk.
    /// </summary>
    public class SFARPackageCache : PackageCache
    {
        public override IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true, Func<string, IMEPackage> openPackageMethod = null)
        {
            // Try the base implementation first (handles cache lookups and normal file loading)
            var package = base.GetCachedPackage(packagePath, openIfNotInCache, openPackageMethod);
            
            // If base returned something or we're not supposed to open if not in cache, return
            if (package != null || !openIfNotInCache || packagePath == null)
            {
                return package;
            }
            
            // Lock for SFAR loading to ensure thread safety
            lock (syncObj)
            {
                // Double-check cache in case another thread loaded it from SFAR
                if (Cache.TryGetValue(packagePath, out package))
                {
                    LastAccessMap[packagePath] = DateTime.Now;
                    return package;
                }
                
                // Check if this is a DLC path (contains DLC\<DLC_FOLDER_NAME>\CookedPCConsole\)
                int dlcIndex = packagePath.IndexOf("/DLC/", StringComparison.OrdinalIgnoreCase);
                if (dlcIndex >= 0)
                {
                    // Find the CookedPCConsole folder
                    int cookedIndex = packagePath.IndexOf("/CookedPCConsole/", dlcIndex, StringComparison.OrdinalIgnoreCase);
                    if (cookedIndex >= 0)
                    {
                        // Get the path up to and including CookedPCConsole
                        string cookedPath = packagePath.Substring(0, cookedIndex + "/CookedPCConsole".Length);
                        string sfarPath = Path.Combine(cookedPath, "Default.sfar");
                        
                        if (File.Exists(sfarPath))
                        {
                            // Extract the filename from the full path (everything after CookedPCConsole\)
                            string fileName = packagePath.Substring(cookedIndex + "/CookedPCConsole/".Length);
                            
                            try
                            {
                                Debug.WriteLine($@"SFARPackageCache {guid} attempting to load from SFAR: {fileName}");
                                
                                // Open the SFAR archive
                                var dlcPackage = new DLCPackage(sfarPath);
                                
                                // Find the file entry in the SFAR
                                int entryIndex = dlcPackage.FindFileEntry(fileName);
                                
                                if (entryIndex >= 0)
                                {
                                    // Decompress the entry from the SFAR
                                    using var decompressedStream = dlcPackage.DecompressEntry(entryIndex);
                                    
                                    // Open as a package, preserving the original path
                                    package = MEPackageHandler.OpenMEPackageFromStream(decompressedStream, packagePath);
                                    
                                    // Insert into cache
                                    InsertIntoCache(package);
                                    
                                    Debug.WriteLine($@"SFARPackageCache {guid} loaded from SFAR: {fileName}");
                                }
                                else
                                {
                                    Debug.WriteLine($@"SFARPackageCache {guid} file not found in SFAR: {fileName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($@"SFARPackageCache {guid} error loading from SFAR: {ex.Message}");
                            }
                        }
                    }
                }
            }
            
            return package;
        }
    }
}
