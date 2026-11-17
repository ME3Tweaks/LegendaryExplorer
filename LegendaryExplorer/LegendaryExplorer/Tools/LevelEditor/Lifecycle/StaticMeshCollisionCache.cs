using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A level's cooked static mesh collision (Level.CachedPhysSMDataMap and Store), which UE3's editor builds on save (ULevel::BuildPhysStaticMeshCache): one entry
/// per mesh with hulls and total scale its components have (UStaticMesh::CookPhysConvexDataForScale). Location and rotation aren't in it, so only a new mesh or
/// scale needs an entry, and one that nothing uses any more is harmless, so it's left. Without an entry the game cooks the hulls itself at load
/// (FKAggregateGeom::InstanceNovodexGeom), so an entry saves load time rather than collision. A per-triangle mesh (UseSimpleRigidBodyCollision off) is left
/// for the game to cook too: LEX can't cook a triangle mesh
/// </summary>
internal static class StaticMeshCollisionCache
{
    /// <summary>Adds the entries <paramref name="components"/>, StaticMeshComponents of <paramref name="level"/>'s package, need and it doesn't have</summary>
    /// <param name="cache">For values inherited from archetypes and meshes imported from other packages</param>
    public static void AddMissing(ExportEntry level, IEnumerable<ExportEntry> components, PackageCache cache)
    {
        Level binary = null;
        bool added = false;
        foreach (var component in components)
        {
            if (Needed(component, cache) is not { } needed) continue;
            binary ??= level.GetBinaryData<Level>();
            if (binary.CachedPhysSMDataMap.MultiFind(needed.Mesh).Any(data => IsNearlyZero(data.Scale3D - needed.Scale))) continue;
            binary.CachedPhysSMDataMap.Add(needed.Mesh, new CachedPhysSMData { Scale3D = needed.Scale, CachedDataIndex = binary.CachedPhysSMDataStore.Count });
            binary.CachedPhysSMDataStore.Add(new KCachedConvexData { CachedConvexElements = KCachedConvexData.Cook(needed.Hulls, needed.Scale) });
            added = true;
        }
        if (!added) return;
        // What every LE1-LE3 level's is (GCurrentCachedPhysDataVersion): the game ignores the entries at any other
        binary.CachedPhysSMDataVersion = BrushComponent.CurrentCachedPhysBrushDataVersion;
        level.WriteBinary(binary);
    }

    /// <summary>
    /// The mesh (a UIndex of <paramref name="component"/>'s package), total scale and hulls of the entry <paramref name="component"/> is looked up by, or null if it
    /// has none: no mesh of its own package, or one with no hulls or per-triangle collision
    /// </summary>
    private static (int Mesh, Vector3 Scale, StructProperty[] Hulls)? Needed(ExportEntry component, PackageCache cache)
    {
        // (an archetype's from another package has no mesh here: its object references are left out)
        var properties = component.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true);
        if (properties.GetProp<ObjectProperty>("StaticMesh") is not { Value: not 0 } meshProperty) return null;
        var mesh = component.FileRef.GetEntry(meshProperty.Value) switch
        {
            ExportEntry export => export,
            ImportEntry import when EntryImporter.TryResolveImport(import, out var resolved, cache) => resolved,
            _ => null
        };
        if (mesh is null || mesh.ClassName != "StaticMesh") return null;
        var meshProperties = mesh.GetProperties();
        if (meshProperties.GetProp<BoolProperty>("UseSimpleRigidBodyCollision")?.Value == false) return null;
        if (meshProperties.GetProp<ObjectProperty>("BodySetup")?.ResolveToEntry(mesh.FileRef) is not ExportEntry bodySetup
            || bodySetup.GetProperty<StructProperty>("AggGeom")?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") is not { Count: > 0 } hulls) return null;
        // The scale the game looks it up by (UPrimitiveComponent::GetTransformAndScale): a collection member's owner is its collection, with no scale.
        // Multiplied in the order AActor::BuildPhysStaticMeshCache does
        float scale = properties.GetProp<FloatProperty>("Scale")?.Value ?? 1f, ownerScale = 1f;
        Vector3 scale3D = properties.GetProp<StructProperty>("Scale3D") is { } scale3DProperty ? CommonStructs.GetVector3(scale3DProperty) : Vector3.One, ownerScale3D = Vector3.One;
        if (component.Parent is ExportEntry owner && owner.IsA("Actor") && properties.GetProp<BoolProperty>("AbsoluteScale")?.Value != true)
        {
            var ownerProperties = owner.GetCondensedProperties(cache, resolveImports: true, mergeStructs: true);
            ownerScale = ownerProperties.GetProp<FloatProperty>("DrawScale")?.Value ?? 1f;
            if (ownerProperties.GetProp<StructProperty>("DrawScale3D") is { } drawScale3D) ownerScale3D = CommonStructs.GetVector3(drawScale3D);
        }
        return (meshProperty.Value, scale * ownerScale * scale3D * ownerScale3D, [.. hulls]);
    }

    //FVector::IsNearlyZero, as ULevel::FindPhysStaticMeshCachedData matches scales
    private static bool IsNearlyZero(Vector3 v) => MathF.Abs(v.X) < 1e-4f && MathF.Abs(v.Y) < 1e-4f && MathF.Abs(v.Z) < 1e-4f;
}
