using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor;

public readonly record struct TransformSnapshot(
    Vector3 Location,
    Rotator Rotation,
    float DrawScale,
    Vector3 DrawScale3D)
{
    public bool Equals(TransformSnapshot other) =>
        Location == other.Location &&
        Rotation == other.Rotation &&
        DrawScale == other.DrawScale &&
        DrawScale3D == other.DrawScale3D;
}

/// <summary>
/// A step of the history. One that also implements <see cref="IDisposable"/> holds something only it can release, such as a view it took out of
/// the scene and can put back; the history disposes it once it drops the step (redo steps a new one replaced, pruned or trimmed steps, or Clear)
/// </summary>
public interface IUndoAction
{
    string Description { get; }
    void Undo();
    void Redo();

    /// <summary>
    /// What it edits was replaced (such as an actor rebuilt after another tool edited it), so undoing or redoing it would change nothing
    /// </summary>
    bool IsOrphaned { get; }

    /// <summary>
    /// The actors whose state it restores, which steps on the far side of it in the history may depend on (see <see cref="UndoHistory.RemoveOrphaned"/>).
    /// Null if it doesn't say, so it's assumed to share state with every other step
    /// </summary>
    IReadOnlyCollection<ActorId> Subjects => null;

    /// <summary>
    /// The assets it restores references to, which undoing or redoing it brings back: a chosen asset's package (<see cref="AssetPackage"/>) is kept while
    /// it's in the history. Every step says, so that none can leave one out: none, for one that restores no asset (an actor it keeps for redo says what
    /// it uses itself: <see cref="AssetPackage.ReferencesIn"/>)
    /// </summary>
    IEnumerable<AssetReference> Assets { get; }
}

/// <summary>
/// An action that can absorb the next one, so that a continuous edit (such as dragging a value) is undone in one step
/// </summary>
public interface IMergeableUndoAction : IUndoAction
{
    /// <summary>
    /// Extends this action to include <paramref name="next"/>, which follows it directly. Returns false if they're separate edits
    /// </summary>
    bool TryMerge(IUndoAction next);
}

public class TransformAction : IUndoAction
{
    private readonly ActorRef _actor;
    private readonly TransformSnapshot _before;
    private readonly TransformSnapshot _after;

    public string Description { get; }
    public IEnumerable<AssetReference> Assets => [];

    public TransformAction(ActorProxy actor, TransformSnapshot before, TransformSnapshot after, string description)
    {
        _actor = new ActorRef(actor);
        _before = before;
        _after = after;
        Description = description;
    }

    public void Undo() => _actor.View.RestoreTransform(_before);
    public void Redo() => _actor.View.RestoreTransform(_after);
    public bool IsOrphaned => _actor.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [_actor.Id];
}

public class UndoHistory : NotifyPropertyChangedBase
{
    private readonly Stack<IUndoAction> _undoStack = new();
    private readonly Stack<IUndoAction> _redoStack = new();

    public const int MaxHistorySize = 1000;

    /// <summary>Raised once steps are dropped (by a new step, trimming, <see cref="Clear"/> or <see cref="RemoveOrphaned"/>) and what they held is released</summary>
    public event Action Dropped;

    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>The steps it can undo, then those it can redo</summary>
    public IEnumerable<IUndoAction> Steps => _undoStack.Concat(_redoStack);
    public bool CanRedo => _redoStack.Count > 0;

    private void NotifyCanUndoRedoChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    //a step being undone or redone; the stacks must not change under it
    private bool _executing;

    /// <summary>Whether a step is being undone or redone</summary>
    public bool IsExecuting => _executing;

    public void Push(IUndoAction action)
    {
        if (_executing) throw new InvalidOperationException("A step can't be recorded while another is being undone or redone.");
        // (dropped once the history is as it will be: see Drop)
        List<IUndoAction> dropped;
        if (_undoStack.TryPeek(out IUndoAction previous) && previous is IMergeableUndoAction mergeable && mergeable.TryMerge(action))
        {
            dropped = [.. _redoStack];
            _redoStack.Clear();
            NotifyCanUndoRedoChanged();
            Drop(dropped);
            return;
        }
        dropped = [.. _redoStack];
        _redoStack.Clear();
        if (_undoStack.Count >= MaxHistorySize)
        {
            // Convert to array, keep most recent entries
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            // Items are in LIFO order (newest first), so keep first MaxHistorySize/2
            for (int i = MaxHistorySize / 2 - 1; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
            dropped.AddRange(items[(MaxHistorySize / 2)..]);
        }
        _undoStack.Push(action);
        NotifyCanUndoRedoChanged();
        Drop(dropped);
    }

    public void Undo() => UndoMultiple(1);

    public void Redo() => RedoMultiple(1);

    /// <summary>
    /// Undoes up to <paramref name="count"/> actions, one at a time. An action moves to the redo stack only once it has succeeded, so if one throws
    /// it stays where it was (and the exception propagates); the ones before it remain undone
    /// </summary>
    public void UndoMultiple(int count) => Move(_undoStack, _redoStack, count, static action => action.Undo());

    /// <inheritdoc cref="UndoMultiple"/>
    public void RedoMultiple(int count) => Move(_redoStack, _undoStack, count, static action => action.Redo());

    private void Move(Stack<IUndoAction> from, Stack<IUndoAction> to, int count, Action<IUndoAction> execute)
    {
        if (_executing) throw new InvalidOperationException("Another step is being undone or redone.");
        _executing = true;
        try
        {
            for (int i = 0; i < count && from.TryPeek(out var action); i++)
            {
                execute(action);
                from.Pop();
                to.Push(action);
            }
        }
        finally
        {
            _executing = false;
            NotifyCanUndoRedoChanged();
        }
    }

    public void Clear()
    {
        List<IUndoAction> dropped = [.. _undoStack, .. _redoStack];
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyCanUndoRedoChanged();
        Drop(dropped);
    }

    /// <summary>
    /// Releases what dropped actions hold (see <see cref="IUndoAction"/>); they're never undone or redone again. Called once they're off the stacks, so
    /// that neither a failure here nor <see cref="Dropped"/>'s handlers find them there
    /// </summary>
    private void Drop(IReadOnlyCollection<IUndoAction> actions)
    {
        if (actions.Count == 0) return;
        foreach (IUndoAction action in actions) (action as IDisposable)?.Dispose();
        Dropped?.Invoke();
    }

    /// <summary>
    /// Forgets the actions whose subjects were replaced, and the ones that depended on them. Removing an action makes its effect permanent, which the
    /// steps beyond it (older ones to undo, later ones to redo) were never applied against: undoing an older spline topology step would restore a
    /// point's connections from before the removed one's edit while its neighbours keep theirs. So every step beyond a removed one that shares an
    /// actor with it (or with any step removed for that reason) goes too, as does every step beyond one whose actors aren't known.
    /// Steps nearer than a removed one were applied on top of its effect, and stay
    /// </summary>
    public void RemoveOrphaned()
    {
        var dropped = new List<IUndoAction>();
        bool Prune(Stack<IUndoAction> stack)
        {
            //nearest first: an undo stack enumerates newest first, a redo stack next to redo first
            var kept = new List<IUndoAction>();
            var removedSubjects = new HashSet<ActorId>();
            bool removedUnknown = false;
            var removed = new List<IUndoAction>();
            foreach (IUndoAction action in stack)
            {
                IReadOnlyCollection<ActorId> subjects = action.Subjects;
                bool dependent = removedUnknown || removedSubjects.Count > 0 && (subjects is null || subjects.Any(removedSubjects.Contains));
                if (!action.IsOrphaned && !dependent)
                {
                    kept.Add(action);
                    continue;
                }
                removed.Add(action);
                if (subjects is null) removedUnknown = true;
                else removedSubjects.UnionWith(subjects);
            }
            if (removed.Count == 0) return false;
            dropped.AddRange(removed);
            stack.Clear();
            for (int i = kept.Count - 1; i >= 0; i--) stack.Push(kept[i]);
            return true;
        }
        if (Prune(_undoStack) | Prune(_redoStack)) NotifyCanUndoRedoChanged();
        Drop(dropped);
    }
}
