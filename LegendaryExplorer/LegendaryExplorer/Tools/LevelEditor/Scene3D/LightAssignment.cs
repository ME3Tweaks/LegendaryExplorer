using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
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

/// <summary>Effective receiver settings. Output mappings and runtime rendering are separate from eligibility.</summary>
/// <param name="UsesLightEnvironment">Lit by a light environment, which excludes level lights except LE1's bypass lights</param>
public readonly record struct LightPrimitiveSettings(LightingChannels Channels, string LevelName,
    bool AcceptsLights = true, bool AcceptsDynamicLights = true, bool SelfContainedLighting = false, bool UsesLightEnvironment = false);

public static class LightAssignment
{
    internal static bool HasStaticShadowing(ExportEntry component, PropertyCollection props, StaticMeshComponent binary) =>
        props.GetProp<BoolProperty>("bUsePrecomputedShadows")?.Value
        ?? (component.Parent?.ClassName is "StaticMeshActor" or "StaticMeshCollectionActor"
            || binary.LODData.Any(l => l.LightMap is { LightMapType: not ELightMapType.LMT_None } || l.ShadowMaps.Length > 0 || l.ShadowVertexBuffers.Length > 0));

    /// <summary>Shared spatial/settings checks. Occlusion is evaluated by the renderer or Lightmass afterwards.</summary>
    public static LightEligibility Evaluate(SceneLight light, BoxSphereBounds bounds, LightPrimitiveSettings primitive,
        bool compareChannels = true, bool honorSelfContained = false)
    {
        if (!primitive.AcceptsLights) return LightEligibility.LightsNotAccepted;
        if (light.HasLightEnvironment) return LightEligibility.BelongsToLightEnvironment;
        if (primitive.UsesLightEnvironment && !light.BypassesLightEnvironments) return LightEligibility.LitByLightEnvironment;
        if (!light.AffectsBounds(bounds)) return LightEligibility.OutOfRange;
        if (light.GetVolumeExclusion(bounds) is var volume and not LightEligibility.Affects) return volume;
        if (compareChannels && !light.LightingChannels.OverlapsWith(primitive.Channels)) return LightEligibility.LightingChannels;
        if (!primitive.AcceptsDynamicLights && !light.HasStaticShadowing) return LightEligibility.DynamicLightsNotAccepted;
        return EvaluateLevel(light, primitive.LevelName, primitive.SelfContainedLighting, honorSelfContained);
    }

    /// <summary>LE1 tests the source package's self-contained flag; later games keep the existing policy</summary>
    internal static LightEligibility EvaluateLevel(SceneLight light, string levelName, bool receiverSelfContained, bool honorSelfContained = true)
    {
        bool sameLevel = string.Equals(light.LevelName, levelName, StringComparison.OrdinalIgnoreCase);
        bool selfContained = light.Export.Game is MEGame.LE1
            ? light.Export.FileRef.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting)
            : receiverSelfContained;
        if (honorSelfContained && selfContained && light.HasStaticShadowing && !sameLevel) return LightEligibility.SelfContainedLighting;
        if (light.OnlyAffectSameAndSpecifiedLevels && !sameLevel && !IsOtherLevelToAffect(light, levelName)) return LightEligibility.OtherLevel;
        return LightEligibility.Affects;
    }

    /// <summary>
    /// LE1 matches each OtherLevelsToAffect entry as a case-insensitive prefix of level names (RVA 0xcd59e0), so "BIOA_NOR10_03"
    /// covers BIOA_NOR10_03_DSG. Later games match names exactly.
    /// </summary>
    private static bool IsOtherLevelToAffect(SceneLight light, string levelName) =>
        light.Export.Game is MEGame.LE1
            ? light.OtherLevelsToAffect.Any(prefix => levelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            : light.OtherLevelsToAffect.Contains(levelName);

    /// <summary>Historical eligibility, independent of the bake's direct/shadow output routing.</summary>
    public static LightEligibility EvaluateHistory(SceneLight light, LightAssignmentPolicy policy, bool known,
        PrimitiveLightAssignments source)
    {
        if (policy == LightAssignmentPolicy.RecomputeAssignments || !known || !source.HasSourceRecord || !light.HasStaticShadowing)
            return LightEligibility.Affects;
        bool assigned = light.IsDominant || !light.HasStaticLighting
            ? source.Shadows.Contains(light.LightGuid)
            : source.Lightmaps.Contains(light.LightmapGuid) || source.Shadows.Contains(light.LightGuid);
        return assigned ? LightEligibility.Affects : LightEligibility.PreservedAssignment;
    }

    /// <summary>
    /// Whether the bake gives the light a shadow-only entry, for the primitives that get it through shadow maps: lights with static shadowing but no
    /// static lighting (toggleable lights), and when preserving assignments, static lights BioWare shadow-mapped on some primitives. Dominant lights
    /// get their shadow maps from their main entry. A light that casts no static shadows has none to map, and sky lights can't be shadow-mapped.
    /// </summary>
    /// <param name="vanillaShadowMapped">Whether some shadow map in the levels is for the light</param>
    public static bool HasShadowMapEntry(SceneLight light, LightAssignmentPolicy policy, bool vanillaShadowMapped, bool hasShadowOnlyReceivers = false) =>
        !light.IsDominant && light.Type != SceneLightType.Sky && light.HasStaticShadowing && light.CastsStaticShadows
        && (!light.HasStaticLighting || hasShadowOnlyReceivers || policy == LightAssignmentPolicy.PreserveExistingAssignments && vanillaShadowMapped);

    /// <summary>Keep BioWare's per-receiver direct/shadow split. New assignments choose exactly one direct contribution.</summary>
    public static bool RoutesToBake(SceneLight light, bool shadowOnly, LightAssignmentPolicy policy, bool known,
        PrimitiveLightAssignments source, bool shadowOnlyReceiver = false)
    {
        if (!light.HasStaticShadowing && !light.HasStaticLighting) return false;
        // No compatible lightmap shader exists on a shadow-only receiver. Solve its shadows without baking a direct contribution.
        // Historical eligibility is still checked separately by EvaluateHistory.
        if (shadowOnlyReceiver)
            return light.Type != SceneLightType.Sky && light.HasStaticShadowing && light.CastsStaticShadows
                && (light.IsDominant ? !shadowOnly : shadowOnly);
        if (policy == LightAssignmentPolicy.PreserveExistingAssignments && known && source.HasSourceRecord)
        {
            bool mapped = source.Shadows.Contains(light.LightGuid);
            if (light.IsDominant) return !shadowOnly && mapped;
            // EvaluateHistory counts a shadow-mapped static light as assigned. Without a shadow-only entry to carry it, its light goes in the lightmap.
            bool direct = light.HasStaticLighting && (source.Lightmaps.Contains(light.LightmapGuid)
                || mapped && !HasShadowMapEntry(light, policy, vanillaShadowMapped: true));
            return shadowOnly ? !direct && mapped : direct;
        }
        if (light.IsDominant) return !shadowOnly; // Lightmass exports bounce plus a shadow map, never baked direct.
        return shadowOnly == !light.HasStaticLighting;
    }
}
