using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorerCore.Packages;

/// <summary>
/// PackageCache implementation that allows looking for packages in parent caches. Parent caches will not open packages on cache miss.
/// </summary>
public class TieredPackageCache : PackageCache
{
#if DEBUG
    /// <summary>
    /// Use to profile how often all tiered caches load files by name. You can find hot files using this to promote them to a higher tier for performance.
    /// </summary>
    public static Dictionary<MEGame, CaseInsensitiveDictionary<int>> StaticTieredHeatMap { get; } = new();
#endif

    /// <summary>
    /// If children caches can promote packages into this cache. Must be careful; too many promotions will result in high memory usage if the cache is long lived
    /// </summary>
    public bool CanBePromotedInto { get; set; }

    /// <summary>
    /// If this package cache should use filenames (rather than file paths) for cache lookups.
    /// </summary>
    public bool FilenameOnlyMode { get; set; }

    /// <summary>
    /// Cache to also look in for packages
    /// </summary>
    public TieredPackageCache ParentCache;

    /// <summary>
    /// Used by children to synchronize the promo dictionary for tracking
    /// </summary>
    private object promoSyncObj = new object();

    /// <summary>
    /// On access, will initialize global packages.
    /// </summary>
    private bool GlobalInitOnFirstUse { get; set; }

    /// <summary>
    /// If next lookup in FilenameOnlyMode should refresh game files
    /// </summary>
    public bool ReloadFileList { get; set; }

    /// <summary>
    /// After child caches open the same package this many times, the package will be promoted into the parent cache. The parent cache must have 'CanBePromotedInto' set to true for this to do anything.
    /// </summary>
    public int PackagePromotionThreshold { get; set; } = 5;

    private string GameRootPath { get; set; }
    private MEGame? Game { get; set; }

    /// <summary>
    /// Creates a tiered cache with the parent cache to look into if necessary
    /// </summary>
    /// <param name="parent">Parent cache to look into</param>
    public TieredPackageCache(TieredPackageCache parent) : base()
    {
        ParentCache = parent;
    }

    /// <summary>
    /// Chains a new child cache to this one and returns the child cache.
    /// </summary>
    /// <param name="filenameOnlyMode">If the cache only operates on filename and not filepath</param>
    /// <param name="canPromoteInto">If the newly chained cache can be promoted into by its possible future children</param>
    /// <returns></returns>
    public TieredPackageCache ChainNewCache(bool filenameOnlyMode = false, bool canPromoteInto = false)
    {
        var cache = new TieredPackageCache(this) { FilenameOnlyMode = filenameOnlyMode, CanBePromotedInto = canPromoteInto };
        return cache;
    }

    /// <summary>
    /// Initializes a TieredPackageCache with the list of files that are globally safe to import from. This should be set as the root parent of tiered caches.
    /// </summary>
    /// <param name="game">What game this cache will be for</param>
    /// <param name="filenameOnlyMode">If key lookups should be done on filename alone</param>
    /// <param name="gameRootPath">Used to override game path</param>
    /// <returns></returns>
    public static TieredPackageCache GetGlobalPackageCache(MEGame game, bool filenameOnlyMode = false, bool triggerOnFirstUse = true, string gameRootPath = null)
    {
        var cache = new TieredPackageCache(null)
        {
            FilenameOnlyMode = filenameOnlyMode,
            Game = game,
            GlobalInitOnFirstUse = triggerOnFirstUse
        };

        if (!triggerOnFirstUse)
        {
            cache.RunGlobalInit(game, gameRootPath);
        }
        else
        {
            cache.Game = game;
            cache.GameRootPath = gameRootPath;
        }

        return cache;
    }

    private void RunGlobalInit(MEGame game, string gameRootPath)
    {
        InsertIntoCache(MEPackageHandler.OpenMEPackages(EntryImporter
            .FilesSafeToImportFrom(game)
            .Select(x => Path.Combine(MEDirectories.GetCookedPath(game, gameRootPath), x))));

        GlobalInitOnFirstUse = false;
    }

    /// <summary>
    /// Moves a package into a higher tier. If a cache is shared, this will effectively cache this package for all children caches. This typically should only be used in a third tier cache; after global cache, chain a promotion tier, and then chain children off that.
    /// </summary>
    /// <param name="package"></param>
    public void PromotePackage(IMEPackage package)
    {
        if (ParentCache != null && !ParentCache.CacheContains(package.FilePath))
        {
            Debug.WriteLine($"Promoting {package.FileNameNoExtension} to a higher tier cache");
            ParentCache.InsertIntoCache(package);
            Cache.Remove(package.FilePath, out _);
        }
    }

    /// <summary>
    /// Returns a cached package. Ensure this cache is synchronized if across threads or you may end up saving two different instances of files to the same location
    /// </summary>
    /// <param name="packageName"></param>
    /// <returns></returns>
    public override IMEPackage GetCachedPackage(string packageName, bool openIfNotInCache = true, Func<string, IMEPackage> openPackageMethod = null)
    {
        if (FilenameOnlyMode)
        {
            packageName = Path.GetFileName(packageName); // Ensure we only use filename
        }

        var parentP = ParentCache?.GetCachedPackage(packageName, false);
        if (parentP != null)
            return parentP;

        if (GlobalInitOnFirstUse)
        {
            // Run first global initialization.
            RunGlobalInit(Game.Value, GameRootPath);
        }


        return GetCachedPackageLocal(packageName, openIfNotInCache, openPackageMethod);
    }

    private IMEPackage GetCachedPackageLocal(string packagePath, bool openIfNotInCache = true, Func<string, IMEPackage> openPackageMethod = null)
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
                if (FilenameOnlyMode)
                {
                    // Find path in game
                    if (MELoadedFiles.GetFilesLoadedInGame(Game.Value, ReloadFileList).TryGetValue(packagePath, out var newPath))
                    {
                        packagePath = newPath;
                    }
                    // No longer need to refresh
                    ReloadFileList = false;
                }

                if (File.Exists(packagePath))
                {
                    // Debug.WriteLine($@"TieredPackageCache local {guid} load: {packagePath}");
                    package = openPackageMethod?.Invoke(packagePath);
                    if (package == null)
                    {
                        package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: AlwaysOpenFromDisk);
                        if (!IsCacheForGame(package.Game))
                        {
                            Debug.WriteLine($"TieredPackageCache WARNING: LOADING PACKAGE FOR A DIFFERENT GAME INTO THIS CACHE! File: {packagePath}");
                        }
                    }

#if DEBUG
                    // Increment the heatmap
                    if (package != null)
                    {
                        lock (StaticTieredHeatMap)
                        {
                            if (!StaticTieredHeatMap.TryGetValue(package.Game, out var gameMap))
                            {
                                gameMap = new CaseInsensitiveDictionary<int>();
                                StaticTieredHeatMap[package.Game] = gameMap;
                            }

                            var fname = Path.GetFileNameWithoutExtension(packagePath);
                            gameMap.TryGetValue(fname, out var currentNum);
                            currentNum++;
                            gameMap[fname] = currentNum;
                        }
                    }
#endif

                    InsertIntoCache(package);

                    // Promotion tracking
                    if (ParentCache != null && ParentCache.CanBePromotedInto)
                    {
                        lock (ParentCache.promoSyncObj)
                        {
                            ParentCache.PromotionTracking.TryGetValue(packagePath, out int count);
                            count++;
                            if (count > PackagePromotionThreshold)
                            {
                                PromotePackage(package);
                                ParentCache.PromotionTracking.Remove(packagePath, out _);
                            }
                            else
                            {
                                ParentCache.PromotionTracking[packagePath] = count;
                            }
                        }
                    }

                    return package;
                }

                Debug.WriteLine($@"TieredPackageCache local {guid} miss: File not found: {packagePath}");
            }
        }

        return null; //Package could not be found
    }

    private bool IsCacheForGame(MEGame game)
    {
        if (Game == null)
        {
            if (ParentCache != null)
                return ParentCache.IsCacheForGame(game);
            return true; // We don't care. No parent, no game set.
        }

        return Game == game;
    }

    public override void InsertIntoCache(IMEPackage package)
    {
        if (FilenameOnlyMode)
        {
            Cache[Path.GetFileName(package.FilePath)] = package;
            LastAccessMap[Path.GetFileName(package.FilePath)] = DateTime.Now;
        }
        else
        {
            Cache[package.FilePath] = package;
            LastAccessMap[package.FilePath] = DateTime.Now;
        }
        CheckCacheFullness();
    }

    /// <summary>
    /// If a cache is promotable, this will be populated with how often packages are opened. If a package is commonly hit, it will be promoted up to the parent
    /// </summary>
    private CaseInsensitiveDictionary<int> PromotionTracking { get; } = new();
}