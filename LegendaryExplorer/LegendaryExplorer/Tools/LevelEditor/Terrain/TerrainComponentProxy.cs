using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

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
