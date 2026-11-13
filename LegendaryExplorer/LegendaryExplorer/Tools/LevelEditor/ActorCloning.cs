using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A copy of an actor, being moved before it's made (an Alt+drag) or placed at once (Duplicate). Move <see cref="Copy"/>; then complete or cancel it
/// </summary>
public interface IActorDuplication
{
    /// <summary>The draft copy: drawn, selected and movable until the duplicate completes or is cancelled</summary>
    ActorProxy Copy { get; }

    /// <summary>Takes on where the copy has been moved (a spline point's connection follows it)</summary>
    void Follow();

    /// <summary>
    /// Finishes it as one undo step for the caller to record. A copy left exactly where its source is isn't a duplicate: it's cancelled, and there's no
    /// step. If it fails, it's cancelled, and the error is thrown
    /// </summary>
    IUndoAction Complete();

    /// <summary>Puts everything back as it was, and discards the copy</summary>
    void Cancel();
}

/// <summary>
/// Copies of committed actors in their own file, as modders have long made them with Package Editor's Clone Tree. A copy is a draft until Commit
/// Changes, which clones the actor and everything under it, and makes unique what UE3's editor makes unique when it duplicates an actor: its NetIndexes
/// and its lights' GUIDs (ULightComponent::PostDuplicate). Everything else is copied as it is, and what it refers to outside itself (meshes, materials,
/// other actors) is shared. Unlike UE3's duplicate, whose text round-trip leaves a copy with no light-maps, that includes its meshes' baked lighting,
/// which is its source's until the level is baked again (the commit marks it changed since the bake). A copy starts as its source is shown, with its
/// uncommitted edits (its transform, its meshes' lighting, its light's settings), which are pending on the copy as they were on its source: the commit
/// clones its source as it's saved, then writes the copy's edits onto the copy
/// </summary>
public static class ActorCloning
{
    /// <summary>Why <paramref name="actor"/> can't be cloned now, or null. Spline points have a duplicate of their own (<see cref="SplineActorProxy.BeginDuplicate"/>)</summary>
    public static string Refusal(ActorProxy actor)
    {
        if (actor is null || actor.IsDisposed) return "Select an actor first.";
        if (actor.Editor?.RenderContext.DrawList_3D.Contains(actor) != true) return "It isn't in the scene.";
        if (actor.OwningFile is not { IsReadOnly: false }) return "Its file is read-only or closed.";
        if (!actor.Package.Game.IsLEGame()) return "Only the Legendary Edition's actors can be cloned.";
        // A placed draft is copied by its recipe, as it was made
        if (actor is { IsDraft: true, CloneSource: null }) return ActorCreation.HasRecipe(actor.ClassName) ? null : $"There's no recipe for a new {actor.ClassName}.";
        // A copy's copy is another copy of the same saved actor, or member
        var saved = actor.Export ?? actor.CloneSource;
        // However it's shown: as an actor, or as a light collection's member
        if (saved.IsA("DominantDirectionalLight") || saved.IsA("DominantDirectionalLightComponent")) return "A level has only one dominant directional light.";
        // A member's copy is added to its collection, as Package Editor's Clone adds one
        if (actor.MemberCollection is { } collection)
        {
            // Drawn from its component as it's saved, which a copy is made of, and in its collection, which it's added to
            if (ActorDeletionPlan.MembershipProblem(saved, collection) is { } problem) return problem;
            return collection.Parent is ExportEntry { ClassName: "Level" } ? null : "Its collection isn't one of its level's actors.";
        }
        // A member that couldn't be drawn is shown by a marker, which a copy of its component wouldn't be
        if (actor.CollectionOwner is not null) return "It's a collection's member that couldn't be drawn.";
        // (The level's BSP model is drawn as an actor)
        if (!saved.IsA("Actor")) return "It isn't an actor.";
        string kind = actor switch
        {
            // A clone would share its connections
            SplineActorProxy => "A spline point is duplicated with a connection to it, not cloned.",
            DecalActorProxy { DecalComponent: null } => "It has no decal component.",
            // A copy's surfaces are worked out where it's put, as a placed decal's are
            DecalActorProxy { DecalComponent.EditUnsupportedReason: { } reason } => $"A copy of it can't be made yet. {reason}",
            TerrainProxy => "A terrain can't be cloned.",
            FoliageFactoryProxy => "Foliage can't be cloned: its instances are placed in the world, not on the actor.",
            PrefabInstanceProxy => "A prefab instance can't be cloned yet.",
            BrushProxy when !saved.IsA("Volume") => "Only volumes' brushes can be cloned, not the level's geometry.",
            // Its light is drawn from its component, whose copy its commit writes the light's settings onto
            LightActorProxy when LightActorProxy.GetLightComponent(saved)?.Parent != saved => "Its light component isn't its own, so a copy wouldn't have it.",
            // One whose transform the Level Editor doesn't edit: a KAsset, or a helper or marker without a position of its own (WorldInfo)
            { CanEditTransform: false } => "Its transform can't be edited, so a copy couldn't be placed anywhere else.",
            _ => null
        };
        if (kind is not null) return kind;
        if (saved.IsA("NavigationPoint")) return "It's a navigation point: use Pathfinding Editor.";
        if (saved.Parent is not ExportEntry { ClassName: "Level" }) return "It isn't one of its level's actors.";
        // A copy would follow its base about, wherever it was put
        if (saved.GetProperty<ObjectProperty>("Base") is { Value: not 0 } attachment)
            return $"It's attached to {actor.Package.GetEntry(attachment.Value)?.ObjectName.Instanced ?? $"#{attachment.Value}"}.";
        return null;
    }

    /// <summary>
    /// Starts cloning <paramref name="source"/>: a draft copy is drawn where it is and selected. Nothing is written to the package until a commit makes it
    /// (<see cref="Materialize"/>)
    /// </summary>
    /// <param name="offset">Where the copy starts from its source, before it's selected (so it isn't an edit of the copy)</param>
    public static IActorDuplication BeginClone(ActorProxy source, Vector3 offset = default) => new Duplication(source, offset);

    private sealed class Duplication : IActorDuplication
    {
        private readonly ActorProxy source;
        private readonly ActorProxy selection;
        private bool finished;

        public ActorProxy Copy { get; }

        public Duplication(ActorProxy source, Vector3 offset)
        {
            if (Refusal(source) is { } refusal) throw new InvalidOperationException($"{source?.ActorName} can't be cloned. {refusal}");
            this.source = source;
            var editor = source.Editor;
            selection = editor.RenderContext.SelectedActor;
            var registry = editor.RenderContext.Registry;
            // A saved actor's class is its export's (a marker's view has no record of it); a draft's is its record's
            var actorClass = (source.Export ?? source.CloneSource)?.Class ?? registry[source.Id].Class;
            var record = registry.CreateDraft(source.Package, actorClass, registry.ProposeName(source.Package, source.ClassName), isSpline: false, isLoft: false);
            try
            {
                Copy = MakeDraft(editor, record, source);
                Copy.Location += offset;
                using var edit = new SceneEdit(editor);
                edit.Attach(Copy);
                edit.Select(Copy);
                edit.Complete();
            }
            catch
            {
                Copy?.Dispose();
                registry.Discard(record.Id);
                throw;
            }
        }

        public void Follow() => ObjectDisposedException.ThrowIf(finished, this);

        public IUndoAction Complete()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            if (Copy.SnapshotTransform().Equals(source.SnapshotTransform()))
            {
                Cancel();
                return null;
            }
            IUndoAction step;
            try
            {
                step = new ActorCreation.PlaceAction(Copy, selection, $"Duplicate {source.ActorName}");
            }
            catch (Exception error)
            {
                try
                {
                    Cancel();
                }
                catch (Exception cancelError)
                {
                    throw new AggregateException(error, cancelError);
                }
                throw;
            }
            finished = true;
            return step;
        }

        public void Cancel()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            // Finished once it's out of the scene, so a cancel that couldn't take it out (another change in progress) can be tried again
            using (var edit = new SceneEdit(source.Editor))
            {
                edit.Detach(Copy);
                edit.Reselect(selection);
                edit.Complete();
            }
            finished = true;
            Copy.Dispose();
            source.Editor.RenderContext.Registry.Discard(Copy.Id);
        }
    }

    /// <summary>
    /// The copy's view. A placed draft's is another draft of its recipe (<see cref="ActorCreation.CopyDraft"/>). Any other's is one drawn from its saved
    /// actor's values without its exports, which a view of those exports would share their light, light environment and lighting edits with: a light is
    /// drawn as a new light of its values, a marker as a new marker, anything else by stand-ins of its meshes and brush (<see cref="ActorCloneProxy"/>). Its
    /// saved values are the saved actor's, which its commit clones; a copy's copy is another copy of the same actor. Either way it starts as its source is
    /// shown: its source's uncommitted edits are pending on it, relative to the same saved values (<see cref="CarryEdits"/>). A collection's member is
    /// copied as its component: a light as a new light of its values, a mesh by a stand-in of it
    /// </summary>
    private static ActorProxy MakeDraft(IActorEditorContext editor, ActorRecord record, ActorProxy source)
    {
        ActorProxy copy;
        if (source is { IsDraft: true, CloneSource: null }) copy = ActorCreation.CopyDraft(editor, record, source);
        else
        {
            var saved = source.Export ?? source.CloneSource;
            var collection = source.MemberCollection;
            // A member is its component, with no actor's values
            var properties = collection is null ? saved.GetCondensedProperties() : [];
            copy = source switch
            {
                // A member light's component is the member
                LightActorProxy or StaticLightCollectionComponentProxy => new LightActorProxy(editor, record, properties,
                    collection is null ? LightActorProxy.GetLightComponent(saved) : saved) { CloneSource = saved, CloneCollection = collection },
                // Projected where it's put, as a placed decal is, from its source's component, with its source's material and Filter
                DecalActorProxy => EffectiveLightingProperties.ResolveReference(saved, "Decal", editor.PackageCache, properties) is { } component
                    ? new DecalActorProxy(editor, record, properties, EditableDecal.MaterialOf(component, editor.PackageCache), component) { CloneSource = saved }
                    : throw new InvalidOperationException($"{source.ActorName} has no decal component."),
                EditorActorProxy => new EditorActorProxy(editor, record, properties, ActorClassification.Get(saved)) { CloneSource = saved },
                _ => new ActorCloneProxy(editor, record, properties, source, saved, isMember: collection is not null) { CloneSource = saved, CloneCollection = collection }
            };
        }
        try
        {
            // A copy is drawn from its saved actor's own objects. One its source is drawn with that isn't (an archetype's) it wouldn't have, nor
            // be seen by, nor be clicked on
            if (copy is ActorCloneProxy clone && source.Components.Where(c => c is MeshComponentProxy or BrushComponentProxy && c is not SplineMeshComponentProxy)
                    .Select(c => c.CopiedFrom ?? c.Export).FirstOrDefault(component => !clone.Draws(component)) is { } missing)
                throw new InvalidOperationException($"{source.ActorName} can't be cloned: it's drawn with {missing.ObjectName.Instanced}, which isn't one of its own objects, so a copy wouldn't have it.");
            CarryEdits(source, copy);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
        return copy;
    }

    /// <summary>
    /// Puts <paramref name="source"/>'s transform and uncommitted edits on <paramref name="copy"/>, whose saved values are the same as its: its meshes'
    /// lighting, its light's settings, its decal's settings
    /// </summary>
    private static void CarryEdits(ActorProxy source, ActorProxy copy)
    {
        copy.OwningFile = source.OwningFile;
        copy.RestoreTransform(source.SnapshotTransform());
        if (copy is LightActorProxy light && source is LightActorProxy { Light.HasChanges: true } sourceLight) light.Light.Seed(sourceLight.Light);
        if (copy is DecalActorProxy { Decal: { } decal } && source is DecalActorProxy { Decal: { HasChanges: true } sourceDecal }) decal.Restore(sourceDecal.Settings);
        foreach (var mesh in copy.Components.OfType<MeshComponentProxy>())
        {
            // A copy of a saved actor's mesh is drawn from that actor's component; a placed draft's copy's is the same component of its recipe as its source's
            var counterpart = mesh.CopiedFrom is { } component
                ? source.MeshDrawing(component)
                : source.Components.OfType<MeshComponentProxy>().FirstOrDefault(m => m.ActorPropertyName == mesh.ActorPropertyName);
            if (counterpart?.ExistingLighting is not { HasChanges: true } lighting) continue;
            mesh.Lighting.Seed(lighting);
            // Drawn with them, as a mesh is redrawn when its lighting is edited (LevelEditor.ApplyActorLightingEdits): its preview reads them into the
            // properties it's drawn with
            mesh.RefreshPreviewJoiningScene(mesh.Export.FileRef, new HashSet<int>(), force: true);
            mesh.UpdateLocalToWorld();
        }
    }

    /// <summary>
    /// For a commit of a copy of <paramref name="source"/>, inside its package edit: clones the actor and everything under it, named
    /// <paramref name="name"/>, as one of <paramref name="level"/>'s actors, made unique as <see cref="CloneTree"/> makes it. Nothing is attached to it: the
    /// actors in its source's Attached are based on its source, and the engine moves everything in an actor's Attached with it (UWorld::MoveActor,
    /// UnLevAct.cpp), so a copy that listed them would pull them away
    /// </summary>
    /// <param name="levelBinary"><paramref name="level"/>'s, which the copy is added to the Actors of. The caller writes it</param>
    /// <param name="copies">The copy's objects, by the source's objects they're copies of</param>
    internal static ExportEntry Materialize(ExportEntry source, ExportEntry level, Level levelBinary, NameReference name, out Dictionary<ExportEntry, ExportEntry> copies)
    {
        if (source.Parent != level || source.FileRef.GetEntry(source.UIndex) != source)
            throw new InvalidOperationException($"{source.ObjectName.Instanced} is no longer one of {level.ObjectName.Instanced}'s actors, so it can't be cloned.");
        var copy = CloneTree(source, name, out copies);
        //(emptied, as a deletion takes actors out of it: ActorDeletionPlan.WithoutAttachment)
        var actorProperties = copy.GetProperties();
        if (actorProperties.GetProp<ArrayProperty<ObjectProperty>>("Attached") is { Count: > 0 })
        {
            actorProperties.AddOrReplaceProp(new ArrayProperty<ObjectProperty>("Attached"));
            copy.WriteProperties(actorProperties);
        }
        levelBinary.Actors.Add(copy.UIndex);
        return copy;
    }

    /// <summary>
    /// For a commit of a copy of <paramref name="source"/>, one of <paramref name="collection"/>'s members, inside its package edit: clones the member and
    /// everything under it, named <paramref name="name"/>, as another of its members, at <paramref name="transform"/>, as Package Editor's Clone adds one
    /// (TryAddToStaticCollectionActor). A collection holds its members' transforms in its binary, one for each entry of its component list, which the game
    /// reads in that order (AStaticMeshCollectionActor::Serialize, AStaticLightCollectionActor::Serialize). Nothing limits how many it has but the cooker's
    /// configuration (UnContentCookers.cpp). It's made unique as an actor's copy is (<see cref="CloneTree"/>)
    /// </summary>
    /// <param name="copies">The copy's objects, by the source's objects they're copies of</param>
    internal static ExportEntry MaterializeMember(ExportEntry source, ExportEntry collection, ExportEntry level, NameReference name, Matrix4x4 transform,
        out Dictionary<ExportEntry, ExportEntry> copies)
    {
        if (collection.Parent != level || collection.FileRef.GetEntry(collection.UIndex) != collection)
            throw new InvalidOperationException($"{collection.ObjectName.Instanced} is no longer one of {level.ObjectName.Instanced}'s actors, so {source.ObjectName.Instanced} can't be cloned into it.");
        if ((source.FileRef.GetEntry(source.UIndex) != source ? "It's no longer in its package." : ActorDeletionPlan.MembershipProblem(source, collection)) is { } problem)
            throw new InvalidOperationException($"{source.ObjectName.Instanced} can't be cloned. {problem}");
        var binary = (StaticCollectionActor)ObjectBinary.From(collection);
        var copy = CloneTree(source, name, out copies);
        var members = collection.GetProperty<ArrayProperty<ObjectProperty>>(binary.ComponentPropName);
        members.Add(new ObjectProperty(copy));
        binary.LocalToWorldTransforms.Add(transform);
        // Its binary has a transform for each entry of its component list as its properties have it, so they're written first (ActorDeletionPlan.RemoveMembers)
        collection.WriteProperty(members);
        collection.WriteBinary(binary);
        return copy;
    }

    /// <summary>
    /// Clones <paramref name="source"/> and everything under it, named <paramref name="name"/>, beside it. Its references inside the copy are to the copy's
    /// own objects (EntryCloner.CloneTree relinks them). Its NetIndexes are INDEX_NONE, as a new actor's (<see cref="ActorProxy.NewActorExport"/>): a
    /// shipping one would collide with the source's. Its light components have new GUIDs, which baked lighting refers to them by (UE3's
    /// ULightComponent::PostDuplicate), and a dominant spot light's baked shadow transitions are dropped, being where its source is
    /// </summary>
    private static ExportEntry CloneTree(ExportEntry source, NameReference name, out Dictionary<ExportEntry, ExportEntry> copies)
    {
        var cloned = new ListenableDictionary<IEntry, IEntry>();
        var copy = EntryCloner.CloneTree(source, objectMap: cloned);
        //(only exports are cloned)
        copies = cloned.ToDictionary(pair => (ExportEntry)pair.Key, pair => (ExportEntry)pair.Value);
        copy.ObjectName = name;
        foreach (var export in copy.GetAllDescendants().OfType<ExportEntry>().Prepend(copy))
        {
            export.NetIndex = -1;
            if (export.IsA("LightComponent"))
            {
                // A dominant spot light's shadow transitions were baked in world space where its source is, so they're dropped until the level is baked
                // again, as UDominantSpotLightComponent::InvalidateLightingCache drops them (SpotLightComponent.cpp). They're serialized ahead of its
                // properties, as their count and entries (ExportEntry.GetPropertyStart). A dominant directional light isn't cloned
                if (export.ClassName == "DominantSpotLightComponent")
                {
                    byte[] prefix = export.GetPrePropBinary();
                    export.SetPrePropBinary([.. BitConverter.GetBytes(0), .. prefix.AsSpan(4 + 2 * BitConverter.ToInt32(prefix, 0))]);
                }
                var properties = export.GetProperties();
                if (properties.GetProp<StructProperty>("DominantLightShadowInfo") is { } shadowInfo)
                {
                    shadowInfo.Properties.AddOrReplaceProp(new IntProperty(0, "ShadowMapSizeX"));
                    shadowInfo.Properties.AddOrReplaceProp(new IntProperty(0, "ShadowMapSizeY"));
                }
                properties.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "LightGuid"));
                properties.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "LightmapGuid"));
                //(it's a new light, which no bake has built into a light-map yet)
                if (properties.GetProp<BoolProperty>("bHasLightEverBeenBuiltIntoLightMap") is { Value: true })
                    properties.AddOrReplaceProp(new BoolProperty(false, "bHasLightEverBeenBuiltIntoLightMap"));
                export.WriteProperties(properties);
            }
        }
        return copy;
    }
}

/// <summary>
/// A copy of an actor, until it's committed: drawn by stand-ins of its source's static mesh, skeletal mesh and brush components (exports that aren't in the
/// package, as a new actor's are), each with its source's values and placed as it is on its source. Its meshes' lighting is edited on its stand-ins, and
/// written by its commit onto its copies of its source's components; once committed, it's drawn as its class is, from its own export. Its light
/// environment isn't drawn (its stand-ins are lit as other meshes are), nor its baked lighting, which its commit copies
/// </summary>
internal sealed class ActorCloneProxy : ActorProxy
{
    private readonly string category;
    private readonly bool isVolume;
    private readonly int hitPriority;
    //the source's component each stand-in is drawn from
    private readonly Dictionary<PrimitiveComponentProxy, ExportEntry> drawnFrom = [];

    public override string Category => category;
    public override bool IsVolume => isVolume;
    public override int HitPriority => hitPriority;

    /// <param name="source">What it's a copy of: <paramref name="saved"/>'s view, or a copy of it</param>
    /// <param name="saved">The saved actor it's a copy of, or the collection's member</param>
    /// <param name="isMember">Whether <paramref name="saved"/> is a collection's member, which is its component alone</param>
    internal ActorCloneProxy(IActorEditorContext context, ActorRecord draft, PropertyCollection properties, ActorProxy source, ExportEntry saved, bool isMember)
        : base(context, draft, properties)
    {
        category = source.Category;
        isVolume = source.IsVolume;
        hitPriority = source.HitPriority;
        var cache = context.PackageCache;
        var drawn = new Dictionary<ExportEntry, PrimitiveComponentProxy>();
        //(the stand-ins drawn so far are disposed with it if it can't be drawn)
        try
        {
            // A member is drawn from its component, as its collection's view draws it
            if (isMember) Draw(saved, null);
            else
            {
                // The components under it that its own properties refer to (not those only its archetype's do), each named by a property that refers to it:
                // one of its own (StaticMeshComponent) before an entry in an array (Components[1]), which is serialized first
                var references = saved.GetProperties().SelectMany(p => p switch
                {
                    ObjectProperty o => [(Name: p.Name.Instanced, o.Value, InArray: false)],
                    ArrayProperty<ObjectProperty> a => a.Select((o, i) => (Name: $"{p.Name.Instanced}[{i}]", o.Value, InArray: true)),
                    _ => []
                }).OrderBy(r => r.InArray);
                foreach (var (name, component, _) in references)
                {
                    if (saved.FileRef.TryGetUExport(component, out var export) && export.Parent == saved) Draw(export, name);
                }
            }
            IsVolumetricMesh = Components.OfType<StaticMeshComponentProxy>().Any(mesh => mesh.IsVolumetric);
            // A character's head is morphed, as its class's view morphs it (BioPawnProxy, SFXSkeletalMeshActorProxy, SFXStuntActorProxy)
            string head = Pcc.Game.IsGame1() ? "m_oHeadMesh" : "HeadMesh";
            if (saved.GetProperty<ObjectProperty>(head)?.ResolveToEntry(Pcc) is ExportEntry headExport && drawn.GetValueOrDefault(headExport) is SkeletalMeshComponentProxy headMesh)
                ApplyMorphFace(headMesh);
            //a draft is wholly uncommitted
            UpdateDirty();
        }
        catch
        {
            Dispose();
            throw;
        }

        PrimitiveComponentProxy Draw(ExportEntry component, string propertyName)
        {
            if (drawn.TryGetValue(component, out var done))
            {
                //drawn first as another's animation parent
                if (done is not null && propertyName is not null) done.ActorPropertyName ??= propertyName;
                return done;
            }
            //(and so, a component that's its own animation parent isn't drawn twice)
            drawn[component] = null;
            var game = component.Game;
            bool Is(string baseClass) => GlobalUnrealObjectInfo.IsA(component.ClassName, baseClass, game);
            // Only meshes and brushes are drawn. A spline mesh is a spline point's connection, which isn't cloned this way
            bool isStaticMesh = Is("StaticMeshComponent") && !Is("SplineMeshComponent"), isSkeletalMesh = Is("SkeletalMeshComponent");
            if (!isStaticMesh && !isSkeletalMesh && component.ClassName != "BrushComponent") return null;
            // Its values are its own (references stripped, as they'd refer to its source's objects); its mesh and materials are its source's
            var values = EffectiveLightingProperties.ReadValues(component, cache);
            PreviewAssets Assets(string meshProperty)
            {
                var (mesh, meshPackage) = EffectiveLightingProperties.Reference(component, meshProperty, cache);
                var (materials, materialPackage) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(component, "Materials", cache);
                return new PreviewAssets(AssetReference.Of(mesh?.ResolveToEntry(meshPackage), Pcc, cache),
                    [.. (materials ?? Enumerable.Empty<ObjectProperty>()).Select(m => AssetReference.Of(m.ResolveToEntry(materialPackage), Pcc, cache))]);
            }
            // None already exists in every package. Pending properties bypass serialization, so even the package's name table stays untouched
            ExportEntry StandIn(ObjectBinary binary = null) => new(Pcc, null, "None", new byte[8], binary: binary);
            PrimitiveComponentProxy proxy;
            if (isStaticMesh)
            {
                proxy = new StaticMeshComponentProxy(context.RenderContext, StandIn(StaticMeshComponent.Create()), this, values, previewAssets: Assets("StaticMesh"));
            }
            else if (isSkeletalMesh)
            {
                // Drawn after the component it's transformed from, which is drawn first if it hasn't been
                var parent = EffectiveLightingProperties.Reference(component, "ParentAnimComponent", cache);
                var animParent = parent.Property?.ResolveToEntry(parent.Package) is ExportEntry parentExport && parentExport.Parent == saved ? Draw(parentExport, null) : null;
                proxy = new SkeletalMeshComponentProxy(context.RenderContext, StandIn(), this, values, Assets("SkeletalMesh"), animParent);
            }
            else proxy = new BrushComponentProxy(context.RenderContext, StandIn(BrushComponent.Create()), this, values);
            proxy.ActorPropertyName = propertyName;
            drawn[component] = proxy;
            drawnFrom[proxy] = component;
            Components.Add(proxy);
            return proxy;
        }
    }

    /// <summary>The saved actor's component <paramref name="drawn"/>, one of its stand-ins, is drawn from</summary>
    internal ExportEntry DrawnFrom(PrimitiveComponentProxy drawn) => drawnFrom.GetValueOrDefault(drawn);

    /// <summary>
    /// For its commit, once its export is made (<see cref="ActorProxy.Materialize"/>): its copy of the component <paramref name="drawn"/>, one of its
    /// stand-ins, is drawn from. Null before, and once the commit is abandoned
    /// </summary>
    internal ExportEntry CommittedComponent(PrimitiveComponentProxy drawn) =>
        Materialized && DrawnFrom(drawn) is { } component ? CloneCopies.GetValueOrDefault(component) : null;

    /// <summary>Whether one of its stand-ins is drawn from <paramref name="component"/>, one of its saved actor's</summary>
    internal bool Draws(ExportEntry component) => drawnFrom.ContainsValue(component);

    internal override MeshComponentProxy MeshDrawing(ExportEntry component) =>
        drawnFrom.FirstOrDefault(p => p.Value == component).Key as MeshComponentProxy;

    /// <summary>
    /// Writes its meshes' lighting edits onto its copies of the components they're drawn from. A volume whose scale isn't the one it was copied (or last
    /// committed) with has its collision cooked again, if it has cooked collision, as a rescaled volume's commit does (<see cref="BrushProxy.CommitChanges"/>)
    /// </summary>
    private protected override void CommitCloneEdits(IReadOnlyDictionary<ExportEntry, ExportEntry> copies)
    {
        ExportEntry CopyOf(ExportEntry component) => copies.GetValueOrDefault(component)
            ?? throw new InvalidOperationException($"{ActorName} has no copy of {component.ObjectName.Instanced}.");
        var copiedIndexes = copies.ToDictionary(p => p.Key.UIndex, p => p.Value.UIndex);
        foreach (var (proxy, component) in drawnFrom)
        {
            if (proxy is MeshComponentProxy { ExistingLighting: { HasChanges: true } lighting }) lighting.Write(CopyOf(component), copiedIndexes);
        }
        if (!IsScaleDirty) return;
        foreach (var (proxy, component) in drawnFrom)
        {
            if (proxy is BrushComponentProxy && CopyOf(component) is var brush && BrushProxy.HasCookedCollision(brush))
                BrushProxy.CookCollision(brush, Editor.RenderContext.PackageCache);
        }
    }
}
