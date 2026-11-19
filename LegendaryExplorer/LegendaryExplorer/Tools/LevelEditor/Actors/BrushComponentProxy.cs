using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders
using VertexType = LegendaryExplorer.Tools.LevelEditor.Scene3D.LEVertex;

namespace LegendaryExplorer.Tools.LevelEditor;

public class BrushComponentProxy : PrimitiveComponentProxy
{
    private readonly Mesh<WorldVertex> Brush;
    // The bounds of its convex elements' vertices, in its own space, as the drawn mesh's are; null if it has none
    private readonly BoxSphereBounds? hullBounds;

    /// <param name="previewProperties">For a draft's component: its values, which <paramref name="componentExport"/> (a stand-in) doesn't have</param>
    public BrushComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null)
        : base(context, componentExport, parent, previewProperties)
    {
        var aggGeom = Properties.GetProp<StructProperty>("BrushAggGeom");
        var box = new Box();
        foreach (var hull in aggGeom?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") ?? Enumerable.Empty<StructProperty>())
        {
            foreach (var vertex in hull.GetProp<ArrayProperty<StructProperty>>("VertexData") ?? Enumerable.Empty<StructProperty>()) box.Add(CommonStructs.GetVector3(vertex));
        }
        hullBounds = box.IsValid > 0 ? new BoxSphereBounds(box) : null;
        //(there's nothing to draw it with without a device)
        Brush = context.Device is null ? null : context.GetMeshFromAggGeom(aggGeom);
        UpdateSelfLocalToWorld();
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (!context.IsComponentVisible(this)) return;
        if (Brush is not null)
        {
            context.RenderMeshAsWireframe(Brush);
        }
    }

    public override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        UpdateSelfLocalToWorld();
    }

    /// <summary>The bounds of the brush's convex elements (BrushAggGeom). Cooked brush models have no polygons</summary>
    public override BoxSphereBounds GetBounds() => hullBounds is { } bounds ? bounds.TransformBy(LocalToWorld) : base.GetBounds();

    private void UpdateSelfLocalToWorld()
    {
        if (Brush is not null)
        {
            Brush.LocalToWorld = LocalToWorld;
        }
    }

    protected override void Dispose(bool disposing)
    {
        Brush?.Dispose();
        base.Dispose(disposing);
    }
}
