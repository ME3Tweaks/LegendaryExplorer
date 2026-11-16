using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A copied actor (Copy, Ctrl+C), which Paste makes copies of: the actor as it was shown when it was copied, its uncommitted edits and all, committed
/// into a level of its own, which the editor doesn't show and nothing else uses, so that what it was copied from can then be edited, deleted or closed.
/// It's made as a copy in another level is (<see cref="ActorCloning"/>): without its baked lighting or its references to its old level's other objects,
/// a collection's member made an actor of its own, with copies of what it uses. A paste is a copy of it in another level, made at its commit from its
/// level, which never changes. Its level is one of <see cref="TemporaryPackages"/>, made from its source's, whose imports are found as its source's are
/// </summary>
public sealed class ActorCapture : IDisposable
{
    private readonly IActorEditorContext editor;

    /// <summary>The level's package</summary>
    public IMEPackage Package { get; }
    /// <summary>The level, as the commit that made the copy wrote it</summary>
    public OpenLevelFile File { get; }
    /// <summary>The copied actor, one of the level's actors</summary>
    public ExportEntry Actor { get; private set; }
    /// <summary>What its source had that it doesn't, as the copy's status says: its baked lighting, its references to its old level's other objects</summary>
    public IReadOnlyList<string> NotCopied { get; private set; } = [];

    private ActorCapture(IActorEditorContext editor, ActorProxy copied)
    {
        var source = copied.Package;
        this.editor = editor;
        Package = TemporaryPackages.Create(source, $"the copy of {copied.ActorName} from {Path.GetFileName(source.FilePath)}");
        try
        {
            var world = Add("Package", "TheWorld", null);
            var level = Add("Level", "PersistentLevel", world);
            level.WriteBinary(LegendaryExplorerCore.Unreal.BinaryConverters.Level.Create(source.Game));
            File = new OpenLevelFile(null, Package, level) { IsCapture = true };
        }
        catch
        {
            TemporaryPackages.Release(Package);
            throw;
        }
    }

    private ExportEntry Add(string className, string name, IEntry parent)
    {
        var export = new ExportEntry(Package, parent, name) { Class = Package.GetEntryOrAddImport($"{(className == "Package" ? "Core" : "Engine")}.{className}", "Class") };
        Package.AddExport(export);
        export.WriteProperties([]);
        return export;
    }

    /// <summary>
    /// Copies <paramref name="source"/> as it's shown. Its source's package is read as it is in memory, and nothing is written to it. If it can't be
    /// copied, the error says why, and nothing is left of the copy
    /// </summary>
    public static ActorCapture Make(IActorEditorContext editor, ActorProxy source)
    {
        string Refused(string refusal) => $"{source?.ActorName} can't be copied. {refusal}";
        if (ActorCloning.Refusal(source, null) is { } refusal) throw new InvalidOperationException(Refused(refusal));
        var capture = new ActorCapture(editor, source);
        ActorProxy draft = null;
        try
        {
            // As a copy into its level is judged (a placed draft's class, a member's actor class), said as Copy's, before MakeCopy judges it again
            if (ActorCloning.Refusal(source, capture.File) is { } intoCapture) throw new InvalidOperationException(Refused(intoCapture));
            // An actor keeps its name; a member is named as an actor of its class
            draft = ActorCloning.MakeCopy(source, capture.File, name: source.MemberCollection is null ? source.ActorName : null);
            // (from here, disposed with the level if anything fails)
            capture.File.Actors.Add(draft);
            // Not drawn: it joined the scene's lighting as its meshes were made, and again as its source's lighting edits were carried
            draft.IsOutOfScene = true;
            draft.LeaveSceneIfOutOfIt();
            capture.NotCopied = NotCopiedFrom(source, draft);
            // Its source's package as it is in memory: what a placed draft uses of its own package may not be in its file
            ActorCommit.Commit([capture.File], alsoSnapshot: [source.Package]);
            capture.Actor = draft.Export ?? throw new InvalidOperationException($"{source.ActorName}'s copy wasn't made.");
            // (a paste is made as changed since its level's bake by its own commit)
            StaticLightingChanges.ClearAll(capture.Package);
            // Only its export is kept: a paste is drawn from a view of it (View)
            capture.File.Actors.Remove(draft);
            draft.Dispose();
        }
        catch (Exception error)
        {
            // (its record goes with the level's: Dispose). The copy's error is the one to say, whatever letting go of the level does
            try { capture.Dispose(); }
            catch (Exception disposal) when (disposal is not OutOfMemoryException) { throw new AggregateException(error.Message, error, disposal); }
            throw;
        }
        return capture;
    }

    //what source has that its copy, draft, won't: its baked lighting, its meshes' light environments that aren't its own, its references to its level
    private static List<string> NotCopiedFrom(ActorProxy source, ActorProxy draft)
    {
        var notCopied = new List<string>();
        if (ActorCloning.HasBakedLighting(source)) notCopied.Add("its meshes' baked lighting");
        if (draft.Components.OfType<MeshComponentProxy>().Any(m => m.ExistingLighting?.DroppedEnvironment == true))
            notCopied.Add("its meshes' uncommitted light environment, which isn't its own");
        var dropped = ActorCloning.DroppedReferences(source);
        if (dropped.Count > 0) notCopied.Add($"its references to its level's {string.Join(", ", dropped.Select(e => e.ObjectName.Instanced))}");
        return notCopied;
    }

    /// <summary>
    /// For a paste: makes the level's package one the previews find by its path, as a paste's view is drawn from its assets. Once is enough until the
    /// previews' packages are let go of (when the last open level closes), so it's done by each paste
    /// </summary>
    public void Use() => editor.RenderContext.PackageCache.UsePackage(Package);

    private ActorProxy view;

    /// <summary>
    /// For a paste: a view of the copied actor, which Paste makes a copy of (<see cref="ActorCloning.BeginClone"/>). Not drawn. Made by the first paste
    /// and kept for the next, until the previews' caches it was made with are emptied (<see cref="ForgetView"/>) or the copy is let go of
    /// </summary>
    public ActorProxy View
    {
        get
        {
            if (view is { IsDisposed: false }) return view;
            var made = ActorProxy.Create(editor, Actor);
            try
            {
                made.OwningFile = File;
                made.IsOutOfScene = true;
                made.LeaveSceneIfOutOfIt();
            }
            catch
            {
                made.Dispose();
                throw;
            }
            return view = made;
        }
    }

    /// <summary>Lets go of <see cref="View"/>, before the previews' caches that its meshes were made with are emptied. The next paste makes another</summary>
    public void ForgetView()
    {
        view?.Dispose();
        view = null;
    }

    /// <summary>Whether any of <paramref name="actors"/> is a copy of it (a paste), which its commit makes from its level</summary>
    public bool IsUsedBy(IEnumerable<ActorProxy> actors) => actors.Any(a => a is { IsDisposed: false } && a.CloneSource?.FileRef == Package);

    public void Dispose()
    {
        ForgetView();
        var context = editor.RenderContext;
        context.PackageCache.ReleasePackage(Package.FilePath);
        context.ForgetPackage(Package);
        context.Registry.Forget(Package);
        File.Dispose();
        TemporaryPackages.Release(Package);
    }
}
