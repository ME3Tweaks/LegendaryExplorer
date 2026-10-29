using System.Collections.Generic;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Whether the game draws a material on a spline mesh or a decal, as UE3 decides. A cooked game can't compile the shaders a material wasn't
/// cooked with, so where one fails it draws an engine material instead: DefaultMaterial for a spline mesh's section, DefaultDecalMaterial for a decal
/// </summary>
public static class MaterialUsage
{
    /// <summary>
    /// Why the game won't draw a material on a spline mesh, or null (FSplineMeshSceneProxy, UMaterial::SetMaterialUsage): its base material must
    /// not be a decal or fog volume material, and must be marked bUsedWithSplineMeshes or be a special engine material. LE1's and LE2's only such
    /// materials are engine and debug ones
    /// </summary>
    public static string SplineMeshProblem(IEntry material, PackageCache cache)
    {
        if (BaseMaterial(material, cache, out _, out var baseMaterial) is { } problem) return problem;
        string name = material.ObjectName.Instanced;
        if (Flag(baseMaterial, "bUsedWithDecals", cache) || Flag(baseMaterial, "bUsedWithFogVolumes", cache))
            return $"{name} is a decal or fog volume material, which the game won't draw on a spline mesh.";
        if (Flag(baseMaterial, "bUsedWithSplineMeshes", cache) || Flag(baseMaterial, "bUsedAsSpecialEngineMaterial", cache)) return null;
        return $"{name}'s base material, {baseMaterial.ObjectName.Instanced}, isn't marked bUsedWithSplineMeshes. The game has no shaders to draw it on a spline mesh, so it draws its default material instead.";
    }

    /// <summary>
    /// Why the game won't draw a material on a decal, or null (UDecalComponent::CaptureDecalState): it must be a DecalMaterial itself, or have a base
    /// material marked bUsedWithDecals (as DecalMaterials are by default) that isn't a fog volume material; and its base material mustn't be a
    /// special engine material. Every shipping decal whose material can be found passes, in all three games
    /// </summary>
    public static string DecalProblem(IEntry material, PackageCache cache)
    {
        if (material is null) return "It has no material.";
        if (BaseMaterial(material, cache, out var resolved, out var baseMaterial) is { } problem) return problem;
        string name = material.ObjectName.Instanced;
        if (Flag(baseMaterial, "bUsedAsSpecialEngineMaterial", cache))
            return $"{name}'s base material, {baseMaterial.ObjectName.Instanced}, is an engine material, which the game won't draw on a decal.";
        if (resolved.IsA("DecalMaterial")) return null;
        if (Flag(baseMaterial, "bUsedWithFogVolumes", cache)) return $"{name} is a fog volume material, which the game won't draw on a decal.";
        if (Flag(baseMaterial, "bUsedWithDecals", cache)) return null;
        return $"{name}'s base material, {baseMaterial.ObjectName.Instanced}, isn't marked bUsedWithDecals. The game has no shaders to draw it on a decal, so it draws its default decal material instead.";
    }

    /// <summary>
    /// Whether the game draws a spline mesh section, or a decal, with its default material in place of this one: there's none, or it's found and
    /// fails. One that can't be found (content the game loads that the editor can't) can't be checked, so it isn't replaced
    /// </summary>
    public static bool SplineMeshDrawsDefault(IEntry material, PackageCache cache) =>
        material is null || BaseMaterial(material, cache, out _, out _) is null && SplineMeshProblem(material, cache) is not null;

    /// <inheritdoc cref="SplineMeshDrawsDefault"/>
    public static bool DecalDrawsDefault(IEntry material, PackageCache cache) =>
        material is null || BaseMaterial(material, cache, out _, out _) is null && DecalProblem(material, cache) is not null;

    /// <summary>The material, resolved, and the base material it's an instance of (through every parent), or why they can't be found</summary>
    private static string BaseMaterial(IEntry material, PackageCache cache, out ExportEntry resolved, out ExportEntry baseMaterial)
    {
        resolved = baseMaterial = null;
        string name = material?.ObjectName.Instanced;
        var visited = new HashSet<IEntry>();
        for (var entry = material; entry is not null && visited.Add(entry);)
        {
            var export = EffectiveLightingProperties.Resolve(entry, cache);
            if (export is null) return $"{name}'s base material couldn't be found ({entry.InstancedFullPath}), so it can't be checked.";
            resolved ??= export;
            if (export.IsA("Material"))
            {
                baseMaterial = export;
                return null;
            }
            if (!export.IsA("MaterialInstance")) return $"{name} isn't a material.";
            entry = export.GetProperty<ObjectProperty>("Parent")?.ResolveToEntry(export.FileRef);
        }
        return name is null ? "It isn't a material." : $"{name} has no base material, or its parents loop.";
    }

    //a usage flag, as the material sets it or its class defaults it (a DecalMaterial is bUsedWithDecals by default)
    private static bool Flag(ExportEntry material, string flag, PackageCache cache) => (material.GetProperty<BoolProperty>(flag)
        ?? EffectiveLightingProperties.ClassDefaultProperty<BoolProperty>(material, flag, cache))?.Value == true;

    /// <summary>
    /// One of the engine's materials, which each LE game's startup package holds: DefaultMaterial (GEngine->DefaultMaterial) or DefaultDecalMaterial
    /// (GEngine->DefaultDecalMaterial), as EngineMaterials.Name. Null where the game isn't installed
    /// </summary>
    public static ExportEntry EngineMaterial(MEGame game, string name, PackageCache cache) =>
        game.IsLEGame() && MELoadedFiles.GetFilesLoadedInGame(game).TryGetValue(game is MEGame.LE3 ? "Startup.pcc" : "Startup_INT.pcc", out string path)
            ? cache.GetCachedPackage(path)?.FindExport($"EngineMaterials.{name}") : null;
}
