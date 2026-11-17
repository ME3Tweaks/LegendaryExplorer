using System;
using System.Linq;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Which actors a Lightmass bake reads (<see cref="LightmassSceneBuilder"/>), so that a commit that adds, changes or deletes one leaves its level's
/// baked lighting out of date (<see cref="Lightmaps.Game.StaticLightingChanges"/>)
/// </summary>
internal static class BakeInputs
{
    /// <summary>
    /// Whether the mesh is one of the classes the bake reads, static and spline mesh components (a new mesh's stand-in is drawn as one: a new actor's
    /// component, or a new spline connection's mesh, until a commit makes it)
    /// </summary>
    public static bool IsBakedMesh(StaticMeshComponentProxy mesh) => BakedComponent(mesh) is not { } component || LightmassSceneBuilder.IsStaticMeshComponent(component);

    /// <summary>
    /// The component the mesh is baked as, with its class and baked lighting: its own, or for a copy's stand-in, the saved one it's drawn from
    /// (<see cref="PrimitiveComponentProxy.CopiedFrom"/>). Null for any other stand-in, a new mesh's, which has neither until a commit makes it
    /// </summary>
    internal static ExportEntry BakedComponent(StaticMeshComponentProxy mesh) => mesh.CopiedFrom ?? (mesh.IsStandIn ? null : mesh.Export);

    /// <summary>
    /// Whether the actor is one a bake reads, as it's shown or as the package has it: a light that contributes to static lighting, a terrain with a
    /// section that takes part in it, an actor with a static or spline mesh component that does, or an importance volume, whose box decides where the
    /// bake spends its detail. The package's own components count too, such as a spline connection's mesh that the edit removes
    /// </summary>
    public static bool Reads(ActorProxy actor, PackageCache cache)
    {
        if (LightmassSceneBuilder.IsImportanceVolume(actor.ClassName)) return true;
        switch (actor)
        {
            case ILightProxy light:
                return actor.OwningFile?.ContributesToStaticLighting(light, cache) ?? true;
            case TerrainProxy { IsDraft: false } terrain:
                var props = EffectiveLightingProperties.Read(terrain.Export, cache);
                return (props.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents") ?? Enumerable.Empty<ObjectProperty>()).Any(section =>
                    terrain.Export.FileRef.TryGetUExport(section.Value, out var export)
                    && LightmassSceneBuilder.TerrainSectionExclusion(EffectiveLightingProperties.Read(export, cache)) is null);
        }
        foreach (var mesh in actor.Components.OfType<StaticMeshComponentProxy>().Where(IsBakedMesh))
        {
            var component = BakedComponent(mesh);
            var binary = component?.GetBinaryData<StaticMeshComponent>() ?? StaticMeshComponent.Create();
            if (LightAssignment.TakesPartInStaticLighting(component ?? mesh.Export, mesh.Properties, binary, component is null ? actor.ClassName : null)) return true;
        }
        if (actor.IsDraft) return false;
        // As the package has them: the actor's (a collection member's export is its component), and the components under it
        return new[] { actor.Export }.Concat(actor.Export.GetChildren<ExportEntry>()).Where(LightmassSceneBuilder.IsStaticMeshComponent)
            .Any(component => LightAssignment.TakesPartInStaticLighting(component, EffectiveLightingProperties.ReadPrimitive(component, cache),
                component.GetBinaryData<StaticMeshComponent>()));
    }

    /// <summary><see cref="Reads"/>, or true if it can't be told: a commit marks too much rather than too little</summary>
    public static bool MayRead(ActorProxy actor, PackageCache cache)
    {
        try { return Reads(actor, cache); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LECLog.Warning($"Couldn't tell whether a bake reads {actor.DisplayText}, so it's taken to: {e.Message}");
            return true;
        }
    }
}
