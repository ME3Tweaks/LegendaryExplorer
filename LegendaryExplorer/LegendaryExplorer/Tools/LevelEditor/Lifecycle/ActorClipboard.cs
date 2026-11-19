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
