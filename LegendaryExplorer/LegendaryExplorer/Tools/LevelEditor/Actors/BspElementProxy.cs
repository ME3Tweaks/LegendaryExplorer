using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>One ModelComponent element: the nodes drawn with one material and light-map</summary>
public sealed class BspElementProxy : PrimitiveComponentProxy
{
    private ModelPreview<LEVertex> Preview;
    public int TriangleCount { get; }
    public bool HasMaterial { get; }
    public int ElementIndex { get; }
    /// <summary>The element's light-map type, as the preview uses it (LMT_None if it has none or it can't be used)</summary>
    public ELightMapType LightMapType => Preview?.LODs[0].StaticLighting?.LightMapType ?? ELightMapType.LMT_None;
    public bool RendersWithGameShaders => GameShaderMaterials.Any(m => m.CanRenderWithGameShaders);
    public IEnumerable<MaterialRenderProxy> GameShaderMaterials => Preview?.GetRenderProxies() ?? [];
    /// <summary>The normal of the element's first front-facing vertex</summary>
    public Vector3 FrontNormal { get; }

    public BspElementProxy(MeshRenderContext context, ExportEntry componentExport, LevelModelProxy owner, Model model, ModelComponent component, int elementIndex)
        : base(context, componentExport, owner)
    {
        ElementIndex = elementIndex;
        var element = component.Elements[elementIndex];
        var geometry = BspElementGeometry.Build(model, component.ZoneIndex, element);
        TriangleCount = geometry.Indices.Length / 3;
        IEntry material = componentExport.FileRef.GetEntry(element.Material);
        HasMaterial = material is not null;
        if (geometry.Vertices.Length > 0) FrontNormal = (Vector3)geometry.Vertices[0].TangentZ;
        if (TriangleCount == 0 || context.Device is null) return;

        var vertices = new List<LEVertex>(geometry.Vertices.Length);
        var lightMapCoordinates = new Vector2[geometry.Vertices.Length];
        for (int i = 0; i < geometry.Vertices.Length; i++)
        {
            ModelVertex v = geometry.Vertices[i];
            Fixed4<Vector4> uvs = default;
            uvs[0] = new Vector4(v.TexCoord, 0, 0);
            vertices.Add((LEVertex)LEVertex.Create(v.Position, (Vector3)v.TangentX, (Vector4)v.TangentZ, uvs));
            lightMapCoordinates[i] = v.ShadowTexCoord;
        }
        var triangles = new List<Triangle>(TriangleCount);
        for (int i = 0; i < geometry.Indices.Length; i += 3)
            triangles.Add(new Triangle((uint)geometry.Indices[i], (uint)geometry.Indices[i + 1], (uint)geometry.Indices[i + 2]));
        var mesh = new Mesh<LEVertex>(context.Device, triangles, vertices);
        MeshStaticLighting lighting = null;
        if (componentExport.Game.IsLEGame())
        {
            var nodes = element.Nodes.Where(n => n < model.Nodes.Length).Select(n => (int)n).ToList();
            lighting = MeshStaticLighting.CreateModelElement(context, componentExport, element, elementIndex, lightMapCoordinates,
                LightmassSceneBuilder.BspChannels(model, nodes));
            lighting.IsInScene = () => true;
            lighting.IsShown = () => context.IsComponentVisible(this);
        }
        Preview = ModelPreview<LEVertex>.CreateSurface(context, mesh, material, lighting);
        Preview.UpdateLocalToWorld(Matrix4x4.Identity);
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (pass is RenderPass.Collision || !context.IsComponentVisible(this)) return;
        Preview?.Render(pass, context, 0);
    }

    public override BoxSphereBounds GetBounds() => Preview?.LODs[0].Mesh.TransformedBounds ?? base.GetBounds();

    protected override void Dispose(bool disposing)
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose(disposing);
    }
}
