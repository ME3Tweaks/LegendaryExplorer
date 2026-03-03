using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace LegendaryExplorer.Tools.LevelEditor;

public class ActorProxy : NotifyPropertyChangedBase, IDisposable, IHitProxy
{
    public IActorEditorContext Editor;
    public OpenLevelFile OwningFile { get; set; }

    public Matrix4x4 LocalToWorld;

    public List<PrimitiveComponentProxy> Components = [];

    protected PropertyCollection Properties;

    public ExportEntry Export { get; }

    public string OwningFileName => System.IO.Path.GetFileName(Export.FileRef.FilePath);

    public string DisplayText { get; }

    private bool isDirty;
    public bool IsDirty
    {
        get => isDirty;
        protected set
        {
            if (SetProperty(ref isDirty, value))
            {
                if (OwningFile is not null)
                {
                    if (isDirty)
                        OwningFile.IsDirty = true;
                    else
                        OwningFile.RecalculateDirty();
                }
            }
        }
    }

    protected TransformSnapshot _cleanSnapshot;

    public void MarkClean()
    {
        _cleanSnapshot = SnapshotTransform();
        IsDirty = false;
    }

    public NameReference Tag;

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
            if (IsReadOnly) return;
            var oldValue = rotation;
            if (rotation != value)
            {
                rotation = value;
                OnPropertyChanged(nameof(Rotation));
                if (value.Pitch != oldValue.Pitch) OnPropertyChanged(nameof(PitchDegrees));
                if (value.Yaw != oldValue.Yaw) OnPropertyChanged(nameof(YawDegrees));
                if (value.Roll != oldValue.Roll) OnPropertyChanged(nameof(RollDegrees));
                UpdateLocalToWorld();
                IsDirty = !SnapshotTransform().Equals(_cleanSnapshot);
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
            if (IsReadOnly) return;
            var oldValue = location;
            if (location != value)
            {
                location = value;
                OnPropertyChanged(nameof(Location));
                if (value.X != oldValue.X) OnPropertyChanged(nameof(XPos));
                if (value.Y != oldValue.Y) OnPropertyChanged(nameof(YPos));
                if (value.Z != oldValue.Z) OnPropertyChanged(nameof(ZPos));
                UpdateLocalToWorld();
                IsDirty = !SnapshotTransform().Equals(_cleanSnapshot);
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
            if (IsReadOnly) return;
            var oldValue = drawScale3D;
            if (drawScale3D != value)
            {
                drawScale3D = value;
                OnPropertyChanged(nameof(DrawScale3D));
                if (value.X != oldValue.X) OnPropertyChanged(nameof(XScale));
                if (value.Y != oldValue.Y) OnPropertyChanged(nameof(YScale));
                if (value.Z != oldValue.Z) OnPropertyChanged(nameof(ZScale));
                UpdateLocalToWorld();
                IsDirty = !SnapshotTransform().Equals(_cleanSnapshot);
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
            if (IsReadOnly) return;
            if (SetProperty(ref drawScale, value))
            {
                UpdateLocalToWorld();
                IsDirty = !SnapshotTransform().Equals(_cleanSnapshot);
            }
        }
    }
    public Vector3 PrePivot
    {
        get => prePivot;
        set
        {
            if (IsReadOnly) return;
            if (SetProperty(ref prePivot, value))
            {
                UpdateLocalToWorld();
                IsDirty = !SnapshotTransform().Equals(_cleanSnapshot);
            }
        }
    }

    public bool IsReadOnly => (OwningFile is null || OwningFile.IsReadOnly)
                           && !(Editor?.IsApplyingUndoRedo ?? false);

    public virtual bool IsVolume => false;
    public bool IsVolumetricMesh { get; protected set; }

    public TransformSnapshot SnapshotTransform() => new(location, rotation, drawScale, drawScale3D);

    public void RestoreTransform(TransformSnapshot snapshot)
    {
        Location = snapshot.Location;
        Rotation = snapshot.Rotation;
        DrawScale = snapshot.DrawScale;
        DrawScale3D = snapshot.DrawScale3D;
    }

    protected ActorProxy(IActorEditorContext context, ExportEntry actorExport)
    {
        Editor = context;
        Export = actorExport;
        Properties = actorExport.GetCondensedProperties();
        PropertyCollection props = Properties;

        Tag = props.GetProp<NameProperty>("Tag")?.Value ?? NameReference.None;

        DisplayText = Export.ObjectName.Instanced;
        if (!Tag.Name.CaseInsensitiveEquals(Export.ClassName))
        {
            DisplayText += $" ({Tag})";
        }

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
        _cleanSnapshot = SnapshotTransform();
    }

    //only for use by the faux actors that are children of the CollectionActors
    protected ActorProxy(ExportEntry actorExport)
    {
        Export = actorExport;
        DisplayText = Export.ObjectName.Instanced;
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

    private static readonly FrozenSet<string> SupportedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "StaticMeshActor",
        "SkeletalMeshActor",
        "DynamicSMActor",
        "Brush",
        "SFXStuntActor",
        "BioArtPlaceable",
        "BioPawn",
        "Pawn",
        "PrefabInstance",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool CanCreate(ExportEntry actorExport)
    {
        return actorExport.IsA(SupportedClasses);
    }

    //KEEP IN SYNC WITH CanCreate!
    public static ActorProxy Create(IActorEditorContext context, ExportEntry actorExport)
    {
        string className = actorExport.ClassName;
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

    protected void AddComponentArray<T>(MeshRenderContext context, ref List<T> components, [CallerArgumentExpression(nameof(components))] string propName = null) where T : PrimitiveComponentProxy
    {
        if (Properties.GetProp<ArrayProperty<ObjectProperty>>(propName) is { } componentArray)
        {
            foreach (IEntry entry in componentArray.ResolveToEntries(Export.FileRef))
            {
                if (entry is ExportEntry cmpExport && PrimitiveComponentProxy.Create(context, cmpExport, this) is T cmpProxy)
                {
                    components.Add(cmpProxy);
                    Components.Add(cmpProxy);
                }
            }
        }
    }

    protected void AddComponent<T>(MeshRenderContext context, ref T component, [CallerArgumentExpression(nameof(component))] string propName = null) where T : PrimitiveComponentProxy
    {
        if (Properties.GetProp<ObjectProperty>(propName)?.ResolveToEntry(Export.FileRef) is ExportEntry componentExport)
        {
            if (PrimitiveComponentProxy.Create(context, componentExport, this) is T cmpProxy)
            {
                component = cmpProxy;
                Components.Add(cmpProxy);
            }
        }
    }

    public virtual void UpdateScene(LevelEditorRenderContext context, float deltaTime)
    {
        foreach (var component in Components)
        {
            component.UpdateScene(context, deltaTime);
        }
    }

    public virtual void Render(LevelEditorRenderContext context, RenderPass pass)
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

    public virtual void SetAnimation(AnimSequence animSequence, float pos)
    {
        if (App.IsDebug && Debugger.IsAttached)
        {
            //If reached, need to add animation support for whatever kind of actor this is
            Debugger.Break();
        }
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
                Components.Clear();
            }
            isDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

public class StaticMeshActorProxy : ActorProxy
{
    public StaticMeshComponentProxy StaticMeshComponent;
    public StaticMeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent.IsVolumetric;
    }
}

public class SkeletalMeshActorProxy : ActorProxy
{
    public SkeletalMeshComponentProxy SkeletalMeshComponent;
    public SkeletalMeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref SkeletalMeshComponent);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        SkeletalMeshComponent?.SetAnimation(animSequence, pos);
    }
}

//interpactor, placeables
public class DynamicSMActorProxy : ActorProxy
{
    public StaticMeshComponentProxy StaticMeshComponent;

    public DynamicSMActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref StaticMeshComponent);
    }
}

//volumes
public class BrushProxy : ActorProxy
{
    public BrushComponentProxy BrushComponent;
    public override bool IsVolume => true;

    public BrushProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref BrushComponent);
    }
    public override int HitPriority => IHitProxy.WireFramePriority;
}
public class SFXStuntActorProxy : ActorProxy
{
    public SkeletalMeshComponentProxy BodyMesh;
    public SkeletalMeshComponentProxy HeadMesh;
    public SkeletalMeshComponentProxy HairMesh;
    public SkeletalMeshComponentProxy HeadGearMesh;
    public SFXStuntActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref BodyMesh);
        AddComponent(context.RenderContext, ref HeadMesh);
        AddComponent(context.RenderContext, ref HairMesh);
        AddComponent(context.RenderContext, ref HeadGearMesh);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        BodyMesh?.SetAnimation(animSequence, pos);
        HeadMesh?.SetAnimation(animSequence, pos);
        HairMesh?.SetAnimation(animSequence, pos);
        HeadGearMesh?.SetAnimation(animSequence, pos);
    }
}
public class BioArtPlaceableProxy : ActorProxy
{
    public MeshComponentProxy PlaceableMesh;
    public MeshComponentProxy DestroyedMesh;
    public BioArtPlaceableProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref PlaceableMesh);
        AddComponent(context.RenderContext, ref DestroyedMesh);
        if (PlaceableMesh is not null)
        {
            DestroyedMesh?.IsVisible = false;
        }
    }
}
public class PawnProxy : ActorProxy
{
    public SkeletalMeshComponentProxy Mesh;
    public PawnProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref Mesh);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        Mesh?.SetAnimation(animSequence, pos);
    }
}
public class BioPawnProxy : PawnProxy
{
    public SkeletalMeshComponentProxy HeadMesh;
    public SkeletalMeshComponentProxy m_oHairMesh;
    public SkeletalMeshComponentProxy m_oHeadGearMesh;
    public SkeletalMeshComponentProxy m_oVisorMesh;
    public SkeletalMeshComponentProxy m_oFacePlateMesh;
    public List<SkeletalMeshComponentProxy> m_aoAccessories = [];

    public BioPawnProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref HeadMesh);
        AddComponent(context.RenderContext, ref m_oHairMesh);
        AddComponent(context.RenderContext, ref m_oHeadGearMesh);
        AddComponent(context.RenderContext, ref m_oVisorMesh);
        AddComponent(context.RenderContext, ref m_oFacePlateMesh);
        AddComponentArray(context.RenderContext, ref m_aoAccessories);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        base.SetAnimation(animSequence, pos);
        HeadMesh?.SetAnimation(animSequence, pos);
        m_oHairMesh?.SetAnimation(animSequence, pos);
        m_oHeadGearMesh?.SetAnimation(animSequence, pos);
        m_oVisorMesh?.SetAnimation(animSequence, pos);
        m_oFacePlateMesh?.SetAnimation(animSequence, pos);
        foreach (var accessory in m_aoAccessories)
        {
            accessory?.SetAnimation(animSequence, pos);
        }
    }
}

public abstract class CollectionActorComponentProxy : ActorProxy
{
    public ExportEntry CollectionActorExport { get; }

    protected CollectionActorComponentProxy(IActorEditorContext context, StaticCollectionActor collectionActor, ExportEntry componentActor, int index) : base(componentActor)
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
        _cleanSnapshot = SnapshotTransform();
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
    public StaticMeshComponentActorProxy(IActorEditorContext context, ExportEntry smcExport, StaticMeshCollectionActor smca, int smcaIndex) : base(context, smca, smcExport, smcaIndex)
    {
        var staticMeshComponentProxy = PrimitiveComponentProxy.Create(context.RenderContext, smcExport, this);
        Components.Add(staticMeshComponentProxy);
        IsVolumetricMesh = (staticMeshComponentProxy as StaticMeshComponentProxy)?.IsVolumetric ?? false;
    }
}

public class PrefabInstanceProxy : ActorProxy
{
    private readonly List<ActorProxy> Actors = [];
    private readonly List<Matrix4x4> RelativeMatrices = [];

    public PrefabInstanceProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
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

    public override void UpdateScene(LevelEditorRenderContext context, float deltaTime)
    {
        foreach (var actor in Actors)
        {
            actor.UpdateScene(context, deltaTime);
        }
    }

    public override void Render(LevelEditorRenderContext context, RenderPass pass)
    {
        foreach (var actor in Actors)
        {
            if (actor.IsVolume && !context.ShowVolumes) continue;
            if (actor.IsVolumetricMesh && !context.ShowVolumetrics) continue;
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