using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Shaders;

/// <summary>One use of a material found in a package.</summary>
/// <param name="BaseMaterial">The Material at the end of the reference's instance chain; null when the chain
/// could not be resolved.</param>
/// <param name="ReferencePath">Instanced full path of the entry the chain stopped at: the base Material, or the
/// unresolved reference.</param>
/// <param name="Usage">The use the game checks before drawing it.</param>
/// <param name="Source">Instanced full path of the object that uses it.</param>
/// <param name="Detail">Extra context for the use, such as the lightmap type; may be null.</param>
public sealed record LE3MaterialUse(ExportEntry BaseMaterial, string ReferencePath, LE3MaterialUsage Usage, string Source, string Detail = null);

/// <summary>
/// Finds what each material in a package is drawn with, for <see cref="LE3MaterialUsageAdvisor"/>. Instances
/// resolve to their base Material, since usage flags live there. Reports only uses it can see in this
/// package; a material used from other packages needs those scanned too.
/// Checked against every shipping LE3 package (LE3MaterialUsageScannerValidation): skeletal mesh, particle,
/// beam, decal, fog, spline, morph, lens flare and material-effect uses match the shipped flags to within a
/// few dozen of 550k. Lightmapped meshes report StaticLighting and VectorLightMaps; shipping leaves
/// VectorLightMaps clear on about 3% of them, so that one is a deliberate superset. SimpleLightMaps and
/// LightEnvironments are not derived: shipping leaves them clear on most uses.
/// </summary>
public static class LE3MaterialUsageScanner
{
    private static readonly LE3MaterialUsage[] StaticLighting =
        [LE3MaterialUsage.StaticLighting, LE3MaterialUsage.VectorLightMaps];

    /// <summary>
    /// True for the exports <see cref="Collect"/> reads; pass to <c>MEPackageHandler.UnsafePartialLoad</c> to skip the rest.
    /// </summary>
    public static bool NeedsExport(ExportEntry export) =>
        export.IsA("PrimitiveComponent") || export.IsA("FogVolumeDensityComponent") || export.IsA("RadialBlurComponent") || export.IsA("MaterialInterface") || export.IsA("MaterialEffect") || export.IsA("LensFlare") ||
        export.ClassName is "SkeletalMesh" or "StaticMesh" or "FracturedStaticMesh" or "MorphTargetSet" or "Model" or "ParticleLODLevel" ||
        export.ClassName.StartsWith("ParticleModule", StringComparison.Ordinal) || export.ClassName.StartsWith("BioParticleModule", StringComparison.Ordinal);

    /// <param name="resolveImport">Resolves an imported material, instance or mesh to its export; may return null.</param>
    public static List<LE3MaterialUse> Collect(IMEPackage package, Func<ImportEntry, ExportEntry> resolveImport)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(resolveImport);
        var uses = new List<LE3MaterialUse>();
        var baseCache = new Dictionary<IEntry, (ExportEntry, string)>();
        var meshMaterials = new Dictionary<ExportEntry, IEntry[]>();

        string detail = null;
        void Add(IEntry reference, string source, params LE3MaterialUsage[] usages)
        {
            if (reference == null) return;
            var (baseMaterial, path) = ResolveBase(reference);
            foreach (var usage in usages) uses.Add(new LE3MaterialUse(baseMaterial, path, usage, source, detail));
        }

        foreach (var export in package.Exports)
        {
            if (export.IsDefaultObject) continue;
            string source = export.InstancedFullPath;
            detail = null;
            try
            {
                if (export.IsA("SkeletalMeshComponent"))
                {
                    var usages = new List<LE3MaterialUsage> { LE3MaterialUsage.SkeletalMesh };
                    if (export.GetProperty<ArrayProperty<ObjectProperty>>("MorphSets")?.Any(m => m.Value != 0) == true) usages.Add(LE3MaterialUsage.MorphTargets);
                    foreach (var material in ComponentMaterials(export, "SkeletalMesh")) Add(material, source, usages.ToArray());
                }
                else if (export.IsA("StaticMeshComponent"))
                {
                    var usages = new List<LE3MaterialUsage>();
                    if (export.IsA("InstancedStaticMeshComponent")) usages.Add(LE3MaterialUsage.InstancedMeshes);
                    if (export.IsA("SplineMeshComponent")) usages.Add(LE3MaterialUsage.SplineMesh);
                    if (export.IsA("FracturedStaticMeshComponent")) usages.Add(LE3MaterialUsage.FracturedMeshes);
                    if (LightMapType(export) is { } lightMap and not ELightMapType.LMT_None) { usages.AddRange(StaticLighting); detail = lightMap.ToString(); }
                    if (usages.Count > 0)
                        foreach (var material in ComponentMaterials(export, "StaticMesh")) Add(material, source, usages.ToArray());
                }
                else if (export.ClassName == "SkeletalMesh")
                {
                    foreach (var material in MeshMaterials(export)) Add(material, source, LE3MaterialUsage.SkeletalMesh);
                }
                else if (export.ClassName == "MorphTargetSet")
                {
                    if (Resolve(export.GetProperty<ObjectProperty>("BaseSkelMesh")?.Value ?? 0, package) is { } mesh)
                        foreach (var material in MeshMaterials(mesh)) Add(material, source, LE3MaterialUsage.MorphTargets);
                }
                else if (export.IsA("DecalComponent"))
                    Add(Entry(export, "DecalMaterial"), source, LE3MaterialUsage.Decals);
                else if (export.IsA("FogVolumeDensityComponent"))
                    Add(Entry(export, "FogMaterial"), source, LE3MaterialUsage.FogVolumes);
                else if (export.IsA("RadialBlurComponent"))
                    Add(Entry(export, "Material"), source, LE3MaterialUsage.RadialBlur);
                else if (export.IsA("FluidSurfaceComponent"))
                    Add(Entry(export, "FluidMaterial"), source, LE3MaterialUsage.FluidSurface);
                else if (export.IsA("MaterialEffect"))
                    Add(Entry(export, "Material"), source, LE3MaterialUsage.MaterialEffect);
                else if (export.IsA("LensFlare"))
                    foreach (var material in ReferencedMaterials(export.GetProperties())) Add(material, source, LE3MaterialUsage.LensFlare);
                else if (export.IsA("SpeedTreeComponent"))
                    foreach (var material in ReferencedMaterials(export.GetProperties())) Add(material, source, LE3MaterialUsage.SpeedTree);
                else if (export.ClassName == "ParticleLODLevel")
                    CollectParticleLevel(export, source);
                else if (export.ClassName == "Model" && export.Parent?.ClassName == "Level")
                    foreach (int material in ObjectBinary.From<Model>(export).Surfs.Select(s => s.Material).Where(m => m != 0).Distinct())
                        Add(package.GetEntry(material), source, StaticLighting);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw new InvalidOperationException($"Scanning {source} in {package.FilePath} failed: {e.Message}", e);
            }
        }
        return uses;

        void CollectParticleLevel(ExportEntry level, string source)
        {
            var required = Resolve(level.GetProperty<ObjectProperty>("RequiredModule")?.Value ?? 0, package);
            if (required == null) return;
            var material = Entry(required, "Material");
            var typeData = Resolve(level.GetProperty<ObjectProperty>("TypeDataModule")?.Value ?? 0, package);
            if (typeData == null)
            {
                bool subUV = required.GetProperty<EnumProperty>("InterpolationMethod")?.Value.Name is { } method && method != "PSUVIM_None";
                Add(material, source, subUV ? LE3MaterialUsage.ParticleSubUV : LE3MaterialUsage.ParticleSprites);
            }
            else if (typeData.IsA("ParticleModuleTypeDataBeam2") || typeData.IsA("ParticleModuleTypeDataTrail2") ||
                     typeData.IsA("ParticleModuleTypeDataRibbon") || typeData.IsA("ParticleModuleTypeDataAnimTrail"))
                Add(material, source, LE3MaterialUsage.BeamTrails);
            // Mesh type data draws the mesh's own materials through the static mesh path: no usage flag.
        }

        IEnumerable<IEntry> ComponentMaterials(ExportEntry component, string meshProperty)
        {
            var overrides = component.GetProperty<ArrayProperty<ObjectProperty>>("Materials")?.Select(m => m.Value).ToArray() ?? [];
            var mesh = Resolve(component.GetProperty<ObjectProperty>(meshProperty)?.Value ?? 0, package);
            var fromMesh = mesh == null ? Array.Empty<IEntry>() : MeshMaterials(mesh);
            int count = Math.Max(overrides.Length, fromMesh.Length);
            for (int i = 0; i < count; i++)
            {
                // A null or missing override slot draws the mesh's own material.
                var material = i < overrides.Length && overrides[i] != 0 ? package.GetEntry(overrides[i]) : i < fromMesh.Length ? fromMesh[i] : null;
                if (material != null) yield return material;
            }
            if (mesh?.ClassName == "FracturedStaticMesh" && Entry(mesh, "LoseChunkOutsideMaterial") is { } outside) yield return outside;
        }

        // Materials of every LOD; the returned entries belong to the mesh's package.
        IEntry[] MeshMaterials(ExportEntry mesh)
        {
            if (meshMaterials.TryGetValue(mesh, out var known)) return known;
            IEnumerable<int> indexes = mesh.ClassName switch
            {
                "SkeletalMesh" => ObjectBinary.From<SkeletalMesh>(mesh).Materials,
                "StaticMesh" or "FracturedStaticMesh" => ObjectBinary.From<StaticMesh>(mesh).LODModels.SelectMany(l => l.Elements.Select(e => e.Material)),
                _ => []
            };
            var materials = indexes.Where(i => i != 0).Distinct().Select(i => mesh.FileRef.GetEntry(i)).Where(e => e != null).ToArray();
            meshMaterials[mesh] = materials;
            return materials;
        }

        (ExportEntry, string) ResolveBase(IEntry reference)
        {
            if (baseCache.TryGetValue(reference, out var known)) return known;
            IEntry current = reference;
            ExportEntry result = null;
            for (int depth = 0; depth < 64 && current != null; depth++)
            {
                var export = current as ExportEntry ?? resolveImport((ImportEntry)current);
                if (export == null) break;
                if (export.IsA("Material")) { result = export; current = export; break; }
                string parentProperty = export.IsA("MaterialInstance") ? "Parent" : export.ClassName == "RvrEffectsMaterialUser" ? "m_pBaseMaterial" : null;
                int parent = parentProperty == null ? 0 : export.GetProperty<ObjectProperty>(parentProperty)?.Value ?? 0;
                if (parent == 0) { current = export; break; }
                current = export.FileRef.GetEntry(parent);
            }
            var resolved = (result, (result ?? current ?? reference).InstancedFullPath);
            baseCache[reference] = resolved;
            return resolved;
        }

        ExportEntry Resolve(int uIndex, IMEPackage owner)
        {
            var entry = uIndex == 0 ? null : owner.GetEntry(uIndex);
            return entry as ExportEntry ?? (entry is ImportEntry import ? resolveImport(import) : null);
        }

        IEntry Entry(ExportEntry export, string property)
        {
            int value = export.GetProperty<ObjectProperty>(property)?.Value ?? 0;
            return value == 0 ? null : export.FileRef.GetEntry(value);
        }

        IEnumerable<IEntry> ReferencedMaterials(IEnumerable<Property> properties)
        {
            foreach (var property in properties)
            {
                switch (property)
                {
                    case ObjectProperty o when o.Value != 0 && package.GetEntry(o.Value) is { } entry && IsMaterialClass(entry.ClassName):
                        yield return entry;
                        break;
                    case StructProperty s:
                        foreach (var nested in ReferencedMaterials(s.Properties)) yield return nested;
                        break;
                    case ArrayProperty<ObjectProperty> objects:
                        foreach (var nested in ReferencedMaterials(objects)) yield return nested;
                        break;
                    case ArrayProperty<StructProperty> structs:
                        foreach (var nested in structs.SelectMany(x => ReferencedMaterials(x.Properties))) yield return nested;
                        break;
                }
            }
        }

        bool IsMaterialClass(string className) => className is "Material" or "DecalMaterial" or "MaterialInstanceConstant" or
            "MaterialInstanceTimeVarying" or "BioMaterialInstanceConstant" or "RvrEffectsMaterialUser";
    }

    private static ELightMapType? LightMapType(ExportEntry component)
    {
        if (component.IsA("InstancedStaticMeshComponent") || component.IsA("SplineMeshComponent")) return null;
        var lods = ObjectBinary.From<StaticMeshComponent>(component).LODData;
        return lods.Select(lod => lod.LightMap?.LightMapType ?? ELightMapType.LMT_None).FirstOrDefault(t => t != ELightMapType.LMT_None);
    }
}
