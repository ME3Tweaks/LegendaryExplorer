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
    public override float? RayHit(Vector3 origin, Vector3 direction, float maxDistance) => RayHit(origin, direction, maxDistance, ignoreHoles: false);

    /// <inheritdoc cref="RayHit(Vector3, Vector3, float)"/>
    /// <param name="ignoreHoles">Whether holes are met too, as the surface they'd have, as UE3's terrain tools trace it (so a sculpting brush can fill them)</param>
    public float? RayHit(Vector3 origin, Vector3 direction, float maxDistance, bool ignoreHoles) =>
        HeightField is { } field && Matrix4x4.Invert(LocalToWorld, out Matrix4x4 worldToLocal)
            ? field.RayHit(Vector3.Transform(origin, worldToLocal), Vector3.TransformNormal(direction, worldToLocal), maxDistance, DrawnSections, ignoreHoles)
            : null;

    /// <summary>The height field it's drawn from, with what's been sculpted; null if it couldn't be read</summary>
    public TerrainHeightField HeightField { get; private set; }
    //the sections it has a component for, which it's drawn as
    private readonly List<TerrainSection> DrawnSections = [];
    private string LoadError;
    private int HoleQuads;
    private PreviewMeshDependencies Dependencies;
    public TerrainLayerMaterials Layers { get; private set; }
    /// <summary>Why no section can be drawn with game shaders, or null</summary>
    internal string GameShadingError { get; private set; }
    //one per cached resource and light-map type, shared by the sections that use them
    private readonly Dictionary<(Guid, ELightMapType), MaterialRenderProxy> Materials = [];

    public override string Category => "Terrain";

    //where the terrain was when its collision was last accepted as written (at load, or a commit that succeeded): its collision vertices are in world space
    private Matrix4x4 collisionLocalToWorld;
    private Matrix4x4 GameLocalToWorld => SplineLoftGeometry.ActorLocalToWorld(Location, Rotation, DrawScale * DrawScale3D, PrePivot);

    //the export's heights and quad flags, as last accepted (at load, or a commit that succeeded), taken when it's first sculpted since; the view's height
    //field holds the sculpted ones
    private ushort[] committedHeights;
    private TerrainInfoFlags[] committedInfo;
    /// <summary>Whether its heights or holes have been sculpted since they were committed</summary>
    public bool IsHeightFieldDirty { get; private set; }

    public TerrainProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        Load(context.RenderContext);
        collisionLocalToWorld = GameLocalToWorld;
    }

    protected override bool HasUncommittedChanges() => base.HasUncommittedChanges() || IsHeightFieldDirty;

    /// <summary>
    /// Moving, turning or scaling a terrain moves its collision, which its components save in world space; sculpting it changes its collision and patch
    /// bounds too. The commit writes the sculpted heights and holes, then its components' collision vertices, BV trees and patch bounds again
    /// (<see cref="TerrainCollisionData"/>). A terrain whose height field or sections can't be read is refused, with nothing written
    /// </summary>
    public override void CommitChanges(PackageCache packageCache = null)
    {
        Matrix4x4 localToWorld = GameLocalToWorld;
        if (IsHeightFieldDirty || localToWorld != collisionLocalToWorld)
        {
            //(the height field the view was made from, with its sculpted heights: any change to the terrain's export makes the view again)
            TerrainHeightField field = HeightField
                ?? throw new InvalidOperationException($"{Export.InstancedFullPath} couldn't be read ({LoadError}), so its collision can't be made again.");
            var cache = packageCache ?? Editor.RenderContext.PackageCache;
            //(before the actor's own values, so a refusal leaves everything unwritten)
            TerrainCollisionData.Write(field, component => EffectiveLightingProperties.ReadPrimitive(component, cache), localToWorld,
                TerrainCollisionData.HasCollision(Export, Properties, cache));
            if (IsHeightFieldDirty) Export.WriteBinary(field.Binary);
        }
        base.CommitChanges(packageCache);
    }

    //(a commit that's rolled back puts the collision and heights back too, so only one that succeeds moves them)
    public override void MarkClean()
    {
        collisionLocalToWorld = GameLocalToWorld;
        (committedHeights, committedInfo, IsHeightFieldDirty) = (null, null, false);
        base.MarkClean();
    }

    /// <summary>
    /// Why its height field can't be sculpted, or null. Only Legendary Edition terrains are: their collision is what LEX writes, which was checked against
    /// every shipping LE terrain, none of which has displacement. One that wasn't read whole, or whose collision can't be made again, is refused, as its
    /// commit would be
    /// </summary>
    public string SculptRefusal
    {
        get
        {
            if (!Export.Game.IsLEGame()) return $"{Export.InstancedFullPath} can't be sculpted: only Legendary Edition terrains can be.";
            if (HeightField is null || LoadError is not null) return $"{Export.InstancedFullPath} couldn't be read ({LoadError}), so it can't be sculpted.";
            //(its CachedDisplacements would go stale, and collision is made without displacement)
            if (HeightField.Binary.MaxCollisionDisplacement != 0) return $"{Export.InstancedFullPath} has displacement, so it can't be sculpted.";
            var cache = Editor.RenderContext.PackageCache;
            return TerrainCollisionData.Refusal(HeightField, component => EffectiveLightingProperties.ReadPrimitive(component, cache));
        }
    }

    /// <summary>A world position in the terrain's own space, where each vertex is a unit apart (null if its transform can't be inverted)</summary>
    public Vector3? ToTerrainSpace(Vector3 world) => Matrix4x4.Invert(LocalToWorld, out Matrix4x4 worldToLocal) ? Vector3.Transform(world, worldToLocal) : null;

    /// <summary>A position in the terrain's own space in the world</summary>
    public Vector3 ToWorld(Vector3 local) => Vector3.Transform(local, LocalToWorld);

    /// <summary>
    /// The point of the height field nearest a terrain-space position in X and Y: the position moved onto the grid if it's off it, at the height
    /// bilinear between the four vertices around it. Not split along the quads' diagonals as they're drawn, so it can be just off the surface: it
    /// places the brush's outline. Null if the height field couldn't be read
    /// </summary>
    public Vector3? SurfacePoint(float x, float y)
    {
        if (HeightField is not { } field) return null;
        x = Math.Clamp(x, 0, field.NumVerticesX - 1);
        y = Math.Clamp(y, 0, field.NumVerticesY - 1);
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float tx = x - x0, ty = y - y0;
        float Z(int vx, int vy) => field.LocalVertex(vx, vy).Z;
        return new Vector3(x, y, float.Lerp(float.Lerp(Z(x0, y0), Z(x0 + 1, y0), tx), float.Lerp(Z(x0, y0 + 1), Z(x0 + 1, y0 + 1), tx), ty));
    }

    /// <summary>
    /// Starts a stroke of <paramref name="tool"/> at a terrain-space position, on a terrain with no <see cref="SculptRefusal"/>. The view isn't read again
    /// until it ends: preview refreshes wait for a viewport drag to end
    /// </summary>
    public TerrainStroke BeginStroke(TerrainSculptTool tool, Vector3 localPosition)
    {
        TerrainHeightField field = HeightField ?? throw new InvalidOperationException($"{Export.InstancedFullPath} couldn't be read ({LoadError}), so it can't be sculpted.");
        //(as committed: nothing has been sculpted since, or it was taken then)
        committedHeights ??= field.Binary.Heights.ToArray();
        committedInfo ??= field.Binary.InfoData.ToArray();
        return new TerrainStroke(field, tool, localPosition);
    }

    /// <summary>Applies a stroke for <paramref name="deltaTime"/> seconds with the brush at a terrain-space position, sizes in world units</summary>
    public void Sculpt(TerrainStroke stroke, Vector3 localPosition, float radius, float falloff, float strength, float deltaTime, bool inverted)
    {
        var (inner, outer, localStrength) = TerrainStroke.LocalBrush(radius, falloff, strength, deltaTime, DrawScale, DrawScale3D);
        if (stroke.Apply(localPosition, inner, outer, localStrength, inverted) is { } changed) RefreshSections(changed);
    }

    /// <summary>Ends a stroke: what it changed, as a history step; null if it changed nothing</summary>
    public TerrainSculptAction FinishStroke(TerrainStroke stroke)
    {
        TerrainEdit edit = stroke.Finish();
        UpdateHeightFieldDirty();
        return edit.IsEmpty ? null : new TerrainSculptAction(this, edit, stroke.Tool);
    }

    /// <summary>Abandons a stroke, putting back what it changed</summary>
    public void CancelStroke(TerrainStroke stroke)
    {
        if (stroke.Revert() is { } changed) RefreshSections(changed);
        UpdateHeightFieldDirty();
    }

    /// <summary>Gives the height field the values from after a stroke (redo), or from before it (undo)</summary>
    public void ApplyEdit(TerrainEdit edit, bool after)
    {
        TerrainHeightField field = HeightField
            ?? throw new InvalidOperationException($"{Export.InstancedFullPath} couldn't be read ({LoadError}), so its sculpting can't be undone or redone.");
        edit.Apply(field, after);
        RefreshSections(edit.Region);
        UpdateHeightFieldDirty();
    }

    private void RefreshSections(TerrainRegion changed)
    {
        foreach (TerrainComponentProxy component in Components.OfType<TerrainComponentProxy>())
        {
            if (changed.Affects(component.Mesh.Section)) component.Mesh.Refresh();
        }
    }

    private void UpdateHeightFieldDirty()
    {
        if (HeightField is not { } field || committedHeights is null) return;
        IsHeightFieldDirty = !field.Binary.Heights.AsSpan().SequenceEqual(committedHeights) || !field.Binary.InfoData.AsSpan().SequenceEqual(committedInfo);
        int holes = field.Binary.InfoData.Count(f => (f & TerrainInfoFlags.TID_Visibility_Off) != 0);
        if (holes != HoleQuads)
        {
            HoleQuads = holes;
            NotifyCoverageChanged();
        }
        UpdateDirty();
    }

    private void Load(MeshRenderContext context, TerrainHeightField sculpted = null)
    {
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        HeightField = null;
        DrawnSections.Clear();
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
                HeightField = null;
                return;
            }
            //read again for a dependency's change, not its own export's: the grid is the same, and it keeps what's been sculpted
            if (sculpted is not null)
            {
                sculpted.Binary.Heights.CopyTo(HeightField.Binary.Heights, 0);
                sculpted.Binary.InfoData.CopyTo(HeightField.Binary.InfoData, 0);
            }
            HoleQuads = HeightField.Binary.InfoData.Count(f => (f & TerrainInfoFlags.TID_Visibility_Off) != 0);
            var cache = context.PackageCache;
            Layers = new TerrainLayerMaterials(HeightField, entry =>
            {
                //Track imports too, including unresolved ones, so relinking a layer retries it.
                Dependencies.Track(entry);
                return entry as ExportEntry ?? EntryImporter.ResolveImport((ImportEntry)entry, cache);
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
                DrawnSections.Add(section);
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
        Load(Editor.RenderContext, IsHeightFieldDirty ? HeightField : null);
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
