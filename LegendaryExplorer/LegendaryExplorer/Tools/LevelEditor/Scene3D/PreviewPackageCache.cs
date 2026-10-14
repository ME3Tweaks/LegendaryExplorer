using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Shares live editor packages and observes dependencies without adding tool windows to their user lists.</summary>
public sealed class PreviewPackageCache : PackageCache
{
    private sealed class Observer(PreviewPackageCache owner, IMEPackage package) : IWeakPackageUser
    {
        public void HandleUpdate(List<PackageUpdate> updates) => owner.PackageUpdated?.Invoke(package, updates);
    }

    private readonly Dictionary<IMEPackage, Observer> Observers = [];
    // Packages looked up while resolving imports, whose exports previews may hold. Includes import resolution's candidates that turned out
    // not to have the import, so it may keep a package that nothing uses until the cache is emptied, but never drops one that's used
    private readonly HashSet<string> LookedUp = new(StringComparer.OrdinalIgnoreCase);
    public event Action<IMEPackage, List<PackageUpdate>> PackageUpdated;

    /// <summary>
    /// Mesh previews' material dependencies, which belong to these packages' contents
    /// </summary>
    public MaterialDependencyCache MaterialDependencies { get; } = new();

    public PreviewPackageCache(bool sharePackages = true) => AlwaysOpenFromDisk = !sharePackages;

    public override IMEPackage GetCachedPackage(string packagePath, bool openIfNotInCache = true,
        Func<string, IMEPackage> openPackageMethod = null)
    {
        // Import resolution supplies a disk-only opener. Editable previews need the shared, complete
        // package so edits in another tool update the very exports used by our dependency snapshots.
        lock (syncObj)
        {
            IMEPackage package = base.GetCachedPackage(packagePath, openIfNotInCache, AlwaysOpenFromDisk ? openPackageMethod : null);
            if (package is not null) LookedUp.Add(packagePath);
            return package;
        }
    }

    public override void InsertIntoCache(IMEPackage package)
    {
        lock (syncObj)
        {
            if (Cache.TryGetValue(package.FilePath, out var previous) && previous != package)
                ReleasePackage(package.FilePath);
            base.InsertIntoCache(package);
            if (!Observers.ContainsKey(package))
            {
                var observer = new Observer(this, package);
                Observers.Add(package, observer);
                OnUIThread(() =>
                {
                    //unless the package was released before this ran
                    bool stillObserved;
                    lock (syncObj) stillObserved = Observers.TryGetValue(package, out var current) && current == observer;
                    if (stillObserved) package.WeakUsers.Add(observer);
                });
            }
        }
    }

    /// <summary>
    /// WeakUsers isn't thread-safe, and packages enumerate it on the UI thread, but loading threads insert packages as they resolve imports.
    /// Queued rather than waited for, since the caller holds the cache's lock, which the UI thread may be waiting for
    /// </summary>
    private static void OnUIThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    /// <summary>Retains an already-open level using the same ownership convention as GetCachedPackage.</summary>
    public void UsePackage(IMEPackage package)
    {
        lock (syncObj)
        {
            if (Cache.TryGetValue(package.FilePath, out var existing) && existing == package) return;
            package.RegisterUse();
            InsertIntoCache(package);
        }
    }

    /// <summary>
    /// Releases a level retained by <see cref="UsePackage"/> when it's closed, unless other previews may hold its exports
    /// because they resolved imports into it. Those keep it until <see cref="ReleasePackages"/>
    /// </summary>
    public void StopUsingPackage(string packagePath)
    {
        lock (syncObj)
        {
            if (!LookedUp.Contains(packagePath)) ReleasePackage(packagePath);
        }
    }

    public override void ReleasePackage(string packagePath)
    {
        lock (syncObj)
        {
            LookedUp.Remove(packagePath);
            if (Cache.TryGetValue(packagePath, out var package))
            {
                Detach(package);
                MaterialDependencies.Forget(package);
                base.ReleasePackage(packagePath);
                package.Dispose();
            }
        }
    }

    private void Detach(IMEPackage package)
    {
        if (Observers.Remove(package, out var observer))
            OnUIThread(() => package.WeakUsers.Remove(observer));
    }

    public override void ReleasePackages(bool gc = false)
    {
        lock (syncObj)
        {
            foreach (var package in Cache.Values) Detach(package);
            LookedUp.Clear();
            MaterialDependencies.Clear();
            base.ReleasePackages(gc);
        }
    }
}
