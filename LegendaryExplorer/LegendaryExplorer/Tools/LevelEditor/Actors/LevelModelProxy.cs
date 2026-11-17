using LegendaryExplorer.Tools.LevelEditor.Scene3D;
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
