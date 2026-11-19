using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders
using VertexType = LegendaryExplorer.Tools.LevelEditor.Scene3D.LEVertex;

namespace LegendaryExplorer.Tools.LevelEditor;

public abstract class MeshComponentProxy : PrimitiveComponentProxy
{
    public bool IsVolumetric;
    public abstract bool RefreshPreview(IMEPackage package, IReadOnlySet<int> updatedExports, bool force = false);
    public string MeshIFP { get; protected set; }
    protected ModelPreview<VertexType> Mesh;
    public bool HasPreviewMesh => Mesh is not null;
    internal ModelPreviewLOD<VertexType> DecalReceiverLOD => Mesh?.LODs[LOD];
    public int LOD;
    public List<IEntry> MaterialOverrides = [];

    // Created only when the component is selected for editing.
    private EditableActorLighting lighting;
    public EditableActorLighting Lighting => lighting ??= new EditableActorLighting(this, RenderContext);
    internal EditableActorLighting ExistingLighting => lighting;

    /// <summary>Transfers the connection's editable lighting model before loading the replacement mesh's rendering resources.</summary>
    protected void RestoreLighting(EditableActorLighting value)
    {
        ActorPropertyName ??= value.ActorPropertyName;
        lighting = value;
        lighting.Rebind(this);
    }

    /// <summary>
    /// How the mesh is part of the light environment that lights it, if it's lit by one
    /// </summary>
    private LightEnvironmentPrimitive LightEnvironmentPrimitive;
    private DynamicLightEnvironment LightEnvironment;

    /// <summary>
    /// How the level's lights light the rendered LOD directly. Null if they don't (or it couldn't be read)
    /// </summary>
    protected MeshStaticLighting StaticLighting;

    /// <summary>
    /// For diagnostics: whether a light reaches the mesh under the viewport's current rules, directly or through its light environment.
    /// Null if the level's lights don't light the mesh in this viewport (its lighting couldn't be read, or the game isn't supported)
    /// </summary>
    /// <param name="ignorePrecomputed">Preview the current scene without sampling baked lighting; the selected assignment policy still applies</param>
    public LightEligibility? GetLightEligibility(SceneLight light, bool ignorePrecomputed)
    {
        if (LightEnvironment is not null) return LightEnvironment.GetEligibility(light, LightEnvironmentPrimitive, GetBounds());
        if (StaticLighting is { UsesLightEnvironment: false }) return StaticLighting.GetEligibility(light, GetBounds(), ignorePrecomputed);
        return null;
    }

    /// <summary>
    /// For the level scan: whether the game draws the light on the mesh as a dynamic light, given what's baked on it as the view has it (pending lighting
    /// edits included), with the mesh bounded by <paramref name="bounds"/>. Null if a light environment lights it, or its lighting couldn't be read
    /// </summary>
    internal LightEligibility? GetBakedEligibility(SceneLight light, BoxSphereBounds bounds) =>
        LightEnvironment is null && StaticLighting is { UsesLightEnvironment: false } ? StaticLighting.GetEligibility(light, bounds, ignorePrecomputed: false) : null;

    public IEnumerable<string> ShadowNotes => StaticLighting?.ShadowNotes() ?? [];

    public bool HasCompatibleBakeMapping => StaticLighting?.HasCompatibleBakeMapping == true;

    /// <summary>The light-map the preview uses for the rendered LOD (LMT_None if it has none or it can't be used)</summary>
    public ELightMapType LightMapType => StaticLighting?.LightMapType ?? ELightMapType.LMT_None;

    /// <summary>Whether a bake builds static lighting for the mesh (see <see cref="MeshStaticLighting.ReceivesStaticLighting"/>)</summary>
    public bool ReceivesStaticLighting => StaticLighting?.ReceivesStaticLighting == true;

    public bool IsLitByLightEnvironment => LightEnvironment is not null;

    /// <summary>The settings of the light environment that lights the mesh, if any</summary>
    public LightEnvironmentSettings LightEnvironmentSettings => LightEnvironment?.Settings;

    /// <summary>The mesh's light environment uses lighting the viewport doesn't implement</summary>
    public bool UsesUnsupportedLightEnvironment { get; protected set; }

    /// <summary>Why, if <see cref="UsesUnsupportedLightEnvironment"/></summary>
    public string UnsupportedLightEnvironmentReason { get; protected set; }

    /// <summary>
    /// Whether the level's lights light the mesh in the viewport, directly or through its light environment
    /// </summary>
    public bool IsLitByLevelLights => LightEnvironment is not null || StaticLighting is { UsesLightEnvironment: false };

    /// <summary>
    /// The mesh's materials that can be rendered with the game's shaders
    /// </summary>
    public IEnumerable<MaterialRenderProxy> GameShaderMaterials => Mesh?.GetRenderProxies() ?? [];

    /// <summary>
    /// Whether the mesh is in the game's scene: neither it nor its actor is hidden in game
    /// </summary>
    protected bool IsInScene() => !HiddenGame && Actor is not { IsHidden: true };

    /// <summary>
    /// Whether the mesh is shown in the editor, so that it casts its dynamic shadows: neither it nor its actor's category is hidden
    /// </summary>
    protected bool IsShown() => RenderContext.IsComponentVisible(this);

    /// <summary>
    /// Whether what <paramref name="uIndex"/> is in the mesh's package has been deleted, or is pending deletion. Its ShadowParent, for one, which a deletion's
    /// commit takes it out of the group of, so that it casts its own shadow (as the preview then shows it)
    /// </summary>
    protected bool IsDeleted(int uIndex) => Export.FileRef.GetEntry(uIndex) is ExportEntry entry
        && (entry.IsTrash() || Actor?.OwningFile?.PendingDeletions.Any(d => d.Entries.Contains(entry)) == true);

    /// <summary>
    /// Adds the mesh to the light environment that lights it, so that the light environment's bounds include it, and it casts the light environment's shadow
    /// </summary>
    /// <param name="castsShadow">CastShadow and bCastDynamicShadow, including inherited values</param>
    /// <param name="staticLighting">For static meshes: its <see cref="MeshStaticLighting.LightEnvironment"/> is set to the light environment joined</param>
    /// <param name="hasBone">For skeletal meshes: whether the mesh has a named bone</param>
    protected void JoinLightEnvironment(MeshRenderContext context, DynamicLightEnvironment lightEnvironment, LightingChannels lightingChannels, bool castsShadow,
        MeshStaticLighting staticLighting = null, Func<string, bool> hasBone = null)
    {
        if (lightEnvironment is null || Mesh is null || Mesh.LODs.Count <= LOD)
        {
            return;
        }
        ModelPreviewLOD<VertexType> lod = Mesh.LODs[LOD];
        int lodIndex = LOD;
        LightEnvironmentPrimitive = new LightEnvironmentPrimitive
        {
            GetBounds = () => lod.Mesh.TransformedBounds,
            GetLocalToWorld = () => lod.Mesh.LocalToWorld,
            DrawShadowCaster = castsShadow ? (ctx, pass) => Mesh?.DrawShadowCaster(ctx, pass, lodIndex) : null,
            DrawModShadowReceiver = (ctx, pass) => Mesh?.DrawModShadowReceiver(ctx, pass, lodIndex),
            ModShadowCulling = ModulatedShadowCullingFlags.Read(Properties, skeletal: GlobalUnrealObjectInfo.IsA(Export.ClassName, "SkeletalMeshComponent", Export.Game)),
            CastShadowAsTwoSided = ShadowParticipation.Read(Properties).TwoSided,
            IsInScene = IsInScene,
            IsShown = IsShown,
            LightingChannels = lightingChannels,
            AcceptsLights = LightAssignment.AcceptsLights(Properties),
            AcceptsDynamicLights = Properties.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false },
            HasStaticShadowing = staticLighting?.HasStaticShadowing ?? Properties.GetProp<BoolProperty>("bUsePrecomputedShadows") is { Value: true },
            HasShadowParent = Properties.GetProp<ObjectProperty>("ShadowParent") is { Value: not 0 } shadowParent ? () => !IsDeleted(shadowParent.Value) : () => false,
            SelfShadowOnly = Properties.GetProp<BoolProperty>("bSelfShadowOnly") is { Value: true },
            Component = Export,
            HasBone = hasBone,
        };
        //another of the light environment's primitives may have joined it first
        LightEnvironment = context.AddLightEnvironmentPrimitive(lightEnvironment, LightEnvironmentPrimitive);
        lod.LightEnvironment = LightEnvironment;
        lod.LightEnvironmentPrimitive = LightEnvironmentPrimitive;
        if (staticLighting is not null)
        {
            staticLighting.LightEnvironment = LightEnvironment;
        }
    }

    /// <summary>
    /// A preview's mesh and material overrides, in place of its pending properties' (which can't refer to another file's): see <see cref="PreviewAssets"/>.
    /// Its mesh is the one its commit wrote, once it's written (<see cref="StaticMeshComponentProxy.AcceptChosenMesh"/>)
    /// </summary>
    protected PreviewAssets PreviewAssets { get; private protected set; }

    /// <summary>The name of the mesh <paramref name="component"/>'s <paramref name="meshProperty"/> refers to, as the bake reads it (inherited too)</summary>
    internal static string SavedMeshName(ExportEntry component, string meshProperty, PackageCache cache)
    {
        var (property, package) = EffectiveLightingProperties.Reference(component, meshProperty, cache);
        return property is { Value: not 0 } ? property.ResolveToEntry(package)?.ObjectName.Instanced ?? "Unresolved mesh" : "None";
    }

    /// <summary>A preview's mesh (<see cref="PreviewAssets"/>): a draft's, or a copy's stand-in's. Null for a saved component's</summary>
    internal AssetReference PreviewMesh => PreviewAssets?.Mesh;

    protected MeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
        PreviewAssets previewAssets = null) : base(context, componentExport, parent, previewProperties)
    {
        PreviewAssets = previewAssets;
        ReadMaterialOverrides();
    }

    /// <summary>
    /// The mesh it draws: its <paramref name="meshProperty"/>, as the bake reads it. A preview's is its <see cref="PreviewAssets"/>' mesh, which may be another
    /// file's (its export, if the package doesn't have it, has none of its own); that mesh and its materials are tracked in <paramref name="dependencies"/>.
    /// Null if it has none, or it can't be found
    /// </summary>
    /// <param name="chosen">A mesh chosen in its place, drawn instead (and tracked)</param>
    private protected ExportEntry ResolveMesh(string meshProperty, PreviewMeshDependencies dependencies, AssetReference chosen = null)
    {
        var cache = RenderContext.PackageCache;
        if (chosen is not null)
        {
            var found = chosen.Find(cache);
            dependencies.Track(found);
            foreach (var material in MaterialOverrides) dependencies.Track(material);
            return EffectiveLightingProperties.Resolve(found, cache);
        }
        if (PreviewProperties is null) return EffectiveLightingProperties.ResolveReference(Export, meshProperty, cache);
        var meshReference = PreviewAssets?.Mesh?.Find(cache);
        dependencies.Track(meshReference);
        foreach (var material in MaterialOverrides) dependencies.Track(material);
        return EffectiveLightingProperties.Resolve(meshReference, cache);
    }

    /// <summary>Overrides chosen in place of its own, which it's drawn with until a commit writes them (<see cref="StaticMeshComponentProxy.ChosenMaterials"/>)</summary>
    private protected virtual IReadOnlyList<AssetReference> ChosenOverrides => null;

    protected void ReadMaterialOverrides()
    {
        MaterialOverrides.Clear();
        if (ChosenOverrides is { } chosen)
        {
            //(one another file no longer has is drawn as none is, as a preview's)
            MaterialOverrides.AddRange(chosen.Select(m => m?.Find(RenderContext.PackageCache)));
            return;
        }
        if (PreviewProperties is not null)
        {
            //a material another file no longer has is drawn as none is: with the mesh's own
            MaterialOverrides.AddRange((PreviewAssets?.Materials ?? []).Select(m => m?.Find(RenderContext.PackageCache)));
            return;
        }
        //as the bake reads them: from the nearest object in the archetype chain that sets them (an empty array overrides its archetypes' too),
        //including archetypes in other packages (prefabs in the persistent level, class defaults' subobjects), which condensed properties leave out
        var (mats, package) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(Export, "Materials", RenderContext.PackageCache);
        if (mats is not null)
        {
            MaterialOverrides.AddRange(mats.Select(x => x.Value != 0 ? x.ResolveToEntry(package) : null));
        }
    }

    public override BoxSphereBounds GetBounds()
    {
        if (Mesh is null or { LODs.Count: 0 })
        {
            return base.GetBounds();
        }
        return Mesh.LODs[LOD].Mesh.TransformedBounds;
    }

    protected void ReleaseMesh()
    {
        if (LightEnvironmentPrimitive is not null)
        {
            RenderContext.RemoveLightEnvironmentPrimitive(LightEnvironment, LightEnvironmentPrimitive);
            LightEnvironmentPrimitive = null;
        }
        LightEnvironment = null;
        UsesUnsupportedLightEnvironment = false;
        UnsupportedLightEnvironmentReason = null;
        StaticLighting = null;
        Mesh?.Dispose();
        Mesh = null;
    }

    // Retained undo views keep their GPU resources, but detached meshes must leave lighting and visibility-trace registries just as they leave the
    // draw list. Undo registers the same resources. Loading a mesh registers it, as a mesh in the scene must be, so a mesh loaded while its actor is
    // out of the scene leaves them again (ActorProxy.LeaveSceneIfOutOfIt)
    private bool sceneResourcesAttached = true;

    /// <summary>
    /// <see cref="RefreshPreview"/>, noting that reloading joins the scene's lighting (even part way, if it fails), whether or not the mesh is in the
    /// scene: a view kept for undo is refreshed too, so that undo doesn't bring back released resources
    /// </summary>
    internal bool RefreshPreviewJoiningScene(IMEPackage package, IReadOnlySet<int> updatedExports, bool force)
    {
        try
        {
            if (!RefreshPreview(package, updatedExports, force)) return false;
        }
        catch
        {
            //(leaving what it didn't join is harmless)
            sceneResourcesAttached = true;
            throw;
        }
        sceneResourcesAttached = true;
        return true;
    }
    internal void SetSceneMembership(bool attached)
    {
        if (sceneResourcesAttached == attached) return;
        if (StaticLighting is not null)
        {
            if (attached) RenderContext.RegisterStaticLighting(StaticLighting);
            else RenderContext.UnregisterStaticLighting(StaticLighting);
        }
        if (LightEnvironmentPrimitive is not null)
        {
            if (attached)
            {
                LightEnvironment = RenderContext.AddLightEnvironmentPrimitive(LightEnvironment, LightEnvironmentPrimitive);
                if (StaticLighting is not null) StaticLighting.LightEnvironment = LightEnvironment;
                if (Mesh is not null && Mesh.LODs.Count > LOD) Mesh.LODs[LOD].LightEnvironment = LightEnvironment;
            }
            else RenderContext.RemoveLightEnvironmentPrimitive(LightEnvironment, LightEnvironmentPrimitive);
        }
        RenderContext.InvalidateLevelGeometry();
        sceneResourcesAttached = attached;
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseMesh();
        base.Dispose(disposing);
    }
}
