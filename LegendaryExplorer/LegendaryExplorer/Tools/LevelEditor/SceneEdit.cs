using System;
using System.Collections.Generic;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Changes to the scene (which actor views are drawn and pickable, and model changes made alongside) that either all take effect or none do.
/// Unless <see cref="Complete"/> is called before it's disposed, each step is undone in reverse order: views it attached are taken out again,
/// views it detached are put back where they were, and the selection is restored. Packages are never touched. Only one can be open on a scene at a time.
/// It never disposes a view: a detached one belongs to whoever detached it (an undo step that can attach the same view again), as does one
/// attached by an edit that was abandoned. A view's owning file's actor list follows the draw list; the window's own actor list does not yet
/// (it doesn't use scene edits)
/// </summary>
public sealed class SceneEdit : IDisposable
{
    private readonly LevelEditorRenderContext scene;
    private readonly ActorProxy selection;
    private readonly List<Action> undo = [];
    private bool finished;

    public SceneEdit(LevelEditorRenderContext scene)
    {
        //a nested edit would complete or roll back part of the outer one's steps on its own
        if (scene.IsEditingScene) throw new InvalidOperationException("Another change to the scene is in progress.");
        this.scene = scene;
        selection = scene.SelectedActor;
        scene.IsEditingScene = true;
    }

    /// <summary>Draws a view, as one of its owning file's actors</summary>
    public void Attach(ActorProxy view)
    {
        ObjectDisposedException.ThrowIf(finished, this);
        ObjectDisposedException.ThrowIf(view.IsDisposed, view);
        if (scene.DrawList_3D.Contains(view)) throw new InvalidOperationException($"{view.DisplayText} is already in the scene.");
        var file = view.OwningFile;
        //recorded first, so a failure part way through adding it still removes it
        undo.Add(() =>
        {
            scene.RemoveActor(view);
            if (file?.Actors.Remove(view) == true) file.RecalculateDirty();
        });
        scene.AddActor(view);
        if (file is not null && !file.Actors.Contains(view))
        {
            file.Actors.Add(view);
            file.RecalculateDirty();
        }
    }

    /// <summary>Stops drawing a view and removes it from its owning file's actors</summary>
    public void Detach(ActorProxy view)
    {
        ObjectDisposedException.ThrowIf(finished, this);
        int index = scene.DrawList_3D.IndexOf(view);
        if (index < 0) throw new InvalidOperationException($"{view.DisplayText} isn't in the scene.");
        var file = view.OwningFile;
        int fileIndex = file?.Actors.IndexOf(view) ?? -1;
        undo.Add(() =>
        {
            scene.InsertActor(index, view);
            if (fileIndex >= 0 && !file.Actors.Contains(view))
            {
                file.Actors.Insert(Math.Min(fileIndex, file.Actors.Count), view);
                file.RecalculateDirty();
            }
        });
        scene.RemoveActor(view);
        if (fileIndex >= 0)
        {
            file.Actors.RemoveAt(fileIndex);
            file.RecalculateDirty();
        }
    }

    /// <summary>Makes a model change that <paramref name="revert"/> undoes if the edit is abandoned</summary>
    public void Do(Action apply, Action revert)
    {
        ObjectDisposedException.ThrowIf(finished, this);
        undo.Add(revert);
        apply();
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(finished, this);
        finished = true;
        scene.IsEditingScene = false;
    }

    public void Dispose()
    {
        if (finished) return;
        finished = true;
        try
        {
            for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
            scene.SelectedActor = selection;
        }
        finally
        {
            scene.IsEditingScene = false;
        }
    }
}
