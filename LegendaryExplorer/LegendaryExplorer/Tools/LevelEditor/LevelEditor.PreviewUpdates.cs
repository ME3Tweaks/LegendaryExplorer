using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    // Mode switches share the same serialized shader worker as package refreshes.
    private void PrepareGameShaders()
    {
        // Switching to a mode that's already prepared needn't suspend rendering, or wait for anything but loads and refreshes,
        // which read the scene in the background. (Saves don't)
        if (OpenFiles.Count == 0 || _busyCount == 0 && !PreviewRefreshInProgress && !RenderContext.NeedsGameShaderPreparation(_lightingMode))
        {
            RenderContext.LightingMode = _lightingMode;
            return;
        }
        PreviewShaderPreparationRequested = true;
        PreviewUpdateTimer.Start();
    }

    //Can be called from a background thread
    private void ReportGameShaderLoadProgress(int loaded, int total) => ReportGameShaderLoadProgress(loaded, total, null);

    private void ReportGameShaderLoadProgress(int loaded, int total, string fileProgress)
    {
        if (loaded % 10 == 0 || loaded == total)
        {
            string progress = $"Loading game shaders ({loaded}/{total})...";
            Dispatcher.InvokeAsync(() => BusyText = fileProgress is null ? progress : $"{fileProgress}\n{progress}");
        }
    }

    private readonly PreviewUpdateQueue PendingPreviewUpdates = new();
    private readonly DispatcherTimer PreviewUpdateTimer;
    private bool previewRefreshInProgress;
    private bool PreviewRefreshInProgress
    {
        get => previewRefreshInProgress;
        set
        {
            previewRefreshInProgress = value;
            RefreshCommandStates();
        }
    }
    private bool PreviewShaderPreparationRequested;
    private bool PreviewClosed;

    public void HandleFileUpdate(OpenLevelFile file, List<PackageUpdate> updates) =>
        QueuePreviewUpdate(file.Package, updates);

    private void QueuePreviewUpdate(IMEPackage package, List<PackageUpdate> updates)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => QueuePreviewUpdate(package, updates));
            return;
        }
        if (PreviewClosed || OpenFiles.Count == 0) return;
        MarkScanStale();
        // A class default object, or a package an import resolves through, may have changed
        EffectiveLightingProperties.InvalidateDefaults(RenderContext.PackageCache);
        //the level's BSP and terrain actors are read again if its Level or Model changed; any update (a class default, say) can change which terrain sections cast
        if (UnshadowedGeometry.TryGetValue(package, out var geometry)
            && updates.Any(update => (update.Change & PackageChange.Entry) != 0 && (update.Index == geometry.LevelUIndex || update.Index == geometry.ModelUIndex)))
        {
            UnshadowedGeometry.Remove(package);
        }
        foreach (var entry in UnshadowedGeometry.Values) entry.TerrainSections = null;
        // Both the open level and the dependency observer may notify us; the queue deduplicates indices.
        PendingPreviewUpdates.Add(package, updates);
        if (PendingPreviewUpdates.HasUpdates) PreviewUpdateTimer.Start();
    }

    private async void ProcessPreviewUpdates(object sender, EventArgs e)
    {
        // Package loading and shader preparation also read the scene on background threads.
        // Keep edits queued until those readers finish, and suspend rendering while replacing GPU resources. (Package saves aren't readers of the scene)
        if (_busyCount > 0 || PreviewRefreshInProgress || RenderContext.IsDragging || RenderContext.IsEditingScene || _isApplyingUndoRedo
            || UndoHistory.IsExecuting) return;
        PreviewUpdateTimer.Stop();
        if (OpenFiles.Count == 0 || PreviewClosed)
        {
            PreviewShaderPreparationRequested = false;
            return;
        }
        if (!PendingPreviewUpdates.HasUpdates && !PreviewShaderPreparationRequested) return;
        PreviewRefreshInProgress = true;
        var watch = Stopwatch.StartNew();
        SceneViewer.SetShouldRender(false);
        SetBusy(PendingPreviewUpdates.HasUpdates ? "Refreshing preview..." : "Preparing lighting...");
        var errors = new List<string>();
        try
        {
            bool changed = PreviewShaderPreparationRequested;
            PreviewShaderPreparationRequested = false;
            // Each package is applied separately, so that one failing doesn't drop the others' updates
            using (RenderContext.BeginLoad())
            {
                foreach (var (package, exports) in PendingPreviewUpdates.Drain())
                    changed |= TryRefresh(Path.GetFileName(package.FilePath), errors, () => ApplyPreviewUpdate(package, exports, errors));
            }
            var mode = LightingMode;
            if (changed)
                await Task.Run(() => RenderContext.LoadPendingGameShaders(ReportGameShaderLoadProgress, mode));
        }
        catch (AggregateException ex)
        {
            //nothing is missing: those materials are retried when rendered, falling back to LEX's shader
            RecordGameShaderLoadFailures(ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not load game shaders: {ex}");
            errors.Add($"Game shaders: {ex.Message}");
        }
        finally
        {
            Debug.WriteLine($"Lighting preview refresh: {watch.Elapsed.TotalMilliseconds:F0} ms");
            LightDiagnosticsStale = true;
            //actors rebuilt by other tools' edits
            UndoHistory.RemoveOrphaned();
            ReleaseUnused();
            RenderContext.LightingMode = _lightingMode;
            PreviewRefreshInProgress = false;
            EndBusy();
            SceneViewer.SetShouldRender(!PreviewClosed && OpenFiles.Count > 0);
            if ((PendingPreviewUpdates.HasUpdates || PreviewShaderPreparationRequested) && !PreviewClosed) PreviewUpdateTimer.Start();
        }
        if (errors.Count > 0 && !PreviewClosed)
        {
            const int maxListed = 8;
            string list = string.Join("\n", errors.Take(maxListed)) + (errors.Count > maxListed ? $"\n...and {errors.Count - maxListed} more" : "");
            MessageBox.Show(this, $"Parts of the preview couldn't be refreshed, and may be missing until their packages change again:\n{list}", "Preview refresh");
        }
    }

    /// <summary>
    /// Runs one part of a preview refresh, recording its failure instead of abandoning the rest: shared resources may already have been
    /// released, so every part still needs its chance to reacquire them
    /// </summary>
    /// <returns>Whether the part changed, which a failed part may have</returns>
    private static bool TryRefresh(string part, List<string> errors, Func<bool> refresh)
    {
        try
        {
            return refresh();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not refresh the preview of {part}: {ex}");
            errors.Add($"{part}: {ex.Message}");
            return true;
        }
    }

    private bool ApplyPreviewUpdate(IMEPackage package, HashSet<int> updatedExports, List<string> errors)
    {
        var file = OpenFiles.FirstOrDefault(open => open.Package == package);
        bool changed = RenderContext.InvalidatePreviewResources(package, updatedExports, out PreviewRebuild rebuild);
        // Meshes that must be rebuilt to reacquire released resources, whether or not they depend on the edited exports
        bool MustRebuild(ActorProxy actor) => rebuild is PreviewRebuild.AllMeshes
            || rebuild is PreviewRebuild.LightEnvironmentMeshes && MeshComponents(actor).Any(mesh => mesh.IsLitByLightEnvironment);
        // Decided against the old dependency snapshots, but reloaded after the actors, so rebuilt light proxies' discarded edits aren't reapplied.
        var lightFiles = OpenFiles.Where(openFile => openFile.Lighting?.IsAffectedBy(package, updatedExports) == true).ToList();
        foreach (var actor in Actors.Where(actor => actor.OwningFile != file).ToList())
            changed |= TryRefresh(actor.DisplayText, errors, () => actor.TryRefreshPreview(package, updatedExports, MustRebuild(actor)));
        // Views out of the scene that undo can bring back (pending deletions, undone placements) mustn't come back with released resources
        var inScene = Actors.ToHashSet();
        List<PendingActorDeletion> cancelled = [];
        foreach (var actor in OpenFiles.SelectMany(f => f.RetainedActors).Where(actor => !inScene.Contains(actor) && !actor.IsDisposed).ToList())
        {
            // A pending deletion of an actor another tool changed is cancelled (once the file's update below has passed over it), as is one that
            // can't be checked: the other tool's change wins
            if (actor.PendingDeletion is { } deletion && actor.Package == package)
            {
                bool overtaken = true;
                TryRefresh(actor.DisplayText, errors, () => { overtaken = !deletion.MatchesSnapshot(); return false; });
                if (overtaken)
                {
                    cancelled.Add(deletion);
                    continue;
                }
            }
            TryRefresh(actor.DisplayText, errors, () => actor.TryRefreshPreview(package, updatedExports, MustRebuild(actor)));
        }
        if (file is not null)
        {
            changed |= ApplyLevelActorUpdate(file, updatedExports, MustRebuild, errors, out bool levelActorsChanged);
            //the level's lights are read from its actors
            if (levelActorsChanged && !lightFiles.Contains(file)) lightFiles.Add(file);
            changed |= CancelDeletions(cancelled, errors);
        }
        changed |= lightFiles.Count > 0;
        if (lightFiles.Count > 0) RenderContext.InvalidateLightFunctions();
        foreach (var openFile in lightFiles)
            TryRefresh($"{openFile.FileName} lights", errors, () =>
            {
                openFile.ReloadLights(openFile.LevelExport.GetBinaryData<Level>(), RenderContext);
                return true;
            });
        return changed;
    }

    /// <param name="levelActorsChanged">Whether the level gained or lost actors</param>
    private bool ApplyLevelActorUpdate(OpenLevelFile file, HashSet<int> updatedExports, Func<ActorProxy, bool> mustRebuild, List<string> errors,
        out bool levelActorsChanged)
    {
        // A change to the Level (another tool's, or a commit's) is followed in place: views of the actors it no longer has go, and those it gained are
        // added, so the file's other actors keep their uncommitted edits, undo steps and pending creations and deletions
        Level level = updatedExports.Contains(file.LevelExport.UIndex) ? file.LevelExport.GetBinaryData<Level>() : null;
        levelActorsChanged = level is not null && RemoveActorsGoneFromLevel(file, level);
        bool changed = levelActorsChanged;
        bool updated = levelActorsChanged;
        int reselectUIndex = 0;
        (Vector3, float, float) savedCamPOV = default;
        Vector3 savedActorPos = default;
        HashSet<ExportEntry> collectionActorsToUpdate = [];
        List<ActorProxy> rebuiltViews = [];
        for (int i = file.Actors.Count - 1; i >= 0; i--)
        {
            ActorProxy alteredActor = file.Actors[i];
            if (TryRefresh(alteredActor.DisplayText, errors, () => alteredActor.TryRefreshPreview(file.Package, updatedExports, mustRebuild(alteredActor))))
            {
                changed = true;
                continue;
            }
            // A draft has no export to be made again from (the rebuild below reads its Export, which it doesn't have): it's left as it is, or as it redrew
            // itself (TryRefreshPreview: a mesh's or a decal's does)
            if (alteredActor.IsDraft) continue;
            if (alteredActor.TestUIndexes(updatedExports))
            {
                changed = updated = true;
                if (alteredActor == SelectedActor)
                {
                    reselectUIndex = alteredActor.Export.UIndex;
                    savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
                    savedActorPos = SelectedActor.Location;
                }
                if (alteredActor.Export.ClassName is "StaticMeshCollectionActor" or "StaticLightCollectionActor")
                {
                    collectionActorsToUpdate.Add(alteredActor.Export);
                    continue;
                }
                if (alteredActor.CollectionOwner is { } collectionOwner)
                {
                    // The whole collection is reloaded, including previously failed components.
                    collectionActorsToUpdate.Add(collectionOwner);
                    continue;
                }
                // A new light its commit just made: the Level gained it, though its view already had its export (so it isn't new to the file), and the
                // scene has its draft's light until the level's lights are reloaded
                if (alteredActor is LightActorProxy { Light.IsDraft: true }) levelActorsChanged = true;
                //another tool changed it: the rebuilt proxy is the same actor (so connections to it still resolve), but its earlier undo steps are orphaned with the old one
                RenderContext.Registry.Invalidate(alteredActor.Export);
                RemoveActor(alteredActor);
                if (file.Package.GetEntry(alteredActor.Export.UIndex) is ExportEntry actorExport)
                {
                    foreach (var view in CreateActorViews(actorExport, file))
                    {
                        AddActor(view);
                        rebuiltViews.Add(view);
                    }
                }
            }
        }
        if (rebuiltViews.Count != 0) file.ReattachTo(rebuiltViews);
        foreach (var collectionActor in collectionActorsToUpdate) RebuildCollection(collectionActor, file);
        // A collection that lost a member loses its uncommitted copies too
        if (file.Actors.Concat(file.RetainedActors).Any(a => a.CloneCollection is { } into && collectionActorsToUpdate.Contains(into))
            && RemoveActorsGoneFromLevel(file, level ?? file.LevelExport.GetBinaryData<Level>()))
            changed = updated = levelActorsChanged = true;
        if (level is not null && AddActorsNewToLevel(file, level))
        {
            changed = updated = levelActorsChanged = true;
        }
        if (updated)
        {
            Actors.Sort(a => a.SortIndex);
            //views with uncommitted edits may have been replaced or taken out
            file.RecalculateDirty();
            UpdateGlobalDirtyState();
        }
        if (reselectUIndex is not 0)
        {
            SelectedActor = Actors.FirstOrDefault(a => a.Export?.UIndex == reselectUIndex && a.Package == file.Package)
                // A collection's member that was upgraded is its new actor's component now
                ?? Actors.FirstOrDefault(a => a.Export is not null && a.Export == file.Package.GetEntry(reselectUIndex)?.Parent);
            if (SelectedActor is not null)
            {
                (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw)
                = (savedCamPOV.Item1 + SelectedActor.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
            }
        }
        return changed;
    }

    /// <summary>
    /// Cancels the pending deletions that another tool's change overtook (<see cref="PendingActorDeletion.Cancel"/>): the <paramref name="overtaken"/>
    /// deletions of actors it changed, and those of any open file whose undo step's other actors (a spline point's neighbours, still referring to it in
    /// the package, or what was deleted with it) are gone, which orphans that step. Each actor is shown as the package now has it, if its Level still lists it.
    /// The other committed actors their undo steps changed are shown as the package has them too, as when another tool changes them: with the
    /// cancelled step, nothing else would take back what it did to them. Rebuilding them can overtake another deletion in turn
    /// </summary>
    /// <returns>Whether any was cancelled</returns>
    private bool CancelDeletions(List<PendingActorDeletion> overtaken, List<string> errors)
    {
        var listedByFile = new Dictionary<OpenLevelFile, HashSet<int>>();
        var rebuilt = new List<ActorProxy>();
        bool cancelled = PendingActorDeletion.CancelAll(overtaken, OpenFiles, Show);
        foreach (var views in rebuilt.GroupBy(view => view.OwningFile)) views.Key.ReattachTo(views.ToArray());
        if (cancelled) UpdateGlobalDirtyState();
        return cancelled;

        void Show(CancelledDeletion left)
        {
            var (file, export, collection, alsoChanged) = left;
            if (!listedByFile.TryGetValue(file, out var listed)) listedByFile[file] = listed = file.LevelExport.GetBinaryData<Level>().Actors.ToHashSet();
            if (collection is not null)
            {
                // A collection's member is shown with the rest of its collection, which is shown as the package has it, as when another tool changes one
                if (file.Package.GetEntry(collection.UIndex) == collection && listed.Contains(collection.UIndex))
                    TryRefresh(collection.ObjectName.Instanced, errors, () =>
                    {
                        RenderContext.Registry.Invalidate(export);
                        foreach (var light in RebuildCollection(collection, file).OfType<ILightProxy>()) OnLightEdited(light);
                        Actors.Sort(a => a.SortIndex);
                        //the other members' uncommitted edits went with their views
                        file.RecalculateDirty();
                        return true;
                    });
            }
            else if (file.Package.GetEntry(export.UIndex) == export && listed.Contains(export.UIndex)) Rebuild(export, file);
            foreach (var other in alsoChanged.Where(a => a.Export is not null && Actors.Contains(a)))
            {
                var (otherExport, owner) = (other.Export, other.OwningFile);
                if (other == SelectedActor) SelectedActor = null;
                RemoveActor(other);
                if (owner.Package.GetEntry(otherExport.UIndex) == otherExport) Rebuild(otherExport, owner);
                owner.RecalculateDirty();
            }
        }

        void Rebuild(ExportEntry export, OpenLevelFile owner) => TryRefresh(export.ObjectName.Instanced, errors, () =>
        {
            RenderContext.Registry.Invalidate(export);
            foreach (var view in CreateActorViews(export, owner))
            {
                AddActor(view);
                rebuilt.Add(view);
                //a light whose deletion was pending had left the scene's lights
                if (view is ILightProxy light) OnLightEdited(light);
            }
            return true;
        });
    }

    /// <summary>
    /// Takes out the views of the actors <paramref name="level"/> no longer lists, and of its BSP if it isn't what the level has (which
    /// <see cref="AddActorsNewToLevel"/> adds). Not drafts, which it doesn't list until they're committed (but copies of what it, or a collection, no
    /// longer has), nor pending deletions, which are already out of the scene and checked again when committed
    /// </summary>
    /// <returns>Whether any were taken out</returns>
    private bool RemoveActorsGoneFromLevel(OpenLevelFile file, Level level)
    {
        var listed = level.Actors.ToHashSet();
        // An uncommitted copy of an actor the level no longer has, or of a member its collection no longer has (another tool took it out), can't be
        // made, so it goes with its source's view. (A paste's source is a copied actor's level, which nothing changes: ActorCapture)
        bool SourceGone(ActorProxy copy) => copy.CloneSource.FileRef == file.Package && (copy.CloneCollection is { } collection
            ? !listed.Contains(collection.UIndex) || ActorDeletionPlan.MembershipProblem(copy.CloneSource, collection) is not null
            : !listed.Contains(copy.CloneSource.UIndex));
        var gone = file.Actors.Where(actor => actor switch
        {
            LevelModelProxy bsp => !bsp.IsReadFrom(level),
            { Export: { } export } => !listed.Contains((actor.CollectionOwner ?? export).UIndex),
            { CloneSource: not null } => SourceGone(actor),
            _ => false
        }).ToList();
        //as is one that's been undone, which redoing would bring back
        var goneUndone = file.RetainedActors.Where(actor => actor is { IsDisposed: false, CloneSource: not null } && !file.Actors.Contains(actor) && SourceGone(actor))
            .ToList();
        foreach (var actor in gone)
        {
            if (actor == SelectedActor) SelectedActor = null;
            //(the copy's undo steps are orphaned with it)
            if (actor.IsDraft) RenderContext.Registry.Discard(actor.Id);
            RemoveActor(actor);
        }
        foreach (var copy in goneUndone)
        {
            copy.Dispose();
            RenderContext.Registry.Discard(copy.Id);
        }
        if (gone.OfType<LevelModelProxy>().Any()) RenderContext.SetLevelModel(file.Package, level.Model);
        return gone.Count > 0;
    }

    /// <summary>Adds views of the actors <paramref name="level"/> lists that the file doesn't show, and of its BSP if it has none</summary>
    /// <returns>Whether any were added</returns>
    private bool AddActorsNewToLevel(OpenLevelFile file, Level level)
    {
        var shown = file.Actors.Where(actor => actor.Export is not null).Select(actor => (actor.CollectionOwner ?? actor.Export).UIndex)
            .Concat(file.PendingDeletions.Select(deletion => deletion.Actor.Export.UIndex)).ToHashSet();
        var added = level.Actors.Distinct().Where(index => !shown.Contains(index) && file.Package.IsUExport(index))
            .SelectMany(index => CreateActorViews(file.Package.GetUExport(index), file)).ToList();
        foreach (var view in added) AddActor(view, false);
        //actors whose base was added with or after them
        if (added.Count > 0) file.ReattachTo(added);
        if (!file.Actors.OfType<LevelModelProxy>().Any()
            && !(file.UndrawnModel is { } undrawn && undrawn.Model == level.Model && undrawn.Components.SequenceEqual(level.ModelComponents))
            && CreateLevelModelView(level, file) is { } bsp)
        {
            AddActor(bsp, false);
            RenderContext.SetLevelModel(file.Package, level.Model);
            added.Add(bsp);
        }
        return added.Count > 0;
    }

    private void ReloadFile(OpenLevelFile file)
    {
        CancelViewportDrag();
        // Remove all actors for this file, then re-load
        (Vector3, float, float) savedCamPOV = default;
        Vector3 savedActorPos = default;
        int reselectUIndex = 0;
        if (SelectedActor is not null && file.Actors.Contains(SelectedActor))
        {
            savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
            savedActorPos = SelectedActor.Location;
            //a selected draft is discarded with the rest of the file's pending changes
            reselectUIndex = SelectedActor.Export?.UIndex ?? 0;
            SelectedActor = null;
        }

        foreach (var actor in file.Actors.ToList())
        {
            Actors.Remove(actor);
            RenderContext.RemoveActor(actor);
            actor.Dispose();
        }
        file.Actors.Clear();
        file.ClearPendingDeletions();
        file.DisposeRetainedActors();
        //reloaded from the package, so these are new actors: undo steps recorded against the old ones are orphaned, and drafts are gone
        RenderContext.Registry.Forget(file.Package);

        Level levelBin = file.LevelExport.GetBinaryData<Level>();
        var (actors, _) = LoadActors(levelBin, file);
        var sorted = actors.OrderBy(a => a.SortIndex).ToList();
        file.Actors.AddRange(sorted);
        Actors.AddRange(sorted);
        RenderContext.LoadActors(sorted);

        if (reselectUIndex is not 0)
        {
            var reselect = Actors.FirstOrDefault(a => a.Export?.UIndex == reselectUIndex && a.Package == file.Package);
            if (reselect is not null)
            {
                SelectedActor = reselect;
                (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw)
                = (savedCamPOV.Item1 + reselect.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
            }
        }

        file.IsDirty = false;
    }
}
