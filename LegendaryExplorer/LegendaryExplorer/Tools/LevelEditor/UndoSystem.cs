using System.Collections.Generic;
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

public interface IUndoAction
{
    string Description { get; }
    void Undo();
    void Redo();
}

public class TransformAction : IUndoAction
{
    private readonly ActorProxy _actor;
    private readonly TransformSnapshot _before;
    private readonly TransformSnapshot _after;

    public string Description { get; }

    public TransformAction(ActorProxy actor, TransformSnapshot before, TransformSnapshot after, string description)
    {
        _actor = actor;
        _before = before;
        _after = after;
        Description = description;
    }

    public void Undo() => _actor.RestoreTransform(_before);
    public void Redo() => _actor.RestoreTransform(_after);
}

public class UndoHistory : NotifyPropertyChangedBase
{
    private readonly Stack<IUndoAction> _undoStack = new();
    private readonly Stack<IUndoAction> _redoStack = new();

    public const int MaxHistorySize = 1000;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    private void NotifyCanUndoRedoChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Push(IUndoAction action)
    {
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
        }
        _undoStack.Push(action);
        _redoStack.Clear();
        NotifyCanUndoRedoChanged();
    }

    public void Undo()
    {
        if (_undoStack.TryPop(out var action))
        {
            action.Undo();
            _redoStack.Push(action);
        }
        NotifyCanUndoRedoChanged();
    }

    public void Redo()
    {
        if (_redoStack.TryPop(out var action))
        {
            action.Redo();
            _undoStack.Push(action);
        }
        NotifyCanUndoRedoChanged();
    }

    public void UndoMultiple(int count)
    {
        for (int i = 0; i < count && _undoStack.TryPop(out var action); i++)
        {
            action.Undo();
            _redoStack.Push(action);
        }
        NotifyCanUndoRedoChanged();
    }

    public void RedoMultiple(int count)
    {
        for (int i = 0; i < count && _redoStack.TryPop(out var action); i++)
        {
            action.Redo();
            _undoStack.Push(action);
        }
        NotifyCanUndoRedoChanged();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyCanUndoRedoChanged();
    }
}
