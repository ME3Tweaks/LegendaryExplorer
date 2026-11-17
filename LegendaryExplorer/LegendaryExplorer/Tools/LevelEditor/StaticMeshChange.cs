using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A static mesh component's mesh, chosen in place of its own (<see cref="StaticMeshComponentProxy.ChooseMesh"/>): a static mesh actor's, an InterpActor's,
/// or a static mesh collection's member's. An InterpActor's ReplicatedMesh (DynamicSMActor's) is transient, set from its component in PostBeginPlay, so the
/// component's StaticMesh is all there is to write
/// </summary>
internal static class StaticMeshChange
{
    /// <summary>
    /// For a commit: writes <paramref name="mesh"/>, an entry of <paramref name="component"/>'s package, as its StaticMesh, keeping its Materials overrides (as
    /// UE3's SetStaticMesh does), and drops its baked lighting, which was made for another mesh's vertices and light-map coordinates (<see cref="DropBakedLighting"/>)
    /// </summary>
    internal static void Write(ExportEntry component, AssetReference mesh)
    {
        var properties = component.GetProperties();
        properties.AddOrReplaceProp(new ObjectProperty(mesh.EntryIn(component.FileRef), "StaticMesh"));
        DropBakedLighting(component, properties);
    }

    /// <summary>
    /// Writes <paramref name="properties"/> (<paramref name="component"/>'s) without IrrelevantLights, and its binary with no LODData, as a new component's
    /// (a bake allocates its LODs again: LightmassMappingAllocator.EnsureLods). The game would draw another mesh's light-map, shadow-map buffers and painted
    /// vertex colors on it: UStaticMeshComponent::Attach discards only a vertex light-map whose sample count isn't the mesh's, and the scene proxy binds
    /// OverrideVertexColors without a count check. Its shadow maps, which nothing else refers to, are trashed, as a bake trashes those it replaces
    /// (LightmassShadowMapWriter); the atlases and light-map textures they're in are left for the next bake, as they're shared
    /// </summary>
    private static void DropBakedLighting(ExportEntry component, PropertyCollection properties)
    {
        var package = component.FileRef;
        var binary = component.GetBinaryData<StaticMeshComponent>();
        var shadowMaps = binary.GetUIndexes(component.Game).Distinct().Select(package.GetEntry).OfType<ExportEntry>()
            .Where(e => e.ClassName is "ShadowMap1D" or "ShadowMap2D" && IsUnder(e, component)).ToList();
        properties.RemoveNamedProperty("IrrelevantLights");
        component.WritePropertiesAndBinary(properties, StaticMeshComponent.Create());
        if (shadowMaps.Count > 0) EntryPruner.TrashEntries(package, shadowMaps);
    }

    /// <summary>Whether <paramref name="component"/> has baked lighting (light or shadow maps), which a commit of a chosen mesh drops</summary>
    internal static bool HasBakedLighting(ExportEntry component) => component.GetBinaryData<StaticMeshComponent>().LODData.Any(lod =>
        lod.LightMap is { LightMapType: not ELightMapType.LMT_None } || lod.ShadowMaps.Length > 0 || lod.ShadowVertexBuffers.Length > 0);

    private static bool IsUnder(IEntry entry, IEntry owner)
    {
        for (var parent = entry.Parent; parent is not null; parent = parent.Parent)
            if (parent == owner) return true;
        return false;
    }

    /// <summary>
    /// For a commit: writes <paramref name="materials"/> (each an entry of <paramref name="component"/>'s package, or null for a slot drawn with the mesh's
    /// own material) as its Materials, keeping its baked lighting: materials don't change the mesh's light-map coordinates. An empty list is written too,
    /// so none of its archetypes' overrides are inherited
    /// </summary>
    internal static void WriteMaterials(ExportEntry component, IReadOnlyList<AssetReference> materials) =>
        component.WriteProperty(MaterialsProperty(materials, component.FileRef));

    /// <summary><paramref name="materials"/> as a Materials property of <paramref name="package"/>'s</summary>
    internal static ArrayProperty<ObjectProperty> MaterialsProperty(IReadOnlyList<AssetReference> materials, IMEPackage package) =>
        new(materials.Select(m => new ObjectProperty(m?.EntryIn(package).UIndex ?? 0)), "Materials");

    /// <summary>Material overrides chosen for a static mesh component, or its own drawn again</summary>
    internal sealed class ChooseMaterialsAction(StaticMeshComponentProxy mesh, IReadOnlyList<AssetReference> before, IReadOnlyList<AssetReference> after,
        string description) : IUndoAction
    {
        public string Description => description;
        public bool IsOrphaned => mesh.Actor is null || mesh.Actor.IsDisposed || !mesh.Actor.Components.Contains(mesh);
        public IReadOnlyCollection<ActorId> Subjects => mesh.Actor is { } actor ? [actor.Id] : null;
        //(the overrides it goes back to with neither are its own, which a preview keeps: ActorProxy.AssetsInUse)
        public IEnumerable<AssetReference> Assets => (before ?? []).Concat(after ?? []).Where(m => m is not null);
        public void Undo() => mesh.ShowMaterials(before);
        public void Redo() => mesh.ShowMaterials(after);
    }

    /// <summary>A mesh chosen for a static mesh component, or its own drawn again</summary>
    internal sealed class ChooseAction(StaticMeshComponentProxy mesh, AssetReference before, AssetReference after) : IUndoAction
    {
        public string Description => after is null ? $"Revert {Name}'s mesh" : $"Choose {after.Name} for {Name}";
        private string Name => mesh.Actor?.ActorName ?? mesh.ActorPropertyName;
        public bool IsOrphaned => mesh.Actor is null || mesh.Actor.IsDisposed || !mesh.Actor.Components.Contains(mesh);
        public IReadOnlyCollection<ActorId> Subjects => mesh.Actor is { } actor ? [actor.Id] : null;
        //(the mesh it goes back to with neither is its actor's own, which its actor keeps: ActorProxy.AssetsInUse)
        public IEnumerable<AssetReference> Assets => [before, after];
        public void Undo() => mesh.ShowMesh(before);
        public void Redo() => mesh.ShowMesh(after);
    }
}
