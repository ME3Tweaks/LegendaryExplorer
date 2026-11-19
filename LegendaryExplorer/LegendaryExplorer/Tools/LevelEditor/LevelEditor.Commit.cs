using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    private readonly LinkedFileEdits LinkedEdits = new();
    public ExportEntry GetDecalDestinationLevel(IMEPackage package) =>
        OpenFiles.FirstOrDefault(f => f.Package == package && !f.IsReadOnly)?.LevelExport;

    /// <summary>Register files changed by one committed operation so they are saved or discarded together.</summary>
    internal void RegisterLinkedFileEdits(params OpenLevelFile[] files) => LinkedEdits.Register(files);

    private bool CanSaveLinkedFiles(IEnumerable<OpenLevelFile> files)
    {
        if (files.FirstOrDefault(f => f.IsReadOnly) is not { } blocked) return true;
        MessageBox.Show(this, $"Saving these linked edits also requires saving {blocked.FileName}, which is read-only.", "Linked file edits");
        return false;
    }

    private async void DeleteActor(object parameter)
    {
        if (!CanMutateScene || (parameter as ActorProxy ?? SelectedActor) is not { } actor || actor.DeleteRefusal is not null) return;
        try
        {
            if (actor.IsDraft)
            {
                UndoHistory.Push(actor.DeleteDraft());
                return;
            }
            ActorDeletionPlan plan;
            SetBusy($"Checking references to {actor.ActorName}...");
            try { plan = await ActorDeletionPlan.PrepareAsync(actor.Export, actor.OwningFile.LevelExport, actor.GetDeletionRepairs()); }
            finally { EndBusy(); }
            if (!CanMutateScene) throw new InvalidOperationException("The editor state changed during reference inspection. Retry deletion.");
            IReadOnlyList<ActorProxy> going = [actor];
            ActorDeletionPlan[] plans = [plan];
            if (ActorDeletionPlan.AttachedActors(actor).Count != 0)
            {
                var attached = await WhileCheckingReferences(actor, () => ActorDeletionPlan.FindAttachedAsync(actor, plan));
                //(with nothing to decide, what can't be deleted is detached, as the deletion's status says: mostly navigation points, on most of what's placed)
                if (attached.All.Going.Count > 1)
                {
                    if (AskAboutAttached(attached) is not { } chosen) return;
                    (going, plans) = (chosen.Going, chosen.Plans);
                }
            }
            plans[0].RequireAllowed();
            var (decals, withDecals) = await WhileCheckingReferences(actor, () => ActorDeletionPlan.FindEmptiedDecalsAsync(going, plans));
            if (decals.Count != 0)
            {
                string question = decals.Count == 1 ? $"Deleting {actor.ActorName} leaves the decal {decals[0].ActorName} with nothing to project onto. Delete it too?"
                    : $"Deleting {actor.ActorName} leaves {decals.Count} decals with nothing to project onto. Delete them too?\n\n{ActorDeletionPlan.Listed(decals.Select(d => d.ActorName).ToArray())}";
                var answer = MessageBox.Show(this, question, "Delete actor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer is not (MessageBoxResult.Yes or MessageBoxResult.No)) return;
                if (answer == MessageBoxResult.Yes) (going, plans) = ([.. going, .. decals], withDecals);
            }
            if (!CanMutateScene) throw new InvalidOperationException("The editor state changed during reference inspection. Retry deletion.");
            UndoHistory.Push(going.Count == 1 ? actor.Delete(plans[0]) : PendingActorDeletion.DeleteTogether(going.Zip(plans).ToList()));
        }
        catch (ActorDeletionBlockedException blocked)
        {
            //(the editor waits for it, while what it lists is changed in the Sequence Editor)
            new DeletionBlockedDialog(blocked).ShowOver(this);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, "Delete actor");
        }
    }

    private async Task<T> WhileCheckingReferences<T>(ActorProxy actor, Func<Task<T>> check)
    {
        SetBusy($"Checking references to {actor.ActorName}...");
        try { return await check(); }
        finally { EndBusy(); }
    }

    /// <summary>Asks what's to go with an actor of what's attached to it (<see cref="AttachedDependents.Question"/>)</summary>
    /// <returns>The answer, or null if its deletion was cancelled</returns>
    /// <summary>Shows <paramref name="export"/> in a Package Editor: one that already has its package, if one does</summary>
    internal static void OpenInPackageEditor(ExportEntry export)
    {
        if (export.FileRef.Users.OfType<PackageEditorWindow>().FirstOrDefault() is { } editor)
        {
            editor.GoToNumber(export.UIndex);
            editor.RestoreAndBringToFront();
            return;
        }
        editor = new PackageEditorWindow();
        editor.Show();
        editor.LoadPackage(export.FileRef, export.UIndex);
        editor.Activate();
    }

    private DeletionChoice AskAboutAttached(AttachedDependents attached)
    {
        bool anyRequired = attached.Least.Going.Count > 1;
        bool anyOptional = attached.Least.Going.Count < attached.All.Going.Count;
        return MessageBox.Show(this, attached.Question(), "Delete actor", anyOptional ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel, MessageBoxImage.Question,
            anyRequired ? MessageBoxResult.Cancel : MessageBoxResult.No) switch
        {
            MessageBoxResult.Yes or MessageBoxResult.OK => attached.All,
            MessageBoxResult.No => attached.Least,
            _ => null
        };
    }

    private void UpgradeActor(object parameter)
    {
        if (!CanMutateScene || (parameter as ActorProxy ?? SelectedActor) is not { } member) return;
        try { UndoHistory.Push(CollectionMemberUpgrade.Upgrade(member)); }
        catch (Exception error) when (error is not OutOfMemoryException) { MessageBox.Show(this, error.Message, "Upgrade to actor"); }
    }

    /// <summary>
    /// Commits every writable file's changes at once: never some files' alone, since a commit clears the undo history, which would lose the others'
    /// </summary>
    private bool CommitChanges() => PackageIsLoaded() && CommitChangesForFiles(OpenFiles.Where(f => !f.IsReadOnly));

    private bool CommitChangesForFiles(IEnumerable<OpenLevelFile> requested)
    {
        // A save or preview refresh reads the packages on another thread, and a scene edit or history step would be cut in two
        if (!CanMutateScene)
        {
            MessageBox.Show(this, "Wait for the current operation (such as saving or refreshing the preview) to finish, then commit again.", "Commit Changes");
            return false;
        }
        CancelViewportDrag();
        var files = requested.ToHashSet();
        // Other files' assets are opened from disk once for the commit, by the decal migration and the commit's own copying
        using var sources = new PackageCache();
        DecalPackageMigration migration;
        try
        {
            // Include receiving files so their pending receiver transforms are committed with the decal.
            var pending = new Queue<OpenLevelFile>(files);
            while (pending.TryDequeue(out var file))
            {
                foreach (var decal in file.Actors.OfType<DecalActorProxy>())
                {
                    decal.PrepareDecalCommit();
                    if (decal.ProjectionPackage != file.Package)
                    {
                        var target = OpenFiles.Single(f => f.Package == decal.ProjectionPackage);
                        if (files.Add(target)) pending.Enqueue(target);
                    }
                }
            }
            foreach (var spline in files.SelectMany(f => f.Actors).OfType<SplineActorProxy>().Where(s => s.IsDirty))
                spline.ValidateConnectionsForCommit();
            migration = DecalPackageMigration.Prepare(files.SelectMany(f => f.Actors).OfType<DecalActorProxy>(), sources);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, "Commit Changes");
            return false;
        }
        using (migration)
        {
            // The decal migration and every actor write are one package edit: a failure anywhere restores all the files and leaves every edit pending.
            // It's made on this thread, and with pending deletions it includes a scan of each package for references. Only the cursor shows it: drawing
            // the busy overlay first would mean running the dispatcher's queue in the middle of the commit
            Exception failure = null;
            Mouse.OverrideCursor = Cursors.Wait;
            try { ActorCommit.Commit(OpenFiles.Where(files.Contains).ToList(), migration.Apply, migration.Moves.Select(m => (ActorProxy)m.Actor).ToHashSet(), sources); }
            catch (Exception error) when (error is not OutOfMemoryException) { failure = error; }
            finally { Mouse.OverrideCursor = null; }
            if (failure is not null)
            {
                MessageBox.Show(this, $"Nothing was committed: {failure.Message}", "Commit Changes");
                return false;
            }
            var selectedMove = migration.Moves.FirstOrDefault(m => m.Actor == SelectedActor);
            foreach (var move in migration.Moves)
            {
                var target = OpenFiles.Single(f => f.Package == move.Destination);
                RegisterLinkedFileEdits(move.Actor.OwningFile, target);
                if (SelectedActor == move.Actor) SelectedActor = null;
                RemoveActor(move.Actor);
            }
            foreach (var move in migration.Moves)
            {
                var actor = ActorProxy.Create(this, move.Destination.GetUExport(move.ActorIndex));
                actor.OwningFile = OpenFiles.Single(f => f.Package == move.Destination);
                AddActor(actor);
                if (move == selectedMove) SelectedActor = actor;
            }
        }
        UpdateGlobalDirtyState();
        //(undone pastes go with the history, which releases their copied actors. A committed paste's view still has its copied actor until it's drawn from
        //its export: ProcessPreviewUpdates)
        ClearUndoHistory();
        //(the history's steps, and the committed actors' picks, are let go of: an empty history raises nothing)
        ReleaseUnused();
        return true;
    }

    private void ClearUndoHistory()
    {
        UndoHistory.Clear();
        _preEditSnapshot = selectedActor?.SnapshotTransform();
        _preEditLightSettings = SelectedLight?.Settings;
    }

    private async void SaveAllFiles()
    {
        if (IsDirty)
        {
            switch (MessageBox.Show("Do you want to commit your Level Editor changes before saving all files?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChanges();
                    if (IsDirty) return;
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        if (!CanSaveLinkedFiles(LinkedEdits.FilesWithPendingEdits)) return;
        foreach (var file in OpenFiles)
        {
            if (!file.IsReadOnly && file.Package.IsModified)
            {
                await file.Package.SaveAsync();
            }
        }
        LinkedEdits.RemoveSavedGroups();
    }

    private async void SaveSingleFileExecute(object parameter)
    {
        OpenLevelFile file = ResolveFileParameter(parameter);
        if (file is null || file.IsReadOnly) return;

        if (file.IsDirty)
        {
            switch (MessageBox.Show($"Do you want to commit your Level Editor changes (to every file) before saving {file.FileName}?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    if (!CommitChanges()) return;
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        var filesToSave = LinkedEdits.GetLinkedFiles(file).ToList();
        if (!CanSaveLinkedFiles(filesToSave)) return;
        foreach (var related in filesToSave)
            await related.Package.SaveAsync();
        LinkedEdits.RemoveSavedGroups();
    }
}
