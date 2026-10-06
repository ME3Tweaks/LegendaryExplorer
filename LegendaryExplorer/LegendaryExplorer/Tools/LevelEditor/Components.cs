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

    protected readonly MeshRenderContext RenderContext;

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
    /// Not rendered in game. Only shown in the level editor when <see cref="LevelEditorRenderContext.ShowHidden"/> is set
    /// </summary>
    public bool HiddenGame { get; }

    protected PrimitiveComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent)
    {
        Actor = parent;
        Export = componentExport;
        RenderContext = context;
        Properties = componentExport.GetCondensedProperties();
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
    public string MeshIFP { get; protected set; }
    protected ModelPreview<VertexType> Mesh;
    public int LOD;
    public List<IEntry> MaterialOverrides = [];

    /// <summary>
    /// How the mesh is part of the light environment that lights it, if it's lit by one
    /// </summary>
    private LightEnvironmentPrimitive LightEnvironmentPrimitive;
    private DynamicLightEnvironment LightEnvironment;

    /// <summary>
    /// Whether the mesh is in the game's scene: neither it nor its actor is hidden in game
    /// </summary>
    protected bool IsInScene() => !HiddenGame && Actor is not { IsHidden: true };

    /// <summary>
    /// Whether the mesh is shown in the editor, so that it casts its dynamic shadows: neither it nor its actor's category is hidden
    /// </summary>
    protected bool IsShown() => IsVisible && (Actor is null || RenderContext.IsActorVisible(Actor));

    /// <summary>
    /// Adds the mesh to the light environment that lights it, so that the light environment's bounds include it, and it casts the light environment's shadow
    /// </summary>
    /// <param name="castsShadow">CastShadow and bCastDynamicShadow, including inherited values</param>
    /// <param name="staticLighting">For static meshes: its <see cref="MeshStaticLighting.LightEnvironment"/> is set to the light environment joined</param>
    protected void JoinLightEnvironment(MeshRenderContext context, DynamicLightEnvironment lightEnvironment, LightingChannels lightingChannels, bool castsShadow,
        MeshStaticLighting staticLighting = null)
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
            DrawShadowCaster = castsShadow ? ctx => Mesh?.DrawShadowCaster(ctx, lodIndex) : null,
            IsInScene = IsInScene,
            IsShown = IsShown,
            LightingChannels = lightingChannels,
        };
        //another of the light environment's primitives may have joined it first
        LightEnvironment = context.AddLightEnvironmentPrimitive(lightEnvironment, LightEnvironmentPrimitive);
        lod.LightEnvironment = LightEnvironment;
        if (staticLighting is not null)
        {
            staticLighting.LightEnvironment = LightEnvironment;
        }
    }

    protected MeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        if (Properties.GetProp<ArrayProperty<ObjectProperty>>("Materials") is { } mats)
        {
            MaterialOverrides.AddRange(mats.Select(x => x.Value != 0 ? x.ResolveToEntry(Export.FileRef) : null));
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

    protected override void Dispose(bool disposing)
    {
        if (LightEnvironmentPrimitive is not null)
        {
            RenderContext.RemoveLightEnvironmentPrimitive(LightEnvironment, LightEnvironmentPrimitive);
            LightEnvironmentPrimitive = null;
        }
        Mesh?.Dispose();
        base.Dispose(disposing);
    }
}

public class StaticMeshComponentProxy : MeshComponentProxy
{
    private readonly Mesh<WorldVertex> CollisionMesh;
    private readonly MeshStaticLighting StaticLighting;

    public StaticMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        if (Properties.GetProp<ObjectProperty>("StaticMesh")?.ResolveToExport(Export.FileRef, context.PackageCache) is ExportEntry meshExport)
        {
            StaticMesh stm = meshExport.GetBinaryData<StaticMesh>();
            if (stm.LODModels.Length > LOD)
            {
                stm.SetMaterials(MaterialOverrides, true);
                MaterialOverrides.Clear();
                MeshStaticLighting staticLighting = MeshStaticLighting.Create(context, Export, stm, LOD);
                Mesh = new ModelPreview<VertexType>(context, stm, LOD, staticLighting);
                MeshIFP = meshExport.InstancedFullPath;
                if (staticLighting is not null)
                {
                    StaticLighting = staticLighting;
                    staticLighting.IsInScene = IsInScene;
                    staticLighting.IsShown = IsShown;
                    //only light environments trace against the level's geometry. (UpdateSelfLocalToWorld adds it to the geometry)
                    if (MeshRenderContext.SupportsLightEnvironments(Export.Game))
                    {
                        staticLighting.GeometryMesh = context.GetLevelGeometryMesh($"{meshExport.FileRef.FilePath}|{meshExport.UIndex}",
                            () => ReadLineCheckTriangles(stm));
                    }
                    JoinLightEnvironment(context, staticLighting.LightEnvironment, staticLighting.Channels, staticLighting.CastsDynamicShadow, staticLighting);
                }
                if (MeshIFP.Contains("Volumetric", StringComparison.OrdinalIgnoreCase)
                    || Mesh.Materials.Keys.Any(matIFP => matIFP.Contains("VolumeLight", StringComparison.OrdinalIgnoreCase)))
                {
                    IsVolumetric = true;
                }
            }
            CollisionMesh = context.GetMeshFromAggGeom(stm.GetCollisionMeshProperty(Export.FileRef));
            UpdateSelfLocalToWorld();
        }
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
        if (!IsVisible) return;
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

public class SkeletalMeshComponentProxy : MeshComponentProxy
{
    SkinnedMeshRenderer skinnedMeshRenderer;
    AnimSequencePlayer animPlayer;

    /// <summary>
    /// When set, the component is transformed relative to this component instead of its actor (ParentAnimComponent with bTransformFromAnimParent).
    /// Must come before this component in its actor's <see cref="ActorProxy.Components"/>, so that it's updated first
    /// </summary>
    private readonly PrimitiveComponentProxy TransformParent;

    protected override Matrix4x4 ParentToWorld => TransformParent?.LocalToWorld ?? base.ParentToWorld;

    public SkeletalMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        bool bTransformFromAnimParent = Properties.GetProp<BoolProperty>("bTransformFromAnimParent")?.Value ?? true;
        if (bTransformFromAnimParent
            && Properties.GetProp<ObjectProperty>("ParentAnimComponent")?.ResolveToEntry(Export.FileRef) is ExportEntry parentAnimExport
            && parent.Components.FirstOrDefault(cmp => cmp.Export == parentAnimExport) is { } parentAnimComponent)
        {
            TransformParent = parentAnimComponent;
            base.UpdateLocalToWorld();
        }
        if (Properties.GetProp<ObjectProperty>("SkeletalMesh")?.ResolveToExport(Export.FileRef, context.PackageCache) is ExportEntry meshExport)
        {
            SkeletalMesh skm = meshExport.GetBinaryData<SkeletalMesh>();
            PropertyCollection condensedProps = null;
            bool usesLightEnvironment = false;
            DynamicLightEnvironment lightEnvironment = null;
            if (skm.LODModels.Length > LOD)
            {
                skm.SetMaterials(MaterialOverrides, true);
                MaterialOverrides.Clear();
                //Without a light environment (or with a disabled one), the mesh is lit by the level's lights directly
                MeshStaticLighting staticLighting = null;
                if (Export.Game.IsLEGame())
                {
                    condensedProps = Export.GetCondensedProperties(context.PackageCache, resolveImports: true, mergeStructs: true);
                    usesLightEnvironment = context.ResolveLightEnvironment(Export, condensedProps, parent?.Export, out lightEnvironment);
                    if (!usesLightEnvironment)
                    {
                        staticLighting = MeshStaticLighting.CreateDynamic(context, Export, condensedProps);
                    }
                }
                Mesh = new ModelPreview<VertexType>(context, skm, staticLighting, LOD);
                MeshIFP = meshExport.InstancedFullPath;
                skinnedMeshRenderer = new SkinnedMeshRenderer();
                skinnedMeshRenderer.BuildFromSkeletalMesh(meshExport.FileRef.Game, skm.LODModels[LOD]);
                animPlayer = new AnimSequencePlayer(skm);
            }
            UpdateSelfLocalToWorld();
            if (Mesh is not null && lightEnvironment is not null)
            {
                //MeshComponent's defaults: CastShadow, bCastDynamicShadow
                bool castsShadow = condensedProps.GetProp<BoolProperty>("CastShadow") is not { Value: false }
                                   && condensedProps.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
                JoinLightEnvironment(context, lightEnvironment, LightingChannels.FromProperty(condensedProps.GetProp<StructProperty>("LightingChannels"), default,
                    isInitialized: false, LightingChannels.DynamicPrimitiveDefault), castsShadow);
            }
        }
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
        if (!IsVisible) return;
        Mesh?.Render(pass, context, LOD);
    }

    public void SetAnimation(AnimSequence animSequence, float pos)
    {
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

    public BrushComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent) : base(context, componentExport, parent)
    {
        Brush = context.GetMeshFromAggGeom(Properties.GetProp<StructProperty>("BrushAggGeom"));
        UpdateSelfLocalToWorld();
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        if (!IsVisible) return;
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