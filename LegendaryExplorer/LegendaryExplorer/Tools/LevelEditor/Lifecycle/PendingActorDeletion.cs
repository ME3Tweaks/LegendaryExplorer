using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>What a cancelled deletion leaves to be shown as the package has it</summary>
/// <param name="File">The actor's file</param>
/// <param name="Export">The actor's export</param>
/// <param name="Collection">The collection it's one of, if it is</param>
/// <param name="AlsoChanged">The other actors its undo step changed</param>
public sealed record CancelledDeletion(OpenLevelFile File, ExportEntry Export, ExportEntry Collection, IReadOnlyList<ActorProxy> AlsoChanged);

/// <summary>Reversible deletion intent, retained independently of history until committed or reset.</summary>
public sealed class PendingActorDeletion
{
    public ActorProxy Actor { get; }
    public string Status { get; private set; }
    private readonly ActorDeletionPlan prepared;
    private IDisposable retained;
    /// <summary>The other actors the deletion's undo step changes: a spline point's neighbours, without their connections to it, and what was deleted with it</summary>
    internal IReadOnlyList<ActorProxy> AlsoChanged { get; private set; }

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
    /// Cancels <paramref name="overtaken"/> (<see cref="Cancel"/>), and any deletion of <paramref name="files"/> whose undo step's other actors
    /// (<see cref="AlsoChanged"/>) are gone, which orphans that step, including those that <paramref name="show"/> leaves gone
    /// </summary>
    /// <param name="show">Shows what each cancelled deletion leaves as the package has it</param>
    /// <returns>Whether any was cancelled</returns>
    public static bool CancelAll(IEnumerable<PendingActorDeletion> overtaken, IEnumerable<OpenLevelFile> files, Action<CancelledDeletion> show)
    {
        var queue = new Queue<PendingActorDeletion>(overtaken);
        bool cancelled = false;
        while (queue.TryDequeue(out var deletion)
               || (deletion = files.SelectMany(f => f.PendingDeletions).FirstOrDefault(d => d.AlsoChanged.Any(a => a.IsDisposed))) is not null)
        {
            var file = deletion.Actor.OwningFile;
            if (!file.PendingDeletions.Contains(deletion)) continue;
            cancelled = true;
            var left = new CancelledDeletion(file, deletion.Actor.Export, deletion.Actor.CollectionOwner, deletion.AlsoChanged);
            deletion.Cancel();
            show(left);
        }
        return cancelled;
    }

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

    /// <summary>Deletes several actors as one undo step, each with its plan. If one can't be, none is</summary>
    public static IUndoAction DeleteTogether(IReadOnlyList<(ActorProxy Actor, ActorDeletionPlan Plan)> deletions)
    {
        var steps = new List<IUndoAction>();
        try
        {
            foreach (var (actor, plan) in deletions) steps.Add(actor.Delete(plan));
        }
        catch
        {
            foreach (var step in Enumerable.Reverse(steps))
            {
                step.Undo();
                (step as IDisposable)?.Dispose();
            }
            throw;
        }
        //one undo step: if another tool's change cancels one, that step is orphaned, so the others have to be cancelled with it, or they'd have no way back
        foreach (var (actor, _) in deletions) actor.PendingDeletion.AlsoChanged = [.. actor.PendingDeletion.AlsoChanged, .. deletions.Select(d => d.Actor).Where(a => a != actor)];
        return new GroupedDeletionAction(steps);
    }

    private sealed class GroupedDeletionAction(IReadOnlyList<IUndoAction> steps) : IUndoAction, IDisposable
    {
        public string Description => $"{steps[0].Description} and {(steps.Count == 2 ? "1 other actor" : $"{steps.Count - 1} other actors")}";
        public bool IsOrphaned => steps.Any(s => s.IsOrphaned);
        public IReadOnlyCollection<ActorId> Subjects => steps.SelectMany(s => s.Subjects).ToArray();
        public IEnumerable<AssetReference> Assets => steps.SelectMany(s => s.Assets);
        public void Undo()
        {
            foreach (var step in Enumerable.Reverse(steps)) step.Undo();
        }
        public void Redo()
        {
            foreach (var step in steps) step.Redo();
        }
        public void Dispose()
        {
            foreach (var step in steps) (step as IDisposable)?.Dispose();
        }
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
    internal void Accept()
    {
        //it's no longer an undo step that could orphan one deleted with it, in a file this commit left out
        foreach (var other in AlsoChanged.Select(a => a.PendingDeletion).OfType<PendingActorDeletion>()) other.AlsoChanged = other.AlsoChanged.Where(a => a != Actor).ToArray();
        Actor.Dispose();
    }

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
        public IEnumerable<AssetReference> Assets => [];
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
        public IEnumerable<AssetReference> Assets => [];
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
