using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Interaction logic for LevelEditor.xaml
/// </summary>
public partial class LevelEditor : WPFBase, IRecents
{
    public readonly LevelEditorRenderContext RenderContext;

    public ObservableCollectionExtended<ActorProxy> Actors { get; } = [];
    private ExportEntry LevelExport;

    private ActorProxy selectedActor;
    public ActorProxy SelectedActor
    {
        get => selectedActor;
        set
        {
            if (SetProperty(ref selectedActor, value) && selectedActor is not null)
            {
                FocusOnBounds(selectedActor.GetBounds());
                RenderContext.TransformWidget.Attach = selectedActor;
            }
        }
    }

    private readonly List<(IMEPackage, ExportEntry)> OverlayLevels = [];
    private readonly List<ActorProxy> OverlayActors = [];

    private bool isDirty;
    public bool IsDirty
    {
        get => isDirty;
        set => SetProperty(ref isDirty, value);
    }

    private bool _showCollision;
    public bool ShowCollision
    {
        get => _showCollision;
        set => SetProperty(ref _showCollision, value);
    }

    private bool _showVolumes = true;
    public bool ShowVolumes
    {
        get => _showVolumes;
        set => SetProperty(ref _showVolumes, value);
    }

    private bool _showLevelsOverlay;
    public bool ShowLevelsOverlay
    {
        get => _showLevelsOverlay;
        set => SetProperty(ref _showLevelsOverlay, value);
    }

    public bool UseLocalCoordsForWidget
    {
        get => RenderContext.TransformWidget.UseLocalCoords; 
        set => SetProperty(ref RenderContext.TransformWidget.UseLocalCoords, value);
    }

    public string Toolname => "LevelEditor";
    public LevelEditor() : base("LevelEditor")
    {
        RenderContext = new LevelEditorRenderContext();

        LoadCommands();
        InitializeComponent();
        RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);

        SceneViewer.Context = RenderContext;
    }

    private string FileQueuedForLoad;
    private int ExportQueuedForFocusing;

    public LevelEditor(ExportEntry exportToLoad) : this()
    {
        FileQueuedForLoad = exportToLoad.FileRef.FilePath;
        ExportQueuedForFocusing = exportToLoad.UIndex;
    }

    private void UpdateScene(object sender, float e)
    {

    }

    private void RenderScene(object sender, EventArgs e)
    {
        Span<RenderPass> passes = ShowCollision 
            ? [RenderPass.Base, RenderPass.Hair, RenderPass.Collision] 
            : [RenderPass.Base, RenderPass.Hair];
        

        foreach (RenderPass pass in passes)
        {
            if (ShowLevelsOverlay)
            {
                RenderContext.CurrentHitTestId = Vector3.Zero;
                foreach (ActorProxy actor in OverlayActors)
                {
                    if (actor.IsVolume && !ShowVolumes) continue;
                    actor.Render(RenderContext, pass);
                }
            }
            DoRenderPass(pass);
        }

        RenderContext.DrawUI();
    }
    void DoRenderPass(RenderPass pass)
    {
        for (int i = 0; i < RenderContext.DrawList_3D.Count; i++)
        {
            ActorProxy actor = RenderContext.DrawList_3D[i];
            if (actor.IsVolume && !ShowVolumes) continue;
            RenderContext.CurrentHitTestId = new Vector3((i & 0xFF) / 255f, ((i >> 8) & 0xFF) / 255f, ((i >> 16) & 0xFF) / 255f);
            if (actor == selectedActor)
            {
                RenderContext.RenderFlags |= LevelEditorRenderContext.ShaderFlags.Selected;
            }
            actor.Render(RenderContext, pass);
            RenderContext.RenderFlags &= ~LevelEditorRenderContext.ShaderFlags.Selected;
        }
    }

    private void ViewportActorSelect(ActorProxy actor)
    {
        //don't set the property normally, as we don't want to trigger FocusOnBounds
        SetProperty(ref selectedActor, actor, nameof(SelectedActor));
        MeshExportsList.ScrollIntoView(selectedActor);
    }

    private void CenterView()
    {
        if (Actors.Count > 0)
        {
            //place camera at the edge of the bounding sphere containing all actors, 30 degrees up, facing the midpoint 
            BoxSphereBounds fullBounds = Actors[0].GetBounds();
            for (int i = 1; i < Actors.Count; i++)
            {
                fullBounds = fullBounds.Union(Actors[i].GetBounds());
            }
            FocusOnBounds(fullBounds);
        }
        else
        {
            RenderContext.Camera.Position = Vector3.Zero;
            RenderContext.Camera.Pitch = -MathF.PI / 5.0f;
            RenderContext.Camera.Yaw = MathF.PI / 4.0f;
        }
    }

    private void FocusOnBounds(BoxSphereBounds fullBounds)
    {
        Vector3 origin = fullBounds.Origin;
        float hyp = fullBounds.SphereRadius.Clamp(10, float.MaxValue) * 2;
        (float sin, float cos) = MathF.SinCos(MathF.PI / 2.5f);
        RenderContext.Camera.Position = new Vector3(origin.X, origin.Y + sin * hyp, origin.Z + cos * hyp);
        RenderContext.Camera.OrientTowards(origin);
    }

    private void LoadLevel(Level level, bool isReload = false)
    {
        (Vector3, float, float) savedCamPOV = default;
        Vector3 savedActorPos = default;
        if (isReload && SelectedActor is not null)
        {
            savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
            savedActorPos = SelectedActor.Location;
            ExportQueuedForFocusing = SelectedActor.Export.UIndex;
        }

        IsBusy = true;
        BusyText = "Loading level...";
        UnloadLevel();
        LevelExport = level.Export;
        Task.Run(() => LoadActors(level)).ContinueWithOnUIThread(prevTask =>
        {
            var (actors, ignoredClasses) = prevTask.Result;
            Actors.AddRange(actors.OrderBy(actor => actor.Export.UIndex));
            RenderContext.LoadLevel(Actors);
            if (!isReload)
            {
                CenterView();
            }

            SceneViewer.SetShouldRender(true);
            IsBusy = false;

            if (ignoredClasses.Count > 0)
            {
                TextBelowActors = $"Unrendered Actor types:\n{string.Join(", ", ignoredClasses)}";
            }

            if (ExportQueuedForFocusing > 0)
            {
                if (Actors.FirstOrDefault(a => a.Export.UIndex == ExportQueuedForFocusing) is { } proxy)
                {
                    SelectedActor = proxy;
                }
                ExportQueuedForFocusing = 0;
                if (isReload)
                {
                    (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw) 
                    = (savedCamPOV.Item1 + SelectedActor.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
                }
            }
        });
    }

    private (List<ActorProxy>, HashSet<string> ignoredActorClasses) LoadActors(Level level)
    {
        var actorExports = level.Actors.Where(level.Export.FileRef.IsUExport).Select(level.Export.FileRef.GetUExport);
        var actors = new List<ActorProxy>();
        HashSet<string> ignoredActorClasses = [];
        foreach (var actorExport in actorExports)
        {
            var className = actorExport.ClassName;
            if (className is "StaticMeshCollectionActor")
            {
                var smca = actorExport.GetBinaryData<StaticMeshCollectionActor>();
                for (int i = 0; i < smca.Components.Count; i++)
                {
                    if (level.Export.FileRef.TryGetUExport(smca.Components[i], out ExportEntry smcExport))
                    {
                        var smcActor = new StaticMeshComponentActorProxy(this, smcExport, smca, i);
                        actors.Add(smcActor);
                    }
                }
            }
            //else if (className is "StaticLightCollectionActor")
            //{
            //    var slca = actorExport.GetBinaryData<StaticLightCollectionActor>();
            //    for (int i = 0; i < slca.Components.Count; i++)
            //    {
            //        if (Pcc.TryGetUExport(slca.Components[i], out ExportEntry lightComponentExport))
            //        {

            //        }
            //    }
            //}
            else if (ActorProxy.Create(this, actorExport) is { } actorProxy)
            {
                actors.Add(actorProxy);
            }
            else if (className is not "BioWorldInfo")
            {
                ignoredActorClasses.Add(className);
            }
        }
        return (actors, ignoredActorClasses);
    }

    public void LoadFile(string s)
    {
        try
        {
            UnloadLevel();
            Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.ContextIdle, null);
            LoadMEPackage(s);

            StatusBar_LeftMostText.Text = Path.GetFileName(s);
            Title = $"Level Editor - {s}";

            RecentsController.AddRecent(s, false, Pcc?.Game);
            RecentsController.SaveRecentList(true);

            if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is { } levelExport)
            {
                Level levelBin = levelExport.GetBinaryData<Level>();
                LoadLevel(levelBin);
            }
            else
            {
                MessageBox.Show(this, "This is not a level file!");
                UnLoadMEPackage();
            }

        }
        catch (Exception e)
        {
            StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(s);
            MessageBox.Show($"Error loading {Path.GetFileName(s)}:\n{e.Message}");
            IsBusy = false;
            IsBusyTaskbar = false;
            //throw e;
        }
    }

    public void UnloadLevel()
    {
        SceneViewer.SetShouldRender(false);
        RenderContext.UnloadLevel();
        ClearOverlay();
        Actors.Clear();
        TextBelowActors = "";
        LevelExport = null;
        IsDirty = false;
    }

    public ICommand OpenFileCommand { get; set; }
    public ICommand SaveFileCommand { get; set; }
    public ICommand SaveAsCommand { get; set; }
    public ICommand ToggleTranslateCommand { get; set; }
    public ICommand ToggleRotateCommand { get; set; }
    public ICommand ToggleScaleCommand { get; set; }
    public ICommand ToggleUniformScaleCommand { get; set; }
    public ICommand CommitChangesCommand { get; set; }
    public ICommand LoadRelatedLevelsCommand { get; set; }
    private void LoadCommands()
    {
        OpenFileCommand = new GenericCommand(OpenFile);
        SaveFileCommand = new GenericCommand(SaveFile, PackageIsLoaded);
        SaveAsCommand = new GenericCommand(SaveFileAs, PackageIsLoaded);
        ToggleTranslateCommand = new GenericCommand(() => RenderContext.TransformWidget.Mode = EWidgetMode.Translate, PackageIsLoaded);
        ToggleRotateCommand = new GenericCommand(() => RenderContext.TransformWidget.Mode = EWidgetMode.Rotate, PackageIsLoaded);
        ToggleScaleCommand = new GenericCommand(() => RenderContext.TransformWidget.Mode = EWidgetMode.Scale, PackageIsLoaded);
        ToggleUniformScaleCommand = new GenericCommand(() => RenderContext.TransformWidget.Mode = EWidgetMode.UniformScale, PackageIsLoaded);
        CommitChangesCommand = new GenericCommand(CommitChanges, PackageIsLoaded);
        LoadRelatedLevelsCommand = new GenericCommand(LoadRelatedLevels, PackageIsLoaded);
    }

    private void LoadRelatedLevels()
    {
        if (Pcc is null) return;

        ClearOverlay();

        string rootFilename = Path.GetFileName(Pcc.FilePath);
        if (rootFilename.StartsWith("Bio") && rootFilename.Length > 3
            && rootFilename[3] is 'P' or 'D' or 'A' or 'S'
            && rootFilename.Split('_') is [_, string levelIdent, ..]
            && levelIdent.Split('.') is [string realLevelIdent, ..])
        {
            List<string> paths = [];
            var regex = new Regex($"^Bio[PDA]_{realLevelIdent}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach ((string filename, string path) in MELoadedFiles.GetFilesLoadedInGame(Pcc.Game))
            {
                if (regex.IsMatch(filename) && !filename.Contains("_LOC_", StringComparison.OrdinalIgnoreCase) && filename != rootFilename)
                {
                    paths.Add(path);
                }
            }
            if (paths.Count is 0) return;

            BusyText = "Loading levels...";
            SceneViewer.SetShouldRender(false);
            IsBusy = true;
            Task.Run(() =>
            {
                foreach (string path in paths)
                {
                    IMEPackage pcc = MEPackageHandler.OpenMEPackage(path);
                    if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is { } levelExport)
                    {
                        OverlayLevels.Add((pcc, levelExport));
                        Level levelBin = levelExport.GetBinaryData<Level>();
                        (var actors, _) = LoadActors(levelBin);
                        OverlayActors.AddRange(actors);
                    }
                }
                foreach (var actor in OverlayActors)
                {
                    actor.Editor = null;
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                IsBusy = false;
                SceneViewer.SetShouldRender(true);
                ShowLevelsOverlay = true;
            });
        }
    }

    private void ClearOverlay()
    {
        foreach ((IMEPackage pcc, _) in OverlayLevels)
        {
            pcc?.Dispose();
        }
        OverlayLevels.Clear();
        OverlayActors.DisposeAndClear();
    }

    private void Goto_TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && !e.IsRepeat)
        {
            GotoButton_Clicked(null, null);
        }
    }

    private void GotoButton_Clicked(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(Goto_TextBox.Text, out int uIdx) 
            && Actors.FirstOrDefault(a => a.Export.UIndex == uIdx) is ActorProxy actor)
        {
            SelectedActor = actor;
        }
    }

    private void CommitChanges()
    {
        if (!PackageIsLoaded() || Actors.Count is 0) return;

        Dictionary<int, StaticCollectionActor> collectionActorMap = [];

        foreach (ActorProxy actor in Actors)
        {
            if (!actor.IsDirty)
            {
                continue;
            }
            if (actor is CollectionActorComponentProxy cacp)
            {
                if (!collectionActorMap.TryGetValue(cacp.CollectionActorExport.UIndex, out var collectionActor))
                {
                    collectionActor = (StaticCollectionActor)ObjectBinary.From(cacp.CollectionActorExport);
                    collectionActorMap.Add(cacp.CollectionActorExport.UIndex, collectionActor);
                }
                cacp.CommitChanges(collectionActor);
            }
            else
            {
                actor.CommitChanges();
            }
        }

        foreach (var collectionActor in collectionActorMap.Values)
        {
            collectionActor.Export.WriteBinary(collectionActor);
        }
        IsDirty = false;
    }

    public override void HandleUpdate(List<PackageUpdate> updates)
    {
        if (LevelExport is null)
        {
            return; //nothing is loaded
        }

        IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.Change.Has(PackageChange.Export));
        HashSet<int> updatedExports = relevantUpdates.Select(x => x.Index).ToHashSet();
        if (LevelExport is not null && updatedExports.Contains(LevelExport.UIndex))
        {
            ReloadLevel();
        }
        else
        {
            bool updated = false;
            int reselectUIndex = 0;
            (Vector3, float, float) savedCamPOV = default;
            Vector3 savedActorPos = default;
            List<ExportEntry> collectionActorsToUpdate = [];
            for (int i = Actors.Count - 1; i >= 0; i--)
            {
                ActorProxy alteredActor = Actors[i];
                if (alteredActor.TestUIndexes(updatedExports))
                {
                    updated = true;
                    if (alteredActor == SelectedActor)
                    {
                        reselectUIndex = alteredActor.Export.UIndex;
                        savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
                        savedActorPos = SelectedActor.Location;
                    }
                    if (alteredActor is CollectionActorComponentProxy cacp)
                    {
                        collectionActorsToUpdate.Add(cacp.Export);
                        continue;
                    }
                    Actors.RemoveAt(i);
                    if (Pcc.GetEntry(alteredActor.Export.UIndex) is ExportEntry actorExport 
                        && ActorProxy.Create(this, actorExport) is { } actorProxy)
                    {
                        Actors.Add(actorProxy);
                    }
                }
            }
            foreach (var collectionActor in collectionActorsToUpdate)
            {
                for (int i = Actors.Count - 1; i >= 0; i++)
                {
                    if (Actors[i] is CollectionActorComponentProxy)
                    {
                        Actors.RemoveAt(i);
                    }
                }
                if (Pcc.GetEntry(collectionActor.UIndex) is ExportEntry newCollectionActor)
                {
                    string className = newCollectionActor.ClassName;
                    if (className is "StaticMeshCollectionActor")
                    {
                        var smca = newCollectionActor.GetBinaryData<StaticMeshCollectionActor>();
                        for (int i = 0; i < smca.Components.Count; i++)
                        {
                            if (Pcc.TryGetUExport(smca.Components[i], out ExportEntry smcExport))
                            {
                                var smcActor = new StaticMeshComponentActorProxy(this, smcExport, smca, i);
                                Actors.Add(smcActor);
                            }
                        }
                    }
                    else if (className is "StaticLightCollectionActor")
                    {

                    }
                }
            }
            if (updated)
            {
                Actors.Sort(a => a.Export.UIndex);
                IsDirty = Actors.Any(a => a.IsDirty);
            }
            if (reselectUIndex is not 0)
            {
                SelectedActor = Actors.FirstOrDefault(a => a.Export.UIndex == reselectUIndex);
                (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw)
                = (savedCamPOV.Item1 + SelectedActor.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
            }
        }
    }

    private bool PackageIsLoaded() => Pcc != null;

    private async void SaveFile()
    {
        if (IsDirty)
        {
            switch (MessageBox.Show("Do you want to commit your Level Editor changes before saving this file?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChanges();
                    break;
                case MessageBoxResult.No:
                    //continue on
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        await Pcc.SaveAsync();
    }

    private async void SaveFileAs()
    {
        if (IsDirty)
        {
            switch (MessageBox.Show("Do you want to commit your Level Editor changes before saving this file?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChanges();
                    break;
                case MessageBoxResult.No:
                    //continue on
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        string fileFilter;
        switch (Pcc.Game)
        {
            case MEGame.ME1:
                fileFilter = GameFileFilters.ME1SaveFileFilter;
                break;
            case MEGame.ME2:
            case MEGame.ME3:
                fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                break;
            default:
                string extension = Path.GetExtension(Pcc.FilePath);
                fileFilter = $"*{extension}|*{extension}";
                break;
        }
        var d = new SaveFileDialog { Filter = fileFilter };
        if (d.ShowDialog() == true)
        {
            IsBusy = true;
            BusyText = "Saving...";
            await Pcc.SaveAsync(d.FileName);
            IsBusy = false;
        }
    }

    private void OpenFile()
    {
        var d = AppDirectories.GetOpenPackageDialog();
        if (d.ShowDialog() == true)
        {
#if !DEBUG
            try
            {
#endif
            LoadFile(d.FileName);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open file:\n" + ex.Message);
            }
#endif
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // Note that you can have more than one file.
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

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // Note that you can have more than one file.
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string ext = Path.GetExtension(files[0]).ToLower();
            if (ext is ".upk" or ".pcc" or ".sfm")
            {
                LoadFile(files[0]);
            }
        }
    }
    private void LevelEditor_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (e.Cancel)
            return;

        UnloadLevel();

        RenderContext.UpdateScene -= UpdateScene;
        RenderContext.RenderScene -= RenderScene;
        RenderContext.SelectActor -= ViewportActorSelect;

        SceneViewer.Dispose();
        RecentsController?.Dispose();
        UnLoadMEPackage();
    }
    public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
    {
        RecentsController.PropogateRecentsChange(false, newRecents);
    }

    private void LevelEditor_Loaded(object sender, RoutedEventArgs e)
    {
        RenderContext.UpdateScene += UpdateScene;
        RenderContext.RenderScene += RenderScene;
        RenderContext.SelectActor += ViewportActorSelect;

        if (!string.IsNullOrEmpty(FileQueuedForLoad))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                //Wait for all children to finish loading
                LoadFile(FileQueuedForLoad);
                FileQueuedForLoad = null;

                Activate();
            }));
        }
    }

    private float _posIncrement = 10f;
    public float PosIncrement
    {
        get => _posIncrement;
        set => SetProperty(ref _posIncrement, value);
    }

    private float _rotIncrement = 5f;
    public float RotIncrement
    {
        get => _rotIncrement;
        set => SetProperty(ref _rotIncrement, value);
    }

    private float _scaleIncrement = 0.1f;
    public float ScaleIncrement
    {
        get => _scaleIncrement;
        set => SetProperty(ref _scaleIncrement, value);
    }

    private string textBelowActors;
    public string TextBelowActors { get => textBelowActors; set => SetProperty(ref textBelowActors, value); }

    private void ResetUncommittedChanges_Click(object sender, RoutedEventArgs e)
    {
        if (IsDirty && MessageBox.Show("Are you sure you want to reset uncommitted changes?", "Reset confirmation", MessageBoxButton.YesNo) is MessageBoxResult.Yes)
        {
            ReloadLevel();
        }
    }

    private void ReloadLevel()
    {
        LoadLevel(LevelExport.GetBinaryData<Level>(), true);
    }
}
