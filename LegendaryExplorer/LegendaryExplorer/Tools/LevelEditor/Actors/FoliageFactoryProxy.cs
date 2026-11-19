using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A FoliageFactory: its authoring volume, and the cooked instances of each of its FoliageComponents. The volume outline is drawn when the
/// actor is selected or volumes are shown.
/// </summary>
public sealed class FoliageFactoryProxy : BrushProxy
{
    public List<FoliageComponentProxy> FoliageComponents = [];

    public override string Category => "Foliage";
    public override int HitPriority => IHitProxy.StandardPriority;

    public FoliageFactoryProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        if (Properties.GetProp<ArrayProperty<StructProperty>>("Meshes") is not { } meshes) return;
        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i].GetProp<ObjectProperty>("Component")?.ResolveToEntry(Pcc) is not ExportEntry componentExport) continue;
            var component = new FoliageComponentProxy(context.RenderContext, componentExport, this) { ActorPropertyName = $"Meshes[{i}].Component" };
            FoliageComponents.Add(component);
            Components.Add(component);
        }
    }

    public override ActorCoverage Coverage
    {
        get
        {
            int instances = FoliageComponents.Sum(c => c.InstanceCount);
            int rendered = FoliageComponents.Count(c => c.HasPreviewMesh);
            if (rendered == 0)
                return new(ActorRepresentation.EditorMarker, instances == 0
                    ? "Authoring volume only: no cooked foliage instances. Procedural regeneration is not implemented."
                    : "Authoring volume only: the cooked instances' mesh could not be loaded.");
            return new(ActorRepresentation.PartialScene,
                $"{instances:N0} cooked instances in {rendered} of {FoliageComponents.Count} foliage components, with each component's mesh and material. "
                + "Partial: static pose (no sway), no distance thinning or draw radius, and the stored per-instance lighting is not applied. "
                + "Procedural regeneration is not implemented.");
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        bool outline = context.IsActorSelected(this) || context.GetCategoryState("Volumes").IsVisible;
        foreach (var component in Components)
        {
            if (component is BrushComponentProxy && !outline || !context.IsComponentVisible(component)) continue;
            component.Render(context, pass);
        }
    }

    public override BoxSphereBounds GetBounds()
    {
        var drawn = FoliageComponents.Where(c => c.HasPreviewMesh).ToList();
        if (drawn.Count == 0) return base.GetBounds();
        var bounds = drawn[0].GetBounds();
        foreach (var c in drawn.Skip(1)) bounds = bounds.Union(c.GetBounds());
        return bounds;
    }
}
