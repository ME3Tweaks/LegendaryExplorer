using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Class that allows you to cache packages in memory for fast accessing, without having to use a global package cache like ME3Explorer's system. Can be subclassed for specific implementations.
    /// </summary>
    public class PackageCache : IDisposable
    {
        private Guid guid = Guid.NewGuid(); // For logging
        /// <summary>
        /// Object used for synchronizing for threads
        /// </summary>
        public readonly object syncObj = new();
        /// <summary>
        /// Cache that should only be accessed read-only. Subclasses of this can reference this shared cache object
        /// </summary>
        public CaseInsensitiveConcurrentDictionary<IMEPackage> Cache { get; } = new();

        /// <summary>
        /// Thread-safe package cache fetch. Can be passed to various methods to help expedite operations by preventing package reopening. Packages opened with this method do not use the global LegendaryExplorerCore caching system and will always load from disk if not in this local cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="openIfNotInCache">Open the specified package if it is not in the cache, and add it to the cache</param>
        /// <returns></returns>
        public virtual IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true)
        {
            // Cannot look up null paths
            if (packagePath == null)
                return null;

            // May need way to set maximum size of dictionary so we don't hold onto too much memory.
            lock (syncObj)
            {
                if (Cache.TryGetValue(packagePath, out var package))
                {
                    //Debug.WriteLine($@"PackageCache hit: {packagePath}");
                    return package;
                }

                if (openIfNotInCache)
                {
                    if (File.Exists(packagePath))
                    {
                        Debug.WriteLine($@"PackageCache {guid} load: {packagePath}");
                        package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
                        Cache[packagePath] = package;
                        return package;
                    }

                    Debug.WriteLine($@"PackageCache {guid} miss: File not found: {packagePath}");
                }
            }

            return null; //Package could not be found
        }

        public void InsertIntoCache(IMEPackage package)
        {
            Cache[package.FilePath] = package;
        }

        public void InsertIntoCache(IEnumerable<IMEPackage> packages)
        {
            foreach (var package in packages)
            {
                Cache[package.FilePath] = package;
            }
        }

        /// <summary>
        /// Releases packages referenced by this cache and can optionally force a garbage collection to reclaim memory they may have used
        /// </summary>
        public void ReleasePackages(bool gc = false)
        {
            foreach (var p in Cache.Values)
            {
                p.Dispose();
            }

            Cache.Clear();
            if (gc)
                GC.Collect();
        }

        /// <summary>
        /// Attempts to open or return the existing cached package. Returns true if a package was either in the cache or was loaded from disk,
        /// false otherwise. Ignores null filepaths.
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="openIfNotInCache"></param>
        /// <param name="cachedPackage"></param>
        /// <returns></returns>
        public virtual bool TryGetCachedPackage(string filepath, bool openIfNotInCache, out IMEPackage cachedPackage)
        {
            cachedPackage = GetCachedPackage(filepath, openIfNotInCache);
            return cachedPackage != null;
        }

        public void Dispose()
        {
            ReleasePackages();
        }

        /// <summary>
        /// Checks if the specified package path is held by a package in the cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <returns></returns>
        public virtual bool CacheContains(string packagePath)
        {
            return Cache.ContainsKey(packagePath);
        }

        /// <summary>
        /// Drops a package from the cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <returns></returns>
        public virtual bool DropPackageFromCache(string packagePath)
        {
            Cache.Remove(packagePath, out var pack);
            return pack != null;
        }
    }
}