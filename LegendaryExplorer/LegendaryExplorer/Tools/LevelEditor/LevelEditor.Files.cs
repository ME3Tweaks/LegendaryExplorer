using LegendaryExplorer.Dialogs;
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
using Microsoft.Win32;
using LegendaryExplorer.UserControls.ExportLoaderControls;
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
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    public async Task LoadFileAsync(string s)
    {
        if (!ConfirmCloseFiles("Open the new files anyway?")) return;

        try
        {
            CloseAllFiles();
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);


            using var guard = new RenderGuard(this);

            await AddLevelFile(s).ConfigureAwait(true);
        }
        catch (Exception e)
        {
            StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
            MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
            IsBusyTaskbar = false;
        }
    }

    /// <summary>Loads private bake packages without reopening their originals or recording them as recent files.</summary>
    public async Task LoadBakePreviewAsync(IReadOnlyList<IMEPackage> levels,
        Func<LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo, byte[]> readTexture)
    {
        if (!IsBakePreview) throw new InvalidOperationException("Use a read-only preview window.");
        loadingBakePreview = true;
        using var guard = new RenderGuard(this);
        try
        {
            CloseAllFiles();
            RenderContext.TextureDataOverride = readTexture;
            for (int i = 0; i < levels.Count; i++)
                await AddLevelPackage(levels[i], i + 1, levels.Count);
            CenterView();
            Title = "Lightmass Preview — close this window to Save or Discard in Lightmass Baker";
        }
        finally
        {
            loadingBakePreview = false;
            RefreshCommandStates();
        }
    }

    private static string FileLoadProgress(string path, int fileNumber, int fileCount) =>
        $"File {fileNumber} of {fileCount} — {Path.GetFileName(path)}";

    private async Task AddLevelFile(string path, int fileNumber = 1, int fileCount = 1)
    {
        path = Path.GetFullPath(path);

        if (OpenFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} is already open.");
            return;
        }

        BusyText = $"{FileLoadProgress(path, fileNumber, fileCount)}\nOpening package...";
        await Dispatcher.Yield(DispatcherPriority.Background);
        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(path);
        await AddLevelPackage(pcc, fileNumber, fileCount);
    }

    private async Task AddLevelPackage(IMEPackage pcc, int fileNumber = 1, int fileCount = 1)
    {
        string path = pcc.FilePath;
        if (!pcc.Game.IsLEGame())
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} is a {pcc.Game} file. The Level Editor opens only Legendary Edition (LE1, LE2 and LE3) levels.");
            return;
        }
        if (OpenFiles.Count > 0 && pcc.Game != Game)
        {
            MessageBox.Show(this, $"Cannot mix games. The open files are {Game}, but {Path.GetFileName(path)} is {pcc.Game}.");
            return;
        }
        Game = pcc.Game;
        ExportEntry levelExport = pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level");
        if (levelExport is null)
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} is not a level file!");
            return;
        }

        var openFile = new OpenLevelFile(this, pcc, levelExport);
        // Register the OpenLevelFile as a user of the package for update notifications
        pcc.RegisterTool(openFile);
        RenderContext.PackageCache.UsePackage(pcc);
        OpenFiles.Add(openFile);
        HasAnyFileOpen = true;

        if (!IsBakePreview) RecordCurrentFilesAsRecent();

        Level levelBin = levelExport.GetBinaryData<Level>();
        bool isFirstFile = OpenFiles.Count == 1;

        // Callers hold a RenderGuard, which keeps the editor busy until every file is loaded
        string fileProgress = FileLoadProgress(path, fileNumber, fileCount);
        BusyText = $"{fileProgress}\nLoading actors...";

        var (actors, ignoredClasses) = await Task.Run(() =>
        {
            RenderContext.CaptureAssignments(pcc, opening: true);
            var result = LoadActors(levelBin, openFile);
            try
            {
                RenderContext.LoadPendingGameShaders((loaded, total) => ReportGameShaderLoadProgress(loaded, total, fileProgress));
            }
            catch (AggregateException e)
            {
                //the level still loads: those materials are retried when rendered, falling back to LEX's shader
                RecordGameShaderLoadFailures(e);
            }
            return result;
        }).ConfigureAwait(true);
        var sorted = actors.OrderBy(actor => actor.SortIndex).ToList();
        openFile.Actors.AddRange(sorted);
        Actors.AddRange(sorted);
        RenderContext.LoadActors(sorted);

        if (isFirstFile)
        {
            PlaceStartingCamera(levelExport);
        }

        if (ignoredClasses.Count > 0)
        {
            string existing = string.IsNullOrEmpty(TextBelowActors) ? "" : TextBelowActors + "\n";
            TextBelowActors = existing + $"{Path.GetFileName(path)} unsupported (retained in outliner): {string.Join(", ", ignoredClasses.Order())}";
        }

        if (ExportQueuedForFocusing > 0)
        {
            if (sorted.FirstOrDefault(a => a.Export?.UIndex == ExportQueuedForFocusing) is { } proxy)
            {
                SelectedActor = proxy;
                ExportQueuedForFocusing = 0;
            }
        }

        UpdateTitle();
    }

    /// <summary>
    /// Materials whose game shaders failed in a batch (<see cref="MeshRenderContext.LoadPendingGameShaders"/>) are retried when drawn. Only one that fails
    /// then too uses LEX's shader, and its error is in the lighting diagnostics (<see cref="MaterialRenderProxy.LoadedGameShaderError"/>), so the batch's
    /// failures are only logged. Can be called from a background thread
    /// </summary>
    private static void RecordGameShaderLoadFailures(AggregateException e) => Debug.WriteLine(e);

    private bool ConfirmCloseFiles(string question)
    {
        if (!CanChangeFiles) return false;

        var dirtyFiles = OpenFiles.Where(f => f.IsDirty || f.Package.IsModified).ToList();
        if (!IsBakePreview && dirtyFiles.Count > 0)
        {
            string fileNames = string.Join(",\n", dirtyFiles.Select(f => f.FileName));
            if (MessageBox.Show(this,
                    $"The following files have unsaved changes:\n{fileNames}\n\n{question}",
                    "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return false;
        }

        // A save or preview refresh can start while the confirmation dialog is open.
        return CanChangeFiles;
    }

    private void CloseAllFiles()
    {
        RememberCameraInRecents();
        CancelViewportDrag();
        Game = MEGame.Unknown;
        // Through SelectActor, so that the details panes stop showing (and offering to select) the closed levels' actors
        SelectActor(null, false);
        PendingLightEdits.Clear();
        SceneViewer.SetShouldRender(false);
        PendingPreviewUpdates.Clear();
        PreviewShaderPreparationRequested = false;
        ForgetCaptureViews();
        RenderContext.UnloadLevel();
        Actors.Clear();
        foreach (var file in OpenFiles)
        {
            file.Dispose();
            StopTrackingSave(file);
        }
        OpenFiles.Clear();
        LinkedEdits.Clear();
        UnshadowedGeometry.Clear();
        HasAnyFileOpen = false;
        TextBelowActors = "";
        IsDirty = false;
        UndoHistory.Clear();
        _preEditSnapshot = null;
        //the chosen assets' packages nothing refers to, which an empty history didn't release: the previews' caches are empty now
        ReleaseUnused();
    }

    public async void CloseFile(OpenLevelFile file)
    {
        if (file is null || !OpenFiles.Contains(file) || IsBusy || PreviewRefreshInProgress) return;

        LinkedEdits.RemoveSavedGroups();
        var group = LinkedEdits.GetLinkedFiles(file).ToList();
        if (group.Count > 1)
        {
            string names = string.Join("\n", group.Select(f => f.FileName));
            var result = MessageBox.Show(this,
                $"Unsaved changes link these files:\n{names}\n\n" +
                $"Yes: Commit and save all linked files, then close {file.FileName}.\n" +
                "No: Close all listed files without saving, including their uncommitted changes.\nCancel: Keep all files open.",
                "Linked file edits", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            var choice = result switch
            {
                MessageBoxResult.Yes => LinkedFileCloseChoice.Save,
                MessageBoxResult.No => LinkedFileCloseChoice.Discard,
                _ => LinkedFileCloseChoice.Cancel
            };
            //busy only once the commit is done: a commit waits for whatever made the window busy
            bool saving = false;
            try
            {
                await LinkedFileEdits.CloseGroupAsync(group, file, choice, async () =>
                {
                    if (!CanSaveLinkedFiles(group) || !CommitChangesForFiles(group)) return false;
                    SetBusy("Saving linked files...");
                    saving = true;
                    // Committing pending edits may have added more files to this group.
                    var linked = LinkedEdits.GetLinkedFiles(file).ToList();
                    if (!CanSaveLinkedFiles(linked)) return false;
                    foreach (var related in linked) await related.Package.SaveAsync();
                    if (linked.Any(f => f.Package.IsModified)) return false;
                    LinkedEdits.RemoveSavedGroups();
                    return true;
                }, CloseFileCore);
                // Keep the association until every member was closed, or the complete group was saved.
                LinkedEdits.RemoveClosedGroups(OpenFiles);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Could not close linked files");
            }
            finally
            {
                if (saving)
                {
                    EndBusy();
                }
            }
            return;
        }

        if (file.IsDirty)
        {
            var result = MessageBox.Show(this,
                $"{file.FileName} has uncommitted changes. Close anyway?",
                "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }
        else if (file.Package.IsModified && file.Package.Users.Count <= 1)
        {
            var result = MessageBox.Show(this,
                $"{file.FileName} has unsaved changes. Close anyway?",
                "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        CloseFileCore(file);
    }

    private void CloseFileCore(OpenLevelFile file)
    {
        if (OpenFiles.Count == 1) RememberCameraInRecents();
        CancelViewportDrag();
        if (SelectedActor is not null && file.Actors.Contains(SelectedActor))
        {
            SelectedActor = null;
        }
        foreach (var actor in file.Actors)
        {
            Actors.Remove(actor);
            RenderContext.RemoveActor(actor);
            actor.Dispose();
        }
        RenderContext.RemoveLights(file.Lights);
        RenderContext.ForgetLevel(file.Package);

        string filePath = file.FilePath;
        file.Dispose();
        StopTrackingSave(file);
        OpenFiles.Remove(file);
        UnshadowedGeometry.Remove(file.Package);
        // Drops the cache's reference (and its update observer), so reopening the file without other users reads it from disk.
        // Kept if other open levels imported from it
        RenderContext.PackageCache.StopUsingPackage(filePath);
        //reopening it starts new actors, even if the package object is shared and comes back
        RenderContext.Registry.Forget(file.Package);
        //its actors' undo steps went with them, and so do the deletions made with them in other files
        List<string> errors = [];
        CancelDeletions([], errors);
        UndoHistory.RemoveOrphaned();
        //(its pastes went with it; the clipboard stays, to paste into another level)
        ReleaseUnused();
        HasAnyFileOpen = OpenFiles.Count > 0;
        UpdateGlobalDirtyState();
        UpdateTitle();

        if (OpenFiles.Count == 0)
        {
            SceneViewer.SetShouldRender(false);
            // No scene still references the caches. This also joins the texture worker and releases its uploads/readers.
            ForgetCaptureViews();
            RenderContext.UnloadLevel();
            TextBelowActors = "";
            UndoHistory.Clear();
            _preEditSnapshot = null;
            ReleaseUnused();
        }
        //(a dialog keeps dispatching, so it comes once the file's gone)
        if (errors.Count != 0) MessageBox.Show(this, string.Join("\n", errors), "Pending deletions");
    }

    public void CloseFileByName(string fileName)
    {
        var file = OpenFiles.FirstOrDefault(f => f.FileName == fileName);
        if (file is not null)
        {
            CloseFile(file);
        }
    }

    private void UpdateTitle()
    {
        if (OpenFiles.Count == 0)
            Title = "Level Editor";
        else if (OpenFiles.Count == 1)
            Title = $"Level Editor - {OpenFiles[0].FilePath}";
        else
            Title = $"Level Editor - {OpenFiles.Count} files";

        StatusBar_LeftMostText.Text = OpenFiles.Count switch
        {
            0 => "Select package file to load",
            1 => OpenFiles[0].FileName,
            _ => $"{OpenFiles.Count} files loaded"
        };
    }

    private async void LoadRelatedLevels()
    {
        if (OpenFiles.Count == 0) return;

        var firstFile = OpenFiles[0];
        string rootFilename = firstFile.FileName;
        MEGame game = Game;

        if (rootFilename.StartsWith("Bio") && rootFilename.Length > 3
            && rootFilename[3] is 'P' or 'D' or 'A' or 'S'
            && rootFilename.Split('_') is [_, string levelIdent, ..]
            && levelIdent.Split('.') is [string realLevelIdent, ..])
        {
            List<(string filename, string path)> candidates = [];
            var regex = new Regex($"^Bio[PDA]_{realLevelIdent}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var openFilePaths = OpenFiles.Select(f => Path.GetFileName(f.FilePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach ((string filename, string path) in MELoadedFiles.GetFilesLoadedInGame(game))
            {
                if (regex.IsMatch(filename) && !filename.Contains("_LOC_", StringComparison.OrdinalIgnoreCase) && !openFilePaths.Contains(filename))
                {
                    candidates.Add((filename, path));
                }
            }
            if (candidates.Count is 0) return;

            var dialogItems = candidates.Select(c => new CheckedListItem
            {
                DisplayName = c.filename,
                IsSelected = true,
                Tag = c.path
            }).ToList();

            var dialog = new CheckedListDialog(dialogItems, "Load Related Levels",
                $"Select levels related to {realLevelIdent} to load:", this);

            if (dialog.ShowDialog() != true) return;

            var selectedPaths = dialog.GetSelectedItems().Select(i => (string)i.Tag).ToList();
            if (selectedPaths.Count is 0) return;

            using var guard = new RenderGuard(this);

            for (int i = 0; i < selectedPaths.Count; i++)
            {
                await AddLevelFile(selectedPaths[i], i + 1, selectedPaths.Count).ConfigureAwait(true);
            }
        }
    }

    private void CloseFileExecute(object parameter)
    {
        OpenLevelFile file = ResolveFileParameter(parameter);
        if (file is not null)
        {
            CloseFile(file);
        }
    }

    private OpenLevelFile ResolveFileParameter(object parameter)
    {
        if (parameter is OpenLevelFile file) return file;
        if (parameter is string fileName)
        {
            return OpenFiles.FirstOrDefault(f => f.FileName == fileName);
        }
        return null;
    }

    private async void SaveFileAs()
    {
        if (IsBakePreview || OpenFiles.Count == 0) return;

        // Save As applies to the first file when only one is open,
        // otherwise prompt which file to save
        OpenLevelFile fileToSave;
        if (OpenFiles.Count == 1)
        {
            fileToSave = OpenFiles[0];
        }
        else
        {
            // For multi-file, Save As saves all files to a chosen directory
            // For simplicity, just save the selected actor's file, or the first file
            fileToSave = SelectedActor?.OwningFile ?? OpenFiles[0];
        }

        if (fileToSave.IsDirty)
        {
            switch (MessageBox.Show($"Do you want to commit your Level Editor changes (to every file) before saving {fileToSave.FileName}?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
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

        //only LE levels are opened (AddLevelPackage)
        string extension = Path.GetExtension(fileToSave.FilePath);
        var d = new SaveFileDialog { Filter = $"*{extension}|*{extension}" };
        if (d.ShowDialog() == true)
        {
            var filesToSave = LinkedEdits.GetLinkedFiles(fileToSave).ToList();
            if (!CanSaveLinkedFiles(filesToSave)) return;
            IReadOnlyList<LinkedFileSaveTarget> targets;
            try { targets = LinkedFileEdits.PlanSaveAs(fileToSave, filesToSave, d.FileName); }
            catch (InvalidOperationException error)
            {
                MessageBox.Show(this, error.Message, "Save linked files");
                return;
            }
            var companionOverwrites = targets.Where(t => t.File != fileToSave && File.Exists(t.Path)
                && !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(t.Path), Path.GetFullPath(t.File.FilePath))).ToList();
            if (companionOverwrites.Count > 0 && MessageBox.Show(this,
                    "Also overwrite these linked files?\n" + string.Join("\n", companionOverwrites.Select(t => t.Path)),
                    "Save linked files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            SetBusy("Saving...");
            try
            {
                foreach (var target in targets) await target.File.Package.SaveAsync(target.Path);
                LinkedEdits.RemoveSavedGroups();
            }
            finally
            {
                EndBusy();
            }
        }
    }

    private async void OpenFile()
    {
        var d = AppDirectories.GetOpenPackageDialog();
        if (d.ShowDialog() == true)
        {
#if !DEBUG
            try
            {
#endif
            await LoadFileAsync(d.FileName);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open file:\n" + ex.Message);
            }
#endif
        }
    }

    private async void AddFiles()
    {
        var d = AppDirectories.GetOpenPackageDialog("Add level files");
        d.Multiselect = true;
        if (d.ShowDialog() == true)
        {
            using var guard = new RenderGuard(this);
            await AddLevelFiles(d.FileNames).ConfigureAwait(true);
        }
    }

    /// <summary>Adds <paramref name="files"/> to the view in turn. One that fails to load is reported, and the rest still load. Callers hold a RenderGuard</summary>
    private async Task AddLevelFiles(IReadOnlyList<string> files)
    {
        for (int i = 0; i < files.Count; i++)
        {
            try
            {
                await AddLevelFile(files[i], i + 1, files.Count).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(files[i]);
                MessageBox.Show(this, $"Error loading {Path.GetFileName(files[i])}:\n{ex.Message}", "Load level");
            }
        }
    }

    // The busy indicator disables the editor's controls, but not dropping files on the window. Loading or refreshing must finish
    // before the scene can be replaced, since background work is still reading it
    private bool CanChangeFiles => !IsBusy && !PreviewRefreshInProgress && !loadingBakePreview;

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (!CanChangeFiles)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string ext = Path.GetExtension(files[0]).ToLower();
            if (ext != ".upk" && ext != ".pcc" && ext != ".sfm")
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (CanChangeFiles && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            bool isFirst = true;
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(file => Path.GetExtension(file).ToLowerInvariant() is ".upk" or ".pcc" or ".sfm").ToArray();
            if (files.Length is 0) return;
            if (PackageIsLoaded())
            {
                string q = files.Length is 1 ? "this file" : "these files";
                var result = MessageBox.Show($"Do you want to add {q} to the existing level view? Select no to unload all open files first.", "Add to files?", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Cancel) return;
                //a refresh can start while the question is shown
                if (!CanChangeFiles)
                {
                    MessageBox.Show(this, "The Level Editor started refreshing its preview. Drop the files again once it has finished.", "Level Editor is busy");
                    return;
                }
                isFirst = result == MessageBoxResult.No;
            }
            if (isFirst && !ConfirmCloseFiles("Open the new files anyway?")) return;
            using var guard = new RenderGuard(this);
            if (isFirst) CloseAllFiles();
            await AddLevelFiles(files).ConfigureAwait(true);
        }
    }

    private readonly struct RenderGuard : IDisposable
    {
        private readonly LevelEditor levelEditor;
        private readonly IDisposable load;

        public RenderGuard(LevelEditor levEd)
        {
            levelEditor = levEd;
            load = levEd.RenderContext.BeginLoad();
            levelEditor.SceneViewer.SetShouldRender(false);
            levelEditor.SetBusy();
        }

        public readonly void Dispose()
        {
            load.Dispose();
            levelEditor.SceneViewer.SetShouldRender(true);
            levelEditor.EndBusy();
        }
    }
}
