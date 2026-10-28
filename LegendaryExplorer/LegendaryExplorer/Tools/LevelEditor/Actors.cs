using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace LegendaryExplorer.Tools.LevelEditor;

public class ActorProxy : NotifyPropertyChangedBase, IDisposable, IHitProxy
{
    public IActorEditorContext Editor;
    private OpenLevelFile owningFile;
    public OpenLevelFile OwningFile
    {
        get => owningFile;
        //an actor that's already dirty (a draft) makes its new file dirty
        set
        {
            owningFile = value;
            if (isDirty && value is not null) value.IsDirty = true;
        }
    }

    public Matrix4x4 LocalToWorld;

    public List<PrimitiveComponentProxy> Components = [];

    #region UProp mirrors. DO NOT CHANGE NAMES!
    public ActorProxy Base;
    public SkeletalMeshComponentProxy BaseSkelComponent;
    public NameReference BaseBoneName;
    public List<ActorProxy> Attached = [];
    public bool bHardAttach;
    public NameReference Tag;
    #endregion

    protected PropertyCollection Properties;

    /// <summary>The actor this proxy draws. A proxy rebuilt for the same actor (or export) has the same id</summary>
    public ActorId Id { get; }

    /// <summary>The committed export. Null for a draft, which has none until a commit writes it</summary>
    public ExportEntry Export { get; }
    public bool IsDraft => Export is null;
    private readonly IMEPackage pcc;
    protected IMEPackage Pcc => pcc;
    /// <summary>The package the actor belongs to, whether or not it has an export there yet</summary>
    public IMEPackage Package => pcc;
    private readonly IEntry actorClass;
    /// <summary>The actor's class name, as <see cref="ExportEntry.ClassName"/> is for its export</summary>
    public string ClassName => Export?.ClassName ?? actorClass?.ObjectName.Name ?? "Class";
    /// <summary>The actor's object name: its export's, or the one proposed for a draft. Use this rather than <see cref="Export"/> for labels</summary>
    public string ActorName => Export?.ObjectName.Instanced ?? Editor?.RenderContext.Registry.NameOf(Id) ?? DisplayText;
    /// <summary>
    /// The outliner's order: committed actors by UIndex, then drafts (which have none). Only for sorting, never a UIndex
    /// </summary>
    public int SortIndex => Export?.UIndex ?? int.MaxValue;
    /// <summary>For the outliner and pending changes: the export's UIndex, or "new" for a draft</summary>
    public string IndexLabel => Export?.UIndex.ToString() ?? "new";
    /// <summary>For the details panel</summary>
    public string ExportLabel => Export is null ? "New: no export until committed" : $"Export {Export.UIndex}";

    public string OwningFileName => System.IO.Path.GetFileName(Pcc.FilePath);

    public string DisplayText { get; }

    // Use the resolved proxy type so subclasses and collection components are
    // categorized consistently, regardless of their export names or tags.
    public virtual string Category => this switch
    {
        ILightProxy => "Lights",
        { IsVolume: true } => "Volumes",
        { IsVolumetricMesh: true } => "Volumetric meshes",
        PawnProxy => "Pawns",
        SkeletalMeshActorProxy or SFXStuntActorProxy => "Skeletal meshes",
        DynamicSMActorProxy or BioArtPlaceableProxy => "Environment",
        StaticMeshActorProxy or StaticMeshComponentActorProxy => "Static meshes",
        PrefabInstanceProxy => "Prefabs",
        SFXDroppedPickupProxy or SFXDroppedAmmoProxy or SFXDroppedGrenadeProxy => "Pickups",
        _ => "Other"
    };

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

    public virtual void MarkClean()
    {
        propertiesBeforeCommit = null;
        _cleanSnapshot = SnapshotTransform();
        IsDirty = false;
    }

    //a commit writes its values into Properties too (replacing top-level properties, never editing one in place), so a shallow copy restores them
    private List<Property> propertiesBeforeCommit;

    /// <summary>
    /// Called before <see cref="CommitChanges"/> when the commit may be abandoned (its package writes rolled back): <see cref="AbandonCommit"/>
    /// then puts back what the commit changed in this view, and <see cref="MarkClean"/> accepts it
    /// </summary>
    internal virtual void BeginCommit() => propertiesBeforeCommit = [.. Properties];

    /// <summary>
    /// Takes on the Components array a commit wrote to <paramref name="owner"/> (registering a new component), if it's this actor's export. Replaced
    /// rather than edited, so abandoning the commit restores the old one
    /// </summary>
    internal void OnComponentsWritten(ExportEntry owner)
    {
        if (Export is null || Export != owner || Properties is null || owner.GetProperty<ArrayProperty<ObjectProperty>>("Components") is not { } components) return;
        Properties.AddOrReplaceProp(components);
    }

    /// <summary>The meshes whose pending lighting edits a commit writes to their own components, alongside this actor's</summary>
    internal virtual IEnumerable<MeshComponentProxy> LightingMeshes => Components.OfType<MeshComponentProxy>();

    /// <summary>Undoes what a commit begun with <see cref="BeginCommit"/> changed in this view, leaving its pending edits as they were</summary>
    internal virtual void AbandonCommit()
    {
        if (propertiesBeforeCommit is null) return;
        Properties.Clear();
        Properties.AddRange(propertiesBeforeCommit);
        propertiesBeforeCommit = null;
    }

    /// <summary>
    /// The transform differs from the committed one
    /// </summary>
    public bool IsTransformDirty => !SnapshotTransform().Equals(_cleanSnapshot);

    protected virtual bool HasUncommittedChanges() => IsTransformDirty
        || Components.OfType<MeshComponentProxy>().Any(mesh => mesh.ExistingLighting?.HasChanges == true);

    internal void RefreshLightingDirty() => UpdateDirty();

    internal void OnActorLightingEdited(MeshComponentProxy mesh, ActorLightingSettings before, ActorLightingSettings after)
    {
        UpdateDirty();
        Editor?.OnActorLightingEdited(mesh, before, after);
    }

    protected void UpdateDirty() => IsDirty = HasUncommittedChanges();

    private bool _isBeingAnimated;
    public bool IsBeingAnimated
    {
        get => _isBeingAnimated;
        private set => SetProperty(ref _isBeingAnimated, value);
    }

    private TransformSnapshot? _prematineeSnapshot;

    public void BeginMatineeControl()
    {
        if (!_isBeingAnimated)
            _prematineeSnapshot = SnapshotTransform();
        IsBeingAnimated = true;
    }

    public void EndMatineeControl()
    {
        IsBeingAnimated = false;
        if (_prematineeSnapshot is TransformSnapshot snap)
            RestoreTransform(snap);
        _prematineeSnapshot = null;
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
                if (!IsBeingAnimated)
                    UpdateDirty();
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
                if (!IsBeingAnimated)
                    UpdateDirty();
            }
        }
    }
    public float XPos { get => location.X; set => Location = location with { X = value }; }
    public float YPos { get => location.Y; set => Location = location with { Y = value }; }
    public float ZPos { get => location.Z; set => Location = location with { Z = value }; }

    //the class default object's, used when the actor doesn't set one
    private readonly Vector3 defaultDrawScale3D = Vector3.One;

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
                if (!IsBeingAnimated)
                    UpdateDirty();
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
                if (!IsBeingAnimated)
                    UpdateDirty();
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
                if (!IsBeingAnimated)
                    UpdateDirty();
            }
        }
    }

    public virtual bool CanEditTransform => true;
    public virtual bool HasSpatialRepresentation => true;
    public virtual ExportEntry CollectionOwner => null;
    public bool IsReadOnly => !CanEditTransform || ((OwningFile is null || OwningFile.IsReadOnly)
                           && !(Editor?.IsApplyingUndoRedo ?? false));

    public virtual ActorCoverage Coverage
    {
        get
        {
            if (this is ILightProxy) return new(ActorRepresentation.PartialScene,
                "Light contribution and editor icon; preview limitations are listed under lighting details.");
            if (this is BrushProxy && !Components.OfType<BrushComponentProxy>().Any())
                return new(ActorRepresentation.Unsupported, "No brush component was loaded for this actor.");
            if (this is BrushProxy) return new(ActorRepresentation.EditorMarker,
                "Brush/volume wireframe only; gameplay and postprocess effects are not simulated.");
            int meshes = Components.OfType<MeshComponentProxy>().Count(c => c.HasPreviewMesh);
            if (meshes == 0) return new(ActorRepresentation.Unsupported, "No renderable mesh was loaded for this actor.");
            bool partial = Components.Any(c => c is not MeshComponentProxy { HasPreviewMesh: true });
            return new(partial ? ActorRepresentation.PartialScene : ActorRepresentation.SceneRendered,
                partial ? "Some components have no renderable mesh." : "Loaded mesh geometry; visibility and material/lighting preview settings still apply.");
        }
    }
    public string CoverageLabel => Coverage.Label;
    public string CoverageReason => Coverage.Reason;
    public string CoverageToolTip => $"{DisplayText}\n{CoverageLabel}: {CoverageReason}";

    internal void NotifyCoverageChanged()
    {
        OnPropertyChanged(nameof(Coverage));
        OnPropertyChanged(nameof(CoverageLabel));
        OnPropertyChanged(nameof(CoverageReason));
        OnPropertyChanged(nameof(CoverageToolTip));
    }

    public virtual bool IsVolume => false;
    private bool isVolumetricMesh;
    public bool IsVolumetricMesh
    {
        get => isVolumetricMesh;
        //a preview refresh can change it, which moves the actor to another outliner category
        protected set
        {
            if (SetProperty(ref isVolumetricMesh, value)) OnPropertyChanged(nameof(Category));
        }
    }
    /// <summary>
    /// The actor's bHidden is set, so none of its components are rendered in game. (Individual components can also be hidden, see <see cref="PrimitiveComponentProxy.HiddenGame"/>)
    /// </summary>
    public bool IsHidden { get; protected set; }

    public TransformSnapshot SnapshotTransform() => new(location, rotation, drawScale, drawScale3D);

    // Prefab placement is derived state, including in read-only previews. It must bypass editor mutation guards.
    internal void SetPrefabTransform(Matrix4x4 transform)
    {
        (location, _, rotation) = transform.UnrealDecompose();
        UpdateLocalToWorld();
    }

    public void RestoreTransform(TransformSnapshot snapshot)
    {
        Location = snapshot.Location;
        Rotation = snapshot.Rotation;
        DrawScale = snapshot.DrawScale;
        DrawScale3D = snapshot.DrawScale3D;
    }

    protected ActorProxy(IActorEditorContext context, ExportEntry actorExport)
        : this(context, actorExport, actorExport.FileRef, actorExport.Class, context.RenderContext.Registry.IdFor(actorExport),
            actorExport.ObjectName.Instanced, actorExport.GetCondensedProperties()) { }

    /// <summary>
    /// A draft actor: drawn from <paramref name="properties"/> (its own, without class defaults), with no export. Nothing is added to its package
    /// </summary>
    protected ActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties)
        : this(context, null, draft.Package, draft.Class, draft.Id, draft.Name, properties) { }

    private ActorProxy(IActorEditorContext context, ExportEntry actorExport, IMEPackage package, IEntry actorClass, ActorId id, string name, PropertyCollection props)
    {
        Editor = context;
        Export = actorExport;
        pcc = package;
        this.actorClass = actorClass;
        Id = id;
        Properties = props;
        var cache = context.RenderContext.PackageCache;

        props.ReadProp(ref Tag);
        //classes such as Trigger are hidden by default
        IsHidden = (props.GetProp<BoolProperty>("bHidden")
                    ?? EffectiveLightingProperties.ClassDefaultProperty<BoolProperty>(actorClass, actorExport, "bHidden", cache))?.Value ?? false;

        DisplayText = name;
        if (!Tag.Name.CaseInsensitiveEquals(actorClass?.ObjectName.Name ?? "Class"))
        {
            DisplayText += $" ({Tag})";
        }

        var rotationProp = props.GetProp<StructProperty>("Rotation");
        var locationsProp = props.GetProp<StructProperty>("location");
        var drawScale3DProp = props.GetProp<StructProperty>("DrawScale3D");
        var prePivotProp = props.GetProp<StructProperty>("PrePivot");

        drawScale = props.GetProp<FloatProperty>("DrawScale")?.Value ?? 1;
        location = locationsProp != null ? CommonStructs.GetVector3(locationsProp) : Vector3.Zero;
        //some classes scale by default: Default__Terrain's DrawScale3D is 256
        if (drawScale3DProp is null && EffectiveLightingProperties.ClassDefaultProperty<StructProperty>(actorClass, actorExport, "DrawScale3D", cache) is { } defaultScale)
        {
            defaultDrawScale3D = CommonStructs.GetVector3(defaultScale);
        }
        drawScale3D = drawScale3DProp != null ? CommonStructs.GetVector3(drawScale3DProp) : defaultDrawScale3D;
        prePivot = prePivotProp != null ? CommonStructs.GetVector3(prePivotProp) : Vector3.Zero;
        rotation = rotationProp != null ? CommonStructs.GetRotator(rotationProp) : new Rotator(0, 0, 0);
        UpdateLocalToWorld();
        _cleanSnapshot = SnapshotTransform();
    }

    // Minimal identity for collection components, helper records and failed previews.
    protected ActorProxy(ExportEntry actorExport)
    {
        Export = actorExport;
        pcc = actorExport.FileRef;
        Id = ActorId.New();
        DisplayText = Export.ObjectName.Instanced;
        drawScale = 1;
        location =  Vector3.Zero;
        drawScale3D =  Vector3.One;
        prePivot =  Vector3.Zero;
        rotation = new Rotator(0, 0, 0);
        Properties = [];
        _cleanSnapshot = SnapshotTransform();
    }

    public void ResolveAttachment(IEnumerable<ActorProxy> actorProxies)
    {
        if (Properties.TryResolveObjectProp(Pcc, "Base", out ExportEntry baseExport)
            && actorProxies.FirstOrDefault(ap => ap.Export == baseExport) is ActorProxy baseActor)
        {
            Base = baseActor;
            baseActor.Attached.Add(this);
            if (Properties.TryResolveObjectProp(Pcc, nameof(BaseSkelComponent), out ExportEntry baseSkelComponentExport))
            {
                BaseSkelComponent = baseActor.Components.OfType<SkeletalMeshComponentProxy>().FirstOrDefault(cmp => cmp.Export == baseSkelComponentExport);
            }
            Properties.ReadProp(ref BaseBoneName);
            Properties.ReadProp(ref bHardAttach);
        }
    }

    public void Detach()
    {
        Base?.Attached.Remove(this);
        Base = null;
        foreach (var attached in Attached)
        {
            attached.Base = null;
        }
        Attached.Clear();
    }

    protected virtual void UpdateLocalToWorld()
    {
        LocalToWorld = ActorUtils.ComposeLocalToWorld(location, rotation, drawScale * drawScale3D, prePivot);
        foreach (var cmp in Components)
        {
            cmp.UpdateLocalToWorld();
        }
    }

    public static bool CanCreate(ExportEntry actorExport, bool includeHelpers = false)
    {
        var kind = ActorClassification.Get(actorExport).Kind;
        switch (kind)
        {
            case ActorKind.Collection:
                // Collection exports are expanded by the level loader, not previewed individually.
                return false;
            case ActorKind.Helper:
            case ActorKind.Spline:
            case ActorKind.Decal:
                // Decal surfaces need their receiving actors loaded; an isolated export only has its helper.
                return includeHelpers && actorExport.IsA("Actor");
            case ActorKind.Light:
                return includeHelpers || LightActorProxy.HasSupportedLight(actorExport);
            default:
                return true;
        }
    }

    public static ActorProxy Create(IActorEditorContext context, ExportEntry actorExport)
    {
        var info = ActorClassification.Get(actorExport);
        try
        {
            return info.Kind switch
            {
                ActorKind.StaticMesh => new StaticMeshActorProxy(context, actorExport),
                ActorKind.SFXSkeletalMesh => new SFXSkeletalMeshActorProxy(context, actorExport),
                ActorKind.SkeletalMesh => new SkeletalMeshActorProxy(context, actorExport),
                ActorKind.KAsset => new KAssetProxy(context, actorExport),
                ActorKind.Decal => new DecalActorProxy(context, actorExport),
                ActorKind.Terrain => new TerrainProxy(context, actorExport),
                ActorKind.SplineLoft or ActorKind.Spline => new SplineActorProxy(context, actorExport),
                ActorKind.Foliage => new FoliageFactoryProxy(context, actorExport),
                ActorKind.DynamicMesh => new DynamicSMActorProxy(context, actorExport),
                ActorKind.Brush => new BrushProxy(context, actorExport),
                ActorKind.Stunt => new SFXStuntActorProxy(context, actorExport),
                ActorKind.Placeable => new BioArtPlaceableProxy(context, actorExport),
                ActorKind.BioPawn => new BioPawnProxy(context, actorExport),
                ActorKind.Pawn => new PawnProxy(context, actorExport),
                ActorKind.Prefab => new PrefabInstanceProxy(context, actorExport),
                ActorKind.Grenade => new SFXDroppedGrenadeProxy(context, actorExport),
                ActorKind.Ammo => new SFXDroppedAmmoProxy(context, actorExport),
                ActorKind.Pickup => new SFXDroppedPickupProxy(context, actorExport),
                ActorKind.Light when LightActorProxy.HasSupportedLight(actorExport) => new LightActorProxy(context, actorExport),
                ActorKind.Light => new EditorActorProxy(context, actorExport,
                    new(ActorKind.Helper, "Effects helpers", "Missing or unsupported light component.")),
                ActorKind.Collection => new EditorActorProxy(context, actorExport,
                    new(ActorKind.Helper, "Data", "Collection container; its components have separate outliner entries.", true)),
                _ => new EditorActorProxy(context, actorExport, info)
            };
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return EditorActorProxy.Failed(context, actorExport, e.Message);
        }
    }

    /// <summary>Retain the container and each local component, including unsupported or malformed components.</summary>
    public static List<ActorProxy> CreateCollection(IActorEditorContext context, ExportEntry export)
    {
        List<ActorProxy> actors = [];
        try
        {
            StaticCollectionActor collection = export.ClassName == "StaticMeshCollectionActor"
                ? export.GetBinaryData<StaticMeshCollectionActor>() : export.GetBinaryData<StaticLightCollectionActor>();
            int unresolved = 0;
            for (int i = 0; i < (collection.Components?.Count ?? 0); i++)
            {
                if (!export.FileRef.TryGetUExport(collection.Components[i], out var component)) { unresolved++; continue; }
                ActorProxy proxy;
                try
                {
                    if (i >= collection.LocalToWorldTransforms.Count) throw new InvalidOperationException("No serialized collection transform for this component.");
                    proxy = collection switch
                    {
                        StaticMeshCollectionActor mesh => new StaticMeshComponentActorProxy(context, component, mesh, i),
                        StaticLightCollectionActor light when SceneLight.GetLightType(component) is { } type
                            => new StaticLightCollectionComponentProxy(context, component, light, i, type),
                        _ => throw new InvalidOperationException("Unsupported collection light component.")
                    };
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    proxy = EditorActorProxy.Failed(context, component, e.Message, export);
                }
                actors.Add(proxy);
            }
            actors.Insert(0, new EditorActorProxy(context, export, new(ActorKind.Helper, "Data",
                $"Collection container; {actors.Count} local component entries. {unresolved} null, imported or unresolved component references.", true)));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            actors.Insert(0, EditorActorProxy.Failed(context, export, e.Message));
        }
        return actors;
    }

    protected void AddComponentArray<T>(MeshRenderContext context, ref List<T> components, [CallerArgumentExpression(nameof(components))] string propName = null) where T : PrimitiveComponentProxy
    {
        if (Properties.GetProp<ArrayProperty<ObjectProperty>>(propName) is { } componentArray)
        {
            for (int i = 0; i < componentArray.Count; i++)
            {
                IEntry entry = componentArray[i].ResolveToEntry(Pcc);
                if (entry is ExportEntry cmpExport && PrimitiveComponentProxy.Create(context, cmpExport, this) is T cmpProxy)
                {
                    cmpProxy.ActorPropertyName = $"{propName}[{i}]";
                    components.Add(cmpProxy);
                    Components.Add(cmpProxy);
                }
            }
        }
    }

    protected void AddComponent<T>(MeshRenderContext context, ref T component, [CallerArgumentExpression(nameof(component))] string propName = null) where T : PrimitiveComponentProxy
    {
        if (Properties.GetProp<ObjectProperty>(propName)?.ResolveToEntry(Pcc) is ExportEntry componentExport)
        {
            if (PrimitiveComponentProxy.Create(context, componentExport, this) is T cmpProxy)
            {
                cmpProxy.ActorPropertyName = propName;
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
            if (!context.IsComponentVisible(component)) continue;
            // Meshes eligible for the selected light are tinted, and outlined after lighting so the outline survives additive light passes.
            bool highlighted = context.HighlightedComponents.Contains(component);
            if (highlighted)
            {
                context.RenderFlags |= LevelEditorRenderContext.ShaderFlags.Highlighted;
                if (pass is RenderPass.Base) context.DrawHighlightBounds(component.GetBounds());
            }
            component.Render(context, pass);
            context.RenderFlags &= ~LevelEditorRenderContext.ShaderFlags.Highlighted;
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
        if (IsDraft) throw new InvalidOperationException($"{DisplayText} is a draft: it has no export to commit to.");
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
        if (props.ContainsNamedProp("DrawScale3D") || DrawScale3D != defaultDrawScale3D)
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

    /// <summary>
    /// Applies mesh dependency edits without replacing the actor that selection and undo actions refer to.
    /// Structural actor/collection edits and non-mesh components still use the existing rebuild path.
    /// </summary>
    public virtual bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        if (Export is not null && Export.FileRef == package && updatedExports.Contains(Export.UIndex) && this is not StaticMeshComponentActorProxy
            || this is CollectionActorComponentProxy collection && collection.CollectionActorExport.FileRef == package
                && updatedExports.Contains(collection.CollectionActorExport.UIndex)
            || Components.Any(component => component is not MeshComponentProxy && component.Export.FileRef == package
                && component.TestUIndexes(updatedExports)))
            return false;

        bool refreshed = false;
        foreach (var component in Components.OfType<MeshComponentProxy>())
            refreshed |= component.RefreshPreview(package, updatedExports, force);
        // ParentAnimComponent transforms must propagate even to children whose own settings did not change.
        if (refreshed)
            foreach (var component in Components) component.UpdateLocalToWorld();
        if (refreshed) NotifyCoverageChanged();
        if (refreshed && this is StaticMeshActorProxy or StaticMeshComponentActorProxy or DynamicSMActorProxy)
            IsVolumetricMesh = Components.OfType<StaticMeshComponentProxy>().Any(component => component.IsVolumetric);
        return refreshed;
    }

    public virtual bool TestUIndexes(HashSet<int> uIndexes)
    {
        if (Export is not null && uIndexes.Contains(Export.UIndex))
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

    // The component ApplyMorphFace morphed, which is morphed again when a preview refresh reloads it
    private SkeletalMeshComponentProxy MorphedComponent;

    protected void ApplyMorphFace(SkeletalMeshComponentProxy skeletalMeshComponent)
    {
        if (skeletalMeshComponent is null) return;
        MorphedComponent = skeletalMeshComponent;
        if (Properties.GetProp<ObjectProperty>("MorphHead")?.ResolveToExport(Pcc, Editor?.PackageCache) is ExportEntry morphHead)
        {
            (BonePosition[] bonePositions, Vector3[][] vertexOffsets) = LegendaryExplorerCore.Unreal.Classes.BioMorphFace.GetBoneAndVertexPositions(morphHead);
            skeletalMeshComponent.ApplyMorph(bonePositions, vertexOffsets);
        }
    }

    /// <summary>
    /// Called when a preview refresh reloads one of the actor's skeletal mesh components, to reapply what the actor sets up on it
    /// (rather than the component's own properties). The morph is reread, so it matches the actor's current MorphHead
    /// </summary>
    internal virtual void OnComponentReloaded(SkeletalMeshComponentProxy component)
    {
        if (component == MorphedComponent) ApplyMorphFace(component);
    }

    #region IDisposable
    protected bool isDisposed;

    /// <summary>
    /// Replaced or closed. Undo actions that refer to it are orphaned
    /// </summary>
    public bool IsDisposed => isDisposed;

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
        IsVolumetricMesh = StaticMeshComponent?.IsVolumetric == true;
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

public class SFXSkeletalMeshActorProxy : SkeletalMeshActorProxy
{
    public SkeletalMeshComponentProxy HeadMesh;
    public SkeletalMeshComponentProxy HairMesh;
    public SkeletalMeshComponentProxy HeadGearMesh; //ME3/LE3
    public SFXSkeletalMeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref HeadMesh);
        ApplyMorphFace(HeadMesh);
        AddComponent(context.RenderContext, ref HairMesh);
        AddComponent(context.RenderContext, ref HeadGearMesh);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        base.SetAnimation(animSequence, pos);
        HeadMesh?.SetAnimation(animSequence, pos);
        HairMesh?.SetAnimation(animSequence, pos);
        HeadGearMesh?.SetAnimation(animSequence, pos);
    }
}

//interpactor, placeables
public class DynamicSMActorProxy : ActorProxy
{
    public StaticMeshComponentProxy StaticMeshComponent;

    public DynamicSMActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent?.IsVolumetric == true;
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
        ApplyDefaultBodyTranslation();
        AddComponent(context.RenderContext, ref HeadMesh);
        ApplyMorphFace(HeadMesh);
        AddComponent(context.RenderContext, ref HairMesh);
        AddComponent(context.RenderContext, ref HeadGearMesh);
    }

    private void ApplyDefaultBodyTranslation()
    {
        if (BodyMesh.Translation == Vector3.Zero)
        {
            //from the defaultproperties, which condenseproperties currently does not fetch
            BodyMesh.Translation = new Vector3(0, 0, -88);
        }
    }

    internal override void OnComponentReloaded(SkeletalMeshComponentProxy component)
    {
        if (component == BodyMesh) ApplyDefaultBodyTranslation();
        base.OnComponentReloaded(component);
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
        AddComponent(context.RenderContext, ref HeadMesh, actorExport.Game.IsGame1() ? "m_oHeadMesh" : nameof(HeadMesh));
        ApplyMorphFace(HeadMesh);
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
    public override ExportEntry CollectionOwner => CollectionActorExport;

    /// <summary>
    /// The transform stored in the collection's binary, as of the last load or commit
    /// </summary>
    public Matrix4x4 SavedLocalToWorld { get; private set; }

    protected CollectionActorComponentProxy(IActorEditorContext context, StaticCollectionActor collectionActor, ExportEntry componentActor, int index) : base(componentActor)
    {
        Editor = context;
        CollectionActorExport = collectionActor.Export;

        LocalToWorld = SavedLocalToWorld = collectionActor.LocalToWorldTransforms[index];
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
        collectionActor.LocalToWorldTransforms[idx] = ComposedLocalToWorld;
    }

    private Matrix4x4 ComposedLocalToWorld => ActorUtils.ComposeLocalToWorld(Location, Rotation, DrawScale * DrawScale3D, PrePivot);

    public override void MarkClean()
    {
        //the transform the commit wrote, if it was moved
        if (IsTransformDirty) SavedLocalToWorld = ComposedLocalToWorld;
        base.MarkClean();
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
        staticMeshComponentProxy.ActorPropertyName = $"{smca.ComponentPropName}[{smcaIndex}]";
        Components.Add(staticMeshComponentProxy);
        IsVolumetricMesh = (staticMeshComponentProxy as StaticMeshComponentProxy)?.IsVolumetric ?? false;
    }
}

public class PrefabInstanceProxy : ActorProxy
{
    public override ActorCoverage Coverage => new(
        Actors.Count > 0 && Actors.All(a => a.Coverage.Representation == ActorRepresentation.SceneRendered)
            ? ActorRepresentation.SceneRendered : ActorRepresentation.PartialScene,
        $"Prefab has {Actors.Count} children; {Actors.Count(a => a.Coverage.Representation != ActorRepresentation.SceneRendered)} have partial, helper-only or unsupported coverage.");

    private readonly List<ActorProxy> Actors = [];

    /// <summary>
    /// The prefab's actors, which aren't in the editor's actor list
    /// </summary>
    internal IReadOnlyList<ActorProxy> Children => Actors;

    internal override IEnumerable<MeshComponentProxy> LightingMeshes => Actors.SelectMany(a => a.LightingMeshes);
    private readonly List<Matrix4x4> RelativeMatrices = [];
    // Not Editor, which is cleared for a prefab nested in another prefab, but is still needed to reload its children
    private readonly IActorEditorContext PreviewContext;
    // The template and its archetype list's entries, including imports, since relinking one changes the prefab without changing any export
    private readonly HashSet<IEntry> TemplateLinks = [];

    public PrefabInstanceProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        PreviewContext = context;
        LoadChildren();
    }

    private void LoadChildren()
    {
        var context = PreviewContext;
        PackageCache packageCache = context.RenderContext.PackageCache;
        TemplateLinks.Clear();
        ObjectProperty templateProp = Properties.GetProp<ObjectProperty>("TemplatePrefab");
        if (templateProp?.ResolveToEntry(Pcc) is { } templateEntry) TemplateLinks.Add(templateEntry);
        if (templateProp?.ResolveToExport(Pcc, packageCache) is not { } prefab) return;
        TemplateLinks.Add(prefab);
        if (prefab.GetProperty<ArrayProperty<ObjectProperty>>("PrefabArchetypes") is { } prefabActors)
        {
            foreach (var objProp in prefabActors)
            {
                if (objProp.ResolveToEntry(prefab.FileRef) is { } archetypeEntry) TemplateLinks.Add(archetypeEntry);
                if (objProp.TryResolveExport(prefab.FileRef, packageCache, out ExportEntry prefabActor)
                    && Create(context, prefabActor) is ActorProxy prefabActorProxy)
                {
                    prefabActorProxy.Editor = null; // prevent IsDirty being marked

                    var actorRelative = ActorUtils.ComposeLocalToWorld(prefabActorProxy.Location, prefabActorProxy.Rotation, Vector3.One);
                    prefabActorProxy.SetPrefabTransform(actorRelative * LocalToWorld);
                    Actors.Add(prefabActorProxy);
                    RelativeMatrices.Add(actorRelative);
                }
            }
        }
    }

    public override bool TryRefreshPreview(IMEPackage package, HashSet<int> updatedExports, bool force = false)
    {
        if (Export.FileRef == package && updatedExports.Contains(Export.UIndex)) return false;
        if (TemplateLinks.Any(link => link.FileRef == package && updatedExports.Contains(link.UIndex)))
        {
            foreach (var child in Actors) child.Dispose();
            Actors.Clear();
            RelativeMatrices.Clear();
            LoadChildren();
            return true;
        }
        bool refreshed = false;
        for (int i = Actors.Count - 1; i >= 0; i--)
        {
            var child = Actors[i];
            if (child.TryRefreshPreview(package, updatedExports, force))
                refreshed = true;
            else if (child.Export.FileRef == package && child.TestUIndexes(updatedExports))
            {
                child.Dispose();
                if (Create(PreviewContext, child.Export) is { } replacement)
                {
                    replacement.Editor = null;
                    var relative = ActorUtils.ComposeLocalToWorld(replacement.Location, replacement.Rotation, Vector3.One);
                    replacement.SetPrefabTransform(relative * LocalToWorld);
                    Actors[i] = replacement;
                    RelativeMatrices[i] = relative;
                }
                else
                {
                    Actors.RemoveAt(i);
                    RelativeMatrices.RemoveAt(i);
                }
                refreshed = true;
            }
        }
        return refreshed;
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
            if (!context.IsActorVisible(actor)) continue;
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
            actor.SetPrefabTransform(RelativeMatrices[i] * LocalToWorld);
        }
    }

    public override BoxSphereBounds GetBounds()
    {
        var spatial = Actors.Where(a => a is not EditorActorProxy && a.HasSpatialRepresentation).ToList();
        if (spatial.Count is 0)
        {
            return new BoxSphereBounds
            {
                Origin = LocalToWorld.Translation
            };
        }
        var bounds = spatial[0].GetBounds();
        for (int i = 1; i < spatial.Count; i++)
        {
            bounds = bounds.Union(spatial[i].GetBounds());
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
public class SFXDroppedPickupProxy : ActorProxy
{
    public SkeletalMeshComponentProxy PickupMesh;
    public SFXDroppedPickupProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref PickupMesh);
    }
}

public class SFXDroppedAmmoProxy : ActorProxy
{
    public StaticMeshComponentProxy AmmoMesh;
    public SFXDroppedAmmoProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref AmmoMesh);
    }
}

public class SFXDroppedGrenadeProxy : ActorProxy
{
    public StaticMeshComponentProxy GrenadeMesh;
    public SFXDroppedGrenadeProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref GrenadeMesh);
    }
}
