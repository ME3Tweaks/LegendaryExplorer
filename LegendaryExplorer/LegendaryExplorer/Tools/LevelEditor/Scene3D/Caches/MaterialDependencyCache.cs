using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Each material's (and material instance's) dependencies, followed once and shared by every mesh that uses it. Thread-safe
/// </summary>
public sealed class MaterialDependencyCache
{
    private readonly Dictionary<ExportEntry, PreviewMeshDependencies> Materials = [];
    private readonly Lock Sync = new();

    internal PreviewMeshDependencies Get(ExportEntry material, PackageCache cache)
    {
        lock (Sync)
        {
            if (Materials.TryGetValue(material, out PreviewMeshDependencies cached)) return cached;
        }
        // Followed outside the lock, since resolving imports takes the package cache's lock. Two threads may both follow it; the first is kept
        PreviewMeshDependencies dependencies = PreviewMeshDependencies.ForMaterial(material, cache, this);
        lock (Sync)
        {
            return Materials.TryAdd(material, dependencies) ? dependencies : Materials[material];
        }
    }

    /// <summary>
    /// Forgets the materials an edit affects, so that the meshes it refreshes follow their new dependencies.
    /// (Meshes still holding the old dependencies are refreshed by them)
    /// </summary>
    public void Invalidate(IMEPackage package, IReadOnlySet<int> updatedExports)
    {
        lock (Sync)
        {
            foreach (var (material, dependencies) in Materials)
            {
                if (dependencies.IsAffectedBy(package, updatedExports)) Materials.Remove(material);
            }
        }
    }

    /// <summary>
    /// Forgets a released package's materials
    /// </summary>
    public void Forget(IMEPackage package)
    {
        lock (Sync)
        {
            foreach (ExportEntry material in Materials.Keys)
            {
                if (material.FileRef == package) Materials.Remove(material);
            }
        }
    }

    public void Clear()
    {
        lock (Sync) Materials.Clear();
    }
}
