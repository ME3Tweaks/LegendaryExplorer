using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A window's own state for the copied actors (<see cref="ActorClipboard"/>) it made or pasted: its view of each, which Paste makes copies of, and what its
/// previews and registry know of each one's level. Only it changes them, from its own events, so nothing another window does reaches them
/// </summary>
public sealed class CopiedActors(IActorEditorContext editor)
{
    //the copies it has something of, with its view of each if it has made one
    private readonly Dictionary<ActorCapture, ActorProxy> held = [];

    /// <summary>Copies <paramref name="source"/>, one of its actors, as it's shown (<see cref="ActorCapture.Make"/>), and has it until it lets go of it</summary>
    public ActorCapture Make(ActorProxy source)
    {
        var capture = ActorCapture.Make(editor, source);
        held.Add(capture, null);
        ActorClipboard.Hold(capture);
        return capture;
    }

    /// <summary>Makes <paramref name="capture"/>, one it made (<see cref="Make"/>), the clipboard's, for every window to paste</summary>
    public void Copy(ActorCapture capture)
    {
        if (!held.ContainsKey(capture)) throw new ArgumentException("Only a copy this window made is put on the clipboard.", nameof(capture));
        ActorClipboard.Copy(capture);
    }

    /// <summary>
    /// For a paste: its view of <paramref name="capture"/>'s actor, which Paste makes a copy of (<see cref="ActorCloning.BeginClone"/>), drawn from the copy's
    /// level, which its previews then find by its path. Not drawn itself. Made by the first paste and kept for the next, until the previews' caches it was
    /// made with are emptied (<see cref="ForgetViews"/>) or it lets go of the copy
    /// </summary>
    public ActorProxy ViewOf(ActorCapture capture)
    {
        if (held.TryAdd(capture, null)) ActorClipboard.Hold(capture);
        // (once is enough until the previews' packages are let go of, when its last open level closes, so it's done for each paste)
        editor.RenderContext.PackageCache.UsePackage(capture.Package);
        if (held[capture] is { IsDisposed: false } view) return view;
        var made = ActorProxy.Create(editor, capture.Actor);
        try
        {
            made.OwningFile = capture.File;
            made.IsOutOfScene = true;
            made.LeaveSceneIfOutOfIt();
        }
        catch
        {
            made.Dispose();
            throw;
        }
        return held[capture] = made;
    }

    /// <summary>Lets go of its views, before the previews' caches that their meshes were made with are emptied. The next paste makes another</summary>
    public void ForgetViews()
    {
        foreach (var capture in held.Keys.ToList())
        {
            held[capture]?.Dispose();
            held[capture] = null;
        }
    }

    /// <summary>
    /// Lets go of the copies that aren't the clipboard's and that none of <paramref name="files"/>' actors is a paste of (an uncommitted one, or an undone
    /// one, which redo would bring back)
    /// </summary>
    public void ReleaseUnused(IEnumerable<OpenLevelFile> files)
    {
        if (held.Keys.All(capture => capture == ActorClipboard.Copied)) return;
        var actors = files.SelectMany(f => f.Actors.Concat(f.RetainedActors)).ToList();
        Release(held.Keys.Where(c => c != ActorClipboard.Copied && !c.IsUsedBy(actors)).ToList());
    }

    /// <summary>Lets go of every copy, as its window closes, once its levels have</summary>
    public void ReleaseAll() => Release(held.Keys.ToList());

    //for each: its view, then what its previews and registry know of the copy's level, then its hold on the level, each whatever the one before does
    private void Release(List<ActorCapture> captures)
    {
        List<Exception> errors = [];
        void Try(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is not OutOfMemoryException) { errors.Add(error); }
        }
        foreach (var capture in captures)
        {
            var view = held[capture];
            held.Remove(capture);
            if (view is not null) Try(view.Dispose);
            Try(() => capture.ForgetIn(editor));
            Try(() => ActorClipboard.LetGo(capture));
        }
        if (errors.Count > 0) throw new AggregateException("The copied actors couldn't all be let go of.", errors);
    }
}
