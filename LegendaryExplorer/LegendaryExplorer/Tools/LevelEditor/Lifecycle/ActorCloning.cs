using System;
using System.Collections.Generic;
using System.IO;
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
/// Copies of committed actors in their own file or another open level, as modders have long made them with Package Editor's Clone Tree. A copy is a draft until Commit
/// Changes, which clones the actor and everything under it, and makes unique what UE3's editor makes unique when it duplicates an actor: its NetIndexes
/// and its lights' GUIDs (ULightComponent::PostDuplicate). Everything else is copied as it is, and what it refers to outside itself (meshes, materials,
/// other actors) is shared. Unlike UE3's duplicate, whose text round-trip leaves a copy with no light-maps, that includes its meshes' baked lighting,
/// which is its source's until the level is baked again (the commit marks it changed since the bake). A copy starts as its source is shown, with its
/// uncommitted edits (its transform, its meshes' lighting, its light's settings), which are pending on the copy as they were on its source: the commit
/// clones its source as it's saved, then writes the copy's edits onto the copy.
/// <para>
/// A copy in another file is made from a snapshot of its source's package as it is in memory when it's committed (<see cref="SourceSnapshots"/>): Copy
/// commits one into a level of its own (<see cref="ActorCapture"/>), and Paste makes copies of that copy in other levels. Either is
/// cloned there, made ready for its new level, and ported in with what it uses, as <see cref="AssetPorting"/> copies assets in. Its baked lighting is
/// dropped (its new level's lights aren't its old one's), and so are its references to other objects of its old level, which one level can't refer to in
/// another: other actors, Kismet (<see cref="DroppedReferences"/>). A volume's brush is its new level's shared empty one. A collection's member is made an
/// actor of its own, as Upgrade to Actor makes one (<see cref="CollectionMemberUpgrade.MakeActor(ExportEntry, ExportEntry, ExportEntry, Level, IEntry, bool, ActorProxy)"/>)
/// </para>
/// </summary>
public static class ActorCloning
{
    /// <summary>Why <paramref name="actor"/> can't be cloned in its own file now, or null. Spline points have a duplicate of their own (<see cref="SplineActorProxy.BeginDuplicate"/>)</summary>
    public static string Refusal(ActorProxy actor) => Refusal(actor, actor?.OwningFile);

    /// <summary>
    /// Why a copy of <paramref name="actor"/> can't be made in <paramref name="destination"/> now, or null: its own file, or another level, which it can be
    /// copied into from a read-only file. With no destination, why it can't be copied (Copy, which makes a copy in a level of its own: <see cref="ActorCapture"/>)
    /// </summary>
    public static string Refusal(ActorProxy actor, OpenLevelFile destination)
    {
        if (actor is null || actor.IsDisposed) return "Select an actor first.";
        // (out of the scene until its deletion is committed or undone)
        if (actor.PendingDeletion is not null) return "It's being deleted: undo the deletion to copy it, or copy it before deleting it.";
        // (a copied actor's view, which is pasted from, isn't shown)
        if (actor.OwningFile is not { IsCapture: true } && actor.Editor?.RenderContext.DrawList_3D.Contains(actor) != true) return "It isn't in the scene.";
        if (actor.OwningFile is null) return "Its file is closed.";
        bool anotherFile = destination is null || InAnotherFile(actor, destination);
        if (destination is not null)
        {
            if (anotherFile && ActorCreation.FileRefusal(destination) is { } refusal) return refusal;
            if (destination.Package.Game != actor.Package.Game) return $"It's a {actor.Package.Game} actor, and {destination.FileName} is a {destination.Package.Game} level.";
            // (another file's is FileRefusal's)
            if (!anotherFile && destination.IsReadOnly) return "Its file is read-only.";
        }
        if (!actor.Package.Game.IsLEGame()) return "Only the Legendary Edition's actors can be cloned.";
        // A placed draft is copied by its recipe, as it was made: in another level, as one placed there would be
        if (actor is { IsDraft: true, CloneSource: null })
        {
            return destination is not null && anotherFile ? ActorCreation.Refusal(destination, actor.ClassName, actor.Editor.RenderContext.PackageCache)
                : ActorCreation.HasRecipe(actor.ClassName) ? null : $"There's no recipe for a new {actor.ClassName}.";
        }
        // A copy's copy is another copy of the same saved actor, or member
        var saved = actor.Export ?? actor.CloneSource;
        // However it's shown: as an actor, or as a light collection's member
        if (saved.IsA("DominantDirectionalLight") || saved.IsA("DominantDirectionalLightComponent")) return "A level has only one dominant directional light.";
        // A member's copy is added to its collection, as Package Editor's Clone adds one
        if (actor.MemberCollection is { } collection)
        {
            // Drawn from its component as it's saved, which a copy is made of, and in its collection, which it's added to
            if (ActorDeletionPlan.MembershipProblem(saved, collection) is { } problem) return problem;
            if (collection.Parent is not ExportEntry { ClassName: "Level" }) return "Its collection isn't one of its level's actors.";
            if (!anotherFile) return null;
            // Another level's copy can't be one of its collection's: it's an actor, as Upgrade to Actor would make it. (Its references to other
            // components are links, which are dropped: SharedComponentRefusal)
            return destination is null ? CollectionMemberUpgrade.OriginRefusal(actor) : CollectionMemberUpgrade.ActorRefusal(actor, destination, out _);
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
        return anotherFile ? SharedComponentRefusal(saved, (ExportEntry)saved.Parent) : null;
    }

    /// <summary>
    /// Whether a copy of <paramref name="actor"/> in <paramref name="destination"/> is made in another package than the one it's made from (its saved
    /// actor's or member's, or a placed draft's own), so it's made for its new level: a copy into a copied actor's level (<see cref="ActorCapture"/>), a
    /// paste from one, and a duplicate of a paste, which is another copy of the copied actor
    /// </summary>
    internal static bool InAnotherFile(ActorProxy actor, OpenLevelFile destination) =>
        destination is not null && destination.Package != ((actor.Export ?? actor.CloneSource)?.FileRef ?? actor.Package);

    /// <summary>
    /// Why <paramref name="saved"/>, an actor, can't be copied out of <paramref name="level"/>, or null: its own properties refer to another actor's
    /// component as one of its own (as some shipping actors do: LE2 BioA_Nor_120's StaticMeshActor_22 is drawn with StaticMeshActor_254's mesh), which a
    /// copy in another level couldn't share. A component's reference to another actor's (a ShadowParent) is a link to it, which is dropped as a reference
    /// to another actor is (<see cref="DroppedReferences"/>)
    /// </summary>
    private static string SharedComponentRefusal(ExportEntry saved, ExportEntry level)
    {
        var tree = Tree(saved).ToHashSet();
        var properties = saved.GetProperties();
        //(its own, emptied in a copy)
        properties.RemoveNamedProperty("Attached");
        ExportEntry shared = null;
        foreach (var property in properties)
        {
            IEnumerable<int> targets = property switch
            {
                ObjectProperty o => [o.Value],
                ArrayProperty<ObjectProperty> a => a.Select(o => o.Value),
                _ => []
            };
            shared = targets.Select(uIndex => LevelObject(saved.FileRef, uIndex, tree, level)).FirstOrDefault(target => target?.IsA("ActorComponent") == true);
            if (shared is not null) break;
        }
        return shared is null ? null
            : $"It uses {shared.ObjectName.Instanced}, {OwnerOf(shared, level)?.ObjectName.Instanced ?? "another actor"}'s {shared.ClassName}, as its own, which a copy in another level couldn't share.";
    }

    /// <summary>
    /// The objects a copy of <paramref name="actor"/> in another level would no longer refer to: its level's other actors and their components, Kismet
    /// (<see cref="LevelReferences"/>)
    /// </summary>
    public static IReadOnlyList<ExportEntry> DroppedReferences(ActorProxy actor)
    {
        if ((actor.Export ?? actor.CloneSource) is not { } saved || saved.Parent is not ExportEntry parent) return [];
        // A member's level is its collection's
        var level = parent.ClassName == "Level" ? parent : parent.Parent as ExportEntry;
        return level is null ? [] : LevelReferences(saved, level);
    }

    /// <summary>Whether <paramref name="actor"/>'s meshes have baked lighting (light or shadow maps), which a copy in another level doesn't (<see cref="PrepareForAnotherLevel"/>)</summary>
    internal static bool HasBakedLighting(ActorProxy actor) => (actor.Export ?? actor.CloneSource) is { } saved
        && Tree(saved).Any(e => e.IsA("StaticMeshComponent") && ObjectBinary.From(e) is StaticMeshComponent mesh && mesh.LODData.Any(lod =>
            lod.LightMap is { LightMapType: not ELightMapType.LMT_None } || lod.ShadowMaps.Length > 0 || lod.ShadowVertexBuffers.Length > 0));

    /// <summary>
    /// The objects of <paramref name="level"/> outside <paramref name="root"/> (an actor, or a collection's member) and what it owns that they refer to:
    /// other actors, their components, Kismet. What a copy of it in another level would no longer refer to, since the copy is made ready for its new
    /// level by the same changes first (<see cref="ForAnotherLevel"/>), and then has these dropped (<see cref="PrepareForAnotherLevel"/>)
    /// </summary>
    internal static List<ExportEntry> LevelReferences(ExportEntry root, ExportEntry level)
    {
        var tree = CopiedTree(root).ToHashSet();
        var found = new List<ExportEntry>();
        int Find(IMEPackage package, int uIndex)
        {
            if (LevelObject(package, uIndex, tree, level) is { } target) found.Add(target);
            return uIndex;
        }
        foreach (var export in tree)
        {
            var (properties, binary, _) = ForAnotherLevel(export, root, tree, level, out _);
            AssetPorting.ForEachReference(properties, uIndex => Find(export.FileRef, uIndex));
            binary?.ForEachUIndex(export.Game, new AssetPorting.Renumber(uIndex => Find(export.FileRef, uIndex)));
        }
        return [.. found.Distinct()];
    }

    //what a copy of root in another level is made of: root and everything under it, but its baked shadow maps, which its new level's lights didn't make
    private static IEnumerable<ExportEntry> CopiedTree(ExportEntry root) => Tree(root).Where(e => e.ClassName is not ("ShadowMap1D" or "ShadowMap2D"));

    /// <summary>
    /// <paramref name="export"/>'s properties and binary (one of <paramref name="tree"/>'s, the copied tree of <paramref name="root"/>) as a copy of it in
    /// another level has them, before its references to <paramref name="level"/>'s other objects are dropped: without what it has of its own in their
    /// place, or no longer has. Nothing is attached to it (as <see cref="Materialize"/> has it). Its baked lighting goes, since its new level's lights
    /// aren't its old one's: its meshes' light and shadow maps, as a bake allocates none (LightmassMappingAllocator.EnsureLods), and IrrelevantLights. A
    /// decal's receivers go, which its commit projects anew. A volume's brush, a model of its level's, goes, to be its new level's shared empty one
    /// </summary>
    /// <param name="replacesBrush">Whether its Brush was a model of the level, which was taken out</param>
    /// <returns>Its properties and binary, and whether the binary was changed, so must be written</returns>
    private static (PropertyCollection Properties, ObjectBinary Binary, bool BinaryChanged) ForAnotherLevel(ExportEntry export, ExportEntry root,
        HashSet<ExportEntry> tree, ExportEntry level, out bool replacesBrush)
    {
        var properties = export.GetProperties();
        var binary = ObjectBinary.From(export);
        bool binaryChanged = false;
        if (export == root) properties.RemoveNamedProperty("Attached");
        switch (binary)
        {
            case StaticMeshComponent mesh:
                foreach (var lod in mesh.LODData)
                {
                    lod.LightMap = new LightMap();
                    lod.ShadowMaps = [];
                    lod.ShadowVertexBuffers = [];
                }
                properties.RemoveNamedProperty("IrrelevantLights");
                binaryChanged = true;
                break;
            case DecalComponent decal:
                decal.StaticReceivers = [];
                properties.RemoveNamedProperty("DecalReceivers");
                binaryChanged = true;
                break;
        }
        replacesBrush = LevelObject(export.FileRef, properties.GetProp<ObjectProperty>("Brush")?.Value ?? 0, tree, level) is { ClassName: "Model" };
        if (replacesBrush) properties.RemoveNamedProperty("Brush");
        return (properties, binary, binaryChanged);
    }

    //the object of level, outside tree, that uIndex is, or null
    private static ExportEntry LevelObject(IMEPackage package, int uIndex, HashSet<ExportEntry> tree, ExportEntry level)
    {
        if (uIndex <= 0 || !package.TryGetUExport(uIndex, out var target) || tree.Contains(target)) return null;
        for (IEntry outer = target; outer is not null; outer = outer.Parent)
        {
            if (outer == level) return target;
        }
        return null;
    }

    //the actor of level that owns target, one of its objects
    private static IEntry OwnerOf(ExportEntry target, ExportEntry level)
    {
        IEntry owner = target;
        while (owner.Parent is { } parent && parent != level) owner = parent;
        return owner.Parent == level && owner != target ? owner : null;
    }

    //an export and everything under it
    private static IEnumerable<ExportEntry> Tree(ExportEntry root) => root.GetAllDescendants().OfType<ExportEntry>().Prepend(root);

    /// <summary>
    /// Starts cloning <paramref name="source"/>: a draft copy is drawn where it is and selected. Nothing is written to the package until a commit makes it
    /// (<see cref="Materialize"/>)
    /// </summary>
    /// <param name="offset">Where the copy starts from its source, before it's selected (so it isn't an edit of the copy)</param>
    /// <param name="destination">The level it's made in: its source's own by default, or another open level (a paste)</param>
    public static IActorDuplication BeginClone(ActorProxy source, Vector3 offset = default, OpenLevelFile destination = null) =>
        new Duplication(source, offset, destination ?? source?.OwningFile);

    /// <summary>
    /// A draft copy of <paramref name="source"/> in <paramref name="destination"/>, <paramref name="offset"/> from it, named <paramref name="name"/> (or the
    /// next of its class's names there): made as <see cref="MakeDraft"/> makes it, and not put in the scene, which is the caller's to do (or not, for a
    /// copied actor's level: <see cref="ActorCapture"/>). If it can't be made, nothing is left of it
    /// </summary>
    internal static ActorProxy MakeCopy(ActorProxy source, OpenLevelFile destination, Vector3 offset = default, string name = null)
    {
        if (Refusal(source, destination) is { } refusal) throw new InvalidOperationException($"{source?.ActorName} can't be cloned. {refusal}");
        bool anotherFile = InAnotherFile(source, destination);
        var editor = source.Editor;
        var registry = editor.RenderContext.Registry;
        // A saved actor's class is its export's (a marker's view has no record of it); a draft's is its record's. In another level, a member's copy
        // is an actor of the class it was cooked from, and a placed draft's is of the class one placed there would be
        IEntry actorClass = null;
        bool becomesActor = anotherFile && source.MemberCollection is not null;
        if (becomesActor) CollectionMemberUpgrade.ActorRefusal(source, destination, out actorClass);
        else if (anotherFile && source is { IsDraft: true, CloneSource: null })
            ActorCreation.Refusal(destination, source.ClassName, editor.RenderContext.PackageCache, out actorClass);
        actorClass ??= (source.Export ?? source.CloneSource)?.Class ?? registry[source.Id].Class;
        var package = destination.Package;
        string className = becomesActor ? actorClass.ObjectName.Name : source.ClassName;
        var record = registry.CreateDraft(package, actorClass, name ?? registry.ProposeName(package, className), isSpline: false, isLoft: false);
        ActorProxy copy = null;
        try
        {
            copy = MakeDraft(editor, record, source, destination);
            copy.Location += offset;
            return copy;
        }
        catch
        {
            copy?.Dispose();
            registry.Discard(record.Id);
            throw;
        }
    }

    private sealed class Duplication : IActorDuplication
    {
        private readonly ActorProxy source;
        private readonly ActorProxy selection;
        //whether the copy is in another level than its source's view, which isn't a duplicate wherever it is
        private readonly bool anotherLevel;
        private bool finished;

        public ActorProxy Copy { get; }

        public Duplication(ActorProxy source, Vector3 offset, OpenLevelFile destination)
        {
            Copy = MakeCopy(source, destination, offset);
            this.source = source;
            anotherLevel = destination != source.OwningFile;
            var editor = source.Editor;
            selection = editor.RenderContext.SelectedActor;
            try
            {
                using var edit = new SceneEdit(editor);
                edit.Attach(Copy);
                edit.Select(Copy);
                edit.Complete();
            }
            catch
            {
                Copy.Dispose();
                editor.RenderContext.Registry.Discard(Copy.Id);
                throw;
            }
        }

        public void Follow() => ObjectDisposedException.ThrowIf(finished, this);

        public IUndoAction Complete()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            // (in another level it's a copy wherever it is)
            if (!anotherLevel && Copy.SnapshotTransform().Equals(source.SnapshotTransform()))
            {
                Cancel();
                return null;
            }
            IUndoAction step;
            try
            {
                step = new ActorCreation.PlaceAction(Copy, selection,
                    anotherLevel ? $"Paste {source.ActorName} into {Copy.OwningFile.FileName}" : $"Duplicate {source.ActorName}");
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
    /// copied as its component: a light as a new light of its values, a mesh by a stand-in of it. In another level, a copy's values have none of its
    /// source's references, which are to its source's objects, and it refers to its assets as another file's (<see cref="AssetReference.Of"/>)
    /// </summary>
    private static ActorProxy MakeDraft(IActorEditorContext editor, ActorRecord record, ActorProxy source, OpenLevelFile destination)
    {
        ActorProxy copy;
        bool anotherFile = InAnotherFile(source, destination);
        if (source is { IsDraft: true, CloneSource: null }) copy = ActorCreation.CopyDraft(editor, record, source);
        else
        {
            var saved = source.Export ?? source.CloneSource;
            var memberOf = source.MemberCollection;
            // In another level, a member is an actor of its own
            var collection = anotherFile ? null : memberOf;
            // A member is its component, with no actor's values
            var properties = memberOf is null ? saved.GetCondensedProperties() : [];
            var decalComponent = source is DecalActorProxy ? EffectiveLightingProperties.ResolveReference(saved, "Decal", editor.PackageCache, properties) : null;
            if (anotherFile) properties = WithoutReferences(properties);
            copy = source switch
            {
                // A member light's component is the member
                LightActorProxy or StaticLightCollectionComponentProxy => new LightActorProxy(editor, record, properties,
                    memberOf is null ? LightActorProxy.GetLightComponent(saved) : saved) { CloneSource = saved, CloneCollection = collection },
                // Projected where it's put, as a placed decal is, from its source's component, with its source's material and (in its own level) its Filter
                DecalActorProxy => decalComponent is not null
                    ? new DecalActorProxy(editor, record, properties, EditableDecal.MaterialOf(decalComponent, editor.PackageCache, record.Package), decalComponent,
                        keepsFilter: !anotherFile) { CloneSource = saved }
                    : throw new InvalidOperationException($"{source.ActorName} has no decal component."),
                EditorActorProxy => new EditorActorProxy(editor, record, properties, ActorClassification.Get(saved)) { CloneSource = saved },
                _ => new ActorCloneProxy(editor, record, properties, source, saved, isMember: memberOf is not null) { CloneSource = saved, CloneCollection = collection }
            };
        }
        try
        {
            // A copy is drawn from its saved actor's own objects. One its source is drawn with that isn't (an archetype's) it wouldn't have, nor
            // be seen by, nor be clicked on
            if (copy is ActorCloneProxy clone && source.Components.Where(c => c is MeshComponentProxy or BrushComponentProxy && c is not SplineMeshComponentProxy)
                    .Select(c => c.CopiedFrom ?? c.Export).FirstOrDefault(component => !clone.Draws(component)) is { } missing)
                throw new InvalidOperationException($"{source.ActorName} can't be cloned: it's drawn with {missing.ObjectName.Instanced}, which isn't one of its own objects, so a copy wouldn't have it.");
            CarryEdits(source, copy, destination);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
        return copy;
    }

    //values without references, which are to another package's objects (as EffectiveLightingProperties.ReadValues has a component's): a draft's values are
    //shown, and its commit writes the copy made for its new level (PrepareForAnotherLevel)
    private static PropertyCollection WithoutReferences(PropertyCollection properties)
    {
        var values = properties.DeepClone();
        values.RemoveAll(p => p is ObjectProperty or ArrayProperty<ObjectProperty> or DelegateProperty);
        //(those left are in structs)
        AssetPorting.ForEachReference(values, _ => 0);
        return values;
    }

    /// <summary>
    /// Puts <paramref name="source"/>'s transform and uncommitted edits on <paramref name="copy"/>, in <paramref name="destination"/>, whose saved values
    /// are the same as its: its meshes' lighting and chosen meshes (as its package refers to them), its light's settings, its decal's settings (in another level than its source's view, with its own
    /// Filter, whose actors are the source's level's, and its material as its new package refers to it)
    /// </summary>
    private static void CarryEdits(ActorProxy source, ActorProxy copy, OpenLevelFile destination)
    {
        copy.OwningFile = destination;
        copy.RestoreTransform(source.SnapshotTransform());
        if (copy is LightActorProxy light && source is LightActorProxy { Light.HasChanges: true } sourceLight) light.Light.Seed(sourceLight.Light);
        if (copy is DecalActorProxy { Decal: { } decal } && source is DecalActorProxy { Decal: { HasChanges: true } sourceDecal })
        {
            decal.Restore(destination.Package == source.Package ? sourceDecal.Settings : sourceDecal.Settings with
            {
                Filter = decal.Settings.Filter,
                Material = sourceDecal.Settings.Material?.In(destination.Package, copy.Editor.PackageCache)
            });
        }
        foreach (var mesh in copy.Components.OfType<MeshComponentProxy>())
        {
            // A copy of a saved actor's mesh is drawn from that actor's component; a placed draft's copy's is the same component of its recipe as its source's
            var counterpart = mesh.CopiedFrom is { } component
                ? source.MeshDrawing(component)
                : source.Components.OfType<MeshComponentProxy>().FirstOrDefault(m => m.ActorPropertyName == mesh.ActorPropertyName);
            bool lit = counterpart?.ExistingLighting is { HasChanges: true };
            if (lit) mesh.Lighting.Seed(counterpart.ExistingLighting);
            // Drawn with them, as a mesh is redrawn when its lighting is edited (LevelEditor.ApplyActorLightingEdits): its preview reads them into the
            // properties it's drawn with. Once, with its chosen mesh and materials (as its new package refers to them) if it has them
            if (counterpart is StaticMeshComponentProxy pending && (pending.HasChosenMesh || pending.HasChosenMaterials))
            {
                var copied = mesh as StaticMeshComponentProxy;
                if (pending.ChosenMesh is { } chosen && copied is not { CanChooseMesh: true })
                    throw new InvalidOperationException($"{source.ActorName} can't be copied with its chosen mesh, {chosen.Name}: commit it first.");
                if (pending.HasChosenMaterials && copied is not { CanChooseMaterials: true })
                    throw new InvalidOperationException($"{source.ActorName} can't be copied with its chosen materials: commit them first.");
                var cache = copy.Editor.PackageCache;
                copied.ShowChosen(pending.ChosenMesh?.In(destination.Package, cache), pending.ChosenMaterials?.Select(m => m?.In(destination.Package, cache)).ToList());
            }
            else if (lit)
            {
                mesh.RefreshPreviewJoiningScene(mesh.Export.FileRef, new HashSet<int>(), force: true);
                mesh.UpdateLocalToWorld();
            }
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
        foreach (var export in Tree(copy))
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

    /// <summary>
    /// For a commit, the packages that copies in other files are made from (<see cref="MaterializeInAnotherFile"/>), as they are in memory before it: each
    /// saved into memory and opened again, so that a copy can be made ready for its new level there without touching its source. Taken before the commit's
    /// edit scope opens, since a package can't be saved while one is open on it, and a copy's source is often in the same commit
    /// </summary>
    internal sealed class SourceSnapshots : IDisposable
    {
        private readonly Dictionary<IMEPackage, IMEPackage> snapshots = [];

        /// <param name="copies">The commit's drafts: the packages of those that are copies of another file's actors are taken</param>
        /// <param name="sources">The commit's cache of other files, which what a copy uses is ported from (<see cref="AssetPorting"/>)</param>
        /// <param name="alsoSnapshot">Other packages an asset may be copied from (<see cref="OfFile"/>): a copied placed draft's own, whose asset may be
        /// one of its package's, which only its package in memory may have</param>
        internal SourceSnapshots(IEnumerable<ActorProxy> copies, PackageCache sources, IEnumerable<IMEPackage> alsoSnapshot = null)
        {
            Sources = sources;
            try
            {
                //(as a decal migration copies a package it changes)
                foreach (var package in copies.Where(c => c.CloneSource is { } source && source.FileRef != c.Package).Select(c => c.CloneSource.FileRef)
                             .Concat(alsoSnapshot ?? []).Distinct())
                    snapshots[package] = DecalPackageMigration.Clone(package);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The commit's cache of other files, carried to where the copies are made, which ports what they use from it</summary>
        internal PackageCache Sources { get; }

        internal IMEPackage Of(IMEPackage package) => snapshots.GetValueOrDefault(package)
            ?? throw new InvalidOperationException($"{TemporaryPackages.FileName(package.FilePath)} wasn't snapshotted for the commit.");

        /// <summary>
        /// The snapshot of the file at <paramref name="filePath"/>, or null: what an asset of a copy's source's file is copied from (a decal's changed
        /// material), as the copy itself is, rather than the file on disk, which may not have it yet
        /// </summary>
        internal IMEPackage OfFile(string filePath) =>
            snapshots.FirstOrDefault(s => string.Equals(s.Key.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).Value;

        public void Dispose()
        {
            foreach (var snapshot in snapshots.Values) snapshot.Dispose();
            snapshots.Clear();
        }
    }

    /// <summary>
    /// For a commit of <paramref name="copy"/>, a copy of another file's actor or collection member (its <see cref="ActorProxy.CloneSource"/>), inside
    /// its package edit: makes it one of <paramref name="level"/>'s actors, named <paramref name="name"/>. In a snapshot of its source's package
    /// (<see cref="SourceSnapshots"/>) its source is cloned (as <see cref="CloneTree"/> clones it), a member made an actor around its component, as
    /// Upgrade to Actor makes one, and the copy made ready for another level (<see cref="PrepareForAnotherLevel"/>); then it's ported in with what it
    /// uses, as an asset is (<see cref="AssetPorting.Port"/>). A volume's brush is <paramref name="level"/>'s shared empty one
    /// </summary>
    /// <param name="levelBinary"><paramref name="level"/>'s, which the copy is added to the Actors of. The caller writes it</param>
    /// <param name="copies">The copy's objects, by its source's objects (in its source's package) they're copies of</param>
    internal static ExportEntry MaterializeInAnotherFile(ActorProxy copy, ExportEntry level, Level levelBinary, NameReference name, SourceSnapshots snapshots,
        out Dictionary<ExportEntry, ExportEntry> copies)
    {
        var source = copy.CloneSource;
        string file = TemporaryPackages.FileName(source.FileRef.FilePath);
        if (source.FileRef.GetEntry(source.UIndex) != source) throw new InvalidOperationException($"{source.ObjectName.Instanced} is no longer in {file}, so it can't be copied.");
        var snapshot = snapshots.Of(source.FileRef);
        // A member's level is its collection's
        bool isMember = source.Parent is ExportEntry { ClassName: not "Level" };
        var sourceLevel = (ExportEntry)(isMember ? source.Parent.Parent : source.Parent);
        ExportEntry Saved(ExportEntry original) => snapshot.TryGetUExport(original.UIndex, out var saved) && saved.InstancedFullPath == original.InstancedFullPath
            && saved.ClassName == original.ClassName ? saved : throw new InvalidOperationException($"{original.InstancedFullPath} isn't in a snapshot of {file}.");
        var clone = CloneTree(Saved(source), isMember ? source.ObjectName : name, out var cloned);
        var root = clone;
        if (isMember)
        {
            var snapshotLevel = Saved(sourceLevel);
            string className = CollectionMemberUpgrade.Origin(clone).ClassName;
            var actorClass = ActorCreation.FindClass(snapshot, className, copy.Editor.PackageCache)
                ?? throw new InvalidOperationException($"{snapshot.Game} has no {className} class.");
            // Its Actors are the snapshot's, which isn't written
            root = CollectionMemberUpgrade.MakeActor(clone, Saved((ExportEntry)source.Parent), snapshotLevel, snapshotLevel.GetBinaryData<Level>(), actorClass,
                copy is ILightProxy, copy);
            root.ObjectName = name;
        }
        var brushes = PrepareForAnotherLevel(root, Saved(sourceLevel));
        // Out of its level, so that what porting finds it uses (EntryImporter.GetAllReferencesOfExport, which follows the outers of what's referred to, and
        // so of its own objects) is its own, not everything its old level has
        root.Parent = null;
        int exportsBefore = level.FileRef.ExportCount;
        ExportEntry ported;
        try
        {
            AssetPorting.PortUnimportable(Tree(root), level.FileRef, snapshots.Sources);
            ported = AssetPorting.Port(root, level.FileRef, snapshots.Sources, level);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Made at Copy (ActorCapture), or by a paste's commit, which is the first time it can be seen: until the paste is deleted, nothing can be
            // committed with it
            throw new InvalidOperationException(copy.OwningFile is { IsCapture: true }
                ? $"{source.ObjectName.Instanced} can't be copied: {error.Message}"
                : $"{copy.DisplayText}, pasted from a copy of {source.ObjectName.Instanced}, can't be made: {error.Message}\n\nDelete it to commit the rest.", error);
        }
        // A new actor of the level's, not one it had at that path (its name is made unique: Materialize)
        if (ported.Parent != level || ported.UIndex <= exportsBefore)
            throw new InvalidOperationException($"{source.ObjectName.Instanced}'s copy wasn't made as a new actor of {level.InstancedFullPath}.");
        levelBinary.Actors.Add(ported.UIndex);
        // Its objects, by their paths below it, which are their clones' below the clone
        var byPath = Tree(ported).ToDictionary(export => PathBelow(export, ported), StringComparer.OrdinalIgnoreCase);
        // (one, which the actor and its component share)
        var emptyBrush = brushes.Count == 0 ? null : BrushProxy.EmptyBrush(level, ported);
        foreach (string path in brushes) byPath[path].WriteProperty(new ObjectProperty(emptyBrush, "Brush"));
        copies = [];
        var prepared = Tree(root).ToHashSet();
        foreach (var original in Tree(source))
        {
            if (cloned.TryGetValue(Saved(original), out var inClone) && prepared.Contains(inClone) && byPath.TryGetValue(PathBelow(inClone, root), out var made))
                copies[original] = made;
        }
        return ported;
    }

    //export's path below root, one of its outers ("" for root itself)
    private static string PathBelow(IEntry export, IEntry root)
    {
        var names = new List<string>();
        for (var entry = export; entry != root; entry = entry.Parent ?? throw new ArgumentException($"{export.InstancedFullPath} isn't under {root.InstancedFullPath}."))
            names.Add(entry.ObjectName.Instanced);
        names.Reverse();
        return string.Join('.', names);
    }

    /// <summary>
    /// Makes <paramref name="root"/>, a copy of an actor of <paramref name="level"/> in a snapshot of its package, ready to be one of another level's
    /// actors (<see cref="ForAnotherLevel"/>): its shadow map objects are taken out of it, so they aren't ported, and its references to the level's
    /// other objects (<see cref="LevelReferences"/>), which one level can't refer to in another, are dropped
    /// </summary>
    /// <returns>The paths below it of its objects whose Brush was a model of the level, to be given their new level's empty one</returns>
    private static List<string> PrepareForAnotherLevel(ExportEntry root, ExportEntry level)
    {
        var tree = CopiedTree(root).ToHashSet();
        foreach (var shadowMap in Tree(root).Where(e => !tree.Contains(e)).ToList()) shadowMap.Parent = null;
        var brushes = new List<string>();
        foreach (var export in tree)
        {
            var (properties, binary, binaryChanged) = ForAnotherLevel(export, root, tree, level, out bool replacesBrush);
            if (replacesBrush) brushes.Add(PathBelow(export, root));
            bool InOldLevel(int uIndex) => LevelObject(export.FileRef, uIndex, tree, level) is not null;
            DropReferences(properties, InOldLevel);
            binary?.ForEachUIndex(export.Game, new AssetPorting.Renumber(uIndex =>
            {
                if (!InOldLevel(uIndex)) return uIndex;
                binaryChanged = true;
                return 0;
            }));
            if (binaryChanged) export.WritePropertiesAndBinary(properties, binary);
            else export.WriteProperties(properties);
        }
        return brushes;
    }

    //takes out the references that dropped says to, as LevelReferences finds them: an array's entry is removed, and anything else refers to nothing
    private static void DropReferences(PropertyCollection properties, Func<int, bool> dropped) =>
        AssetPorting.ForEachReference(properties, uIndex => dropped(uIndex) ? 0 : uIndex, dropped);
}
