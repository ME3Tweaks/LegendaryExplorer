using LegendaryExplorer.Misc;
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
            AcceptsLights = Properties.GetProp<BoolProperty>("bAcceptsLights") is not { Value: false },
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

    protected MeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null) : base(context, componentExport, parent, previewProperties)
    {
        ReadMaterialOverrides();
    }

    protected void ReadMaterialOverrides()
    {
        MaterialOverrides.Clear();
        //as the bake reads them: from the nearest object in the archetype chain that sets them (an empty array overrides its archetypes' too),
        //including archetypes in other packages (prefabs in the persistent level, class defaults' subobjects), which condensed properties leave out
        var (mats, package) = PreviewProperties is not null ? (PreviewProperties.GetProp<ArrayProperty<ObjectProperty>>("Materials"), Export.FileRef)
            : EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(Export, "Materials", RenderContext.PackageCache);
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
    private readonly bool preservePreviewLighting;

    public StaticMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null, bool preservePreviewLighting = false, EditableActorLighting lighting = null) : base(context, componentExport, parent, previewProperties)
    {
        this.preservePreviewLighting = preservePreviewLighting;
        if (lighting is not null) RestoreLighting(lighting);
        LoadMesh();
    }

    private void LoadMesh()
    {
        var context = RenderContext;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        var meshReference = PreviewProperties?.GetProp<ObjectProperty>("StaticMesh")?.ResolveToEntry(Export.FileRef);
        //a preview's mesh and materials are its pending properties' (its export, which the package doesn't have, has none of its own)
        if (PreviewProperties is not null)
        {
            Dependencies.Track(meshReference);
            foreach (var material in PreviewProperties.GetProp<ArrayProperty<ObjectProperty>>("Materials") ?? Enumerable.Empty<ObjectProperty>())
                Dependencies.Track(material.ResolveToEntry(Export.FileRef));
        }
        if ((PreviewProperties is not null ? EffectiveLightingProperties.Resolve(meshReference, context.PackageCache)
            : EffectiveLightingProperties.ResolveReference(Export, "StaticMesh", context.PackageCache)) is { } meshExport)
        {
            StaticMesh stm = context.GetStaticMesh(meshExport);
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
                MeshStaticLighting staticLighting = MeshStaticLighting.Create(context, Export, stm, LOD, Properties, PreviewProperties is not null && !preservePreviewLighting);
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
public sealed class SplineMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null, bool preservePreviewLighting = false, EditableActorLighting lighting = null)
    : StaticMeshComponentProxy(context, componentExport, parent, previewProperties, preservePreviewLighting, lighting)
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

    public SkeletalMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        LoadMesh();
    }

    private void LoadMesh()
    {
        var context = RenderContext;
        var parent = Actor;
        Dependencies = new PreviewMeshDependencies(Export, context.PackageCache);
        TransformParent = null;
        bool bTransformFromAnimParent = Properties.GetProp<BoolProperty>("bTransformFromAnimParent")?.Value ?? true;
        if (bTransformFromAnimParent
            && Properties.GetProp<ObjectProperty>("ParentAnimComponent")?.ResolveToEntry(Export.FileRef) is ExportEntry parentAnimExport
            && parent.Components.FirstOrDefault(cmp => cmp.Export == parentAnimExport) is { } parentAnimComponent)
        {
            TransformParent = parentAnimComponent;
            base.UpdateLocalToWorld();
        }
        //like the static mesh, it may come from an archetype in another package
        if (EffectiveLightingProperties.ResolveReference(Export, "SkeletalMesh", context.PackageCache) is { } meshExport)
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
