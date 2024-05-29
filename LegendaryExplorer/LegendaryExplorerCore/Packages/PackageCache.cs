using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Class that allows you to cache packages in memory for fast accessing, without having to use a global package cache like ME3Explorer's system. Can be subclassed for specific implementations.
    /// </summary>
    public class PackageCache : IDisposable
    {
        /// <summary>
        /// Unique identifier for this cache
        /// </summary>
        public readonly Guid guid = Guid.NewGuid(); // For logging
        /// <summary>
        /// Object used for synchronizing for threads
        /// </summary>
        public readonly object syncObj = new();
        /// <summary>
        /// Cache that should only be accessed read-only. Subclasses of this can reference this shared cache object
        /// </summary>
        public CaseInsensitiveConcurrentDictionary<IMEPackage> Cache { get; } = new();

        /// <summary>
        /// The last access order. Packages at the bottom are the last accessed, the ones at the top are first.
        /// This is only for dropping packages if the count is not 0.
        /// </summary>
        public readonly CaseInsensitiveConcurrentDictionary<DateTime> LastAccessMap = new();

        public PackageCache() { }

        /// <summary>
        /// The list of packages that will not be dropped from last access staleness
        /// </summary>
        public readonly List<string> ResidentPackages = new();

        /// <summary>
        /// The maximum amount of packages this cache can hold open at a time. The default is unlimited (0). Global packages like SFXGame, Core, etc do not count against this.
        /// When a new package is opened, the stalest package is dropped if the amount of open packages exceeds this number. 
        /// </summary>
        public int CacheMaxSize { get; set; }

        /// <summary>
        /// When the <see cref="PackageCache"/> opens a package, should it always load from disk, or should it acquire one from the global cache if possible.
        /// Defaults to <value>true</value>
        /// </summary>
        public bool AlwaysOpenFromDisk { get; init; } = true;

        /// <summary>
        /// Thread-safe package cache fetch. Can be passed to various methods to help expedite operations by preventing package reopening.
        /// If <see cref="AlwaysOpenFromDisk"/> is true, then packages opened with this method will not use the global LegendaryExplorerCore caching system and will always load from disk if not in this local cache.
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="openIfNotInCache">Open the specified package if it is not in the cache, and add it to the cache</param>
        /// <returns></returns>
        public virtual IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true, Func<string, IMEPackage> openPackageMethod = null)
        {
            // Cannot look up null paths
            if (packagePath == null)
                return null;

            // May need way to set maximum size of dictionary so we don't hold onto too much memory.
            lock (syncObj)
            {
                if (Cache.TryGetValue(packagePath, out IMEPackage package))
                {
                    //Debug.WriteLine($@"PackageCache hit: {packagePath}");
                    LastAccessMap[packagePath] = DateTime.Now; // Update access time
                    return package;
                }

                if (openIfNotInCache)
                {
                    if (File.Exists(packagePath))
                    {
                        Debug.WriteLine($@"PackageCache {guid} load: {packagePath}");
                        package = openPackageMethod?.Invoke(packagePath) ?? MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: AlwaysOpenFromDisk);
                        InsertIntoCache(package);
                        return package;
                    }

                    Debug.WriteLine($@"PackageCache {guid} miss: File not found: {packagePath}");
                }
            }

            return null; //Package could not be found
        }

        /// <summary>
        /// Inserts a package into the the cache, based on its file path.
        /// </summary>
        /// <param name="package"></param>
        public virtual void InsertIntoCache(IMEPackage package)
        {
            Cache[package.FilePath] = package;
            LastAccessMap[package.FilePath] = DateTime.Now;
            CheckCacheFullness();
        }

        /// <summary>
        /// Makes the specified package, if in the cache, not drop when the cache is full. If the cache is uncapped, this does nothing.
        /// </summary>
        /// <param name="package"></param>
        public virtual void AddResidentPackage(IMEPackage package)
        {
            if (package.FilePath != null)
                AddResidentPackage(package.FilePath);
        }

        /// <summary>
        /// Makes the specified package path, if in the cache, not drop when the cache is full. If the cache is uncapped, this does nothing.
        /// </summary>
        public virtual void AddResidentPackage(string packagePath)
        {
            if (CacheMaxSize <= 0)
                return; // Does nothing.

            if (CacheContains(packagePath) && !ResidentPackages.Contains(packagePath))
            {
                ResidentPackages.Add(packagePath);
            }
        }

        public virtual void CheckCacheFullness()
        {
            if (CacheMaxSize > 1 && Cache.Count > CacheMaxSize)
            {
                var accessOrder = LastAccessMap.OrderBy(x => x.Value).ToList();
                while (CacheMaxSize > 1 && Cache.Count > CacheMaxSize)
                {
                    // Find the oldest package
                    if (!IsResidentPackage(accessOrder[0].Key))
                    {
                        ReleasePackage(accessOrder[0].Key);
                    }
                    accessOrder.RemoveAt(0);
                }
            }

            if (CacheMaxSize == 0)
            {
                //Debug.WriteLine(guid);
                //Debugger.Break();
            }
        }

        /// <summary>
        /// Returns if this package is marked as a Resident Package. Packages without <see cref="IMEPackage.FilePath"/> set will return false.
        /// </summary>
        /// <param name="package">Package object to check</param>
        /// <returns></returns>
        public bool IsResidentPackage(IMEPackage package)
        {
            if (package.FilePath != null)
            {
                return IsResidentPackage(package.FilePath);
            }

            return false;
        }

        /// <summary>
        /// Returns if this package path (case insensitive) is marked as a Resident Package.
        /// </summary>
        /// <param name="packagePath">Path of the package file</param>
        /// <returns></returns>
        public virtual bool IsResidentPackage(string packagePath)
        {
            return ResidentPackages.Contains(packagePath, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Releases a package by it's filepath from the cache.
        /// </summary>
        /// <param name="packagePath"></param>
        public virtual void ReleasePackage(string packagePath)
        {
            if (Cache.Remove(packagePath, out var package))
            {
                Debug.WriteLine($"Package Cache {guid} dropping package: {packagePath}");
                LastAccessMap.TryRemove(packagePath, out _);
            }
        }

        public void InsertIntoCache(IEnumerable<IMEPackage> packages)
        {
            foreach (var package in packages)
            {
                InsertIntoCache(package);
            }
        }

        /// <summary>
        /// Releases all packages referenced by this cache and can optionally force a garbage collection to reclaim memory they may have used. This also empties resident packages.
        /// </summary>
        public virtual void ReleasePackages(bool gc = false)
        {
            foreach (var p in Cache.Values)
            {
                p.Dispose();
            }

            LastAccessMap.Clear();
            Cache.Clear();
            if (gc)
                GC.Collect();
        }

        /// <summary>
        /// Releases all packages referenced by this cache that match the specified predicate, and can optionally force a garbage collection to reclaim memory they may have used. This does not remove resident packages.
        /// </summary>
        public void ReleasePackages(Predicate<string> packagesToDropPredicate, bool gc = false)
        {
            var keys = Cache.Keys.ToList();
            foreach (var key in keys)
            {
                if (!ResidentPackages.Contains(key) && (packagesToDropPredicate?.Invoke(key) ?? true))
                {
                    Cache[key].Dispose();
                    Cache.Remove(key, out _);
                    LastAccessMap.TryRemove(key, out _);
                }
            }

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
            if (cachedPackage != null && !openIfNotInCache)
            {
                LastAccessMap[filepath] = DateTime.Now; // Update access time
            }
            return cachedPackage != null;
        }

        public virtual void Dispose()
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

        /// <summary>
        /// Enumerates the list of files and returns the first one that is already present in the cache, or null if none of the files are currently in the cache.
        /// </summary>
        /// <param name="packageNames">List of package names to test, in order, if they're in the cache</param>
        /// <returns></returns>
        public virtual IMEPackage GetFirstCachedPackage(IEnumerable<string> packageNames)
        {
            foreach (var pn in packageNames)
            {
                if (Cache.TryGetValue(pn, out var cached))
                    return cached;
            }

            return null;
        }

        public virtual IReadOnlyCollection<IMEPackage> GetPackages()
        {
            return new ReadOnlyCollection<IMEPackage>(Cache.Values.ToList());
        }

        /// <summary>
        /// Gets the list of packages in this cache - the list is a copy
        /// </summary>
        /// <returns></returns>
        public virtual List<IMEPackage> GetPackageList()
        {
            return Cache.Values.ToList();
        }
    }
}