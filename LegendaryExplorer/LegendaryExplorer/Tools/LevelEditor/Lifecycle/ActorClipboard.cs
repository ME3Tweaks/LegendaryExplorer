using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// The copied actor (Copy, Ctrl+C), shared by every Level Editor window, so that Paste in any of them makes copies of the one copied last in any of them.
/// All the windows share is the copies' levels, which they only read. Each window has its own state for the copies it made or pasted
/// (<see cref="CopiedActors"/>), and lets go of it itself, so nothing one window does changes another's. A copy's level is let go of once it isn't the
/// clipboard's and no window has anything of it; every one, the clipboard's included, once the last window has closed. Used on the UI thread
/// </summary>
public static class ActorClipboard
{
    //the windows sharing it, each told when the clipboard's copy changes
    private static readonly List<Action> windows = [];
    //the copies, with how many windows have something of each
    private static readonly Dictionary<ActorCapture, int> holders = [];

    /// <summary>The actor copied last, which Paste makes copies of, or null</summary>
    public static ActorCapture Copied { get; private set; }

    /// <summary>
    /// For a window that's been shown: it shares the clipboard, and <paramref name="changed"/> is called whenever another copy is the clipboard's, until
    /// the membership is disposed, once the window has closed and let go of what it had of the copies (<see cref="CopiedActors.ReleaseAll"/>). The last to
    /// go lets go of every copy's level, the clipboard's included
    /// </summary>
    public static IDisposable Join(Action changed)
    {
        windows.Add(changed);
        return new Membership(changed);
    }

    private sealed class Membership(Action changed) : IDisposable
    {
        private bool left;

        public void Dispose()
        {
            if (left) return;
            left = true;
            windows.Remove(changed);
            if (windows.Count == 0) ReleaseAll();
        }
    }

    //once no window is open: each copy's level, whatever letting go of another's does
    private static void ReleaseAll()
    {
        var all = holders.Keys.ToList();
        holders.Clear();
        Copied = null;
        List<Exception> errors = [];
        foreach (var capture in all)
        {
            try { capture.Release(); }
            catch (Exception error) when (error is not OutOfMemoryException) { errors.Add(error); }
        }
        if (errors.Count > 0) throw new AggregateException("The copied actors couldn't all be let go of.", errors);
    }

    /// <summary>
    /// Why the clipboard's copy can't be pasted into <paramref name="destination"/> as it's another game's, or null. Paste says so with a message box: it
    /// can't be pasted into any of that game's levels
    /// </summary>
    public static string GameRefusal(OpenLevelFile destination) => Copied is { Package.Game: var game } && game != destination.Package.Game
        ? $"The copied actor is an {game} actor, and {destination.FileName} is an {destination.Package.Game} level. Actors can't be copied from one game to another."
        : null;

    /// <summary>
    /// Makes <paramref name="capture"/>, which a window holds (<see cref="CopiedActors.Copy"/>), the clipboard's: what Paste makes copies of, in any window.
    /// The one it replaces goes, if no window has anything of it. Then every window is told, each handling its own errors
    /// </summary>
    internal static void Copy(ActorCapture capture)
    {
        if (holders.GetValueOrDefault(capture) == 0) throw new InvalidOperationException("A copy is put on the clipboard by a window that has it.");
        var replaced = Copied;
        Copied = capture;
        try { ReleaseIfUnheld(replaced); }
        finally
        {
            foreach (var changed in windows.ToList()) changed();
        }
    }

    /// <summary>For a window making state of its own for <paramref name="capture"/> (<see cref="CopiedActors"/>): its level is kept until the window lets go</summary>
    internal static void Hold(ActorCapture capture) => holders[capture] = holders.GetValueOrDefault(capture) + 1;

    /// <summary>For a window that has let go of its state for <paramref name="capture"/>: its level goes if it isn't the clipboard's and no other window has any</summary>
    internal static void LetGo(ActorCapture capture)
    {
        if (holders.GetValueOrDefault(capture) == 0) throw new InvalidOperationException("A copy was let go of by a window that didn't have it.");
        holders[capture]--;
        ReleaseIfUnheld(capture);
    }

    private static void ReleaseIfUnheld(ActorCapture capture)
    {
        if (capture is null || capture == Copied || holders.GetValueOrDefault(capture) > 0) return;
        holders.Remove(capture);
        capture.Release();
    }
}

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
