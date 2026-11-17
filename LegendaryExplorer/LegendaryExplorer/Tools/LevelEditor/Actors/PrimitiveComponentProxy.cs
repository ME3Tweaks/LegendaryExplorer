using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Numerics;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders

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
