using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The source records of each package as it was when first captured in a bake or editor session; recapturing a package keeps its first
/// records. Immutable: changes return a new history, so it can be read on any thread while the owner swaps in the next one.
/// </summary>
public sealed class LightAssignmentHistory
{
    /// <summary>One package's records, read without touching any history.</summary>
    public sealed class PackageRecords
    {
        internal readonly IMEPackage Package;
        internal readonly Dictionary<(ExportEntry, string), PrimitiveLightAssignments> Primitives = [];
        internal readonly HashSet<Guid> Lightmaps = [];
        internal readonly HashSet<Guid> Shadows = [];
        internal readonly List<string> Problems = [];

        internal PackageRecords(IMEPackage package) => Package = package;
    }

    public static LightAssignmentHistory Empty { get; } = new([]);

    private readonly Dictionary<IMEPackage, PackageRecords> packages;
    private readonly HashSet<Guid> lightmaps = [];
    private readonly HashSet<Guid> shadows = [];
    public IReadOnlyList<string> Problems { get; }

    private LightAssignmentHistory(Dictionary<IMEPackage, PackageRecords> packages)
    {
        this.packages = packages;
        foreach (var records in packages.Values)
        {
            lightmaps.UnionWith(records.Lightmaps);
            shadows.UnionWith(records.Shadows);
        }
        Problems = packages.Values.SelectMany(records => records.Problems).ToArray();
    }

    public bool IsKnown(SceneLight light) => lightmaps.Contains(light.LightmapGuid) || shadows.Contains(light.LightGuid);

    public bool Contains(IMEPackage package) => packages.ContainsKey(package);

    /// <summary>This history with records read earlier by <see cref="Read"/>. A package already captured keeps its first records.</summary>
    public LightAssignmentHistory With(PackageRecords records) => Contains(records.Package) ? this
        : new LightAssignmentHistory(new Dictionary<IMEPackage, PackageRecords>(packages) { [records.Package] = records });

    public LightAssignmentHistory Without(IMEPackage package)
    {
        if (!Contains(package)) return this;
        var remaining = new Dictionary<IMEPackage, PackageRecords>(packages);
        remaining.Remove(package);
        return new LightAssignmentHistory(remaining);
    }

    /// <summary>Reads a package's source records. Parses every primitive, so callers shouldn't hold locks around it.</summary>
    public static PackageRecords Read(IMEPackage package)
    {
        var records = new PackageRecords(package);
        foreach (var export in package.Exports)
        {
            try
            {
                foreach (var source in Sources(export))
                {
                    var record = PrimitiveLightAssignments.From(source);
                    records.Primitives[(source.Owner, source.Slot)] = record;
                    records.Lightmaps.UnionWith(record.Lightmaps);
                    records.Shadows.UnionWith(record.Shadows);
                }
                if (export.ClassName is "ShadowMap1D" or "ShadowMap2D")
                    records.Shadows.Add(PackageShadowMaps.Read(export).LightGuid);
            }
            catch (Exception e)
            {
                records.Problems.Add($"{package.FileNameNoExtension}.{export.InstancedFullPath}: source assignments could not be read ({e.Message})");
            }
        }
        return records;
    }

    private static IEnumerable<LightmapRef> Sources(ExportEntry component)
    {
        switch (component.ClassName)
        {
            case "StaticMeshComponent":
            case "SplineMeshComponent":
                var smc = component.GetBinaryData<StaticMeshComponent>();
                for (int i = 0; i < smc.LODData.Length; i++) yield return new(component, $"LOD{i}", smc.LODData[i].LightMap, smc);
                break;
            case "ModelComponent":
                var model = component.GetBinaryData<ModelComponent>();
                for (int i = 0; i < model.Elements.Length; i++) yield return new(component, $"Element{i}", model.Elements[i].LightMap, model);
                break;
            case "TerrainComponent":
                var terrain = component.GetBinaryData<TerrainComponent>();
                yield return new(component, "Terrain", terrain.LightMap, terrain);
                break;
        }
    }

    public PrimitiveLightAssignments Get(ExportEntry component, string slot) =>
        packages.GetValueOrDefault(component.FileRef)?.Primitives.GetValueOrDefault((component, slot)) ?? PrimitiveLightAssignments.Empty;
}

/// <param name="HasSourceRecord">Whether a bake recorded lighting for the primitive: a lightmap (LMT_None, which every unbaked primitive
/// deserializes as, isn't one) or shadow maps. Without one there is nothing to preserve, as for a newly placed mesh.</param>
public sealed record PrimitiveLightAssignments(bool HasSourceRecord, IReadOnlySet<Guid> Lightmaps, IReadOnlySet<Guid> Shadows)
{
    public static PrimitiveLightAssignments Empty { get; } = new(false, new HashSet<Guid>(), new HashSet<Guid>());
    public static PrimitiveLightAssignments From(LightmapRef source) => Create(source.LightMap, PackageShadowMaps.LightGuids(source).ToHashSet());

    public static PrimitiveLightAssignments Create(LightMap lightmap, IReadOnlySet<Guid> shadows) =>
        new(lightmap is { LightMapType: not ELightMapType.LMT_None } || shadows.Count > 0, Guids(lightmap).ToHashSet(), shadows);

    public static Guid[] Guids(LightMap map) => map switch
    {
        LightMap_1D m => m.LightGuids ?? [],
        LightMap_2D m => m.LightGuids ?? [],
        LightMap_3 m => m.LightGuids ?? [],
        LightMap_4or6 m => m.LightGuids ?? [],
        LightMap_5 m => m.LightGuids ?? [],
        _ => []
    };
}
