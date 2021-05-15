using System;
using System.IO;
using ME3ExplorerCore.Misc;

namespace ME3ExplorerCore.Packages
{
    /// <summary>
    /// Class that allows you to cache packages in memory for fast accessing, without having to use a global package cache like ME3Explorer's system
    /// </summary>
    public class PackageCache
    {
        private object syncObj = new object();
        /// <summary>
        /// Cache that should only be accessed read-only. Subclasses of this can reference this shared cache object
        /// </summary>
        public CaseInsensitiveConcurrentDictionary<IMEPackage> Cache { get; }= new CaseInsensitiveConcurrentDictionary<IMEPackage>();

        /// <summary>
        /// Thread-safe package cache fetch. Can be passed to various methods to help expedite operations by preventing package reopening. Packages opened with this method do not use the global ME3ExplorerCore caching system and will always load from disk if not in this local cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="openIfNotInCache">Open the specified package if it is not in the cache, and add it to the cache</param>
        /// <returns></returns>
        public virtual IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true)
        {
            // May need way to set maximum size of dictionary so we don't hold onto too much memory.
            lock (syncObj)
            {
                if (Cache.TryGetValue(packagePath, out var package))
                {
                    return package;
                }

                if (openIfNotInCache)
                {
                    if (File.Exists(packagePath))
                    {
                        package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
                        Cache[packagePath] = package;
                        return package;
                    }
                }
            }

            return null; //Package could not be found
        }

        public void InsertIntoCache(IMEPackage package)
        {
            Cache[package.FilePath] = package;
        }

        /// <summary>
        /// Releases packages referenced by this cache and forces a garbage collection to reclaim memory they may have used
        /// </summary>
        public void ReleasePackages(bool gc = false)
        {
            Cache.Clear();
            if (gc)
                GC.Collect();
        }
    }
}