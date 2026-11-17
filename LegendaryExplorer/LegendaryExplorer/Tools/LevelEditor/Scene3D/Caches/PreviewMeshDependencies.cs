using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            // Only object arrays: a TerrainLayerSetup's Materials are FilteredMaterial structs, whose materials TerrainProxy's layer resolver tracks
            foreach (string name in new[] { "Materials", "Expressions" })
                if (props.FirstOrDefault(p => p.Name == name) is ArrayProperty<ObjectProperty> entries)
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

    /// <summary>A directly read asset, without inheriting its class/archetype settings (for example AggGeom).</summary>
    internal void TrackData(ExportEntry export)
    {
        if (export is not null) Exports.Add(export);
    }

    /// <param name="updatedExports">Updated entries' UIndexes: imports' are negative</param>
    public bool IsAffectedBy(IMEPackage package, IReadOnlySet<int> updatedExports) =>
        Exports.Any(export => export.FileRef == package && updatedExports.Contains(export.UIndex))
        || Imports.Any(import => import.FileRef == package && updatedExports.Contains(import.UIndex))
        || SharedMaterials.Any(material => material.IsAffectedBy(package, updatedExports));
}
