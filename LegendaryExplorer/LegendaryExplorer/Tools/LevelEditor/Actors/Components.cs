using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Animation;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders
using VertexType = LegendaryExplorer.Tools.LevelEditor.Scene3D.LEVertex;

namespace LegendaryExplorer.Tools.LevelEditor;

public class PrimitiveComponentProxy : NotifyPropertyChangedBase, IDisposable
{
    public Matrix4x4 LocalToWorld;

    public PropertyCollection Properties;

    public ExportEntry Export { get; protected set; }

    public ActorProxy Actor;

    /// <summary>The actor property used to load this component, including its array index when applicable.</summary>
    public string ActorPropertyName { get; internal set; }

    protected readonly MeshRenderContext RenderContext;
    protected readonly PropertyCollection PreviewProperties;

    private Rotator rotation;
    private Vector3 translation;
    private Vector3 scale3D;
    private float scale;
    private bool absoluteTranslation;
    private bool absoluteRotation;
    private bool absoluteScale;
    public Rotator Rotation
    {
        get => rotation;
        set { if (SetProperty(ref rotation, value)) UpdateLocalToWorld(); }
    }
    public Vector3 Translation
    {
        get => translation;
        set { if (SetProperty(ref translation, value)) UpdateLocalToWorld(); }
    }
    public Vector3 Scale3D
    {
        get => scale3D;
        set { if (SetProperty(ref scale3D, value)) UpdateLocalToWorld(); }
    }
    public float Scale
    {
        get => scale;
        set { if (SetProperty(ref scale, value)) UpdateLocalToWorld(); }
    }
    public bool AbsoluteTranslation
    {
        get => absoluteTranslation;
        set { if (SetProperty(ref absoluteTranslation, value)) UpdateLocalToWorld(); }
    }
    public bool AbsoluteRotation
    {
        get => absoluteRotation;
        set { if (SetProperty(ref absoluteRotation, value)) UpdateLocalToWorld(); }
    }
    public bool AbsoluteScale
    {
        get => absoluteScale;
        set { if (SetProperty(ref absoluteScale, value)) UpdateLocalToWorld(); }
    }

    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Whether it draws a stand-in export, which isn't in its package: a new actor's component, or a new spline connection's mesh, until a commit makes
    /// the component
    /// </summary>
    public bool IsStandIn => Export.FileRef.GetEntry(Export.UIndex) != Export;

    /// <summary>
    /// For a copy's stand-in: the saved component it's drawn from, which the copy's commit clones, with its baked lighting and its actor's light
    /// environments (<see cref="ActorCloneProxy"/>). Null for anything else
    /// </summary>
    internal ExportEntry CopiedFrom => (Actor as ActorCloneProxy)?.DrawnFrom(this);

    /// <summary>
    /// Not rendered in game. Shown in the editor when ShowHidden is set or its actor is selected.
    /// </summary>
    public bool HiddenGame { get; private set; }

    protected PrimitiveComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null)
    {
        Actor = parent;
        Export = componentExport;
        RenderContext = context;
        PreviewProperties = previewProperties;
        ReadProperties();
    }

    protected void ReadProperties()
    {
        Properties = PreviewProperties?.DeepClone() ?? EffectiveLightingProperties.ReadPrimitive(Export, RenderContext.PackageCache);
        if (this is MeshComponentProxy mesh) mesh.ExistingLighting?.ReloadAndApply(Properties);
        HiddenGame = Properties.GetProp<BoolProperty>("HiddenGame")?.Value ?? false;

        var rotationProp = Properties.GetProp<StructProperty>("Rotation");
        var translationProp = Properties.GetProp<StructProperty>("Translation");
        var scale3DProp = Properties.GetProp<StructProperty>("Scale3D");

        scale = Properties.GetProp<FloatProperty>("Scale")?.Value ?? 1;
        translation = translationProp != null ? CommonStructs.GetVector3(translationProp) : Vector3.Zero;
        scale3D = scale3DProp != null ? CommonStructs.GetVector3(scale3DProp) : Vector3.One;
        rotation = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);

        absoluteTranslation = Properties.GetProp<BoolProperty>("AbsoluteTranslation")?.Value ?? false;
        absoluteRotation = Properties.GetProp<BoolProperty>("AbsoluteRotation")?.Value ?? false;
        absoluteScale = Properties.GetProp<BoolProperty>("AbsoluteScale")?.Value ?? false;

        UpdateSelfLocalToWorld();
    }

    public static PrimitiveComponentProxy Create(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent)
    {
        string className = componentExport.ClassName;
        switch (className)
        {
            case "BrushComponent":
                return new BrushComponentProxy(context, componentExport, parent);
        }
        // Before static meshes: the undeformed source mesh is not a valid preview
        if (GlobalUnrealObjectInfo.IsA(className, "SplineMeshComponent", componentExport.Game))
        {
            return new SplineMeshComponentProxy(context, componentExport, parent);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "StaticMeshComponent", componentExport.Game))
        {
            return new StaticMeshComponentProxy(context, componentExport, parent);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "SkeletalMeshComponent", componentExport.Game))
        {
            return new SkeletalMeshComponentProxy(context, componentExport, parent);
        }

        return new PrimitiveComponentProxy(context, componentExport, parent);
    }

    public virtual void Render(MeshRenderContext context, RenderPass pass) { }

    public virtual void UpdateScene(MeshRenderContext context, float deltaTime) { }

    /// <summary>
    /// The transform the component's own transform is relative to
    /// </summary>
    protected virtual Matrix4x4 ParentToWorld => Actor.LocalToWorld;

    private void UpdateSelfLocalToWorld()
    {
        var parentMatrix = ParentToWorld;
        if (absoluteTranslation)
        {
            parentMatrix.Translation = Vector3.Zero;
        }
        if (absoluteRotation || absoluteScale)
        {
            Vector3 x = parentMatrix.GetAxis(0);
            Vector3 y = parentMatrix.GetAxis(1);
            Vector3 z = parentMatrix.GetAxis(2);

            if (absoluteScale)
            {
                x = x.Normal();
                y = y.Normal();
                z = z.Normal();
            }
            if (absoluteRotation)
            {
                x = new Vector3(x.Length(), 0, 0);
                y = new Vector3(0, y.Length(), 0);
                z = new Vector3(0, 0, z.Length());
            }
            parentMatrix[0, 0] = x.X; parentMatrix[0, 1] = x.Y; parentMatrix[0, 2] = x.Z;
            parentMatrix[1, 0] = y.X; parentMatrix[1, 1] = y.Y; parentMatrix[1, 2] = y.Z;
            parentMatrix[2, 0] = z.X; parentMatrix[2, 1] = z.Y; parentMatrix[2, 2] = z.Z;
        }

        LocalToWorld = ActorUtils.ComposeLocalToWorld(translation, rotation, scale * scale3D) * parentMatrix;
    }

    public virtual void UpdateLocalToWorld()
    {
        UpdateSelfLocalToWorld();
    }

    public virtual BoxSphereBounds GetBounds()
    {
        return new BoxSphereBounds
        {
            Origin = LocalToWorld.Translation
        };
    }
    public virtual bool TestUIndexes(HashSet<int> uIndexes)
    {
        if (uIndexes.Contains(Export.UIndex))
        {
            return true;
        }
        return false;
    }

    #region IDisposeable
    private bool isDisposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            isDisposed = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~PrimitiveComponentProxy()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

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

public class StaticMeshComponentProxy : MeshComponentProxy
{
    private Mesh<WorldVertex> CollisionMesh;
    private PreviewMeshDependencies Dependencies;
    private readonly ExportEntry bakedLighting;

    /// <param name="previewProperties">For a preview (a draft's, or a pending edit's): its values, which its export doesn't have. Its mesh and materials
    /// are <paramref name="previewAssets"/></param>
    /// <param name="bakedLighting">For a preview: the saved component whose baked lighting it's drawn with (its own export, or for a copy in its source's
    /// level, its source's component). Without one, a preview has none</param>
    public StaticMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
        ExportEntry bakedLighting = null, EditableActorLighting lighting = null, PreviewAssets previewAssets = null)
        : base(context, componentExport, parent, previewProperties, previewAssets)
    {
        this.bakedLighting = bakedLighting;
        if (lighting is not null) RestoreLighting(lighting);
        LoadMesh();
    }

    /// <summary>The mesh it draws, as it's shown (a preview's <see cref="PreviewAssets"/> mesh). Null if it has none, or it can't be found</summary>
    internal ExportEntry StaticMeshExport { get; private set; }

    /// <summary>
    /// The mesh chosen in place of its own (Choose mesh: <see cref="ChooseMesh"/>), an entry of its package or another file's, which a commit writes
    /// (<see cref="StaticMeshChange"/>). Null while it has its own. It's drawn without baked lighting, which the commit drops. Its material overrides are
    /// kept, as UE3's SetStaticMesh keeps them
    /// </summary>
    public AssetReference ChosenMesh { get; private set; }

    /// <summary>
    /// The mesh it has until a commit writes another: its saved component's, as its package refers to it; a draft's (its <see cref="PreviewAssets"/>), or
    /// for a copy's stand-in, its source's component's
    /// </summary>
    internal AssetReference OwnMesh
    {
        get
        {
            if (PreviewProperties is not null) return PreviewAssets?.Mesh;
            var (mesh, package) = EffectiveLightingProperties.Reference(Export, "StaticMesh", RenderContext.PackageCache);
            return AssetReference.Of(mesh?.ResolveToEntry(package), Export.FileRef, RenderContext.PackageCache);
        }
    }

    /// <summary>Whether a mesh is chosen, which a commit writes</summary>
    public bool HasChosenMesh => ChosenMesh is not null;

    /// <summary>
    /// What its commit does with a chosen mesh, or null if none is: it drops the baked lighting the mesh is drawn with in the level (its own, or for a copy in
    /// its source's level, its source's, which its commit copies), and its painted vertex colors. A new mesh, or a copy in another level, has neither.
    /// Worked out when a mesh is chosen
    /// </summary>
    public string ChosenMeshNote { get; private set; }

    private string NoteFor(AssetReference chosen) => chosen is null ? null
        : BakedLightingOf() is { } baked && StaticMeshChange.HasBakedLighting(baked)
            ? "Uncommitted. Committing drops this mesh's baked lighting and painted vertex colors: rebake its level."
            : "Uncommitted.";

    //the component whose baked lighting it's drawn with in its level, if any
    private ExportEntry BakedLightingOf() => CopiedFrom is { } source ? (source.FileRef == Export.FileRef ? source : null) : IsStandIn ? null : Export;

    /// <summary>Whether its mesh can be chosen now (<see cref="MeshChangeRefusal"/>), which Choose mesh is enabled by</summary>
    public bool CanChangeMesh => MeshChangeRefusal is null;

    private void NotifyChosenMesh() => NotifyChosen(mesh: true, materials: false);

    private void NotifyChosenMaterials() => NotifyChosen(mesh: false, materials: true);

    private void NotifyChosen(bool mesh, bool materials)
    {
        if (mesh)
        {
            ChosenMeshNote = NoteFor(ChosenMesh);
            OnPropertyChanged(nameof(ChosenMesh));
            OnPropertyChanged(nameof(HasChosenMesh));
            OnPropertyChanged(nameof(ChosenMeshNote));
            OnPropertyChanged(nameof(MeshName));
            ExistingLighting?.NotifyMeshName();
        }
        if (materials)
        {
            OnPropertyChanged(nameof(ChosenMaterials));
            OnPropertyChanged(nameof(HasChosenMaterials));
        }
        //its slots are the mesh's, and whether it keeps baked lighting decides what can be drawn in them
        NotifyMaterialSlots();
    }

    /// <summary>
    /// What decides whether its mesh and materials can be chosen now changed: its actor's deletion (<see cref="ActorProxy.PendingDeletion"/>), or whether its
    /// file is read-only (<see cref="OpenLevelFile.IsReadOnly"/>)
    /// </summary>
    internal void NotifyCanChange()
    {
        OnPropertyChanged(nameof(MeshChangeRefusal));
        OnPropertyChanged(nameof(CanChangeMesh));
        OnPropertyChanged(nameof(MaterialChangeRefusal));
        OnPropertyChanged(nameof(CanChangeMaterials));
        NotifyMaterialSlots();
    }

    /// <summary>The name of the mesh it's shown with until a commit: the chosen one, or its own (as the bake reads it, for a saved component)</summary>
    public string MeshName
    {
        get
        {
            if ((ChosenMesh ?? (PreviewProperties is not null ? PreviewAssets?.Mesh : null)) is { } shown) return shown.Name;
            return PreviewProperties is not null ? "None" : SavedMeshName(Export, "StaticMesh", RenderContext.PackageCache);
        }
    }

    /// <summary>What the Mesh section calls it: the actor property it's loaded from, or for a collection member's copy, which has none, its class</summary>
    public string MeshLabel => ActorPropertyName ?? "StaticMeshComponent";

    /// <summary>
    /// Whether its mesh can be chosen: it's the StaticMeshComponent of a static mesh actor or an InterpActor, a static mesh collection's member, or a copy's
    /// stand-in drawn from one of those. Other DynamicSMActors (KActors and other physics actors) aren't offered, as their mesh is also their rigid body
    /// </summary>
    public bool CanChooseMesh => canChooseMesh ??= this is not SplineMeshComponentProxy && Actor switch
    {
        MeshActorProxy mesh => mesh.StaticMeshComponent == this && IsMeshChangeClass(mesh.ClassName),
        StaticMeshComponentActorProxy => true,
        // A member's copy is drawn from the member itself; an actor's, from the component its StaticMeshComponent is
        ActorCloneProxy clone => clone.DrawnFrom(this) is { } component && clone.CloneSource is { } saved && (component == saved
            || IsMeshChangeClass(saved.ClassName)
            && saved.GetProperty<ObjectProperty>(nameof(MeshActorProxy.StaticMeshComponent))?.ResolveToEntry(saved.FileRef) == component),
        _ => false
    };

    private bool? canChooseMesh;

    private bool IsMeshChangeClass(string className) => GlobalUnrealObjectInfo.IsA(className, "StaticMeshActor", Export.Game)
        || GlobalUnrealObjectInfo.IsA(className, "InterpActor", Export.Game);

    /// <summary>Why its mesh can't be chosen now, or null</summary>
    public string MeshChangeRefusal =>
        !CanChooseMesh ? "Only a static mesh actor's, an InterpActor's or a static mesh collection member's mesh can be chosen."
        : Actor.IsDisposed ? "It isn't in the scene."
        : Actor.IsReadOnly ? "Its file is read-only."
        : Actor.PendingDeletion is not null ? "It's being deleted." : null;

    /// <summary>
    /// Draws <paramref name="mesh"/>, an entry of its package or another file's static mesh, in place of its own, until a commit writes it; or with its own
    /// again. Throws why it can't
    /// </summary>
    /// <returns>The undo step, or null if nothing changed</returns>
    public IUndoAction ChooseMesh(AssetReference mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (MeshChangeRefusal is { } refusal) throw new InvalidOperationException(refusal);
        if (mesh.Entry is { } entry && entry.FileRef != Export.FileRef)
            throw new InvalidOperationException($"{mesh.Description} is another package's entry: choose a static mesh of its package, or another file's.");
        if (mesh.Find(RenderContext.PackageCache) is not { } found || !found.IsA(AssetPicking.ClassOf(AssetKind.StaticMesh)))
            throw new InvalidOperationException($"{mesh.Description} isn't a static mesh that can be found.");
        var before = ChosenMesh;
        if (Shown(mesh) == before) return null;
        ShowMesh(mesh);
        return new StaticMeshChange.ChooseAction(this, before, ChosenMesh);
    }

    /// <summary>Draws its own mesh again, discarding the chosen one</summary>
    /// <returns>The undo step, or null if none was chosen</returns>
    public IUndoAction RevertMesh()
    {
        if (ChosenMesh is null) return null;
        if (MeshChangeRefusal is { } refusal) throw new InvalidOperationException(refusal);
        var before = ChosenMesh;
        ShowMesh(null);
        return new StaticMeshChange.ChooseAction(this, before, null);
    }

    /// <summary>
    /// Draws <paramref name="mesh"/> in place of its own (none, or its own, draws its own), and its actor is dirty for it, or clean once it has its own. If it
    /// can't be drawn, it's drawn as it was, with what was chosen before, and why is thrown
    /// </summary>
    internal void ShowMesh(AssetReference mesh) => Show(mesh, ChosenMaterials);

    /// <summary>
    /// Draws <paramref name="materials"/> in place of its own overrides (null, or its own, draws its own), and its actor is dirty for them, or clean once it
    /// has its own. If they can't be drawn, it's drawn as it was, and why is thrown
    /// </summary>
    internal void ShowMaterials(IReadOnlyList<AssetReference> materials) => Show(ChosenMesh, materials);

    /// <summary>For a copy carrying its source's pending changes: draws both, once (see <see cref="ShowMesh"/>, <see cref="ShowMaterials"/>)</summary>
    internal void ShowChosen(AssetReference mesh, IReadOnlyList<AssetReference> materials) => Show(mesh, materials);

    private void Show(AssetReference mesh, IReadOnlyList<AssetReference> materials)
    {
        var (shownMesh, shownMaterials) = (ChosenMesh, ChosenMaterials);
        ChosenMesh = Shown(mesh);
        ChosenMaterials = ShownMaterials(materials);
        try { Redraw(); }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            // (an undo step's failure is shown and the step stays where it was, as an edit's is: both are told of it as an InvalidOperationException)
            string couldnt = $"{Drawing(ChosenMesh, ChosenMaterials, "Its")} couldn't be drawn: {failure.Message}";
            (ChosenMesh, ChosenMaterials) = (shownMesh, shownMaterials);
            try { Redraw(); }
            catch (Exception restoring) when (restoring is not OutOfMemoryException)
            {
                throw new InvalidOperationException($"{couldnt} Nor could {Drawing(shownMesh, shownMaterials, "its")}, which it had: {restoring.Message}",
                    new AggregateException(failure, restoring));
            }
            throw new InvalidOperationException(couldnt, failure);
        }
        finally { NotifyChosen(mesh: true, materials: true); }
    }

    //what it's drawn with, for a message: its mesh (chosen, or its own) and its overrides if they're chosen
    private static string Drawing(AssetReference mesh, IReadOnlyList<AssetReference> materials, string its) =>
        (mesh?.Description ?? $"{its} own mesh") + (materials is null ? "" : " with the chosen materials");

    //what choosing mesh makes chosen: nothing, for its own
    private AssetReference Shown(AssetReference mesh) => mesh is null || mesh == OwnMesh ? null : mesh;

    //reloads its preview, and its actor takes on what it draws
    private void Redraw()
    {
        using var load = RenderContext.BeginLoad();
        long mark = RenderContext.MarkShaderLoads();
        try { RefreshPreviewJoiningScene(Export.FileRef, new HashSet<int>(), force: true); }
        finally
        {
            UpdateLocalToWorld();
            Actor?.OnMeshChosen();
            Actor?.Editor?.OnMeshChosen(this);
        }
        // Its materials' game shaders and its light shaders now, before another frame is drawn, rather than in the window's next preparation batch, until
        // which it would be drawn with LEX's shader (they're mostly in the caches: the materials it was drawn with are its level's). Only for how it's drawn:
        // what fails is drawn with LEX's shader, as a batch's failures are
        try { RenderContext.LoadGameShadersNow(mark); }
        catch (Exception error) when (error is not OutOfMemoryException) { System.Diagnostics.Debug.WriteLine($"Could not load {MeshLabel}'s game shaders: {error}"); }
    }

    /// <summary>
    /// For a commit: puts <paramref name="mesh"/> (the chosen one's copy, <see cref="AssetPorting"/>) in the chosen one's place, without redrawing it, as
    /// it's already drawn
    /// </summary>
    internal void UseCopiedMesh(AssetReference mesh) => ChosenMesh = mesh;

    /// <summary>
    /// Once a commit has written the chosen mesh: it's its own. A saved component is redrawn from what was written (and a new actor's or a copy's view is
    /// made again from its export); until then a preview's own mesh is the one written
    /// </summary>
    internal void AcceptChosenMesh()
    {
        if (ChosenMesh is null) return;
        AcceptWrittenMesh(ChosenMesh);
    }

    /// <summary>
    /// For a preview whose commit wrote <paramref name="mesh"/> (its package's entry: the chosen one, or a new actor's, as copied in): its own mesh is that one,
    /// until its view is made again from its export, so it no longer refers to another file's (or its package: <see cref="ActorProxy.AssetsInUse"/>)
    /// </summary>
    internal void AcceptWrittenMesh(AssetReference mesh)
    {
        if (PreviewProperties is not null) PreviewAssets = (PreviewAssets ?? new PreviewAssets(null, [])) with { Mesh = mesh };
        ChosenMesh = null;
        NotifyChosenMesh();
    }

    /// <summary>
    /// Material overrides chosen in place of its own (by MaterialIndex; null for a slot drawn with the mesh's own), each an entry of its package or
    /// another file's material, which a commit writes as its Materials (<see cref="StaticMeshChange.WriteMaterials"/>). Null while it has its own. Its baked
    /// lighting is kept: materials don't change the mesh's light-map coordinates
    /// </summary>
    public IReadOnlyList<AssetReference> ChosenMaterials { get; private set; }

    private protected override IReadOnlyList<AssetReference> ChosenOverrides => ChosenMaterials;

    /// <summary>Whether materials are chosen, which a commit writes</summary>
    public bool HasChosenMaterials => ChosenMaterials is not null;

    /// <summary>
    /// The overrides it has until a commit writes others: its saved component's, as the bake reads them (inherited too, from archetypes in other packages
    /// as well), as its package refers to them; a draft's or a copy's stand-in's (its <see cref="PreviewAssets"/>)
    /// </summary>
    internal IReadOnlyList<AssetReference> OwnMaterials
    {
        get
        {
            if (PreviewProperties is not null) return PreviewAssets?.Materials ?? [];
            var cache = RenderContext.PackageCache;
            var (materials, package) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(Export, "Materials", cache);
            return materials?.Select(m => m.Value != 0 ? AssetReference.Of(m.ResolveToEntry(package), Export.FileRef, cache) : null).ToList() ?? [];
        }
    }

    /// <summary>The overrides it's drawn with until a commit: the chosen ones, or its own</summary>
    internal IReadOnlyList<AssetReference> ShownMaterialList => ChosenMaterials ?? OwnMaterials;

    //what choosing materials makes chosen: nothing, for its own (trailing slots with none are as good as none)
    private IReadOnlyList<AssetReference> ShownMaterials(IReadOnlyList<AssetReference> materials)
    {
        if (materials is null) return null;
        var trimmed = Trimmed(materials);
        return trimmed.SequenceEqual(Trimmed(OwnMaterials)) ? null : trimmed;
    }

    private static List<AssetReference> Trimmed(IEnumerable<AssetReference> materials)
    {
        var list = materials.ToList();
        while (list.Count > 0 && list[^1] is null) list.RemoveAt(list.Count - 1);
        return list;
    }

    /// <summary>Whether two lists of overrides are the same (null for none chosen)</summary>
    internal static bool SameMaterials(IReadOnlyList<AssetReference> a, IReadOnlyList<AssetReference> b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b);

    //the mesh's own material for each of its slots (by MaterialIndex), as drawn: null where it has none. Null if its mesh can't be read
    private IEntry[] meshSlotMaterials;

    /// <summary>
    /// Its material slots, as its drawn mesh has them (MaterialIndex 0 up to its sections' highest, in any LOD), each with its override or the mesh's own
    /// material; then any override past them up to the last that isn't none, which the game never reads (UStaticMeshComponent::GetMaterial). Every override
    /// if the mesh can't be read
    /// </summary>
    public IReadOnlyList<MeshMaterialSlot> MaterialSlots => materialSlots ??= ReadMaterialSlots();

    //(made again when what they show changes: NotifyMaterialSlots)
    private IReadOnlyList<MeshMaterialSlot> materialSlots;

    private void NotifyMaterialSlots()
    {
        materialSlots = null;
        OnPropertyChanged(nameof(MaterialSlots));
    }

    private List<MeshMaterialSlot> ReadMaterialSlots()
    {
        var overrides = ShownMaterialList;
        var cache = RenderContext.PackageCache;
        bool staticLighting = HasStaticLighting;
        bool canChange = CanChangeMaterials;
        int slots = meshSlotMaterials?.Length ?? overrides.Count;
        // Past its slots, up to the last override: trailing ones with none are as good as none (deleting one would be no change)
        int listed = overrides.Count;
        while (listed > slots && overrides[listed - 1] is null) listed--;
        var rows = new List<MeshMaterialSlot>();
        for (int i = 0; i < Math.Max(slots, listed); i++)
        {
            var material = i < overrides.Count ? overrides[i] : null;
            bool extra = i >= slots;
            if (material is not null)
            {
                string problem = extra ? null : material.Find(cache) is { } found ? MaterialUsage.StaticMeshProblem(found, cache, staticLighting)
                    : $"{material.Description} can't be found.";
                rows.Add(new MeshMaterialSlot(this, i, material.Name, material.Description, IsOverride: true, extra, canChange, problem));
            }
            else if (!extra)
            {
                var own = meshSlotMaterials?[i];
                rows.Add(new MeshMaterialSlot(this, i, own?.ObjectName.Instanced ?? "None", own?.InstancedFullPath, IsOverride: false, IsExtra: false, canChange, null));
            }
            else rows.Add(new MeshMaterialSlot(this, i, "None", null, IsOverride: true, IsExtra: true, canChange, null));
        }
        return rows;
    }

    /// <summary>
    /// Whether it's drawn with static lighting once committed and its level baked, so the game draws a slot whose material isn't for static lighting with
    /// its default material (<see cref="MaterialUsage.StaticMeshProblem"/>): it takes part in a bake, as the bake decides
    /// (<see cref="LightAssignment.TakesPartInStaticLighting"/>), or it keeps baked lighting it has. A chosen mesh's commit drops its baked lighting
    /// </summary>
    public bool HasStaticLighting
    {
        get
        {
            var component = BakeInputs.BakedComponent(this);
            var binary = ChosenMesh is null && component is not null ? component.GetBinaryData<StaticMeshComponent>() : StaticMeshComponent.Create();
            return LightAssignment.TakesPartInStaticLighting(component ?? Export, Properties, binary, component is null ? Actor?.ClassName : null)
                || ChosenMesh is null && BakedLightingOf() is { } baked && StaticMeshChange.HasBakedLighting(baked);
        }
    }

    /// <summary>
    /// Why a commit can't write its chosen materials, or null: one chosen for a slot that the game wouldn't draw there, as what decides that can change
    /// after it's chosen (another mesh chosen or reverted, its lighting edited). An override it had already isn't its commit's to refuse
    /// </summary>
    internal string ChosenMaterialProblem
    {
        get
        {
            if (ChosenMaterials is not { } chosen) return null;
            var own = OwnMaterials;
            var cache = RenderContext.PackageCache;
            bool staticLighting = HasStaticLighting;
            // (one that can't be found is AssetPorting's to refuse, as it copies them in)
            for (int i = 0; i < Math.Min(chosen.Count, meshSlotMaterials?.Length ?? chosen.Count); i++)
            {
                if (chosen[i] is not { } material || i < own.Count && Equals(own[i], material)) continue;
                if (material.Find(cache) is { } found && MaterialUsage.StaticMeshProblem(found, cache, staticLighting) is { } problem) return $"Slot {i}: {problem}";
            }
            return null;
        }
    }

    /// <summary>
    /// Whether its material overrides can be chosen: it's the StaticMeshComponent of a static mesh actor or any DynamicSMActor (a KActor's too: materials
    /// aren't its rigid body), a static mesh collection's member, or a copy's stand-in drawn from one of those
    /// </summary>
    public bool CanChooseMaterials => canChooseMaterials ??= this is not SplineMeshComponentProxy && Actor switch
    {
        MeshActorProxy mesh => mesh.StaticMeshComponent == this,
        StaticMeshComponentActorProxy => true,
        ActorCloneProxy clone => clone.DrawnFrom(this) is { } component && clone.CloneSource is { } saved && (component == saved
            || ActorClassification.Get(saved).Kind is ActorKind.StaticMesh or ActorKind.DynamicMesh
            && saved.GetProperty<ObjectProperty>(nameof(MeshActorProxy.StaticMeshComponent))?.ResolveToEntry(saved.FileRef) == component),
        _ => false
    };

    private bool? canChooseMaterials;

    /// <summary>Why its materials can't be chosen now, or null</summary>
    public string MaterialChangeRefusal =>
        !CanChooseMaterials ? "Only a static mesh actor's, a DynamicSMActor's or a static mesh collection member's materials can be chosen."
        : Actor.IsDisposed ? "It isn't in the scene."
        : Actor.IsReadOnly ? "Its file is read-only."
        : Actor.PendingDeletion is not null ? "It's being deleted." : null;

    /// <summary>Whether its materials can be chosen now (<see cref="MaterialChangeRefusal"/>)</summary>
    public bool CanChangeMaterials => MaterialChangeRefusal is null;

    /// <summary>
    /// Draws <paramref name="material"/>, an entry of its package or another file's material, in slot <paramref name="slot"/> (a MaterialIndex its mesh has)
    /// until a commit writes it. Throws why it can't, including why the game wouldn't draw it there (<see cref="MaterialUsage.StaticMeshProblem"/>)
    /// </summary>
    /// <returns>The undo step, or null if nothing changed</returns>
    public IUndoAction ChooseMaterial(int slot, AssetReference material)
    {
        ArgumentNullException.ThrowIfNull(material);
        RefuseSlotEdit(slot);
        var cache = RenderContext.PackageCache;
        if (material.Entry is { } entry && entry.FileRef != Export.FileRef)
            throw new InvalidOperationException($"{material.Description} is another package's entry: choose a material of its package, or another file's.");
        if (material.Find(cache) is not { } found || !found.IsA(AssetPicking.ClassOf(AssetKind.Material)))
            throw new InvalidOperationException($"{material.Description} isn't a material that can be found.");
        if (MaterialUsage.StaticMeshProblem(found, cache, HasStaticLighting) is { } problem) throw new InvalidOperationException(problem);
        return ChangeMaterials(With(slot, material), $"Choose {material.Name} for slot {slot} of {MaterialOwnerName}");
    }

    /// <summary>Draws the mesh's own material in slot <paramref name="slot"/> again, with no override</summary>
    /// <returns>The undo step, or null if it had none</returns>
    public IUndoAction ClearMaterial(int slot)
    {
        RefuseSlotEdit(slot);
        return ChangeMaterials(With(slot, null), $"Use the mesh's material in slot {slot} of {MaterialOwnerName}");
    }

    /// <summary>Removes the override at <paramref name="slot"/>, one past the mesh's slots, which the game never reads (those after it stay past them)</summary>
    /// <returns>The undo step, or null if there's none there</returns>
    public IUndoAction DeleteExtraMaterial(int slot)
    {
        if (MaterialChangeRefusal is { } refusal) throw new InvalidOperationException(refusal);
        if (meshSlotMaterials is null) throw new InvalidOperationException($"{MeshName} can't be read, so which overrides are past its slots isn't known.");
        if (slot < meshSlotMaterials.Length) throw new InvalidOperationException($"Slot {slot} is one of {MeshName}'s, so it can't be deleted.");
        var list = ShownMaterialList.ToList();
        if (slot >= list.Count) return null;
        list.RemoveAt(slot);
        return ChangeMaterials(list, $"Delete override {slot} of {MaterialOwnerName}");
    }

    /// <summary>Draws its own overrides again, discarding the chosen ones</summary>
    /// <returns>The undo step, or null if none were chosen</returns>
    public IUndoAction RevertMaterials()
    {
        if (ChosenMaterials is null) return null;
        if (MaterialChangeRefusal is { } refusal) throw new InvalidOperationException(refusal);
        return ChangeMaterials(null, $"Revert {MaterialOwnerName}'s materials");
    }

    private string MaterialOwnerName => Actor?.ActorName ?? ActorPropertyName;

    //an override can be chosen or cleared only in one of the mesh's slots
    private void RefuseSlotEdit(int slot)
    {
        if (MaterialChangeRefusal is { } refusal) throw new InvalidOperationException(refusal);
        int slots = meshSlotMaterials?.Length ?? ShownMaterialList.Count;
        if (slot < 0 || slot >= slots) throw new InvalidOperationException($"{MeshName} has no material slot {slot}.");
    }

    //the overrides with slot's set to material (none, for the mesh's own)
    private List<AssetReference> With(int slot, AssetReference material)
    {
        var list = ShownMaterialList.ToList();
        while (list.Count <= slot) list.Add(null);
        list[slot] = material;
        return list;
    }

    private IUndoAction ChangeMaterials(IReadOnlyList<AssetReference> materials, string description)
    {
        var before = ChosenMaterials;
        if (SameMaterials(ShownMaterials(materials), before)) return null;
        ShowMaterials(materials);
        return new StaticMeshChange.ChooseMaterialsAction(this, before, ChosenMaterials, description);
    }

    /// <summary>For a commit: puts <paramref name="materials"/> (the chosen ones' copies, <see cref="AssetPorting"/>) in their place, without redrawing</summary>
    internal void UseCopiedMaterials(IReadOnlyList<AssetReference> materials)
    {
        ChosenMaterials = materials;
        materialSlots = null;
    }

    /// <summary>
    /// Once a commit has written the chosen materials: they're its own. A preview's own are the ones written until its view is made again from its export
    /// </summary>
    internal void AcceptChosenMaterials()
    {
        if (ChosenMaterials is null) return;
        if (PreviewProperties is not null) PreviewAssets = (PreviewAssets ?? new PreviewAssets(null, [])) with { Materials = ChosenMaterials };
        ChosenMaterials = null;
        NotifyChosenMaterials();
    }

    //the mesh's own material for each of its slots, by MaterialIndex: LOD 0's section with it, or another LOD's
    private static IEntry[] SlotMaterials(StaticMesh stm, IMEPackage meshPackage)
    {
        var elements = stm.LODModels.SelectMany(lod => lod.Elements).Where(e => e.MaterialIndex >= 0).ToList();
        if (elements.Count == 0) return [];
        var slots = new IEntry[elements.Max(e => e.MaterialIndex) + 1];
        foreach (var element in elements) slots[element.MaterialIndex] ??= meshPackage.GetEntry(element.Material);
        return slots;
    }

    private void LoadMesh()
    {
        var context = RenderContext;
        meshSlotMaterials = null;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        StaticMeshExport = ResolveMesh("StaticMesh", Dependencies, ChosenMesh);
        if (StaticMeshExport is { } meshExport)
        {
            StaticMesh stm = context.GetStaticMesh(meshExport);
            meshSlotMaterials = SlotMaterials(stm, meshExport.FileRef);
            Dependencies.TrackData(meshExport.FileRef.GetEntry(stm.BodySetup) as ExportEntry);
            SplineMeshDeformation deformation = ReadDeformation(stm);
            if (stm.LODModels.Length > LOD)
            {
                foreach (var element in stm.LODModels[LOD].Elements)
                    Dependencies.Track(meshExport.FileRef.GetEntry(element.Material));
                // Only a spline mesh has a deformation. The game draws its sections whose materials can't be drawn on one with its default material
                if (deformation is not null)
                {
                    SplineActorProxy.UseGameSplineMeshMaterials(stm.LODModels[LOD].Elements, meshExport.FileRef, MaterialOverrides, context.PackageCache,
                        MaterialUsage.EngineMaterial(Export.Game, "DefaultMaterial", context.PackageCache));
                }
                // A chosen mesh is drawn without baked lighting, which was another mesh's, nor its IrrelevantLights, as its commit leaves it
                // (StaticMeshChange.Write)
                var lightingProperties = Properties;
                if (ChosenMesh is not null)
                {
                    lightingProperties = Properties.DeepClone();
                    lightingProperties.RemoveNamedProperty("IrrelevantLights");
                }
                MeshStaticLighting staticLighting = MeshStaticLighting.Create(context, Export, stm, LOD, lightingProperties,
                    ChosenMesh is not null || PreviewProperties is not null && bakedLighting is null, bakedLighting);
                Mesh = new ModelPreview<VertexType>(context, stm, LOD, staticLighting, MaterialOverrides, deformation);
                MaterialOverrides.Clear();
                MeshIFP = meshExport.InstancedFullPath;
                if (staticLighting is not null)
                {
                    StaticLighting = staticLighting;
                    UsesUnsupportedLightEnvironment = staticLighting.UsesLightEnvironment && staticLighting.LightEnvironment is null;
                    UnsupportedLightEnvironmentReason = staticLighting.UnsupportedLightEnvironmentReason;
                    staticLighting.IsInScene = IsInScene;
                    staticLighting.IsShown = IsShown;
                    staticLighting.IsDeleted = IsDeleted;
                    //only light environments trace against the level's geometry. (UpdateSelfLocalToWorld adds it to the geometry)
                    //USplineMeshComponent::LineCheck never hits, so spline meshes don't block those traces
                    if (MeshRenderContext.SupportsLightEnvironments(Export.Game) && deformation is null)
                    {
                        staticLighting.GeometryMesh = context.GetLevelGeometryMesh($"{meshExport.FileRef.FilePath}|{meshExport.UIndex}",
                            () => ReadLineCheckTriangles(stm), stm.kDOPTreeME3UDKLE);
                    }
                    JoinLightEnvironment(context, staticLighting.LightEnvironment, staticLighting.Channels, staticLighting.CastsDynamicShadow, staticLighting);
                }
                if (MeshIFP.Contains("Volumetric", StringComparison.OrdinalIgnoreCase)
                    || Mesh.Materials.Keys.Any(matIFP => matIFP.Contains("VolumeLight", StringComparison.OrdinalIgnoreCase)))
                {
                    IsVolumetric = true;
                }
            }
            //a spline mesh's collision isn't deformed (and UE3 doesn't collide with it)
            if (deformation is null)
                CollisionMesh = context.GetStaticMeshCollision(stm);
            UpdateSelfLocalToWorld();
        }
    }

    /// <summary>For spline meshes, the deformation the vertices are drawn with</summary>
    protected virtual SplineMeshDeformation ReadDeformation(StaticMesh mesh) => null;

    /// <summary>
    /// Refreshes only this component's rendering resources. Its actor, transform, selection, and undo history stay intact.
    /// Called on the UI/render thread between frames.
    /// </summary>
    public override bool RefreshPreview(IMEPackage package, IReadOnlySet<int> updatedExports, bool force = false)
    {
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        RenderContext.InvalidateLevelGeometry();
        ReleaseMesh();
        CollisionMesh?.Dispose();
        CollisionMesh = null;
        MeshIFP = null;
        IsVolumetric = false;
        ReadProperties();
        ReadMaterialOverrides();
        LoadMesh();
        NotifyMaterialSlots();
        return true;
    }

    /// <summary>
    /// The triangles the mesh blocks light environments' visibility traces with, in its local space: its collision (kDOP) triangles, all of them.
    /// The traces are TRACE_ShadowCast, for which UStaticMeshComponent::LineCheck never uses simple collision.
    /// Indices may be out of range; <see cref="LevelGeometry.TriangleMesh.Create"/> skips those triangles
    /// </summary>
    private static (Vector3[] Positions, int[] Indices) ReadLineCheckTriangles(StaticMesh stm)
    {
        //the collision triangles index LOD 0's positions
        var kDOPTriangles = stm.kDOPTreeME3UDKLE?.Triangles ?? [];
        if (stm.LODModels.Length == 0 || kDOPTriangles.Length == 0)
        {
            return ([], []);
        }
        var vertexData = stm.LODModels[0].PositionVertexBuffer.VertexData;
        var lodPositions = new Vector3[vertexData.Length];
        for (int i = 0; i < vertexData.Length; i++)
        {
            lodPositions[i] = new Vector3(vertexData[i].X, vertexData[i].Y, vertexData[i].Z);
        }
        var triangleIndices = new int[kDOPTriangles.Length * 3];
        for (int i = 0; i < kDOPTriangles.Length; i++)
        {
            triangleIndices[i * 3] = kDOPTriangles[i].Vertex1;
            triangleIndices[i * 3 + 1] = kDOPTriangles[i].Vertex2;
            triangleIndices[i * 3 + 2] = kDOPTriangles[i].Vertex3;
        }
        return (lodPositions, triangleIndices);
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (!context.IsComponentVisible(this)) return;
        if (pass is RenderPass.Collision)
        {
            if (CollisionMesh is not null)
            {
                context.RenderMeshAsWireframe(CollisionMesh);
            }
            return;
        }
        Mesh?.Render(pass, context, LOD);
    }

    public override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        UpdateSelfLocalToWorld();
    }

    private void UpdateSelfLocalToWorld()
    {
        if (CollisionMesh is not null)
        {
            CollisionMesh.LocalToWorld = LocalToWorld;
        }
        if (Mesh is not null)
        {
            Mesh.UpdateLocalToWorld(LocalToWorld);
            if (StaticLighting is { GeometryMesh: not null, CanBlockVisibilityTraces: true })
            {
                //it blocks light environments' visibility traces in its new place
                RenderContext.InvalidateLevelGeometry();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        CollisionMesh?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// A SplineMeshComponent: its static mesh's LOD 0 deformed along its stored SplineParams, or along the ones its loft actor rebuilt after an edit
/// </summary>
public sealed class SplineMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
    ExportEntry bakedLighting = null, EditableActorLighting lighting = null, PreviewAssets previewAssets = null)
    : StaticMeshComponentProxy(context, componentExport, parent, previewProperties, bakedLighting, lighting, previewAssets)
{
    /// <summary>The deformation the mesh is drawn with</summary>
    public SplineMeshDeformation Deformation { get; private set; }
    /// <summary>The stored SplineParams' deformation</summary>
    public SplineMeshDeformation StoredDeformation { get; private set; }
    /// <summary>The deformed static mesh's bounds, which place it along the spline</summary>
    public BoxSphereBounds MeshBounds { get; private set; }

    protected override SplineMeshDeformation ReadDeformation(StaticMesh mesh)
    {
        MeshBounds = mesh.Bounds;
        return Deformation = StoredDeformation = SplineMeshDeformation.Read(Properties, mesh.Bounds);
    }

    /// <summary>Draws the mesh deformed by <paramref name="deformation"/> (its stored one, or a rebuilt one) without reloading it</summary>
    internal void SetDeformation(SplineMeshDeformation deformation)
    {
        if (ReferenceEquals(deformation, Deformation)) return;
        Deformation = deformation;
        Mesh?.Redeform(deformation);
    }

    /// <summary>After a commit wrote <see cref="Deformation"/> to the component, it's the stored one</summary>
    internal void AcceptDeformation() => StoredDeformation = Deformation;
}

public class SkeletalMeshComponentProxy : MeshComponentProxy
{
    SkinnedMeshRenderer skinnedMeshRenderer;
    AnimSequencePlayer animPlayer;
    private PreviewMeshDependencies Dependencies;
    private AnimSequence CurrentAnimation;
    private float AnimationPosition;

    /// <summary>
    /// When set, the component is transformed relative to this component instead of its actor (ParentAnimComponent with bTransformFromAnimParent).
    /// Must come before this component in its actor's <see cref="ActorProxy.Components"/>, so that it's updated first
    /// </summary>
    private PrimitiveComponentProxy TransformParent;

    protected override Matrix4x4 ParentToWorld => TransformParent?.LocalToWorld ?? base.ParentToWorld;

    //a preview's: the component its ParentAnimComponent is drawn as, which its pending properties can't refer to
    private readonly PrimitiveComponentProxy previewAnimParent;

    /// <param name="previewProperties">For a preview (a clone's, until its commit makes the component): its values, which its export (a stand-in) doesn't
    /// have. Its mesh and materials are <paramref name="previewAssets"/>, and its ParentAnimComponent <paramref name="previewAnimParent"/></param>
    public SkeletalMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
        PreviewAssets previewAssets = null, PrimitiveComponentProxy previewAnimParent = null) : base(context, componentExport, parent, previewProperties, previewAssets)
    {
        this.previewAnimParent = previewAnimParent;
        LoadMesh();
    }

    private void LoadMesh()
    {
        var context = RenderContext;
        var parent = Actor;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        TransformParent = null;
        // LE1 declares it an int (Engine.SkeletalMeshComponent.bTransformFromAnimParent is an IntProperty)
        bool bTransformFromAnimParent = Properties.GetProp<Property>("bTransformFromAnimParent") switch
        {
            BoolProperty flag => flag.Value,
            IntProperty flag => flag.Value != 0,
            _ => true
        };
        var parentAnimComponent = PreviewProperties is not null ? previewAnimParent
            : Properties.GetProp<ObjectProperty>("ParentAnimComponent")?.ResolveToEntry(Export.FileRef) is ExportEntry parentAnimExport
                ? parent.Components.FirstOrDefault(cmp => cmp.Export == parentAnimExport) : null;
        if (bTransformFromAnimParent && parentAnimComponent is not null)
        {
            TransformParent = parentAnimComponent;
            base.UpdateLocalToWorld();
        }
        //like the static mesh, it may come from an archetype in another package
        var meshExport = ResolveMesh("SkeletalMesh", Dependencies);
        if (meshExport is not null)
        {
            SkeletalMesh skm = meshExport.GetBinaryData<SkeletalMesh>();
            bool usesLightEnvironment = false;
            DynamicLightEnvironment lightEnvironment = null;
            if (skm.LODModels.Length > LOD)
            {
                foreach (int material in skm.Materials)
                    Dependencies.Track(meshExport.FileRef.GetEntry(material));
                //Without a light environment (or with a disabled one), the mesh is lit by the level's lights directly
                MeshStaticLighting staticLighting = null;
                if (Export.Game.IsLEGame())
                {
                    usesLightEnvironment = context.ResolveLightEnvironment(Export, Properties, parent?.Export, out lightEnvironment, out string unsupportedReason);
                    UsesUnsupportedLightEnvironment = usesLightEnvironment && lightEnvironment is null;
                    UnsupportedLightEnvironmentReason = unsupportedReason;
                    if (!usesLightEnvironment)
                    {
                        staticLighting = MeshStaticLighting.CreateDynamic(context, Export, Properties);
                        if (staticLighting is not null) staticLighting.IsDeleted = IsDeleted;
                    }
                }
                Mesh = new ModelPreview<VertexType>(context, skm, staticLighting, LOD, MaterialOverrides);
                StaticLighting = staticLighting;
                MaterialOverrides.Clear();
                MeshIFP = meshExport.InstancedFullPath;
                skinnedMeshRenderer = new SkinnedMeshRenderer();
                skinnedMeshRenderer.BuildFromSkeletalMesh(meshExport.FileRef.Game, skm.LODModels[LOD]);
                animPlayer = new AnimSequencePlayer(skm);
            }
            UpdateSelfLocalToWorld();
            if (Mesh is not null && lightEnvironment is not null)
            {
                //MeshComponent's defaults: CastShadow, bCastDynamicShadow
                bool castsShadow = Properties.GetProp<BoolProperty>("CastShadow") is not { Value: false }
                                   && Properties.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
                var boneNames = new HashSet<string>(skm.RefSkeleton.Select(bone => bone.Name.Instanced), StringComparer.OrdinalIgnoreCase);
                JoinLightEnvironment(context, lightEnvironment, LightingChannels.FromProperty(Properties.GetProp<StructProperty>("LightingChannels"), default,
                    isInitialized: false, LightingChannels.DynamicPrimitiveDefault), castsShadow, hasBone: boneNames.Contains);
            }
        }
    }

    public override bool RefreshPreview(IMEPackage package, IReadOnlySet<int> updatedExports, bool force = false)
    {
        if (!force && !Dependencies.IsAffectedBy(package, updatedExports)) return false;
        ReleaseMesh();
        skinnedMeshRenderer = null;
        animPlayer = null;
        MeshIFP = null;
        ReadProperties();
        ReadMaterialOverrides();
        LoadMesh();
        // Its actor's class defaults and morph face. (Its transform is updated by the actor after the refresh)
        Actor?.OnComponentReloaded(this);
        SetAnimation(CurrentAnimation, AnimationPosition);
        return true;
    }

    public override void UpdateScene(MeshRenderContext context, float deltaTime)
    {
        if (Mesh is not null && skinnedMeshRenderer.NeedsUpdate)
        {
            skinnedMeshRenderer.UpdateSkinning(context.ImmediateContext, Mesh.LODs[LOD].Mesh, animPlayer);
        }
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (!context.IsComponentVisible(this)) return;
        Mesh?.Render(pass, context, LOD);
    }

    public void SetAnimation(AnimSequence animSequence, float pos)
    {
        CurrentAnimation = animSequence;
        AnimationPosition = pos;
        if (animPlayer is null) return;
        if (animSequence is null)
        {
            if (animPlayer.HasAnimation)
            {
                //cancel animation, reset to ref pose
                animPlayer.SetAnimation(null);
                skinnedMeshRenderer.NeedsUpdate = true;
            }
            return;
        }
        if (animSequence.Name != animPlayer.AnimName)
        {
            animPlayer.SetAnimation(animSequence);
        }
        animPlayer.SetCurrentTime(pos);
        skinnedMeshRenderer.NeedsUpdate = true;
    }

    public void ApplyMorph(LegendaryExplorerCore.Unreal.Classes.BonePosition[] bonePositions, Vector3[][] morphLods)
    {
        if (Mesh is null) return;
        if (morphLods?.Length > LOD)
        {
            skinnedMeshRenderer.UpdateVertexPositions(morphLods[LOD]);
            skinnedMeshRenderer.NeedsUpdate = true;
        }
        if (bonePositions is not null && animPlayer is not null)
        {
            animPlayer.ApplyBonePositions(bonePositions);
            skinnedMeshRenderer.NeedsUpdate = true;
        }
    }

    public override void UpdateLocalToWorld()
    {
        base.UpdateLocalToWorld();
        UpdateSelfLocalToWorld();
    }

    private void UpdateSelfLocalToWorld()
    {
        Mesh?.UpdateLocalToWorld(LocalToWorld);
    }
}

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
