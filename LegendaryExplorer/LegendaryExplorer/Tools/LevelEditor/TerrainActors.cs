using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A Terrain actor's height field, one component per TerrainComponent, drawn at maximum tessellation with holes and section boundaries. With game shaders,
/// each section is drawn with the cached terrain material of its layers, its light-map and shadow maps, and the level's lights; otherwise, or where that
/// isn't possible, with neutral shading
/// </summary>
public sealed class TerrainProxy : ActorProxy
{
    /// <summary>Its height field's surface as its sections draw it, in the terrain's frame (which the sections are placed in)</summary>
    public override float? RayHit(Vector3 origin, Vector3 direction, float maxDistance) =>
        HeightField is { IsValid: true } field && Matrix4x4.Invert(LocalToWorld, out Matrix4x4 worldToLocal)
            ? field.RayHit(Vector3.Transform(origin, worldToLocal), Vector3.TransformNormal(direction, worldToLocal), maxDistance,
                Components.OfType<TerrainComponentProxy>().Select(c => c.Mesh.Section).ToList())
            : null;

    private TerrainHeightField HeightField;
    private string LoadError;
    private int HoleQuads;
    private PreviewMeshDependencies Dependencies;
    public TerrainLayerMaterials Layers { get; private set; }
    /// <summary>Why no section can be drawn with game shaders, or null</summary>
    internal string GameShadingError { get; private set; }
    //one per cached resource and light-map type, shared by the sections that use them
    private readonly Dictionary<(Guid, ELightMapType), MaterialRenderProxy> Materials = [];

    public override string Category => "Terrain";

    public TerrainProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        Load(context.RenderContext);
    }

    private void Load(MeshRenderContext context)
    {
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        HeightField = null;
        Layers = null;
        LoadError = GameShadingError = null;
        HoleQuads = 0;
        try
        {
            HeightField = new TerrainHeightField(Export, Properties);
            if (!HeightField.IsValid)
            {
                LoadError = $"Height field has {HeightField.Binary.Heights.Length} heights and {HeightField.Binary.InfoData.Length} info flags for a "
                            + $"{HeightField.NumVerticesX} × {HeightField.NumVerticesY} vertex grid.";
                return;
            }
            HoleQuads = HeightField.Binary.InfoData.Count(f => (f & TerrainInfoFlags.TID_Visibility_Off) != 0);
            var cache = context.PackageCache;
            Layers = new TerrainLayerMaterials(HeightField, entry =>
            {
                //Track imports too, including unresolved ones, so relinking a layer retries it.
                Dependencies.Track(entry);
                return entry as ExportEntry ?? cache.ResolveImport((ImportEntry)entry);
            });
            GameShadingError = !Export.Game.IsLEGame() ? "game shaders are only supported for Legendary Edition games"
                : HeightField.Binary.MaxCollisionDisplacement != 0 ? "the terrain has displacement, which isn't supported"
                : null;
            foreach (TerrainSection section in HeightField.ReadSections(component => EffectiveLightingProperties.ReadPrimitive(component, cache)))
            {
                Dependencies.Track(section.Component);
                if (!HeightField.Contains(section)) continue;
                Components.Add(new TerrainComponentProxy(context, this, new TerrainSectionMesh(HeightField, section))
                {
                    ActorPropertyName = "TerrainComponents"
                });
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            LoadError = e.Message;
        }
    }

    /// <summary>
    /// The material a section with <paramref name="mask"/> is drawn with: its cached FTerrainMaterialResource, its parameters from the layer materials and
    /// its weight maps. Null if the terrain has no resource for the mask
    /// </summary>
    internal MaterialRenderProxy GetMaterial(MeshRenderContext context, ulong mask, ELightMapType lightMapType)
    {
        if (Layers.FindCachedMaterial(mask) is not { } resource) return null;
        if (Materials.TryGetValue((resource.ID, lightMapType), out MaterialRenderProxy material)) return material;
        material = new MaterialRenderProxy(context, $"{Export.InstancedFullPath} terrain material {resource.ID}", Export.FileRef, resource,
            MaterialRenderProxy.TerrainVertexFactoryTypeName, TerrainGameVertex.InputElements,
            Layers.WeightMapParameters(mask).Select(p => (p.Name, (IEntry)p.Texture)),
            Layers.Materials.Select(m => m.Material).Where(m => m is not null));
        var textureMap = new Dictionary<string, PreviewTextureCache.TextureEntry>();
        foreach (IEntry texture in material.Textures)
        {
            if (!textureMap.ContainsKey(texture.InstancedFullPath) && context.TextureCache.LoadTexture(texture, context.PackageCache) is { } loaded)
            {
                textureMap.Add(texture.InstancedFullPath, loaded);
            }
        }
        material.TextureMap = textureMap;
        material.QueueGameShaderLoad(lightMapType);
        Materials.Add((resource.ID, lightMapType), material);
        return material;
    }

    public override ActorCoverage Coverage
    {
        get
        {
            if (LoadError is not null) return new(ActorRepresentation.Unsupported, $"Terrain could not be read: {LoadError}");
            var sections = Components.OfType<TerrainComponentProxy>().ToList();
            if (sections.Count == 0) return new(ActorRepresentation.Unsupported, "Terrain has no sections within its height field.");
            string geometry = $"Height field geometry: {sections.Count} sections, {sections.Sum(s => s.Mesh.TriangleCount):N0} triangles at maximum tessellation, "
                              + $"{HoleQuads:N0} hole quads.";
            const string omissions = "Decoration layers, distance tessellation, shadows cast by terrain, modulated-shadow masking and decals on terrain are not rendered.";
            int shaded = sections.Count(s => s.GameShadingError is null);
            if (shaded == 0)
            {
                string reason = sections.Select(s => s.GameShadingError).FirstOrDefault(e => e is not null) ?? GameShadingError;
                return new(ActorRepresentation.PartialScene, $"{geometry} Partial: neutral shading only ({reason}). {omissions}");
            }
            int combinations = sections.Where(s => s.GameShadingError is null).Select(s => s.Mask).Distinct().Count();
            string unshaded = shaded < sections.Count
                ? $" {sections.Count - shaded} sections use neutral shading ({sections.Select(s => s.GameShadingError).First(e => e is not null)})."
                : "";
            return new(ActorRepresentation.PartialScene,
                $"{geometry} With game shaders, {shaded} sections use the terrain's cached materials ({combinations} layer combinations of {Layers.Materials.Count} layer "
                + $"materials, weight maps) with their light-maps, shadow maps and the level's lights.{unshaded} Partial: {omissions}");
        }
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        //Structural actor edits use the editor's actor rebuild path. Dependency refreshes preserve pending transforms and undo identity.
        if (package == Pcc && updatedExports.Contains(Export.UIndex)) return false;
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        Components.DisposeAndClear();
        ClearMaterials();
        Load(Editor.RenderContext);
        NotifyCoverageChanged();
        return true;
    }

    private void ClearMaterials()
    {
        foreach (MaterialRenderProxy material in Materials.Values)
        {
            material.CancelPendingShaderLoad();
        }
        Materials.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        ClearMaterials();
    }
}

/// <summary>One TerrainComponent: its section of the height field, in the section's frame</summary>
public sealed class TerrainComponentProxy : PrimitiveComponentProxy
{
    public TerrainSectionMesh Mesh { get; }
    private readonly TerrainProxy Terrain;
    //made when the section is first drawn with game shaders, like its GPU buffers: reading a component's light-map means parsing its collision data too
    private TerrainSectionShading Shading;
    /// <summary>The layer materials weighted in the section (its full batch mask). Null if the weight maps couldn't be read</summary>
    public ulong? Mask { get; }
    /// <summary>Why the section can't be drawn with game shaders, or null</summary>
    public string GameShadingError { get; }
    /// <summary>The section's light-map type, as the preview uses it (LMT_None if it has none or it can't be used)</summary>
    public ELightMapType LightMapType => Shading?.Lighting.LightMapType ?? ELightMapType.LMT_None;
    /// <summary>The material the section is drawn with when game shaders are on, or null</summary>
    public MaterialRenderProxy GameMaterial => Shading?.Material;
    public bool RendersWithGameShaders => Shading?.Material.CanRenderWithGameShaders ?? false;

    public TerrainComponentProxy(MeshRenderContext context, TerrainProxy terrain, TerrainSectionMesh mesh)
        : base(context, mesh.Section.Component, terrain)
    {
        Mesh = mesh;
        Terrain = terrain;
        UpdateLocalToWorld();
        TerrainLayerMaterials layers = terrain.Layers;
        Mask = layers.SectionMask(mesh.Section);
        GameShadingError = terrain.GameShadingError
                           ?? (Mask is not { } mask ? $"its weight maps couldn't be read{(layers.Error is null ? "" : $": {layers.Error}")}"
                               : layers.FindCachedMaterial(mask) is null ? $"the terrain has no cached material for its layers (mask {mask:X})"
                               : !mesh.FitsGameVertices ? "the section is larger than the game's vertex format allows"
                               : null);
    }

    /// <summary>The section's material and precomputed lighting, made the first time it's needed. Null if it can't be drawn with game shaders</summary>
    private TerrainSectionShading GetShading(MeshRenderContext context)
    {
        if (Shading is not null || GameShadingError is not null || context.Device is null) return Shading;
        TerrainHeightField field = Mesh.HeightField;
        ExportEntry componentExport = Mesh.Section.Component;
        var lighting = MeshStaticLighting.CreateTerrainSection(context, componentExport, componentExport.GetBinaryData<TerrainComponent>().LightMap,
            field.WithTerrainLightingSettings(Mesh.Section.Properties));
        MaterialRenderProxy material = Terrain.GetMaterial(context, Mask!.Value, lighting.LightMapType);
        lighting.GetBounds = GetBounds;
        lighting.GetLocalToWorld = () => LocalToWorld;
        lighting.Materials = [material];
        lighting.IsShown = () => context.IsComponentVisible(this);
        return Shading = new TerrainSectionShading(Mesh, material, lighting, () => LocalToWorld, GetBounds, () => Terrain.DrawScale3D,
            field.Properties.GetProp<FloatProperty>("TesselationDistanceScale")?.Value ?? 1);
    }

    // UTerrainComponent::SetParentToWorld: the terrain's frame, moved to the section base. (Read before the section is set by the base constructor)
    protected override Matrix4x4 ParentToWorld => Mesh?.Section.LocalToWorld(Actor.LocalToWorld) ?? Actor.LocalToWorld;

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (pass is not (RenderPass.Base or RenderPass.Lighting or RenderPass.ANY) || !context.IsComponentVisible(this)) return;
        if (context.CullingFrustum is { } frustum && !frustum.Intersects(GetBounds())) return;
        if (pass is RenderPass.Lighting)
        {
            Shading?.QueueLightPasses(context);
            return;
        }
        if (context.UseGameShaders && !context.Wireframe && GetShading(context) is { } shading
            && shading.DrawBasePass(context, new LEHitProxyConstants { HitProxyID = context.CurrentHitTestId, Flags = context.RenderFlags }))
        {
            return;
        }
        Mesh.Draw(context, LocalToWorld);
    }

    public override BoxSphereBounds GetBounds() => Mesh is null ? base.GetBounds() : Mesh.LocalBounds.TransformBy(LocalToWorld);

    protected override void Dispose(bool disposing)
    {
        Shading?.Lighting.Dispose();
        Mesh?.Dispose();
        base.Dispose(disposing);
    }
}
