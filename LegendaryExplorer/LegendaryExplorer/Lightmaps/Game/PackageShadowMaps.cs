using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// One light's precomputed shadowing of a primitive (LE3_Lightmaps.md §9): a ShadowMap2D, which places it in a PF_G8 ShadowMapTexture2D
/// atlas like a lightmap, as visibility or (bIsShadowFactorTexture false) a signed distance field; or a ShadowMap1D, a visibility per vertex.
/// The renderer matches it to the light by the light's LightGuid (not its LightmapGuid, which lightmaps list).
/// </summary>
public sealed record ShadowMapRef(ExportEntry Export, Guid LightGuid, bool SignedDistanceField, bool PerVertex);

public static class PackageShadowMaps
{
    /// <summary>Every light some shadow map in the package is for, with whether its shadow map is a signed distance field.</summary>
    public static IEnumerable<(Guid LightGuid, bool SignedDistanceField)> Lights(IMEPackage package)
    {
        foreach (var export in package.Exports)
            if (export.ClassName is "ShadowMap2D" or "ShadowMap1D" && Read(export) is { } s)
                yield return (s.LightGuid, s.SignedDistanceField);
    }

    public static ShadowMapRef Read(ExportEntry export)
    {
        if (export.ClassName == "ShadowMap1D")
            return new ShadowMapRef(export, ObjectBinary.From<ShadowMap1D>(export).LightGuid, false, true);
        var props = export.GetProperties();
        var guid = props.GetProp<StructProperty>("LightGuid") is { } g ? CommonStructs.GetGuid(g) : Guid.Empty;
        // Default__ShadowMap2D has bIsShadowFactorTexture true.
        return new ShadowMapRef(export, guid, props.GetProp<BoolProperty>("bIsShadowFactorTexture") is { Value: false }, false);
    }

    /// <summary>
    /// The shadow maps of the primitive a lightmap belongs to (see <see cref="PackageLightmaps.Enumerate"/>): a static mesh LOD's ShadowMaps
    /// and ShadowVertexBuffers, a BSP element's ShadowMaps, or a terrain component's ShadowMaps property.
    /// </summary>
    public static List<ShadowMapRef> Of(LightmapRef r) => UIndices(r).Where(u => r.Owner.FileRef.IsUExport(u))
        .Select(u => Read(r.Owner.FileRef.GetUExport(u))).ToList();

    /// <summary>The lights (LightGuid) a lightmap's primitive has shadow maps of.</summary>
    public static HashSet<Guid> LightGuids(LightmapRef r) => Of(r).Select(s => s.LightGuid).ToHashSet();

    static IEnumerable<int> UIndices(LightmapRef r)
    {
        switch (r.Binary)
        {
            case StaticMeshComponent smc when r.Slot.StartsWith("LOD") && int.TryParse(r.Slot.AsSpan(3), out int lod) && lod < smc.LODData.Length:
                return smc.LODData[lod].ShadowMaps.Select(u => (int)u).Concat(smc.LODData[lod].ShadowVertexBuffers.Select(u => (int)u));
            case ModelComponent mc when r.Slot.StartsWith("Element") && int.TryParse(r.Slot.AsSpan(7), out int e) && e < mc.Elements.Length:
                return mc.Elements[e].ShadowMaps.Select(u => (int)u);
            case TerrainComponent:
                return r.Owner.GetProperty<ArrayProperty<ObjectProperty>>("ShadowMaps")?.Select(o => o.Value) ?? [];
            default:
                return [];
        }
    }
}
