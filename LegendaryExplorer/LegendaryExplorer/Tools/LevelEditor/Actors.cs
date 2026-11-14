using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

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
    public ExportEntry Export { get; private set; }
    public bool IsDraft => Export is null;
    private readonly IMEPackage pcc;
    protected IMEPackage Pcc => pcc;
    /// <summary>The package the actor belongs to, whether or not it has an export there yet</summary>
    public IMEPackage Package => pcc;
    private readonly IEntry actorClass;
    protected IEntry ActorClass => actorClass;

    /// <summary>
    /// Gives a draft the export a commit created for it, as its actor's export too; null takes it back when the commit is abandoned
    /// </summary>
    private protected void BindExport(ExportEntry export)
    {
        Export = export;
        Editor.RenderContext.Registry.Bind(Id, export);
    }

    /// <summary>The label for an actor named <paramref name="name"/>: with its Tag, unless that's the class name (the default), or it has none (a member's copy)</summary>
    private string Label(string name) => Tag == NameReference.None || Tag.Name.CaseInsensitiveEquals(actorClass?.ObjectName.Name ?? "Class") ? name : $"{name} ({Tag})";

    /// <summary>A committed draft is labelled by the name its export was given</summary>
    private protected void OnCommittedAsNew()
    {
        DisplayText = Label(Export.ObjectName.Instanced);
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ActorName));
        OnPropertyChanged(nameof(IndexLabel));
        OnPropertyChanged(nameof(ExportLabel));
        OnPropertyChanged(nameof(SortIndex));
    }
    /// <summary>What an actor's Location property is called: Game 3 serializes it as "location"</summary>
    internal static string LocationPropertyName(MEGame game) => game.IsGame3() ? "location" : "Location";
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
    private PendingActorDeletion pendingDeletion;
    internal PendingActorDeletion PendingDeletion
    {
        get => pendingDeletion;
        set
        {
            pendingDeletion = value;
            OnPendingDeletionChanged();
            NotifyPendingChange();
        }
    }
    private protected virtual void OnPendingDeletionChanged() { }
    public bool IsPendingChange => IsDraft || IsDirty || PendingDeletion is not null;
    public string ChangeKind => PendingDeletion is not null ? "Delete" : IsDraft ? "Create" : EditKind;
    /// <summary>What <see cref="ChangeKind"/> calls an uncommitted change to a committed actor</summary>
    private protected virtual string EditKind => "Edit";
    public string PendingChangeStatus => PendingDeletion?.Status ?? $"{ChangeKind} {ActorName}";
    /// <summary>Why Delete can't remove this actor now, or null</summary>
    public virtual string DeleteRefusal => (IsDraft ? ActorDeletionPlan.RemovalRefusal(this) : ActorDeletionPlan.Refusal(this))
        ?? (upgradeRequested ? "Its upgrade to an actor is pending: undo that first."
        // A projection that only lands on it moves off it, as it does a mesh with an actor of its own. A Filter is the user's, and would be left listing nothing
        : neededBy?.Any(d => d.Decal?.Settings.Filter.Actors.Contains(Export) == true) == true ? "A decal's uncommitted Filter lists it: uncheck it there first." : null);

    /// <summary>
    /// The collection whose member it is (<see cref="CollectionActorComponentProxy"/>), or for a copy of a member, the collection its commit adds it to
    /// (<see cref="CloneCollection"/>). Null for anything else
    /// </summary>
    internal virtual ExportEntry MemberCollection => CloneCollection;
    /// <summary>Whether it's one of a collection's members, or a copy of one, which can be upgraded to an actor</summary>
    public bool IsCollectionMember => MemberCollection is not null;
    /// <summary>Why it can't be upgraded to an actor now, or null</summary>
    public string UpgradeRefusal => CollectionMemberUpgrade.Refusal(this);

    private bool upgradeRequested;
    //(made when a decal first needs it: most actors are never a member a decal lands on)
    private HashSet<DecalActorProxy> neededBy;

    /// <summary>For a collection's member: whether Upgrade to Actor was used on it, and not undone</summary>
    internal bool UpgradeRequested
    {
        get => upgradeRequested;
        set
        {
            upgradeRequested = value;
            OnUpgradeChanged();
        }
    }

    /// <summary>
    /// For a collection's member: whether the commit makes it an actor of its own (<see cref="CollectionMemberUpgrade"/>), because it was asked to, or
    /// because a decal needs one. It looks the same until then, and can be edited as ever. A copy of a member is made one, then upgraded, by the same commit
    /// </summary>
    internal bool UpgradePending => upgradeRequested || neededBy?.Count > 0;

    /// <summary>
    /// For a collection's member: says whether <paramref name="decal"/>'s uncommitted changes need it to be an actor: its Filter lists it, which only an actor
    /// can be, or its projection lands on it (in LE1-LE3's shipping levels, every static mesh a decal lands on has an actor of its own). This follows the
    /// decal, so it's no undo step of its own: the decal moving off, its Filter dropping it or its edit being undone ends it
    /// </summary>
    internal void SetNeededBy(DecalActorProxy decal, bool needed)
    {
        if (needed ? (neededBy ??= []).Add(decal) : neededBy?.Remove(decal) == true) OnUpgradeChanged();
    }

    private void OnUpgradeChanged()
    {
        UpdateDirty();
        NotifyPendingChange();
        NotifyRefusals();
    }
    /// <summary>Why it can't be duplicated now (by Duplicate, or an Alt+drag), or null</summary>
    public virtual string DuplicateRefusal => ActorCloning.Refusal(this);
    /// <summary>Why it can't be copied (for Paste, into its own level or another), or null</summary>
    public string CopyRefusal => ActorCloning.Refusal(this, null);
    /// <summary>Whether it can be copied (<see cref="CopyRefusal"/>), which the actor menu's Copy is enabled by</summary>
    public bool CanCopy => CopyRefusal is null;
    /// <summary>Why it can't be opened in Package Editor, or null</summary>
    public string PackageEditorRefusal => IsDraft ? "It's new: it has no export to open until it's committed." : null;

    /// <summary>
    /// For a copy (<see cref="ActorCloning"/>): the saved actor it's a copy of, which its commit clones. A copy of a copy is another copy of the same actor.
    /// Null for anything else
    /// </summary>
    internal ExportEntry CloneSource { get; init; }

    /// <summary>
    /// For a copy of a collection's member: the collection its commit adds it to, as one of its members (<see cref="ActorCloning.MaterializeMember"/>).
    /// Its source's collection when it was made, whatever has happened to its source since. Null for anything else
    /// </summary>
    internal ExportEntry CloneCollection { get; init; }

    /// <summary>For a placed draft whose recipe has an asset (<see cref="ActorCreation.AssetKindOf"/>): the one it uses, which a copy of it uses too. Null otherwise</summary>
    internal virtual AssetReference DraftAsset => null;

    /// <summary>For a copy of a placed draft: its own values, its transform among them, which the copy is made with (<see cref="ActorCreation.CopyDraft"/>)</summary>
    internal PropertyCollection CopyOwnValues() => Properties.DeepClone();

    /// <summary>Whether its scale differs from the committed one, so that its commit cooks its brush's collision again (if it has cooked data)</summary>
    internal bool IsScaleDirty => DrawScale * DrawScale3D != _cleanSnapshot.DrawScale * _cleanSnapshot.DrawScale3D;

    /// <summary>The mesh drawn for <paramref name="component"/>, one of its components (a copy's: one of its source's), or null</summary>
    internal virtual MeshComponentProxy MeshDrawing(ExportEntry component) => Components.OfType<MeshComponentProxy>().FirstOrDefault(m => m.Export == component);

    /// <summary>
    /// Asks for <see cref="DeleteRefusal"/>, <see cref="UpgradeRefusal"/>, <see cref="DuplicateRefusal"/>, <see cref="CopyRefusal"/> and <see cref="PackageEditorRefusal"/> again: they
    /// depend on more than the actor (its file, the scene, its commit), which don't notify them
    /// </summary>
    internal void NotifyRefusals()
    {
        OnPropertyChanged(nameof(DeleteRefusal));
        OnPropertyChanged(nameof(UpgradeRefusal));
        OnPropertyChanged(nameof(DuplicateRefusal));
        OnPropertyChanged(nameof(CopyRefusal));
        OnPropertyChanged(nameof(CanCopy));
        OnPropertyChanged(nameof(PackageEditorRefusal));
    }

    /// <summary>
    /// For a committed actor's reference check (<see cref="ActorDeletionPlan.Prepare(ExportEntry, ExportEntry, DeletionRepairs)"/>): what the commit
    /// that deletes it also changes. It trashes what's already pending deletion in its file. Found now, so the check can run off the dispatcher
    /// </summary>
    public virtual DeletionRepairs GetDeletionRepairs() =>
        DeletionRepairs.None with { Trashed = OwningFile.PendingDeletions.SelectMany(d => d.Entries).ToHashSet<IEntry>() };

    /// <summary>
    /// Takes the actor out of the scene as one undo step for the caller to record: a draft outright, a committed actor as a pending deletion, with its
    /// reference check (<paramref name="plan"/>, made with <see cref="GetDeletionRepairs"/>). Its package is untouched until Commit Changes
    /// </summary>
    public virtual IUndoAction Delete(ActorDeletionPlan plan) => PendingActorDeletion.Delete(this, plan);

    /// <summary>Takes a draft out of the scene (<see cref="Delete"/>), which needs no reference check</summary>
    public IUndoAction DeleteDraft() => IsDraft ? Delete(null) : throw new InvalidOperationException("Only an uncommitted actor can be deleted without a reference check.");
    internal void NotifyPendingChange()
    {
        OnPropertyChanged(nameof(IsPendingChange));
        OnPropertyChanged(nameof(ChangeKind));
        OnPropertyChanged(nameof(PendingChangeStatus));
    }
    /// <summary>For the details panel</summary>
    public string ExportLabel => Export is null ? "New: no export until committed" : $"Export {Export.UIndex}";

    public string OwningFileName => System.IO.Path.GetFileName(Pcc.FilePath);

    public string DisplayText { get; private set; }

    // Use the resolved proxy type so subclasses and collection components are
    // categorized consistently, regardless of their export names or tags.
    public virtual string Category => this switch
    {
        ILightProxy => "Lights",
        { IsVolume: true } => "Volumes",
        { IsVolumetricMesh: true } => "Volumetric meshes",
        PawnProxy => "Pawns",
        SkeletalMeshActorProxy or SFXStuntActorProxy => "Skeletal meshes",
        DynamicSMActorProxy => PlacementCategory.Dynamics.Name,
        BioArtPlaceableProxy => "Environment",
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
                NotifyPendingChange();
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
        upgradeRequested = false;
        neededBy = null;
        propertiesBeforeCommit = null;
        _cleanSnapshot = SnapshotTransform();
        IsDirty = false;
        if (Materialized)
        {
            Materialized = false;
            OnCommittedAsNew();
        }
    }

    //a commit writes its values into Properties too (replacing top-level properties, never editing one in place), so a shallow copy restores them
    private List<Property> propertiesBeforeCommit;

    /// <summary>
    /// Called before <see cref="CommitChanges"/> when the commit may be abandoned (its package writes rolled back): <see cref="AbandonCommit"/>
    /// then puts back what the commit changed in this view, and <see cref="MarkClean"/> accepts it
    /// </summary>
    internal virtual void BeginCommit() => propertiesBeforeCommit = [.. Properties];

    // A new actor's export is made as every placed Note and TargetPoint in LE1-LE3's shipping levels, and every spline point in LE3's (the only game
    // whose levels have them), is: these flags, its class as an import, no archetype, the Level as its outer, a Class_N name, a state frame for its
    // class, no binary after its properties, no owned objects, and one entry in Level.Actors. Theirs have assigned NetIndexes; see Materialize
    private const EObjectFlags NewActorFlags = EObjectFlags.Transactional | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer
        | EObjectFlags.LoadForEdit | EObjectFlags.HasStack;

    /// <summary>
    /// Whether committing it as a draft writes all its values (its properties, not only its transform), so that its export can be made from them:
    /// the views of the classes with a recipe (<see cref="ActorCreation.RecipeClasses"/>)
    /// </summary>
    private protected virtual bool WritesAllValuesWhenCreated => false;

    /// <summary>A commit created this draft's export (<see cref="Materialize"/>), which it gives back if the commit is abandoned</summary>
    private protected bool Materialized { get; private set; }

    /// <summary>
    /// For a commit of a draft, inside its package edit: creates the export, in <paramref name="level"/> and its actors, so that every reference to the
    /// actor can be written, importing its class if the package has none. Its values are written by <see cref="CommitChanges"/>
    /// </summary>
    /// <param name="levelBinary"><paramref name="level"/>'s, which the new export is added to the Actors of. The caller writes it</param>
    /// <param name="snapshots">The commit's snapshots of other files, which a copy of another file's actor is made from</param>
    internal void Materialize(ExportEntry level, Level levelBinary, ActorCloning.SourceSnapshots snapshots)
    {
        if (!IsDraft) throw new InvalidOperationException($"{DisplayText} already has an export.");
        // Not a universal factory: only the classes whose placed actors are known to be made this way, drawn by a view that writes all their values,
        // and copies of actors, which are their sources' values
        if (CloneSource is null && (!ActorCreation.RecipeClasses.Contains(ClassName) || !WritesAllValuesWhenCreated))
            throw new InvalidOperationException($"{ClassName} has no validated construction recipe.");
        var name = NameReference.FromInstancedString(Editor.RenderContext.Registry.NameOf(Id));
        //the package may have gained the proposed name since, under what it's made under
        var parent = CloneCollection ?? level;
        if (Pcc.Exports.Any(e => e.Parent == parent && e.ObjectName.Instanced == name.Instanced)) name = Pcc.GetNextIndexedName(ClassName);
        if (CloneCollection is not null)
        {
            BindExport(ActorCloning.MaterializeMember(CloneSource, CloneCollection, level, name, LocalToWorld, out var copies));
            CloneCopies = copies;
        }
        else if (CloneSource is not null && CloneSource.FileRef != Pcc)
        {
            BindExport(ActorCloning.MaterializeInAnotherFile(this, level, levelBinary, name, snapshots, out var copies));
            CloneCopies = copies;
        }
        else if (CloneSource is not null)
        {
            BindExport(ActorCloning.Materialize(CloneSource, level, levelBinary, name, out var copies));
            CloneCopies = copies;
        }
        else BindExport(NewActorExport(Pcc, level, name, ActorCreation.ImportOf(ActorClass, Pcc), levelBinary));
        Materialized = true;
    }

    /// <summary>A new actor's export, with no properties yet, as one of <paramref name="level"/>'s actors</summary>
    /// <param name="actorClass">An entry of <paramref name="package"/>'s</param>
    /// <param name="levelBinary"><paramref name="level"/>'s, which the new export is added to the Actors of. The caller writes it</param>
    internal static ExportEntry NewActorExport(IMEPackage package, ExportEntry level, NameReference name, IEntry actorClass, Level levelBinary)
    {
        // UE3 registers an object with a NetIndex as one of its package's net objects, and reports one that's another's or out of range
        // (UObject::SetNetIndex, UPackage::AddNetObject). INDEX_NONE registers nothing, so it can't collide
        byte[] stack = [.. StateFrame.Create(actorClass.UIndex).ToBytes(package), .. BitConverter.GetBytes(-1)];
        var export = new ExportEntry(package, level, name, stack) { Class = actorClass, ObjectFlags = NewActorFlags };
        package.AddExport(export);
        levelBinary.Actors.Add(export.UIndex);
        return export;
    }

    // A placed actor's own component, in LE1-LE3's shipping levels (static mesh actors' and standalone lights'): the actor's subobject Class_N, an
    // instance of its class default's (its archetype), with these flags, and no template owner class or template name
    private const EObjectFlags NewComponentFlags = EObjectFlags.Transactional | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit;

    /// <summary>
    /// For a commit of a draft, after <see cref="Materialize"/>: makes the new export's component from <paramref name="template"/>, its class default's,
    /// with <paramref name="properties"/> (its own values) and <paramref name="binary"/>, importing its class and template
    /// </summary>
    private protected ExportEntry NewComponent(ExportEntry template, PropertyCollection properties, ObjectBinary binary)
    {
        if (!Materialized) throw new InvalidOperationException($"{DisplayText}'s export wasn't just made.");
        //no template owner class, and INDEX_NONE as its NetIndex, as a new actor has (see Materialize)
        byte[] prePropertyData = [0, 0, 0, 0, .. BitConverter.GetBytes(-1)];
        var component = new ExportEntry(Pcc, Export, Pcc.GetNextIndexedName(template.ClassName), prePropertyData, properties, binary)
        {
            Class = ActorCreation.ImportOf(template.Class, Pcc),
            Archetype = ActorCreation.ImportOf(template, Pcc),
            ObjectFlags = NewComponentFlags
        };
        Pcc.AddExport(component);
        return component;
    }

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

    /// <summary>The assets a commit of its pending values would write references to, which <see cref="AssetPorting"/> copies in first if they're other files'</summary>
    internal virtual IEnumerable<AssetReference> AssetsToCopy => [];

    /// <summary>
    /// For a commit, once <see cref="AssetsToCopy"/> are copied in: uses what <paramref name="copyOf"/> gives for each (its copy, or the asset itself) in
    /// its place, as if it had been chosen. Returns what puts them back, for when the commit is abandoned
    /// </summary>
    internal virtual Action UseCopiedAssets(Func<AssetReference, AssetReference> copyOf) => () => { };

    /// <summary>Undoes what a commit begun with <see cref="BeginCommit"/> changed in this view, leaving its pending edits as they were</summary>
    internal virtual void AbandonCommit()
    {
        if (Materialized)
        {
            Materialized = false;
            BindExport(null);
            CloneCopies = null;
        }
        if (propertiesBeforeCommit is null) return;
        Properties.Clear();
        Properties.AddRange(propertiesBeforeCommit);
        propertiesBeforeCommit = null;
    }

    /// <summary>
    /// The transform differs from the committed one
    /// </summary>
    public bool IsTransformDirty => !SnapshotTransform().Equals(_cleanSnapshot);

    protected virtual bool HasUncommittedChanges() => IsDraft || IsTransformDirty
        || Components.OfType<MeshComponentProxy>().Any(mesh => mesh.ExistingLighting?.HasChanges == true);

    /// <summary>Whether its uncommitted changes are more than a pending upgrade, which moves a collection member under an actor of its own unchanged</summary>
    internal virtual bool HasChangesBesidesUpgrade => HasUncommittedChanges();

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
    private protected readonly Vector3 defaultDrawScale3D = Vector3.One;

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
    /// <summary>
    /// The collection whose member it draws, or null. A copy of a member is its collection's once its commit has made it (a draft isn't one yet, so a change
    /// to the collection, which shows the collection again, leaves it)
    /// </summary>
    public virtual ExportEntry CollectionOwner => Export is null ? null : CloneCollection;
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

        DisplayText = Label(name);

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

    /// <summary>
    /// Lets go of what a commit deleted, which took it out of this actor's attachment (<see cref="ActorDeletionPlan"/>). A commit writes <see cref="Properties"/>
    /// whole, so they'd bring it back otherwise
    /// </summary>
    /// <param name="indexes">The UIndexes of what was deleted in this actor's file, when it was</param>
    internal void ForgetDeletedAttachments(IReadOnlySet<int> indexes) => ActorDeletionPlan.WithoutAttachment(Properties, indexes);

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

    /// <summary>
    /// Where a ray in world space (from <paramref name="origin"/> along a unit <paramref name="direction"/>) first meets this actor's surface, within
    /// <paramref name="maxDistance"/>, for an actor whose surface the level's geometry (<see cref="LevelGeometry"/>) doesn't include; null if it
    /// doesn't, or if the actor can't tell
    /// </summary>
    public virtual float? RayHit(Vector3 origin, Vector3 direction, float maxDistance) => null;

    /// <summary>The normal (unnormalized, and either way up) of its surface at <paramref name="point"/>, where <see cref="RayHit"/> met it; null if it can't tell</summary>
    public virtual Vector3? SurfaceNormalAt(Vector3 point) => null;

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
        ThrowIfCopy();
        WriteTransform(Properties);
        Export.WriteProperties(Properties);
    }

    /// <summary>
    /// A copy's view is drawn with its source's values, which refer to its source's objects, so it's written only by <see cref="CommitClone"/>: a view's
    /// own commit would point the copy at its source's objects
    /// </summary>
    private protected void ThrowIfCopy()
    {
        if (CloneSource is not null) throw new InvalidOperationException($"{DisplayText} is a copy, which only its own commit writes (CommitClone).");
    }

    /// <summary>
    /// For a commit of a copy's view (<see cref="CloneSource"/>), once <see cref="Materialize"/> has made its export: writes its transform onto the export's
    /// own properties, which are its source's, with its references to its own objects, and its other pending edits onto the objects they're of
    /// (<see cref="CommitCloneEdits"/>). Its view is drawn from stand-ins, so nothing else of the view's is written. A member's transform is its
    /// collection's, which <see cref="ActorCloning.MaterializeMember"/> wrote (a light's commit also writes it onto its component's cached copy of its
    /// owner's transform, as a moved collection light's does)
    /// </summary>
    internal void CommitClone()
    {
        if (Export is null || CloneSource is null) throw new InvalidOperationException($"{DisplayText} isn't a copy with an export.");
        if (CloneCollection is null)
        {
            var props = Export.GetProperties();
            WriteTransform(props);
            Export.WriteProperties(props);
        }
        CommitCloneEdits(CloneCopies ?? throw new InvalidOperationException($"{DisplayText}'s export wasn't made by a commit."));
    }

    /// <summary>
    /// For a copy whose export a commit made (<see cref="Materialize"/>): its objects, by its saved actor's objects they're copies of, as
    /// EntryCloner.CloneTree copied them. Null otherwise
    /// </summary>
    internal IReadOnlyDictionary<ExportEntry, ExportEntry> CloneCopies { get; private set; }

    /// <summary>
    /// For <see cref="CommitClone"/>: writes the copy's pending edits other than its transform (its meshes' lighting, its light's settings) onto its
    /// exports, each the copy of the source's object its stand-in was drawn from, found in <paramref name="copies"/>
    /// </summary>
    private protected virtual void CommitCloneEdits(IReadOnlyDictionary<ExportEntry, ExportEntry> copies) { }

    /// <summary>Puts the actor's transform in <paramref name="props"/>: each value it has there, or that isn't its default</summary>
    private void WriteTransform(PropertyCollection props)
    {
        string locationPropName = LocationPropertyName(Export.Game);
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
    }

    /// <summary>Whether it was taken out of the scene and not put back (<see cref="LevelEditorRenderContext.RemoveActor"/>): a view kept for undo</summary>
    internal bool IsOutOfScene
    {
        get => isOutOfScene;
        set
        {
            if (isOutOfScene == value) return;
            isOutOfScene = value;
            OnOutOfSceneChanged();
        }
    }
    private bool isOutOfScene;

    /// <summary>Called when it leaves the scene (undone, or a draft deleted) or comes back</summary>
    private protected virtual void OnOutOfSceneChanged() { }

    /// <summary>
    /// Takes its meshes out of the scene's lighting and visibility-trace registries if it's out of the scene: a mesh made or reloaded meanwhile
    /// joined them, as loading does
    /// </summary>
    internal void LeaveSceneIfOutOfIt()
    {
        if (!IsOutOfScene) return;
        foreach (var mesh in Components.OfType<MeshComponentProxy>()) mesh.SetSceneMembership(false);
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
        try
        {
            foreach (var component in Components.OfType<MeshComponentProxy>())
                refreshed |= component.RefreshPreviewJoiningScene(package, updatedExports, force);
        }
        catch (Exception refreshing) when (IsOutOfScene)
        {
            //(leaving can't hide why the refresh failed)
            try { LeaveSceneIfOutOfIt(); }
            catch (Exception leaving) { throw new AggregateException(refreshing, leaving); }
            throw;
        }
        LeaveSceneIfOutOfIt();
        // ParentAnimComponent transforms must propagate even to children whose own settings did not change.
        if (refreshed)
            foreach (var component in Components) component.UpdateLocalToWorld();
        if (refreshed) NotifyCoverageChanged();
        if (refreshed && this is MeshActorProxy or StaticMeshComponentActorProxy)
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

    private int historyOwners;

    /// <summary>Several lifecycle steps can retain the same view; only the last one releases an absent view.</summary>
    internal IDisposable RetainForHistory()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        historyOwners++;
        OwningFile?.RetainedActors.Add(this);
        return new HistoryLease(this);
    }

    private sealed class HistoryLease(ActorProxy actor) : IDisposable
    {
        private ActorProxy retained = actor;

        public void Dispose()
        {
            if (retained is not { } view) return;
            retained = null;
            if (--view.historyOwners != 0) return;
            view.OwningFile?.RetainedActors.Remove(view);
            if (view.Editor?.RenderContext.DrawList_3D.Contains(view) != true)
            {
                view.Dispose();
                //an undone placement or copy that history has let go of: its proposed name is free again
                if (view.IsDraft) view.Editor?.RenderContext.Registry.Discard(view.Id);
            }
        }
    }

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

public sealed class StaticMeshActorProxy : MeshActorProxy
{
    public StaticMeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport) { }

    internal StaticMeshActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties, mesh, componentTemplate) { }
}

/// <summary>An actor drawn by its StaticMeshComponent: a static mesh actor, or one that can move (<see cref="DynamicSMActorProxy"/>)</summary>
public abstract class MeshActorProxy : ActorProxy
{
    public StaticMeshComponentProxy StaticMeshComponent;
    protected MeshActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent?.IsVolumetric == true;
    }

    //a draft's: the class default's component its own is made from, and the mesh it draws
    private readonly ExportEntry componentTemplate;
    private AssetReference mesh;

    /// <summary>
    /// A new mesh actor (a draft) drawing <paramref name="mesh"/>, an entry of its package or another file's mesh. Its component is drawn as the
    /// commit makes it: from <paramref name="componentTemplate"/>'s values with the mesh, and the material overrides it inherits from the template. Its
    /// package is untouched until then. Its light environment, if it has one, is its class default's (its template's references aren't read)
    /// </summary>
    private protected MeshActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties)
    {
        this.componentTemplate = componentTemplate;
        this.mesh = mesh;
        var cache = context.PackageCache;
        var componentProperties = EffectiveLightingProperties.ReadValues(componentTemplate, cache);
        var (materials, materialPackage) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(componentTemplate, "Materials", cache);
        var assets = new PreviewAssets(mesh, [.. (materials ?? Enumerable.Empty<ObjectProperty>()).Select(m => AssetReference.Of(m.ResolveToEntry(materialPackage), Pcc, cache))]);
        // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
        var preview = new ExportEntry(Pcc, null, "None", new byte[8], binary: LegendaryExplorerCore.Unreal.BinaryConverters.StaticMeshComponent.Create());
        StaticMeshComponent = new StaticMeshComponentProxy(context.RenderContext, preview, this, componentProperties, previewAssets: assets)
        {
            ActorPropertyName = nameof(StaticMeshComponent)
        };
        Components.Add(StaticMeshComponent);
        IsVolumetricMesh = StaticMeshComponent.IsVolumetric;
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private protected override bool WritesAllValuesWhenCreated => true;

    internal override AssetReference DraftAsset => componentTemplate is not null ? mesh : null;

    internal override IEnumerable<AssetReference> AssetsToCopy => IsDraft && componentTemplate is not null ? [mesh] : [];

    //the preview, already drawing the asset, is left as it is
    internal override Action UseCopiedAssets(Func<AssetReference, AssetReference> copyOf)
    {
        var chosen = mesh;
        mesh = copyOf(mesh);
        return () => mesh = chosen;
    }

    //a draft's mesh lighting is written to the component its commit makes
    internal override IEnumerable<MeshComponentProxy> LightingMeshes => componentTemplate is null ? base.LightingMeshes : [];

    /// <summary>
    /// For a draft's commit, once it's written: the component it made, which its StaticMeshComponent now refers to. Null before, and once the commit is
    /// abandoned (its properties are put back)
    /// </summary>
    internal ExportEntry CommittedComponent => Materialized && Properties.GetProp<ObjectProperty>(nameof(StaticMeshComponent)) is { } property
        ? property.ResolveToEntry(Pcc) as ExportEntry : null;

    public override void CommitChanges(PackageCache packageCache = null)
    {
        // Its export was just made (Materialize): its component is made with it, as shipping levels' are (see NewComponent), with a StaticMeshComponent
        // binary (no light-maps until a bake). The actor refers to it as its StaticMeshComponent and CollisionComponent; neither has a Components array
        if (Materialized && componentTemplate is not null)
        {
            var component = NewComponent(componentTemplate, [new ObjectProperty(mesh.EntryIn(Pcc), "StaticMesh")],
                LegendaryExplorerCore.Unreal.BinaryConverters.StaticMeshComponent.Create());
            StaticMeshComponent.ExistingLighting?.Write(component);
            Properties.AddOrReplaceProp(new ObjectProperty(component, nameof(StaticMeshComponent)));
            Properties.AddOrReplaceProp(new ObjectProperty(component, "CollisionComponent"));
        }
        base.CommitChanges(packageCache);
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
public sealed class DynamicSMActorProxy : MeshActorProxy
{
    public DynamicSMActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport) { }

    /// <summary>
    /// A new InterpActor (a draft), made as a static mesh actor is. It has no light environment export of its own, so it has its class default's
    /// MyLightEnvironment, which is disabled: as 85-94% of LE1-LE3's shipping ones do (the rest have one of their own, mostly enabled)
    /// </summary>
    internal DynamicSMActorProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, AssetReference mesh, ExportEntry componentTemplate)
        : base(context, draft, properties, mesh, componentTemplate) { }
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

    /// <summary>The size of a new volume's box: UE3's default cube builder brush</summary>
    public const float NewBoxSize = 256;

    //a draft's: the class default's component its own is made from, and its collision (BrushAggGeom)
    private readonly ExportEntry componentTemplate;
    private readonly StructProperty collision;

    // The new box's hull, the same for every one, which PhysX works out once (one at a time, as its cooking library is global). A failure (as when it
    // can't be loaded) isn't kept, so the next placement tries again
    private static StructProperty newBoxHull;
    private static StructProperty NewBoxHull()
    {
        if (newBoxHull is null)
        {
            const float half = NewBoxSize / 2;
            var corners = Enumerable.Range(0, 8).Select(i => new Vector3((i & 1) == 0 ? -half : half, (i & 2) == 0 ? -half : half, (i & 4) == 0 ? -half : half)).ToArray();
            newBoxHull = KConvexElem.CreateFromHull(corners).GetStructProperty();
        }
        return newBoxHull.DeepClone();
    }

    /// <summary>
    /// A new volume (a draft): a box of <see cref="NewBoxSize"/> around its location, drawn as the commit makes its component, from
    /// <paramref name="componentTemplate"/>'s values with the box's hull. Its package is untouched until then
    /// </summary>
    internal BrushProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, ExportEntry componentTemplate) : base(context, draft, properties)
    {
        this.componentTemplate = componentTemplate;
        var cache = context.RenderContext.PackageCache;
        var aggGeom = GlobalUnrealObjectInfo.getDefaultStructValue(Pcc.Game, "KAggregateGeom", true, Pcc, cache);
        aggGeom.AddOrReplaceProp(new ArrayProperty<StructProperty>([NewBoxHull()], "ConvexElems"));
        collision = new StructProperty("KAggregateGeom", aggGeom, "BrushAggGeom");
        var componentProperties = EffectiveLightingProperties.ReadValues(componentTemplate, cache);
        componentProperties.AddOrReplaceProp(collision.DeepClone());
        // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
        var preview = new ExportEntry(Pcc, null, "None", new byte[8], binary: LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent.Create());
        BrushComponent = new BrushComponentProxy(context.RenderContext, preview, this, componentProperties) { ActorPropertyName = nameof(BrushComponent) };
        Components.Add(BrushComponent);
        //a draft is wholly uncommitted
        UpdateDirty();
    }

    private protected override bool WritesAllValuesWhenCreated => true;

    public override void CommitChanges(PackageCache packageCache = null)
    {
        // Its export was just made (Materialize): its component is made with it, as shipping levels' volumes' are (see NewComponent), with its hull and
        // the level's empty brush, which cooked volumes share. The actor refers to them as its Brush, BrushComponent and CollisionComponent
        ExportEntry component = null;
        if (Materialized && componentTemplate is not null)
        {
            var brush = EmptyBrush(OwningFile.LevelExport, Export);
            component = NewComponent(componentTemplate, [new ObjectProperty(brush, "Brush"), collision.DeepClone(), new ObjectProperty(0, "ReplacementPrimitive")],
                LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent.Create());
            Properties.AddOrReplaceProp(new ObjectProperty(brush, "Brush"));
            Properties.AddOrReplaceProp(new ObjectProperty(component, nameof(BrushComponent)));
            Properties.AddOrReplaceProp(new ObjectProperty(component, "CollisionComponent"));
        }
        // Its cached PhysX data, which is cooked at the actor's scale, so a new scale makes it stale; its hull is unscaled, and its location and rotation are
        // applied at runtime (UE3's BuildPhysBrushData, MakeCachedConvexDataForAggGeom). With none, there's nothing to go stale: if it has rigid-body
        // collision, the game cooks it at load, at the scale it has then
        else if (IsScaleDirty
                 && Export.GetProperty<ObjectProperty>(nameof(BrushComponent))?.ResolveToEntry(Pcc) is ExportEntry own && HasCookedCollision(own))
        {
            component = own.Parent == Export ? own
                : throw new InvalidOperationException($"{Export.InstancedFullPath}'s BrushComponent isn't its own, so its collision can't be cooked at its new scale.");
        }
        // A new volume's: as a cooked blocking volume has it, if it has rigid-body collision (bDisableAllRigidBody, which Volume's component template sets and
        // BlockingVolume's clears). Without it, the game would rebuild the hull at load from the brush, which a cooked volume's is empty, and lose it
        bool cook = component is not null && (!Materialized || BrushComponent.Properties.GetProp<BoolProperty>("bDisableAllRigidBody")?.Value != true);
        base.CommitChanges(packageCache);
        // cooked at the scale the actor was just written with
        if (cook) CookCollision(component, packageCache ?? Editor?.RenderContext.PackageCache);
    }

    /// <summary>Whether <paramref name="component"/>, a BrushComponent, has cached PhysX data, which is cooked at its actor's scale</summary>
    internal static bool HasCookedCollision(ExportEntry component) =>
        component.GetBinaryData<LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent>().CachedPhysBrushData.CachedConvexElements.Length > 0;

    /// <summary>Cooks <paramref name="component"/>'s collision again, at the scale its actor has been written with</summary>
    internal static void CookCollision(ExportEntry component, PackageCache cache)
    {
        var binary = component.GetBinaryData<LegendaryExplorerCore.Unreal.BinaryConverters.BrushComponent>();
        binary.RegenCachedPhysBrushData(cache);
        component.WriteBinary(binary);
    }

    // A Model and its Polys as the cooker leaves them for a level's volumes to share: under the level, with no geometry, and no properties. The Polys is
    // its own Owner
    private const EObjectFlags BrushFlags = EObjectFlags.Transactional | EObjectFlags.LoadForClient | EObjectFlags.LoadForServer | EObjectFlags.LoadForEdit;

    /// <summary>
    /// The empty brush model that <paramref name="level"/>'s volumes other than <paramref name="volume"/> share, or a new one made as the cooker makes it if
    /// none does
    /// </summary>
    internal static ExportEntry EmptyBrush(ExportEntry level, ExportEntry volume)
    {
        var package = level.FileRef;
        foreach (var other in package.Exports.Where(e => e.Parent == level && e != volume && !e.IsDefaultObject && e.IsA("Volume")))
        {
            if (other.GetProperty<ObjectProperty>("Brush")?.ResolveToEntry(package) is ExportEntry { ClassName: "Model" } brush && brush.Parent == level
                && brush.GetBinaryData<Model>() is { Nodes.Length: 0 } shared
                && (shared.Polys == 0 || package.GetEntry(shared.Polys) is ExportEntry { ClassName: "Polys" } sharedPolys && sharedPolys.GetBinaryData<Polys>().Elements.Length == 0))
                return brush;
        }
        //(no NetIndex, as INDEX_NONE registers nothing)
        byte[] noNetIndex = BitConverter.GetBytes(-1);
        var polys = new ExportEntry(package, level, package.GetNextIndexedName("Polys"), noNetIndex)
        {
            Class = package.GetEntryOrAddImport("Engine.Polys", "Class"), ObjectFlags = BrushFlags
        };
        package.AddExport(polys);
        var polysBinary = Polys.Create();
        polysBinary.Owner = polys.UIndex;
        polys.WritePropertiesAndBinary([], polysBinary);
        var model = new ExportEntry(package, level, package.GetNextIndexedName("Model"), noNetIndex)
        {
            Class = package.GetEntryOrAddImport("Engine.Model", "Class"), ObjectFlags = BrushFlags
        };
        package.AddExport(model);
        var modelBinary = Model.Create();
        modelBinary.Self = model.UIndex;
        modelBinary.Polys = polys.UIndex;
        modelBinary.RootOutside = true;
        modelBinary.NumSharedSides = 4;
        modelBinary.LightingGuid = Guid.NewGuid();
        model.WritePropertiesAndBinary([], modelBinary);
        return model;
    }
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

    internal override ExportEntry MemberCollection => CollectionActorExport;
    private protected override string EditKind => UpgradePending ? "Upgrade" : base.EditKind;
    // Subclasses add their own changes to the member's (HasMemberChanges), so that what's more than an upgrade is known from one place
    protected sealed override bool HasUncommittedChanges() => HasMemberChanges() || UpgradePending;
    protected virtual bool HasMemberChanges() => base.HasUncommittedChanges();
    internal sealed override bool HasChangesBesidesUpgrade => HasMemberChanges();

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

/// <summary>
/// A placed prefab's record, as a marker: UE3's editor uses it to update a level's actors from the prefab. Its members are ordinary actors of the level (every
/// live one in LE1-LE3's levels is), which draw themselves. Its template isn't drawn: it lists what the level may no longer have, since a blank entry in the
/// instance's map is a member that was deleted. It can't be moved, since that wouldn't move its members
/// </summary>
public sealed class PrefabInstanceProxy(IActorEditorContext context, ExportEntry actorExport)
    : EditorActorProxy(context, actorExport, new(ActorKind.Prefab, "Prefabs", "A placed prefab's record. Its members are ordinary actors of the level."))
{
    public override bool CanEditTransform => false;
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
