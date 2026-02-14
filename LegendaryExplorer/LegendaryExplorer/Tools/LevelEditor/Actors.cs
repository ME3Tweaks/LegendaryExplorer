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

namespace LegendaryExplorer.Tools.LevelEditor;

public class ActorProxy : NotifyPropertyChangedBase, IDisposable, IHitProxy
{
    public LevelEditor Editor;

    public Matrix4x4 LocalToWorld;

    public List<PrimitiveComponentProxy> Components = [];

    protected PropertyCollection Properties;

    public ExportEntry Export { get; }

    private bool isDirty;
    public bool IsDirty
    {
        get => isDirty;
        protected set
        {
            if (SetProperty(ref isDirty, value) && isDirty && Editor is not null)
            {
                Editor.IsDirty = true;
            }
        }
    }

    protected Rotator rotation;
    protected Vector3 location;
    protected Vector3 drawScale3D;
    protected float drawScale;
    protected Vector3 prePivot;
    public Rotator Rotation
    {
        get => rotation;
        set
        {
            var oldValue = rotation;
            if (rotation != value)
            {
                rotation = value;
                OnPropertyChanged(nameof(Rotation));
                if (value.Pitch != oldValue.Pitch) OnPropertyChanged(nameof(PitchDegrees));
                if (value.Yaw != oldValue.Yaw) OnPropertyChanged(nameof(YawDegrees));
                if (value.Roll != oldValue.Roll) OnPropertyChanged(nameof(RollDegrees));
                UpdateLocalToWorld();
                IsDirty = true;
            }
        }
    }
    public float PitchDegrees { get => rotation.Pitch.UnrealRotationUnitsToDegrees(); set => Rotation = new Rotator(value.DegreesToUnrealRotationUnits(), rotation.Yaw, rotation.Roll); }
    public float YawDegrees { get => rotation.Yaw.UnrealRotationUnitsToDegrees(); set => Rotation = new Rotator(rotation.Pitch, value.DegreesToUnrealRotationUnits(), rotation.Roll); }
    public float RollDegrees { get => rotation.Roll.UnrealRotationUnitsToDegrees(); set => Rotation = new Rotator(rotation.Pitch, rotation.Yaw, value.DegreesToUnrealRotationUnits()); }
    public Vector3 Location
    {
        get => location;
        set 
        {
            var oldValue = location;
            if (location != value)
            {
                location = value;
                OnPropertyChanged(nameof(Location));
                if (value.X != oldValue.X) OnPropertyChanged(nameof(XPos));
                if (value.Y != oldValue.Y) OnPropertyChanged(nameof(YPos));
                if (value.Z != oldValue.Z) OnPropertyChanged(nameof(ZPos));
                UpdateLocalToWorld();
                IsDirty = true;
            }
        }
    }
    public float XPos { get => location.X; set => Location = location with { X = value }; }
    public float YPos { get => location.Y; set => Location = location with { Y = value }; }
    public float ZPos { get => location.Z; set => Location = location with { Z = value }; }

    public Vector3 DrawScale3D
    {
        get => drawScale3D;
        set
        {
            var oldValue = drawScale3D;
            if (drawScale3D != value)
            {
                drawScale3D = value;
                OnPropertyChanged(nameof(DrawScale3D));
                if (value.X != oldValue.X) OnPropertyChanged(nameof(XScale));
                if (value.Y != oldValue.Y) OnPropertyChanged(nameof(YScale));
                if (value.Z != oldValue.Z) OnPropertyChanged(nameof(ZScale));
                UpdateLocalToWorld();
                IsDirty = true;
            }
        }
    }
    public float XScale { get => drawScale3D.X; set => DrawScale3D = drawScale3D with { X = value }; }
    public float YScale { get => drawScale3D.Y; set => DrawScale3D = drawScale3D with { Y = value }; }
    public float ZScale { get => drawScale3D.Z; set => DrawScale3D = drawScale3D with { Z = value }; }
    public float DrawScale
    {
        get => drawScale;
        set 
        { 
            if (SetProperty(ref drawScale, value))
            {
                UpdateLocalToWorld();
                IsDirty = true;
            }
        }
    }
    public Vector3 PrePivot
    {
        get => prePivot;
        set 
        { 
            if (SetProperty(ref prePivot, value))
            {
                UpdateLocalToWorld();
                IsDirty = true;
            }
        }
    }

    public virtual bool IsVolume => false;

    protected ActorProxy(LevelEditor context, ExportEntry actorExport)
    {
        Editor = context;
        Export = actorExport;
        Properties = actorExport.GetCondensedProperties();
        PropertyCollection props = Properties;

        var rotationProp = props.GetProp<StructProperty>("Rotation");
        var locationsProp = props.GetProp<StructProperty>("location");
        var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
        var prePivotProp = props.GetProp<StructProperty>("PrePivot");

        drawScale = props.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;
        location = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
        drawScale3D = drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : Vector3.One;
        prePivot = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
        rotation = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);
        UpdateLocalToWorld();
    }

    //only for use by the faux actors that are children of the CollectionActors
    protected ActorProxy(ExportEntry actorExport)
    {
        Export = actorExport;
        drawScale = 1;
        location =  Vector3.Zero;
        drawScale3D =  Vector3.One;
        prePivot =  Vector3.Zero;
        rotation = new Rotator(0, 0, 0);
    }

    protected virtual void UpdateLocalToWorld()
    {
        LocalToWorld = ActorUtils.ComposeLocalToWorld(location, rotation, drawScale * drawScale3D, prePivot);
        foreach (var cmp in Components)
        {
            cmp.UpdateLocalToWorld();
        }
    }

    public static ActorProxy Create(LevelEditor context, ExportEntry actorExport)
    {
        string className = actorExport.ClassName;
        switch (className)
        {
        }
        if (GlobalUnrealObjectInfo.IsA(className, "StaticMeshActor", actorExport.Game))
        {
            return new StaticMeshActorProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "SkeletalMeshActor", actorExport.Game))
        {
            return new SkeletalMeshActorProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "DynamicSMActor", actorExport.Game))
        {
            return new DynamicSMActorProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "Brush", actorExport.Game))
        {
            return new BrushProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "SFXStuntActor", actorExport.Game))
        {
            return new SFXStuntActorProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "BioArtPlaceable", actorExport.Game))
        {
            return new BioArtPlaceableProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "BioPawn", actorExport.Game))
        {
            return new BioPawnProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "Pawn", actorExport.Game))
        {
            return new PawnProxy(context, actorExport);
        }
        if (GlobalUnrealObjectInfo.IsA(className, "PrefabInstance", actorExport.Game))
        {
            return new PrefabInstanceProxy(context, actorExport);
        }
        return null;
        //return new ActorProxy(context, actorExport);
    }

    protected void AddComponents(MeshRenderContext context, params Span<string> propNames)
    {
        foreach (var propName in propNames)
        {
            if (Properties.GetProp<ObjectProperty>(propName)?.ResolveToEntry(Export.FileRef) is ExportEntry componentExport)
            {
                if (PrimitiveComponentProxy.Create(context, componentExport, this) is { } cmpProxy)
                {
                    Components.Add(cmpProxy);
                }
            }
            else if (Properties.GetProp<ArrayProperty<ObjectProperty>>(propName) is { } componentArray)
            {
                foreach (ObjectProperty prop in componentArray)
                {
                    if (prop?.ResolveToEntry(Export.FileRef) is ExportEntry cmpExport)
                    {
                        if (PrimitiveComponentProxy.Create(context, cmpExport, this) is { } cmpProxy)
                        {
                            Components.Add(cmpProxy);
                        }
                    }
                }
            }
        }
    }

    public virtual void Render(MeshRenderContext context, RenderPass pass)
    {
        foreach (var component in Components)
        {
            component.Render(context, pass);
        }
    }

    public virtual BoxSphereBounds GetBounds()
    {
        if (Components.Count is 0)
        {
            return new BoxSphereBounds
            {
                Origin = LocalToWorld.Translation
            };
        }
        var bounds = Components[0].GetBounds();
        for (int i = 1; i < Components.Count; i++)
        {
            bounds = bounds.Union(Components[i].GetBounds());
        }
        return bounds;
    }

    public int HitID { get; set; }

    public virtual int HitPriority => IHitProxy.StandardPriority;

    public virtual void CommitChanges(PackageCache packageCache = null)
    {
        var props = Properties;

        string locationPropName = Export.Game.IsGame3() ? "location" : "Location";
        if (props.ContainsNamedProp(locationPropName) || Location != Vector3.Zero)
        {
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(Location, locationPropName));
        }
        if (props.ContainsNamedProp("DrawScale") || DrawScale != 1f)
        {
            props.AddOrReplaceProp(new FloatProperty(DrawScale, "DrawScale"));
        }
        if (props.ContainsNamedProp("DrawScale3D") || DrawScale3D != Vector3.One)
        {
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(DrawScale3D, "DrawScale3D"));
        }
        if (props.ContainsNamedProp("Rotation") || !Rotation.IsZero)
        {
            props.AddOrReplaceProp(CommonStructs.RotatorProp(Rotation, "Rotation"));
        }
        if (props.ContainsNamedProp("PrePivot") || PrePivot != Vector3.Zero)
        {
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(PrePivot, "PrePivot"));
        }
        Export.WriteProperties(props);
    }

    public virtual bool TestUIndexes(HashSet<int> uIndexes)
    {
        if (uIndexes.Contains(Export.UIndex))
        {
            return true;
        }
        foreach (var cmp in Components)
        {
            if (cmp.TestUIndexes(uIndexes))
            {
                return true;
            }
        }
        return false;
    }

    #region IDisposable
    protected bool isDisposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                foreach (var cmp in Components)
                {
                    cmp.Dispose();
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            isDisposed = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ActorProxy()
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

file class StaticMeshActorProxy : ActorProxy
{
    public StaticMeshActorProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "StaticMeshComponent");
    }
}

file class SkeletalMeshActorProxy : ActorProxy
{
    public SkeletalMeshActorProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "SkeletalMeshComponent");
    }
}

//interpactor, placeables
file class DynamicSMActorProxy : ActorProxy
{
    public DynamicSMActorProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "StaticMeshComponent");
    }
}

//volumes
file class BrushProxy : ActorProxy
{
    public override bool IsVolume => true;

    public BrushProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "BrushComponent");
    }
    public override int HitPriority => IHitProxy.WireFramePriority;
}
file class SFXStuntActorProxy : ActorProxy
{
    public SFXStuntActorProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "BodyMesh", "HeadMesh", "HairMesh", "HeadGearMesh");
    }
}
file class BioArtPlaceableProxy : ActorProxy
{
    public BioArtPlaceableProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "PlaceableMesh");
        if (Components.Count is 0)
        {
            AddComponents(context.RenderContext, "DestroyedMesh");
        }
    }
}
file class PawnProxy : ActorProxy
{
    public PawnProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "Mesh");
    }
}
file class BioPawnProxy : PawnProxy
{
    public BioPawnProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponents(context.RenderContext, "HeadMesh", "m_oHairMesh", "m_oHeadGearMesh", "m_oVisorMesh", "m_oFacePlateMesh", "m_aoAccessories");
    }
}

public abstract class CollectionActorComponentProxy : ActorProxy
{
    public ExportEntry CollectionActorExport { get; }

    public CollectionActorComponentProxy(LevelEditor context, StaticCollectionActor collectionActor, ExportEntry componentActor, int index) : base(componentActor)
    {
        Editor = context;
        CollectionActorExport = collectionActor.Export;

        LocalToWorld = collectionActor.LocalToWorldTransforms[index];
        (location, drawScale3D, rotation) = collectionActor.GetDecomposedTransformationForIndex(index);
        if (drawScale3D.X == drawScale3D.Y && drawScale3D.X == drawScale3D.Z)
        {
            drawScale = drawScale3D.X;
            drawScale3D = Vector3.One;
        }
    }

    public override void CommitChanges(PackageCache packageCache = null)
    {
        throw new InvalidOperationException($"Cannot be called on a {nameof(CollectionActorComponentProxy)}.");
    }

    public void CommitChanges(StaticCollectionActor collectionActor)
    {
        if (!(collectionActor.Components.FindIndex(uIdx => uIdx == Export.UIndex) is int idx and >= 0))
        {
            throw new ArgumentException("Does not contain this component", nameof(collectionActor));
        }
        Matrix4x4 m = ActorUtils.ComposeLocalToWorld(Location, Rotation, DrawScale * DrawScale3D, PrePivot);
        collectionActor.LocalToWorldTransforms[idx] = m;
    }

    public override bool TestUIndexes(HashSet<int> uIndexes)
    {
        return base.TestUIndexes(uIndexes) || uIndexes.Contains(CollectionActorExport.UIndex);
    }
}

public class StaticMeshComponentActorProxy : CollectionActorComponentProxy
{
    public StaticMeshComponentActorProxy(LevelEditor context, ExportEntry smcExport, StaticMeshCollectionActor smca, int smcaIndex) : base(context, smca, smcExport, smcaIndex)
    {
        var staticMeshComponentProxy = PrimitiveComponentProxy.Create(context.RenderContext, smcExport, this);
        Components.Add(staticMeshComponentProxy);
    }
}

file class PrefabInstanceProxy : ActorProxy
{
    private readonly List<ActorProxy> Actors = [];
    private readonly List<Matrix4x4> RelativeMatrices = [];

    public PrefabInstanceProxy(LevelEditor context, ExportEntry actorExport) : base(context, actorExport)
    {
        PackageCache packageCache = context.RenderContext.PackageCache;
        if (Properties.GetProp<ObjectProperty>("TemplatePrefab")?
            .ResolveToExport(actorExport.FileRef, packageCache) is ExportEntry prefab
            && prefab.GetProperty<ArrayProperty<ObjectProperty>>("PrefabArchetypes") is { } prefabActors)
        {

            foreach (var objProp in prefabActors)
            {
                if (objProp.TryResolveExport(prefab.FileRef, packageCache, out ExportEntry prefabActor)
                    && Create(context, prefabActor) is ActorProxy prefabActorProxy)
                {
                    
                    prefabActorProxy.Editor = null; // prevent IsDirty being marked

                    var actorRelative = ActorUtils.ComposeLocalToWorld(prefabActorProxy.Location, prefabActorProxy.Rotation, Vector3.One);
                    (prefabActorProxy.Location, _, prefabActorProxy.Rotation) = (actorRelative * LocalToWorld).UnrealDecompose();
                    Actors.Add(prefabActorProxy);
                    RelativeMatrices.Add(actorRelative);
                }
            }
        }
    }

    public override void Render(MeshRenderContext context, RenderPass pass)
    {
        foreach (var actor in Actors)
        {
            actor.Render(context, pass);
        }
    }

    protected override void UpdateLocalToWorld()
    {
        //Unreal appears to ignore scaling on a prefab
        LocalToWorld = ActorUtils.ComposeLocalToWorld(Location, Rotation, Vector3.One);
        for (int i = 0; i < Actors.Count; i++)
        {
            ActorProxy actor = Actors[i];
            (actor.Location, _, actor.Rotation) = (RelativeMatrices[i] * LocalToWorld).UnrealDecompose();
        }
    }

    public override BoxSphereBounds GetBounds()
    {
        if (Actors.Count is 0)
        {
            return new BoxSphereBounds
            {
                Origin = LocalToWorld.Translation
            };
        }
        var bounds = Actors[0].GetBounds();
        for (int i = 1; i < Actors.Count; i++)
        {
            bounds = bounds.Union(Actors[i].GetBounds());
        }
        return bounds;
    }

    public override void CommitChanges(PackageCache packageCache = null)
    {
        var props = Properties;

        string locationPropName = Export.Game.IsGame3() ? "location" : "Location";
        if (props.ContainsNamedProp(locationPropName) || Location != Vector3.Zero)
        {
            props.AddOrReplaceProp(CommonStructs.Vector3Prop(Location, locationPropName));
        }
        if (props.ContainsNamedProp("Rotation") || !Rotation.IsZero)
        {
            props.AddOrReplaceProp(CommonStructs.RotatorProp(Rotation, "Rotation"));
        }
        Export.WriteProperties(props);
    }

    public override bool TestUIndexes(HashSet<int> uIndexes)
    {
        if (uIndexes.Contains(Export.UIndex))
        {
            return true;
        }
        foreach (var actor in Actors)
        {
            if (actor.TestUIndexes(uIndexes))
            {
                return true;
            }
        }
        return false;
    }
    protected override void Dispose(bool disposing)
    {
        if (!isDisposed)
        {
            if (disposing)
            {
                foreach (var actor in Actors)
                {
                    actor.Dispose();
                }
            }
            isDisposed = true;
        }
    }
}