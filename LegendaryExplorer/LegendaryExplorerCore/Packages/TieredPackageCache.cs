using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorerCore.Packages;

/// <summary>
/// PackageCache implementation that allows looking for packages in parent caches. Parent caches will not open packages on cache miss.
/// </summary>
public class TieredPackageCache : PackageCache
{
    /// <summary>
    /// If this package cache should use filenames (rather than file paths) for cache lookups.
    /// </summary>
    public bool FilenameOnlyMode { get; set; }

    /// <summary>
    /// Cache to also look in for packages
    /// </summary>
    public TieredPackageCache ParentCache;

    /// <summary>
    /// On access, will initialize global packages.
    /// </summary>
    private bool GlobalInitOnFirstUse { get; set; }

    /// <summary>
    /// If next lookup in FilenameOnlyMode should refresh game files
    /// </summary>
    public bool ReloadFileList { get; set; }

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
    /// <param name="parent"></param>
    /// <returns></returns>
    public TieredPackageCache ChainNewCache(bool filenameOnlyMode = false)
    {
        var cache = new TieredPackageCache(this) { FilenameOnlyMode = filenameOnlyMode };
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
                    Debug.WriteLine($@"TieredPackageCache local {guid} load: {packagePath}");
                    package = openPackageMethod?.Invoke(packagePath);
                    if (package == null)
                    {
                        package = MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: AlwaysOpenFromDisk);
                        if (!IsCacheForGame(package.Game))
                        {
                            Debug.WriteLine($"TieredPackageCache WARNING: LOADING PACKAGE FOR A DIFFERENT GAME INTO THIS CACHE! File: {packagePath}");
                        }
                    }

                    InsertIntoCache(package);
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
}