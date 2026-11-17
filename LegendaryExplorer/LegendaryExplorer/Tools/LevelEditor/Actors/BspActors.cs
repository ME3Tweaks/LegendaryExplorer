using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A level's BSP world surfaces (Level.Model and its ModelComponents), one component per ModelComponent element. Not an actor: it has no
/// transform to edit, and is listed separately from brush and volume outlines.
/// </summary>
public sealed class LevelModelProxy : ActorProxy
{
    private readonly IActorEditorContext PreviewContext;
    private readonly ExportEntry LevelExport;
    private PreviewMeshDependencies Dependencies;
    //the Level's ModelComponents it was read from: what of the Level it depends on
    private int[] LoadedComponents = [];
    private string LoadError;
    private int MissingMaterialElements;

    public override string Category => "BSP";
    public override bool CanEditTransform => false;

    public LevelModelProxy(IActorEditorContext context, ExportEntry modelExport, ExportEntry levelExport) : base(modelExport)
    {
        Editor = context;
        PreviewContext = context;
        LevelExport = levelExport;
        LocalToWorld = Matrix4x4.Identity;
        Load();
    }

    private void Load()
    {
        var context = PreviewContext.RenderContext;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        MissingMaterialElements = 0;
        LoadError = null;
        LoadedComponents = [];
        try
        {
            var model = Export.GetBinaryData<Model>();
            LoadedComponents = LevelExport.GetBinaryData<Level>().ModelComponents.ToArray();
            foreach (int componentIndex in LoadedComponents)
            {
                if (!Pcc.TryGetUExport(componentIndex, out var componentExport) || componentExport.ClassName != "ModelComponent") continue;
                Dependencies.Track(componentExport);
                var component = componentExport.GetBinaryData<ModelComponent>();
                if (component.Model != Export.UIndex) continue;
                for (int e = 0; e < component.Elements.Length; e++)
                {
                    var element = component.Elements[e];
                    if (Pcc.GetEntry(element.Material) is { } material) Dependencies.Track(material);
                    var proxy = new BspElementProxy(context, componentExport, this, model, component, e);
                    if (proxy.TriangleCount == 0)
                    {
                        proxy.Dispose();
                        continue;
                    }
                    if (!proxy.HasMaterial) MissingMaterialElements++;
                    proxy.ActorPropertyName = $"Elements[{e}]";
                    Components.Add(proxy);
                }
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LoadError = e.Message;
        }
    }

    public override ActorCoverage Coverage
    {
        get
        {
            if (LoadError is not null) return new(ActorRepresentation.Unsupported, $"BSP could not be read: {LoadError}");
            var elements = Components.OfType<BspElementProxy>().ToList();
            if (elements.Count == 0) return new(ActorRepresentation.Unsupported, "The level's model has no drawable BSP surfaces.");
            string summary = $"{elements.Count} BSP surface elements, {elements.Sum(e => e.TriangleCount):N0} triangles from the cooked model, with element "
                             + "materials and light-maps; brush and volume outlines are listed separately.";
            return MissingMaterialElements > 0
                ? new(ActorRepresentation.PartialScene, $"{summary} {MissingMaterialElements} elements have no material (the engine's default material) and are only drawn in wireframe.")
                : new(ActorRepresentation.SceneRendered, $"{summary} Material and lighting preview limits apply as for static meshes.");
        }
    }

    public override BoxSphereBounds GetBounds() => Components.Count == 0 ? new BoxSphereBounds() : base.GetBounds();

    public override void CommitChanges(PackageCache packageCache = null) { }

    /// <summary>
    /// Whether it's what <paramref name="level"/> has: its model, read from the ModelComponents it lists. A change to the Level (an actor added or
    /// removed) needs no other refresh; the editor replaces it if this is false
    /// </summary>
    public bool IsReadFrom(Level level) => level.Model == Export.UIndex && level.ModelComponents.SequenceEqual(LoadedComponents);

    /// <summary>Read again if the model, its components or their materials changed. (Not the Level, which <see cref="IsReadFrom"/> is for)</summary>
    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        Components.DisposeAndClear();
        Load();
        NotifyCoverageChanged();
        return true;
    }
}

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
