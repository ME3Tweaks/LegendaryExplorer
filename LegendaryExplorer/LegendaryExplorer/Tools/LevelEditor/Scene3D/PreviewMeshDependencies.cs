using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Exports used by a mesh preview. Tracks settings and material inheritance, including currently
/// overridden values, so removing an override or repairing a missing mesh can refresh the preview.
/// Texture contents and shader-cache binaries have separate resource lifetimes and are not tracked here.
/// </summary>
public sealed class PreviewMeshDependencies
{
    private readonly PackageCache Cache;
    private readonly MaterialDependencyCache Materials;
    private readonly HashSet<ExportEntry> Exports = [];
    // Imports followed (or that couldn't be resolved), since relinking one changes what it resolves to without changing any export
    private readonly HashSet<ImportEntry> Imports = [];
    // Materials' dependencies, shared with the other meshes that use them
    private readonly List<PreviewMeshDependencies> SharedMaterials = [];
    // For a material's own dependencies: the material, which is followed rather than looked up
    private readonly ExportEntry Root;

    /// <param name="cache">Materials' dependencies are shared through it, if it's a <see cref="PreviewPackageCache"/></param>
    public PreviewMeshDependencies(ExportEntry component, PackageCache cache) : this(cache, (cache as PreviewPackageCache)?.MaterialDependencies, null)
    {
        Track(component);
    }

    private PreviewMeshDependencies(PackageCache cache, MaterialDependencyCache materials, ExportEntry root)
    {
        Cache = cache;
        Materials = materials;
        Root = root;
    }

    /// <summary>
    /// A material's (or material instance's) dependencies, including its parents'
    /// </summary>
    internal static PreviewMeshDependencies ForMaterial(ExportEntry material, PackageCache cache, MaterialDependencyCache materials)
    {
        var dependencies = new PreviewMeshDependencies(cache, materials, material);
        dependencies.Track(material);
        return dependencies;
    }

    public void Track(IEntry entry)
    {
        try
        {
            var export = entry switch
            {
                ExportEntry e => e,
                ImportEntry i when Imports.Add(i) => EntryImporter.ResolveImport(i, Cache),
                _ => null
            };
            if (export is null || Exports.Contains(export)) return;
            if (Materials is not null && export != Root && export.IsA("MaterialInterface"))
            {
                PreviewMeshDependencies material = Materials.Get(export, Cache);
                if (!SharedMaterials.Contains(material)) SharedMaterials.Add(material);
                return;
            }
            Exports.Add(export);
            Track(export.Archetype);
            Track(export.Class);
            if (EffectiveLightingProperties.ClassDefault(export, Cache) is { } cdo) Track(cdo);
            // Object indices belong to the package at each inheritance link.
            var props = export.GetProperties();
            foreach (string name in new[] { "StaticMesh", "SkeletalMesh", "LightEnvironment", "m_pBaseMaterial", "Parent" })
                Track(props.GetProp<ObjectProperty>(name)?.ResolveToEntry(export.FileRef));
            foreach (string name in new[] { "Materials", "Expressions" })
                if (props.GetProp<ArrayProperty<ObjectProperty>>(name) is { } entries)
                    foreach (var dependency in entries)
                        Track(dependency.ResolveToEntry(export.FileRef));
            // Edited textures are reloaded in place by the texture cache, so only their imports are tracked:
            // relinking one changes the material without changing any export
            TrackImport(props.GetProp<ObjectProperty>("Texture")?.ResolveToEntry(export.FileRef));
            if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is { } textureParameters)
                foreach (var parameter in textureParameters)
                    TrackImport(parameter.GetProp<ObjectProperty>("ParameterValue")?.ResolveToEntry(export.FileRef));
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not track preview dependency {entry?.InstancedFullPath}: {e.Message}");
        }
    }

    private void TrackImport(IEntry entry)
    {
        if (entry is ImportEntry import) Imports.Add(import);
    }

    /// <param name="updatedExports">Updated entries' UIndexes: imports' are negative</param>
    public bool IsAffectedBy(IMEPackage package, IReadOnlySet<int> updatedExports) =>
        Exports.Any(export => export.FileRef == package && updatedExports.Contains(export.UIndex))
        || Imports.Any(import => import.FileRef == package && updatedExports.Contains(import.UIndex))
        || SharedMaterials.Any(material => material.IsAffectedBy(package, updatedExports));
}

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
