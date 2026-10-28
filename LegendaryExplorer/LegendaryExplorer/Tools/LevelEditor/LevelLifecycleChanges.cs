using System;
using System.Collections.Generic;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// The actors a level gains or loses at its next commit, by actor id: drafts that are present (created) and committed actors taken out of the scene
/// (deleted). Unlike an actor's own dirty state these need no view, so a pending deletion, or a level whose only change is a removed actor, is still
/// a change. Creating an actor and removing it again is no change at all
/// </summary>
public sealed class LevelLifecycleChanges
{
    private readonly HashSet<ActorId> created = [];
    private readonly HashSet<ActorId> deleted = [];

    public IReadOnlyCollection<ActorId> Created => created;
    public IReadOnlyCollection<ActorId> Deleted => deleted;
    public bool HasChanges => created.Count > 0 || deleted.Count > 0;

    public event Action Changed;

    /// <summary>A draft is now part of the level</summary>
    public void AddCreated(ActorId draft) => Update(created.Add(draft));

    /// <summary>A draft is no longer part of the level (its creation was undone, or it was deleted)</summary>
    public void RemoveCreated(ActorId draft) => Update(created.Remove(draft));

    /// <summary>A committed actor is no longer part of the level</summary>
    public void AddDeleted(ActorId actor) => Update(deleted.Add(actor));

    /// <summary>A committed actor that was deleted is part of the level again</summary>
    public void RemoveDeleted(ActorId actor) => Update(deleted.Remove(actor));

    /// <summary>Back to the committed level: after a commit, or when its pending changes are discarded</summary>
    public void Clear()
    {
        bool changed = HasChanges;
        created.Clear();
        deleted.Clear();
        Update(changed);
    }

    private void Update(bool changed)
    {
        if (changed) Changed?.Invoke();
    }
}
