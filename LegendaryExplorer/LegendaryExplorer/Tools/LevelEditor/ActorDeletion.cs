using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// What a commit changes, besides deleting an actor, that takes away references to it: the entries it trashes with it (other pending deletions'),
/// and the properties it writes to other exports (a spline point's neighbours', without their connections to it)
/// </summary>
public sealed record DeletionRepairs(IReadOnlySet<IEntry> Trashed, IReadOnlyDictionary<ExportEntry, PropertyCollection> WrittenProperties)
{
    public static DeletionRepairs None { get; } = new(new HashSet<IEntry>(), new Dictionary<ExportEntry, PropertyCollection>());
}

/// <summary>Reference inspection and commit repairs for supported actor ownership recipes. Preparation never changes the package.</summary>
public sealed class ActorDeletionPlan
{
    public ExportEntry Actor { get; }
    public ExportEntry Level { get; }
    public IReadOnlyList<string> Blockers => blockers;
    private readonly List<string> blockers;
    public IReadOnlyList<ExportEntry> Entries { get; }
    public string Repairs => $"Committing removes it from the level{(cleansDestroyedContent ? " and from the world's destroyed-actor list" : "")}, and trashes it"
                             + (Entries.Count > 1 ? $" with the {Entries.Count - 1} objects it owns." : ".")
                             // Light-maps and shadow-maps refer to a light by its GUIDs, not by reference
                             + (LightClasses.Contains(Actor.ClassName) ? " What it lit stays lit by it in the level's baked lighting until a rebake." : "");
    // Whether BioWorldInfo's ClientDestroyedActorContent refers to it, which TrashEntries cleans. Found by CheckReferences, which makes the plan with Inspect
    private bool cleansDestroyedContent;
    private readonly (ExportEntry Entry, int Index, byte[] Header, byte[] Data)[] snapshots;
    private readonly ExportEntry worldInfo;
    private readonly bool canCleanDestroyedContent;

    private static IReadOnlyList<string> LightClasses => LightActorProxy.StandaloneClasses;
    private static readonly string[] SupportedClasses = ["Note", "TargetPoint", "StaticMeshActor", "InterpActor", "SplineActor", "SplineLoftActor", .. LightClasses];
    private static bool Supports(MEGame game, string className) => game.IsLEGame() && SupportedClasses.Contains(className);

    /// <summary>Why an actor can't be taken out of the scene now, or null: all a draft (which has nothing in its package to check) needs</summary>
    public static string RemovalRefusal(ActorProxy actor)
    {
        if (actor is null || actor.IsDisposed) return "Select an actor first.";
        if (actor.Editor?.RenderContext.DrawList_3D.Contains(actor) != true) return "It isn't in the scene.";
        if (actor.OwningFile is not { IsReadOnly: false }) return "Its file is read-only or closed.";
        if (actor.Editor.RenderContext.IsDragging || actor.Editor.RenderContext.IsEditingScene) return "Finish the current scene operation first.";
        return null;
    }

    /// <summary>Why a committed actor can't be deleted now, or null: <see cref="RemovalRefusal"/>, and whether its class's deletion is supported</summary>
    public static string Refusal(ActorProxy actor)
    {
        if (RemovalRefusal(actor) is { } refusal) return refusal;
        if (!Supports(actor.Package.Game, actor.ClassName))
            return $"Committed deletion supports only the Legendary Edition's {string.Join(", ", SupportedClasses)}.";
        if (actor.Base is not null || actor.Attached.Count != 0) return "Attached actors need a separate detach operation.";
        return null;
    }

    private ActorDeletionPlan(ExportEntry actor, ExportEntry level, List<ExportEntry> entries, List<string> blockers, ExportEntry worldInfo,
        bool canCleanDestroyedContent)
    {
        Actor = actor;
        Level = level;
        this.blockers = blockers;
        Entries = entries.AsReadOnly();
        snapshots = entries.Select(e => (e, e.UIndex, Header(e), e.DataReadOnly.ToArray())).ToArray();
        this.worldInfo = worldInfo;
        this.canCleanDestroyedContent = canCleanDestroyedContent;
    }

    internal bool MatchesSnapshot()
    {
        var current = Actor.GetAllDescendants().Prepend(Actor).ToHashSet();
        return current.SetEquals(Entries) && snapshots.All(s => Actor.FileRef.GetEntry(s.Index) == s.Entry
            && Header(s.Entry).SequenceEqual(s.Header) && s.Entry.DataReadOnly.SequenceEqual(s.Data));
    }

    /// <summary>An export's header without its DataOffset, which saving the package rewrites without changing the export</summary>
    private static byte[] Header(ExportEntry export)
    {
        byte[] header = export.Header;
        Array.Clear(header, ExportEntry.OFFSET_DataOffset, sizeof(int));
        return header;
    }

    private static bool IsLightComponent(ExportEntry e) => e.ClassName is "PointLightComponent" or "SpotLightComponent" or "DirectionalLightComponent";

    private static List<ExportEntry> CollectOwnedEntries(ExportEntry actor, List<string> blockers)
    {
        var entries = new List<ExportEntry> { actor };
        bool meshActor = actor.ClassName is "StaticMeshActor" or "InterpActor";
        bool splineActor = actor.ClassName is "SplineActor" or "SplineLoftActor";
        bool lightActor = LightClasses.Contains(actor.ClassName);
        foreach (var child in actor.GetAllDescendants())
        {
            // Ownership comes from the outer chain, never from an asset/property reference. Unknown
            // children (including meshes, materials and textures) must not be swept up by recursive trash.
            bool supported = child is ExportEntry e && !e.IsDefaultObject && !e.IsArchetype && !e.IsClass
                && (meshActor && (e.Parent == actor && e.ClassName is ("StaticMeshComponent" or "DynamicLightEnvironmentComponent")
                        || e.Parent?.ClassName == "StaticMeshComponent" && e.ClassName is ("RB_BodyInstance" or "ShadowMap1D" or "ShadowMap2D"))
                    // A point's outgoing connections: their curves and loft meshes (and the meshes' light/shadow data)
                    || splineActor && (e.Parent == actor && e.ClassName is ("SplineComponent" or "SplineMeshComponent" or "DynamicLightEnvironmentComponent")
                        || e.Parent?.ClassName == "SplineMeshComponent" && e.ClassName is ("RB_BodyInstance" or "ShadowMap1D" or "ShadowMap2D"))
                    // A light's component, and its light function
                    || lightActor && (e.Parent == actor && IsLightComponent(e) || e.Parent is ExportEntry owner && IsLightComponent(owner) && e.ClassName == "LightFunction"));
            if (supported) entries.Add((ExportEntry)child);
            else blockers.Add($"#{child.UIndex} {child.InstancedFullPath}: unsupported owned subobject.");
        }
        if (lightActor)
        {
            foreach (var prop in actor.GetProperties().Where(p => p.Name == "LightComponent"))
            {
                if (prop is not ObjectProperty reference || reference.Value != 0
                    && !entries.Any(e => e.Parent == actor && e.UIndex == reference.Value && IsLightComponent(e)))
                    blockers.Add("LightComponent must refer to a light component owned by this actor.");
            }
        }
        if (meshActor)
        {
            var props = actor.GetProperties();
            foreach (string name in new[] { "StaticMeshComponent", "CollisionComponent", "LightEnvironment" })
            {
                foreach (var prop in props.Where(p => p.Name == name))
                {
                    if (prop is not ObjectProperty reference || reference.Value != 0
                        && !entries.Any(e => e.Parent == actor && e.UIndex == reference.Value
                            && e.ClassName == (name == "LightEnvironment" ? "DynamicLightEnvironmentComponent" : "StaticMeshComponent")))
                        blockers.Add($"{name} must refer to a supported component owned by this actor.");
                }
            }
            foreach (var prop in props.Where(p => p.Name == "Components"))
            {
                if (prop is not ArrayProperty<ObjectProperty> components
                    || components.Any(c => c.Value != 0 && !entries.Any(e => e.Parent == actor && e.UIndex == c.Value)))
                    blockers.Add("Components must contain only supported components owned by this actor.");
            }
        }
        return entries;
    }

    /// <summary>
    /// <see cref="Prepare(ExportEntry, ExportEntry, DeletionRepairs)"/>, with the scan of the package for references away from the dispatcher, refusing
    /// results if the package changed during the scan (<see cref="IMEPackage.ChangeCount"/>): another tool can still edit it meanwhile. A scan that
    /// failed because of such an edit (its exports changing as they're read) is refused the same way. Only the scan runs away from the dispatcher:
    /// it reads exports' headers, properties and binaries, not the package's lookup tree, which an edit invalidates and its next reader rebuilds
    /// unsynchronized (as <see cref="Inspect"/> reads it, finding the actor's owned entries). Without the shared package cache, which ChangeCount
    /// needs to see edits, the scan is made here instead
    /// </summary>
    public static async Task<ActorDeletionPlan> PrepareAsync(ExportEntry actor, ExportEntry level, DeletionRepairs repairs = null)
    {
        if (!MEPackageHandler.GlobalSharedCacheEnabled) return Prepare(actor, level, repairs);
        int changes = actor.FileRef.ChangeCount;
        var plan = Inspect(actor, level);
        try
        {
            await Task.Run(() => CheckReferences([plan], repairs));
        }
        catch (Exception) when (actor.FileRef.ChangeCount != changes)
        {
            //refused below, as the package changed
        }
        if (actor.FileRef.ChangeCount != changes) throw new InvalidOperationException("The package changed during reference inspection. Retry deletion.");
        return plan;
    }

    /// <summary>
    /// Scans the whole package, including undrawn objects. A reference is allowed only if the commit takes it away: its referrer is trashed with the
    /// actor, or the referrer as the commit leaves it (<paramref name="repairs"/>' written properties, the Level without the actor among its Actors,
    /// BioWorldInfo without it in ClientDestroyedActorContent, which TrashEntries cleans) refers to none of the actor's entries. A commit's own check
    /// passes no written properties, so it sees what those edits actually wrote
    /// </summary>
    public static ActorDeletionPlan Prepare(ExportEntry actor, ExportEntry level, DeletionRepairs repairs = null)
    {
        var plan = Inspect(actor, level);
        CheckReferences([plan], repairs);
        return plan;
    }

    /// <summary>Adds what blocks each of <paramref name="plans"/> (<see cref="Inspect"/>ed) because of references to it, from one scan of each package</summary>
    internal static void CheckReferences(IReadOnlyList<ActorDeletionPlan> plans, DeletionRepairs repairs)
    {
        var references = plans.SelectMany(p => p.Entries).GroupBy(e => e.FileRef)
            .SelectMany(package => package.ToList().GetEntriesThatReferenceThese())
            .ToDictionary(r => r.Key, r => r.Value);
        foreach (var plan in plans) plan.CheckReferences(references, repairs ?? DeletionRepairs.None);
    }

    /// <summary>The actor's owned entries, and what blocks deleting it besides references to them</summary>
    internal static ActorDeletionPlan Inspect(ExportEntry actor, ExportEntry level)
    {
        var blockers = new List<string>();
        if (actor is null || level is null || actor.FileRef != level.FileRef || actor.Parent != level
            || actor.IsDefaultObject || actor.IsArchetype || actor.IsClass || !Supports(actor.Game, actor.ClassName) || level.ClassName != "Level")
            throw new InvalidOperationException("This is not a supported level actor instance.");
        if (actor.FileRef.GetEntry(actor.UIndex) != actor || level.FileRef.GetEntry(level.UIndex) != level
            || actor.IsTrash())
            throw new InvalidOperationException("The actor or its Level was removed or replaced.");
        var entries = CollectOwnedEntries(actor, blockers);
        if (actor.GetProperty<ObjectProperty>("Base") is { Value: not 0 })
            blockers.Add("Attached actors need a separate detach operation.");
        var binary = level.GetBinaryData<Level>();
        if (!binary.Actors.Contains(actor.UIndex)) blockers.Add("The actor no longer belongs to its Level.Actors array.");
        if (binary.Actors.Take(2).Contains(actor.UIndex)) blockers.Add("Reserved leading Level.Actors entries cannot be deleted.");
        var worldInfo = binary.Actors.Count > 0 ? actor.FileRef.GetEntry(binary.Actors[0]) as ExportEntry : null;
        if (level.InstancedFullPath != "TheWorld.PersistentLevel" || worldInfo?.ClassName != "BioWorldInfo")
            blockers.Add("Deletion requires a PersistentLevel with a valid leading BioWorldInfo entry.");
        var destroyedContentProperties = worldInfo?.GetProperties().Where(p => p.Name == "ClientDestroyedActorContent").ToArray();
        bool canCleanDestroyedContent = destroyedContentProperties is [ArrayProperty<ObjectProperty>];
        return new ActorDeletionPlan(actor, level, entries, blockers, worldInfo, canCleanDestroyedContent);
    }

    /// <param name="references">For each of <see cref="Entries"/>, what refers to it and where (which is only reported)</param>
    private void CheckReferences(Dictionary<IEntry, Dictionary<IEntry, List<string>>> references, DeletionRepairs repairs)
    {
        var ownership = Entries.Cast<IEntry>().ToHashSet();
        var targets = Entries.Select(e => e.UIndex).ToHashSet();
        var referrers = Entries.SelectMany(target => references[target].Keys).Where(r => !ownership.Contains(r) && !repairs.Trashed.Contains(r)).Distinct();
        foreach (var referrer in referrers)
        {
            if (referrer is ExportEntry export && TryRepair(export, repairs, out var properties, out var binary)
                && !export.WouldReferenceAny(targets, properties, binary))
            {
                cleansDestroyedContent |= export == worldInfo;
                continue;
            }
            foreach (var target in Entries)
            {
                foreach (string field in references[target].GetValueOrDefault(referrer) ?? [])
                    blockers.Add($"#{referrer.UIndex} {referrer.InstancedFullPath}: {field} references {target.InstancedFullPath}");
            }
        }
    }

    /// <summary>
    /// <paramref name="referrer"/> as the commit leaves it, if the commit changes it: its properties and binary, null for what's unchanged
    /// </summary>
    private bool TryRepair(ExportEntry referrer, DeletionRepairs repairs, out PropertyCollection properties, out ObjectBinary binary)
    {
        properties = null;
        binary = null;
        if (referrer == Level)
        {
            var level = Level.GetBinaryData<Level>();
            level.Actors.RemoveAll(i => i == Actor.UIndex);
            binary = level;
            return true;
        }
        if (referrer == worldInfo && canCleanDestroyedContent)
        {
            properties = worldInfo.GetProperties();
            var trashed = Entries.Select(e => e.UIndex).ToHashSet();
            //as TrashEntries cleans it
            properties.GetProp<ArrayProperty<ObjectProperty>>("ClientDestroyedActorContent").RemoveAll(o => trashed.Contains(o.Value));
            return true;
        }
        return repairs.WrittenProperties.TryGetValue(referrer, out properties);
    }

    private static int Depth(IEntry entry)
    {
        int depth = 0;
        for (IEntry outer = entry.Parent; outer is not null; outer = outer.Parent) depth++;
        return depth;
    }

    internal void RequireAllowed()
    {
        const int listed = 10;
        if (Blockers.Count != 0)
            throw new InvalidOperationException($"Cannot delete {Actor.ObjectName}:\n{string.Join("\n", Blockers.Take(listed))}"
                                                + (Blockers.Count > listed ? $"\n...and {Blockers.Count - listed} more" : ""));
    }

    /// <summary>
    /// Deletes the planned actors, with one write of each Level and one TrashEntries of each package. Called only inside ActorCommit's
    /// PackageEditScope. Saving to disk remains a separate operation.
    /// </summary>
    internal static void Apply(IReadOnlyList<ActorDeletionPlan> plans)
    {
        //(the plans are Revalidate's, which has required each to be allowed)
        foreach (var plan in plans)
        {
            if (!plan.Actor.FileRef.IsInEditScope) throw new InvalidOperationException("Actor deletion requires a package edit scope.");
        }
        foreach (var level in plans.GroupBy(p => p.Level))
        {
            var deleted = level.Select(p => p.Actor.UIndex).ToHashSet();
            var binary = level.Key.GetBinaryData<Level>();
            binary.Actors.RemoveAll(deleted.Contains);
            level.Key.WriteBinary(binary);
        }
        foreach (var package in plans.GroupBy(p => p.Actor.FileRef))
        {
            // Materialize the order before trashing changes outers; descendants must go first.
            var trash = package.SelectMany(p => p.Entries).OrderByDescending(Depth).ToArray();
            EntryPruner.TrashEntries(package.Key, trash);
        }
    }
}

/// <summary>Reversible deletion intent, retained independently of history until committed or reset.</summary>
public sealed class PendingActorDeletion
{
    public ActorProxy Actor { get; }
    public string Status { get; private set; }
    private readonly ActorDeletionPlan prepared;
    private IDisposable retained;
    /// <summary>The other actors the deletion's undo step changes (a spline point's neighbours, without their connections to it)</summary>
    internal IReadOnlyList<ActorProxy> AlsoChanged { get; }

    private PendingActorDeletion(ActorProxy actor, ActorDeletionPlan plan, IReadOnlyList<ActorProxy> alsoChanged)
    {
        Actor = actor;
        prepared = plan;
        Status = plan.Repairs;
        AlsoChanged = alsoChanged;
    }
    /// <summary>Whether the actor and what it owns are still as they were when its deletion was prepared</summary>
    internal bool MatchesSnapshot() => prepared.MatchesSnapshot();

    /// <summary>
    /// For when another tool changed the actor or what it owns since its deletion was prepared (<see cref="MatchesSnapshot"/>). Its change wins, as
    /// it does over an actor's uncommitted edits: the deletion is dropped, and its view disposed, which orphans the undo steps that would bring back
    /// what the actor was. The caller shows the actor as the package now has it, and the actors its undo step also changed (<see cref="AlsoChanged"/>),
    /// whose changes that step alone could take back
    /// </summary>
    internal void Cancel()
    {
        Actor.OwningFile.RemoveDeletion(this);
        Actor.Dispose();
    }

    internal void Retain() => retained = Actor.RetainForHistory();
    internal void Release() { retained?.Dispose(); retained = null; }
    /// <summary>The actor's export and the subobjects it owns, which the commit trashes</summary>
    internal IReadOnlyList<ExportEntry> Entries => prepared.Entries;

    /// <summary>The intent to delete <paramref name="actor"/>, which a caller's undo step adds to and removes from its file</summary>
    /// <param name="alsoChanged">The other actors that undo step changes (<see cref="AlsoChanged"/>)</param>
    internal static PendingActorDeletion Create(ActorProxy actor, ActorDeletionPlan plan, IReadOnlyList<ActorProxy> alsoChanged = null)
    {
        plan.RequireAllowed();
        if (plan.Actor != actor.Export || plan.Level != actor.OwningFile.LevelExport
            || !plan.MatchesSnapshot())
            throw new InvalidOperationException("The actor or its owned subobjects changed during deletion preparation. Retry deletion.");
        return new PendingActorDeletion(actor, plan, alsoChanged ?? []);
    }

    /// <summary>
    /// <see cref="ActorProxy.Delete"/> for an actor with no topology of its own: a draft (with no <paramref name="plan"/>, and nothing in its package
    /// to check) taken out of the scene, or a committed actor marked for deletion. Its package is untouched either way
    /// </summary>
    internal static IUndoAction Delete(ActorProxy actor, ActorDeletionPlan plan)
    {
        IUndoAction action;
        if (actor.IsDraft)
        {
            if (ActorDeletionPlan.RemovalRefusal(actor) is { } refusal) throw new InvalidOperationException(refusal);
            action = new DraftDeletionAction(actor);
        }
        else
        {
            if (ActorDeletionPlan.Refusal(actor) is { } refusal) throw new InvalidOperationException(refusal);
            action = new DeletionAction(Create(actor, plan ?? throw new ArgumentNullException(nameof(plan))));
        }
        try { action.Redo(); return action; }
        catch { ((IDisposable)action).Dispose(); throw; }
    }

    /// <summary>
    /// A commit's own check of its <paramref name="deletions"/>, after its other writes: each actor is still as it was prepared, and nothing refers to
    /// it but what the commit trashes. One scan of each package for them all
    /// </summary>
    internal static ActorDeletionPlan[] Revalidate(IReadOnlyList<PendingActorDeletion> deletions)
    {
        // Every deletion is checked, so each shows its own status, before the first failure is thrown
        Exception failure = null;
        foreach (var deletion in deletions)
        {
            var error = deletion.Check(() =>
            {
                if (deletion.Actor.IsDisposed || deletion.Actor.OwningFile is not { IsReadOnly: false })
                    throw new InvalidOperationException("The actor's file is closed or read-only.");
                //(the editor cancels it once it applies the change: see Cancel)
                if (!deletion.MatchesSnapshot())
                    throw new InvalidOperationException("The actor or its owned subobjects changed outside the editor, which cancels its deletion.");
            });
            failure ??= error;
        }
        if (failure is not null) ExceptionDispatchInfo.Throw(failure);
        var plans = new ActorDeletionPlan[deletions.Count];
        for (int i = 0; i < deletions.Count; i++)
        {
            var deletion = deletions[i];
            int index = i;
            var error = deletion.Check(() => plans[index] = ActorDeletionPlan.Inspect(deletion.Actor.Export, deletion.Actor.OwningFile.LevelExport));
            failure ??= error;
        }
        if (failure is not null) ExceptionDispatchInfo.Throw(failure);
        try
        {
            ActorDeletionPlan.CheckReferences(plans, DeletionRepairs.None with { Trashed = deletions.SelectMany(d => d.Entries).ToHashSet<IEntry>() });
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            //the scan of the whole package for them all, which isn't any one's
            foreach (var deletion in deletions)
            {
                deletion.Status = $"The package couldn't be checked for references: {error.Message}";
                deletion.Actor.NotifyPendingChange();
            }
            throw;
        }
        for (int i = 0; i < deletions.Count; i++)
        {
            var (deletion, plan) = (deletions[i], plans[i]);
            var error = deletion.Check(() =>
            {
                plan.RequireAllowed();
                deletion.Status = plan.Repairs;
            });
            failure ??= error;
        }
        if (failure is not null) ExceptionDispatchInfo.Throw(failure);
        return plans;
    }

    /// <summary>Runs one of the deletion's checks, showing its failure as the deletion's status</summary>
    /// <returns>The failure, or null</returns>
    private Exception Check(Action check)
    {
        try
        {
            check();
            return null;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Status = error.Message;
            return error;
        }
        finally { Actor.NotifyPendingChange(); }
    }

    // A successful commit invalidates retained history for the now-trashed actor, even for non-window callers.
    internal void Accept() => Actor.Dispose();

    private sealed class DraftDeletionAction : IUndoAction, IDisposable
    {
        private readonly ActorProxy draft;
        private readonly IDisposable retained;
        private readonly ActorProxy selection;
        private readonly int sceneIndex, fileIndex;
        public DraftDeletionAction(ActorProxy draft)
        {
            this.draft = draft;
            retained = draft.RetainForHistory();
            selection = draft.Editor.RenderContext.SelectedActor;
            sceneIndex = draft.Editor.RenderContext.DrawList_3D.IndexOf(draft);
            fileIndex = draft.OwningFile.Actors.IndexOf(draft);
        }
        public string Description => $"Delete new {draft.ActorName}";
        public bool IsOrphaned => draft.IsDisposed;
        public IReadOnlyCollection<ActorId> Subjects => [draft.Id];
        public void Undo()
        {
            using var edit = new SceneEdit(draft.Editor);
            edit.Attach(draft, sceneIndex, fileIndex);
            edit.Reselect(selection);
            edit.Complete();
        }
        public void Redo()
        {
            using var edit = new SceneEdit(draft.Editor);
            edit.Detach(draft);
            edit.Complete();
        }
        public void Dispose() => retained.Dispose();
    }

    private sealed class DeletionAction : IUndoAction, IDisposable
    {
        private readonly PendingActorDeletion deletion;
        private readonly IDisposable retained;
        private readonly ActorProxy selection;
        private readonly int sceneIndex, fileIndex;
        private ActorProxy Actor => deletion.Actor;
        public DeletionAction(PendingActorDeletion deletion)
        {
            this.deletion = deletion;
            retained = Actor.RetainForHistory();
            selection = Actor.Editor.RenderContext.SelectedActor;
            sceneIndex = Actor.Editor.RenderContext.DrawList_3D.IndexOf(Actor);
            fileIndex = Actor.OwningFile.Actors.IndexOf(Actor);
        }
        public string Description => $"Delete {Actor.ActorName}";
        public bool IsOrphaned => Actor.IsDisposed;
        public IReadOnlyCollection<ActorId> Subjects => [Actor.Id];
        // The deletion is listed only while the actor is out of the scene, so the change list never shows it twice. (This step holds the actor,
        // so neither order lets it go)
        public void Undo()
        {
            using var edit = new SceneEdit(Actor.Editor);
            edit.Do(() => Actor.OwningFile.RemoveDeletion(deletion), () => Actor.OwningFile.AddDeletion(deletion));
            edit.Attach(Actor, sceneIndex, fileIndex);
            edit.Reselect(selection);
            edit.Complete();
        }
        public void Redo()
        {
            if (Actor.IsDisposed) throw new InvalidOperationException("The actor's view was replaced.");
            using var edit = new SceneEdit(Actor.Editor);
            edit.Detach(Actor);
            edit.Do(() => Actor.OwningFile.AddDeletion(deletion), () => Actor.OwningFile.RemoveDeletion(deletion));
            edit.Complete();
        }
        public void Dispose() => retained.Dispose();
    }
}
