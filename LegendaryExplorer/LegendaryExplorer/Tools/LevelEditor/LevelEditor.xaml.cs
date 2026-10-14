using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Interaction logic for LevelEditor.xaml
/// </summary>
public class RecentFileSet
{
    public MEGame Game { get; set; }
    public List<string> FilePaths { get; set; } = [];
    public List<string> ReadOnlyFilePaths { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => FilePaths.Count switch
    {
        0 => "(empty)",
        1 => Path.GetFileName(FilePaths[0]),
        _ => $"{Path.GetFileName(FilePaths[0])} (+{FilePaths.Count - 1} more)"
    };

    [JsonIgnore]
    public string TooltipText => string.Join("\n", FilePaths.Select(Path.GetFileName));
}

public partial class LevelEditor : NotifyPropertyChangedWindowBase, IActorEditorContext
{
    public LevelEditorRenderContext RenderContext { get; }

    public ObservableCollectionExtended<OpenLevelFile> OpenFiles { get; } = [];
    public ObservableCollectionExtended<ActorProxy> Actors { get; } = [];
    public ICollectionView ActorsView { get; }
    public ListCollectionView PendingChangesView { get; }
    private string _actorFilterText = "";

    // Store expansion by group identity, not its recycled visual container.
    // A level subgroup has independent state within each category.
    private readonly Dictionary<(string Category, object Name), OutlinerGroupState> _outlinerGroupStates = [];

    internal OutlinerGroupState GetOutlinerGroupState(CollectionViewGroup group)
    {
        string category = null;
        if (GroupActorsByCategory && group.IsBottomLevel)
        {
            category = ActorsView.Groups.Cast<CollectionViewGroup>()
                .FirstOrDefault(parent => parent.Items.Contains(group))?.Name as string;
        }
        var key = (category, group.Name);
        if (!_outlinerGroupStates.TryGetValue(key, out var state))
        {
            // Categories start collapsed; levels, including those inside a category, start expanded.
            _outlinerGroupStates[key] = state = new OutlinerGroupState
            {
                Category = group.Name is string name ? RenderContext.GetCategoryState(name) : null,
                IsExpanded = group.Name is not string
            };
        }
        return state;
    }

    private void RevealActorInOutliner(ActorProxy actor)
    {
        foreach (CollectionViewGroup group in ActorsView.Groups)
        {
            if (group.IsBottomLevel)
            {
                if (group.Name == actor.OwningFile) GetOutlinerGroupState(group).IsExpanded = true;
            }
            else if (Equals(group.Name, actor.Category))
            {
                GetOutlinerGroupState(group).IsExpanded = true;
                foreach (CollectionViewGroup level in group.Items)
                {
                    if (level.Name == actor.OwningFile) GetOutlinerGroupState(level).IsExpanded = true;
                }
            }
        }
        MeshExportsList.UpdateLayout();
        MeshExportsList.ScrollIntoView(actor);
    }

    private bool _groupActorsByCategory = true;
    public bool GroupActorsByCategory
    {
        get => _groupActorsByCategory;
        set
        {
            if (SetProperty(ref _groupActorsByCategory, value))
            {
                OnPropertyChanged(nameof(GroupActorsByLevel));
                UpdateOutlinerGrouping();
            }
        }
    }

    public bool GroupActorsByLevel
    {
        get => !GroupActorsByCategory;
        set { if (value) GroupActorsByCategory = false; }
    }

    private void UpdateOutlinerGrouping()
    {
        var selection = SelectedActor;
        using (ActorsView.DeferRefresh())
        {
            ActorsView.GroupDescriptions.Clear();
            ActorsView.SortDescriptions.Clear();
            if (GroupActorsByCategory)
            {
                ActorsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActorProxy.Category)));
                ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.Category), ListSortDirection.Ascending));
            }
            // Keep package identity and package actions available in both modes.
            ActorsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActorProxy.OwningFile)));
            ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.OwningFileName), ListSortDirection.Ascending));
            ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.DisplayText), ListSortDirection.Ascending));
            ActorsView.SortDescriptions.Add(new SortDescription("Export.UIndex", ListSortDirection.Ascending));
        }
        // Refresh can temporarily clear ListBox selection. Restore without moving the camera.
        SelectActor(selection, false);
        if (selection is not null) MeshExportsList?.ScrollIntoView(selection);
    }

    private bool _hasAnyFileOpen;
    public bool HasAnyFileOpen
    {
        get => _hasAnyFileOpen;
        private set => SetProperty(ref _hasAnyFileOpen, value);
    }

    private MEGame _game = MEGame.Unknown;
    public MEGame Game
    {
        get => _game;
        private set => SetProperty(ref _game, value);
    }

    private ActorProxy selectedActor;
    public ActorProxy SelectedActor
    {
        get => selectedActor;
        set
        {
            SelectActor(value, true);
        }
    }

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

    private bool _showHidden = false;
    public bool ShowHidden
    {
        get => _showHidden;
        set => SetProperty(ref _showHidden, value);
    }

    private readonly ViewFrustum viewFrustum = new();
    private bool _useFrustumCulling = true;
    public bool UseFrustumCulling
    {
        get => _useFrustumCulling;
        set => SetProperty(ref _useFrustumCulling, value);
    }

    private bool _useGameShaders = true;
    public bool UseGameShaders
    {
        get => _useGameShaders;
        set
        {
            if (SetProperty(ref _useGameShaders, value))
            {
                if (value)
                {
                    EnableGameShaders();
                }
                else
                {
                    RenderContext.UseGameShaders = false;
                    LightDiagnosticsStale = true;
                }
                OnPropertyChanged(nameof(CanConfigureLevelLighting));
            }
        }
    }

    private ViewportLightingMode _lightingMode = ViewportLightingMode.Level;
    public ViewportLightingMode LightingMode
    {
        get => _lightingMode;
        set
        {
            if (SetProperty(ref _lightingMode, value))
            {
                OnPropertyChanged(nameof(IsLevelLighting));
                OnPropertyChanged(nameof(IsPreviewLighting));
                OnPropertyChanged(nameof(IsNeutralLighting));
                OnPropertyChanged(nameof(IsUnlit));
                OnPropertyChanged(nameof(LightingMenuHeader));
                OnPropertyChanged(nameof(CanConfigureLevelLighting));
                OnPropertyChanged(nameof(LightingRulesNote));
                LightDiagnosticsStale = true;
                if (value is (ViewportLightingMode.Level or ViewportLightingMode.Preview) && RenderContext.UseGameShaders)
                {
                    EnableGameShaders();
                }
                else
                {
                    RenderContext.LightingMode = value;
                }
            }
        }
    }

    public bool IsLevelLighting => LightingMode == ViewportLightingMode.Level;
    public bool IsNeutralLighting => LightingMode == ViewportLightingMode.Neutral;
    public bool IsPreviewLighting => LightingMode == ViewportLightingMode.Preview;
    public bool IsUnlit => LightingMode == ViewportLightingMode.Unlit;
    public bool CanConfigureLevelLighting => IsLevelLighting && UseGameShaders;
    public string LightingMenuHeader => LightingMode switch
    {
        ViewportLightingMode.Unlit => "Unlit ▾",
        ViewportLightingMode.Neutral => "Neutral lighting ▾",
        ViewportLightingMode.Preview => "Preview lighting ▾",
        _ => "Level lighting ▾"
    };

    public bool UseDynamicLighting
    {
        get => RenderContext.UseDynamicLighting;
        set
        {
            RenderContext.UseDynamicLighting = value;
            OnPropertyChanged();
        }
    }

    public bool UseLightMaps
    {
        get => RenderContext.UseLightMaps;
        set
        {
            RenderContext.UseLightMaps = value;
            OnPropertyChanged();
        }
    }

    public ICommand SetLightingModeCommand { get; private set; }

    // Mode switches share the same serialized shader worker as package refreshes.
    private void EnableGameShaders()
    {
        // Switching to a mode that's already prepared needn't suspend rendering, or wait for anything but loads and refreshes,
        // which read the scene in the background. (Saves don't)
        if (OpenFiles.Count == 0 || _busyCount == 0 && !PreviewRefreshInProgress && !RenderContext.NeedsGameShaderPreparation(_lightingMode))
        {
            RenderContext.UseGameShaders = _useGameShaders;
            RenderContext.LightingMode = _lightingMode;
            return;
        }
        PreviewShaderPreparationRequested = true;
        PreviewUpdateTimer.Start();
    }

    //Can be called from a background thread
    private void ReportGameShaderLoadProgress(int loaded, int total)
    {
        if (loaded % 10 == 0 || loaded == total)
        {
            Dispatcher.InvokeAsync(() => BusyText = $"Loading game shaders ({loaded}/{total})...");
        }
    }

    public bool UseLocalCoordsForWidget
    {
        get => RenderContext.TransformWidget.UseLocalCoords;
        set => SetProperty(ref RenderContext.TransformWidget.UseLocalCoords, value);
    }

    public bool TranslateSnapEnabled
    {
        get => RenderContext.TransformWidget.TranslateSnapEnabled;
        set => SetProperty(ref RenderContext.TransformWidget.TranslateSnapEnabled, value);
    }
    public float TranslateSnapValue
    {
        get => RenderContext.TransformWidget.TranslateSnapValue;
        set => SetProperty(ref RenderContext.TransformWidget.TranslateSnapValue, value);
    }
    public bool RotateSnapEnabled
    {
        get => RenderContext.TransformWidget.RotateSnapEnabled;
        set => SetProperty(ref RenderContext.TransformWidget.RotateSnapEnabled, value);
    }
    public float RotateSnapValue
    {
        get => RenderContext.TransformWidget.RotateSnapValue;
        set => SetProperty(ref RenderContext.TransformWidget.RotateSnapValue, value);
    }
    public bool ScaleSnapEnabled
    {
        get => RenderContext.TransformWidget.ScaleSnapEnabled;
        set => SetProperty(ref RenderContext.TransformWidget.ScaleSnapEnabled, value);
    }
    public float ScaleSnapValue
    {
        get => RenderContext.TransformWidget.ScaleSnapValue;
        set => SetProperty(ref RenderContext.TransformWidget.ScaleSnapValue, value);
    }

    public ObservableCollectionExtended<RecentFileSet> RecentSets { get; } = [];

    private static string RecentSetsFile => Path.Combine(
        Directory.CreateDirectory(Path.Combine(AppDirectories.AppDataFolder, "LevelEditor")).FullName,
        "RECENTSETS");

    public bool IsBakePreview { get; }
    public bool CanEditScene => !IsBakePreview;
    private bool loadingBakePreview;

    public LevelEditor() : this(false) { }

    public LevelEditor(bool readOnlyPreview)
    {
        IsBakePreview = readOnlyPreview;
        RenderContext = new LevelEditorRenderContext(readOnlyPreview)
        {
            UseGameShaders = _useGameShaders
        };
        // Not Background: continuous rendering of a slow scene (such as preview lighting) never leaves the dispatcher idle,
        // so a Background timer could starve and leave lighting-mode switches and package refreshes pending indefinitely.
        PreviewUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(75)
        };
        PreviewUpdateTimer.Tick += ProcessPreviewUpdates;
        RenderContext.PackageCache.PackageUpdated += QueuePreviewUpdate;
        RenderContext.TransformWidget.OnDragComplete = OnWidgetDragComplete;
        RenderContext.LightGizmo.OnDragComplete = OnLightGizmoDragComplete;
        ActorsView = CollectionViewSource.GetDefaultView(Actors);
        ActorsView.Filter = ActorFilter;
        // A preview refresh can change a mesh's category (whether it's volumetric)
        if (ActorsView is ICollectionViewLiveShaping { CanChangeLiveGrouping: true } liveView)
        {
            liveView.LiveGroupingProperties.Add(nameof(ActorProxy.Category));
            liveView.IsLiveGrouping = true;
        }
        UpdateOutlinerGrouping();

        // A separate view keeps the change list independent of outliner searching/grouping.
        // Live filtering also updates it for viewport drags, undo, redo and commits.
        PendingChangesView = new ListCollectionView(Actors)
        {
            Filter = actor => actor is ActorProxy { IsDirty: true }
        };
        PendingChangesView.LiveFilteringProperties.Add(nameof(ActorProxy.IsDirty));
        PendingChangesView.IsLiveFiltering = true;

        LoadCommands();
        InitializeComponent();
        if (IsBakePreview)
        {
            // Reclaim the space reserved for the editor's details and changes panes.
            DetailsColumn.MinWidth = 0;
            DetailsColumn.Width = new GridLength(0);
            DetailsSplitterColumn.Width = new GridLength(0);
            ChangesRow.MinHeight = 0;
            ChangesRow.Height = new GridLength(0);
            ChangesSplitterRow.Height = new GridLength(0);
        }
        AllowDrop = !IsBakePreview;
        if (!IsBakePreview) LoadRecentSets();

        SceneViewer.Context = RenderContext;
        UndoHistory.PropertyChanged += UndoHistory_PropertyChanged;
        OpenFiles.CollectionChanged += (_, _) =>
        {
            foreach (var key in _outlinerGroupStates.Keys
                         .Where(key => key.Name is OpenLevelFile file && !OpenFiles.Contains(file)).ToArray())
            {
                _outlinerGroupStates.Remove(key);
            }
        };
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
        ApplyActorLightingEdits();
        ApplyLightEdits();
        UpdateLightHighlights();
        if (LightDiagnosticsStale && !RenderContext.LightGizmo.IsDragging && !RenderContext.TransformWidget.IsDragging
            && LightDiagnosticsThrottle.ElapsedMilliseconds > 250)
        {
            RefreshLightingDiagnostics();
        }
        // Match MeshRenderContext.CreateRasterizerState, which disables near/far depth clipping.
        if (UseFrustumCulling) viewFrustum.Update(RenderContext.Camera.ViewProjectionMatrix, depthClipEnabled: false);
        RenderContext.CullingFrustum = UseFrustumCulling ? viewFrustum : null;
        RenderContext.ShowHidden = ShowHidden;
        Span<RenderPass> passes = ShowCollision
            ? [RenderPass.Base, RenderPass.Hair, RenderPass.Lighting, RenderPass.Translucent, RenderPass.Collision]
            : [RenderPass.Base, RenderPass.Hair, RenderPass.Lighting, RenderPass.Translucent];

        foreach (RenderPass pass in passes)
        {
            DoRenderPass(pass);
            if (pass is RenderPass.Lighting)
            {
                //draws the queued light passes, then light environments' shadows, which darken the lit scene but not translucency
                RenderContext.EndLightingPass();
            }
        }

        RenderContext.DrawUI();
    }
    void DoRenderPass(RenderPass pass)
    {
        for (int i = 0; i < RenderContext.DrawList_3D.Count; i++)
        {
            ActorProxy actor = RenderContext.DrawList_3D[i];
            if (!RenderContext.IsActorVisible(actor)) continue;
            int hitID = actor.HitID;
            RenderContext.CurrentHitTestId = new Vector3((hitID & 0xFF) / 255f, ((hitID >> 8) & 0xFF) / 255f, ((hitID >> 16) & 0xFF) / 255f);
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
        if (actor is null) SelectActor(null, false);
        else SelectAndRevealActor(actor, false);
    }

    private void SelectActor(ActorProxy actor, bool focus)
    {
        // A handle drag belongs to the light it started on, so it's recorded before the gizmo moves to another
        if (actor != selectedActor) RenderContext.LightGizmo.EndDrag();
        var prev = selectedActor;
        if (SetProperty(ref selectedActor, actor, nameof(SelectedActor)))
        {
            if (prev is not null)
            {
                prev.PropertyChanged -= OnActorPropertyChanged;
            }
            if (prev is ILightProxy { Light: { } prevLight })
            {
                prevLight.PropertyChanged -= OnSelectedLightChanged;
            }
            if (selectedActor is not null)
            {
                selectedActor.PropertyChanged += OnActorPropertyChanged;
                _preEditSnapshot = selectedActor.SnapshotTransform();
            }
            else
            {
                _preEditSnapshot = null;
            }
            _preEditLightSettings = null;
            if (selectedActor is ILightProxy { Light: { } light })
            {
                light.PropertyChanged += OnSelectedLightChanged;
                _preEditLightSettings = light.Settings;
            }
            RenderContext.LightGizmo.Attach = selectedActor as ILightProxy;
            OnPropertyChanged(nameof(SelectedLight));
            OnPropertyChanged(nameof(IsLightSelected));
            RefreshLightingDiagnostics();
        }
        if (focus && selectedActor is not null)
        {
            FocusOnBounds(selectedActor.GetBounds());
            RenderContext.TransformWidget.Attach = selectedActor;
        }
    }

    private void CenterView()
    {
        if (Actors.Count > 0)
        {
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
        if (RenderContext.Camera.IsOrthographic)
        {
            RenderContext.Camera.Position = new Vector3(origin.X, origin.Y, RenderContext.Camera.ZFar * 0.4f);
            RenderContext.Camera.OrthoWidth = fullBounds.SphereRadius.Clamp(10, float.MaxValue) * 3f;
        }
        else
        {
            float hyp = fullBounds.SphereRadius.Clamp(10, float.MaxValue) * 2;
            (float sin, float cos) = MathF.SinCos(MathF.PI / 2.5f);
            RenderContext.Camera.Position = new Vector3(origin.X, origin.Y + sin * hyp, origin.Z + cos * hyp);
            RenderContext.Camera.OrientTowards(origin);
        }
    }

    #region File Management

    public async Task LoadFileAsync(string s)
    {
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
            foreach (var package in levels) await AddLevelPackage(package);
            CenterView();
            Title = "Lightmass Preview — close this window to Save or Discard in Lightmass Baker";
        }
        finally
        {
            loadingBakePreview = false;
        }
    }

    private async Task AddLevelFile(string path)
    {
        path = Path.GetFullPath(path);

        if (OpenFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} is already open.");
            return;
        }

        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(path);
        await AddLevelPackage(pcc);
    }

    private async Task AddLevelPackage(IMEPackage pcc)
    {
        string path = pcc.FilePath;
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
        BusyText = $"Loading {Path.GetFileName(path)}...";

        bool loadGameShaders = RenderContext.UseGameShaders;
        var (actors, ignoredClasses) = await Task.Run(() =>
        {
            var result = LoadActors(levelBin, openFile);
            if (loadGameShaders)
            {
                RenderContext.LoadPendingGameShaders(ReportGameShaderLoadProgress);
            }
            return result;
        }).ConfigureAwait(true);
        var sorted = actors.OrderBy(actor => actor.Export.UIndex).ToList();
        openFile.Actors.AddRange(sorted);
        Actors.AddRange(sorted);
        RenderContext.LoadActors(sorted);

        if (isFirstFile)
        {
            CenterView();
        }

        if (ignoredClasses.Count > 0)
        {
            string existing = string.IsNullOrEmpty(TextBelowActors) ? "" : TextBelowActors + "\n";
            TextBelowActors = existing + $"{Path.GetFileName(path)} unrendered: {string.Join(", ", ignoredClasses)}";
        }

        if (ExportQueuedForFocusing > 0)
        {
            if (sorted.FirstOrDefault(a => a.Export.UIndex == ExportQueuedForFocusing) is { } proxy)
            {
                SelectedActor = proxy;
                ExportQueuedForFocusing = 0;
            }
        }

        UpdateTitle();
    }

    private void CloseAllFiles()
    {
        Game = MEGame.Unknown;
        // Through SelectActor, so that the details panes stop showing (and offering to select) the closed levels' actors
        SelectActor(null, false);
        PendingLightEdits.Clear();
        SceneViewer.SetShouldRender(false);
        PendingPreviewUpdates.Clear();
        RenderContext.UnloadLevel();
        Actors.Clear();
        foreach (var file in OpenFiles)
        {
            file.Dispose();
            StopTrackingSave(file);
        }
        OpenFiles.Clear();
        HasAnyFileOpen = false;
        TextBelowActors = "";
        IsDirty = false;
        UndoHistory.Clear();
        _preEditSnapshot = null;
    }

    public void CloseFile(OpenLevelFile file)
    {
        if (file is null) return;

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
        // Drops the cache's reference (and its update observer), so reopening the file without other users reads it from disk.
        // Kept if other open levels imported from it
        RenderContext.PackageCache.StopUsingPackage(filePath);
        UndoHistory.RemoveOrphaned();
        HasAnyFileOpen = OpenFiles.Count > 0;
        UpdateGlobalDirtyState();
        UpdateTitle();

        if (OpenFiles.Count == 0)
        {
            SceneViewer.SetShouldRender(false);
            TextBelowActors = "";
            UndoHistory.Clear();
            _preEditSnapshot = null;
        }
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

    #endregion

    #region Actor Loading

    private readonly record struct LoadActorsResult(List<ActorProxy> actors, HashSet<string> ignoredActorClasses);

    private LoadActorsResult LoadActors(Level level, OpenLevelFile owningFile)
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
                        smcActor.OwningFile = owningFile;
                        actors.Add(smcActor);
                    }
                }
            }
            else if (className is "StaticLightCollectionActor")
            {
                actors.AddRange(CreateCollectionLights(actorExport, owningFile));
            }
            else if (ActorProxy.Create(this, actorExport) is { } actorProxy)
            {
                actorProxy.OwningFile = owningFile;
                actors.Add(actorProxy);
            }
            else if (className is not "BioWorldInfo")
            {
                ignoredActorClasses.Add(className);
            }
        }
        foreach (var actor in actors)
        {
            actor.ResolveAttachment(actors);
        }

        //The level's lights, for rendering static meshes the way the game lights them. (Light actors aren't otherwise shown)
        owningFile.ReloadLights(level, RenderContext);
        //The level's BSP blocks light environments' visibility traces
        RenderContext.SetLevelModel(level.Export.FileRef, level.Model);
        return new(actors, ignoredActorClasses);
    }

    /// <summary>
    /// Proxies for a StaticLightCollectionActor's supported lights. Lights without a stored transform can't be placed, so they aren't editable
    /// </summary>
    private List<ActorProxy> CreateCollectionLights(ExportEntry collectionExport, OpenLevelFile owningFile)
    {
        var lights = new List<ActorProxy>();
        var slca = collectionExport.GetBinaryData<StaticLightCollectionActor>();
        if (slca.Components is null) return lights;
        for (int i = 0; i < slca.Components.Count && i < slca.LocalToWorldTransforms.Count; i++)
        {
            if (collectionExport.FileRef.TryGetUExport(slca.Components[i], out ExportEntry lightComponent)
                && SceneLight.GetLightType(lightComponent) is { } type)
            {
                lights.Add(new StaticLightCollectionComponentProxy(this, lightComponent, slca, i, type) { OwningFile = owningFile });
            }
        }
        return lights;
    }

    public void RemoveActor(ActorProxy actor)
    {
        if (Actors.Remove(actor))
        {
            actor.Detach();
            actor.OwningFile?.Actors.Remove(actor);
            RenderContext.RemoveActor(actor);
            actor.Dispose();
        }
    }

    public void AddActor(ActorProxy actor, bool sort = true)
    {
        if (!Actors.Contains(actor))
        {
            Actors.Add(actor);
            actor.ResolveAttachment(Actors);
            actor.OwningFile?.Actors.Add(actor);
            RenderContext.AddActor(actor);
            if (sort)
            {
                Actors.Sort(a => a.Export.UIndex);
            }
        }
    }

    #endregion

    #region Commands

    public ICommand OpenFileCommand { get; set; }
    public ICommand AddFileCommand { get; set; }
    public ICommand SaveAllCommand { get; set; }
    public ICommand SaveAsCommand { get; set; }
    public ICommand SaveSingleFileCommand { get; set; }
    public ICommand CloseFileCommand { get; set; }
    public ICommand ToggleTranslateCommand { get; set; }
    public ICommand ToggleRotateCommand { get; set; }
    public ICommand ToggleScaleCommand { get; set; }
    public ICommand ToggleUniformScaleCommand { get; set; }
    public ICommand CommitChangesCommand { get; set; }
    public ICommand CommitSingleFileCommand { get; set; }
    public ICommand LoadRelatedLevelsCommand { get; set; }
    public ICommand FocusSelectedCommand { get; set; }
    public ICommand ToggleLocalCoordsCommand { get; set; }
    public ICommand OpenInPackageEditorCommand { get; set; }
    public ICommand OpenRecentSetCommand { get; set; }
    public ICommand UndoCommand { get; set; }
    public ICommand RedoCommand { get; set; }
    public ICommand ToggleOrthoViewCommand { get; set; }
    public ICommand FocusFileCommand { get; set; }
    public ICommand SelectChangedActorCommand { get; set; }
    public ICommand SelectLightCommand { get; set; }
    private void LoadCommands()
    {
        SetLightingModeCommand = new RelayCommand(mode => LightingMode = (ViewportLightingMode)mode);
        OpenFileCommand = new GenericCommand(OpenFile, () => CanEditScene);
        AddFileCommand = new GenericCommand(AddFile, () => CanEditScene);
        SaveAllCommand = new GenericCommand(SaveAllFiles, () => CanEditScene && PackageIsLoaded());
        SaveAsCommand = new GenericCommand(SaveFileAs, () => CanEditScene && PackageIsLoaded());
        SaveSingleFileCommand = new RelayCommand(SaveSingleFileExecute, _ => CanEditScene && PackageIsLoaded());
        CloseFileCommand = new RelayCommand(CloseFileExecute, _ => CanEditScene);
        ToggleTranslateCommand = new GenericCommand(() => { RenderContext.TransformWidget.Mode = EWidgetMode.Translate; CurrentModeName = "Translate"; }, PackageIsLoaded);
        ToggleRotateCommand = new GenericCommand(() => { RenderContext.TransformWidget.Mode = EWidgetMode.Rotate; CurrentModeName = "Rotate"; }, PackageIsLoaded);
        ToggleScaleCommand = new GenericCommand(() => { RenderContext.TransformWidget.Mode = EWidgetMode.Scale; CurrentModeName = "Scale"; }, PackageIsLoaded);
        ToggleUniformScaleCommand = new GenericCommand(() => { RenderContext.TransformWidget.Mode = EWidgetMode.UniformScale; CurrentModeName = "Uniform Scale"; }, PackageIsLoaded);
        CommitChangesCommand = new GenericCommand(CommitChanges, () => CanEditScene && PackageIsLoaded());
        CommitSingleFileCommand = new RelayCommand(CommitSingleFileExecute, _ => CanEditScene && PackageIsLoaded());
        LoadRelatedLevelsCommand = new GenericCommand(LoadRelatedLevels, () => CanEditScene && PackageIsLoaded());
        FocusSelectedCommand = new GenericCommand(() =>
        {
            if (SelectedActor is not null)
            {
                SelectAndRevealActor(SelectedActor, false);
                FocusOnBounds(SelectedActor.GetBounds());
            }
        }, () => PackageIsLoaded() && SelectedActor is not null);
        ToggleLocalCoordsCommand = new GenericCommand(() => UseLocalCoordsForWidget = !UseLocalCoordsForWidget, PackageIsLoaded);
        OpenInPackageEditorCommand = new GenericCommand(() =>
        {
            if (SelectedActor is not null)
            {
                var p = new PackageEditorWindow();
                p.Show();
                p.LoadFile(SelectedActor.Export.FileRef.FilePath, SelectedActor.Export.UIndex);
                p.Activate();
            }
        }, () => CanEditScene && PackageIsLoaded() && SelectedActor is not null);
        OpenRecentSetCommand = new RelayCommand(obj => { if (obj is RecentFileSet set) OpenRecentFileSet(set); });
        UndoCommand = new GenericCommand(Undo, () => UndoHistory.CanUndo);
        RedoCommand = new GenericCommand(Redo, () => UndoHistory.CanRedo);
        ToggleOrthoViewCommand = new GenericCommand(() => IsOrthographicView = !IsOrthographicView);
        FocusFileCommand = new RelayCommand(parameter =>
        {
            if (parameter is OpenLevelFile file && file.Actors.FirstOrDefault() is { } actor)
            {
                SelectAndRevealActor(actor);
            }
        });
        SelectChangedActorCommand = new RelayCommand(parameter =>
        {
            if (parameter is ActorProxy actor)
            {
                SelectAndRevealActor(actor);
            }
        });
        SelectLightCommand = new RelayCommand(parameter =>
        {
            if (parameter is ActorProxy light)
            {
                // Lights are hidden by default, and selecting one from the details pane means wanting to see it
                RenderContext.GetCategoryState(light.Category).IsVisible = true;
                SelectAndRevealActor(light);
            }
        });
    }

    #endregion

    #region Undo/Redo
    public readonly UndoHistory UndoHistory = new();
    private TransformSnapshot? _preEditSnapshot;
    private bool _isApplyingUndoRedo;
    public bool IsApplyingUndoRedo => _isApplyingUndoRedo;

    public bool CanUndo => UndoHistory.CanUndo;
    public bool CanRedo => UndoHistory.CanRedo;

    private void UndoHistory_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Undo_Clicked(object sender, RoutedEventArgs e) => Undo();
    public void Undo()
    {
        if (UndoHistory.CanUndo)
        {
            _isApplyingUndoRedo = true;
            UndoHistory.Undo();
            _isApplyingUndoRedo = false;
            if (SelectedActor is not null)
            {
                _preEditSnapshot = SelectedActor.SnapshotTransform();
            }
            _preEditLightSettings = SelectedLight?.Settings;
        }
    }

    public void Redo_Clicked(object sender, RoutedEventArgs e) => Redo();
    void Redo()
    {
        if (UndoHistory.CanRedo)
        {
            _isApplyingUndoRedo = true;
            UndoHistory.Redo();
            _isApplyingUndoRedo = false;
            if (SelectedActor is not null)
            {
                _preEditSnapshot = SelectedActor.SnapshotTransform();
            }
            _preEditLightSettings = SelectedLight?.Settings;
        }
    }
    #endregion

    #region Load Related Levels

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

            foreach (string path in selectedPaths)
            {
                await AddLevelFile(path).ConfigureAwait(true);
            }
        }
    }

    #endregion

    #region Lightmass Baker

    private void OpenInLightmassBaker_Click(object sender, RoutedEventArgs e)
    {
        if (OpenFiles.Count is 0) return;
        if (IsDirty || OpenFiles.Any(f => !f.IsReadOnly && f.Package.IsModified))
        {
            if (MessageBox.Show(this, "The Lightmass Baker bakes the files as saved on disk, so uncommitted and unsaved changes here aren't part of the bake, " +
                                      "and this editor won't show the new lighting until the files are reopened. Open the baker anyway?",
                    "Bake Lighting", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
            {
                return;
            }
        }
        new LightmassBaker.LightmassBakerWindow(OpenFiles.Select(f => (f.FilePath, !f.IsReadOnly))).Show();
    }

    #endregion

    #region Commit & Save

    private void CommitChanges()
    {
        if (!PackageIsLoaded() || Actors.Count is 0) return;

        foreach (var file in OpenFiles)
        {
            if (!file.IsReadOnly)
                CommitChangesForFile(file);
        }
        IsDirty = false;
    }

    private void CommitChangesForFile(OpenLevelFile file)
    {
        Dictionary<int, StaticCollectionActor> collectionActorMap = [];

        foreach (ActorProxy actor in file.Actors)
        {
            if (!actor.IsDirty)
            {
                continue;
            }
            if (actor is CollectionActorComponentProxy cacp)
            {
                bool moved = cacp.IsTransformDirty;
                // A light whose settings alone changed must not rewrite (and so rebuild) its whole collection.
                if (moved)
                {
                    if (!collectionActorMap.TryGetValue(cacp.CollectionActorExport.UIndex, out var collectionActor))
                    {
                        collectionActor = (StaticCollectionActor)ObjectBinary.From(cacp.CollectionActorExport);
                        collectionActorMap.Add(cacp.CollectionActorExport.UIndex, collectionActor);
                    }
                    cacp.CommitChanges(collectionActor);
                }
                if (cacp is ILightProxy collectionLight)
                {
                    collectionLight.Light.Commit(moved ? cacp.LocalToWorld : null);
                }
            }
            else
            {
                if (actor.IsTransformDirty || actor is ILightProxy) actor.CommitChanges();
            }
            foreach (var mesh in MeshComponents(actor)) mesh.ExistingLighting?.Commit();
            actor.MarkClean();
        }

        foreach (var collectionActor in collectionActorMap.Values)
        {
            collectionActor.Export.WriteBinary(collectionActor);
        }
        file.IsDirty = false;
        // Committing rebuilds the committed actors, which would orphan the actions that refer to them
        ClearUndoHistory();
    }

    private void ClearUndoHistory()
    {
        UndoHistory.Clear();
        _preEditSnapshot = selectedActor?.SnapshotTransform();
        _preEditLightSettings = SelectedLight?.Settings;
    }

    private void CommitSingleFileExecute(object parameter)
    {
        OpenLevelFile file = ResolveFileParameter(parameter);
        if (file is not null && !file.IsReadOnly)
        {
            CommitChangesForFile(file);
        }
    }

    private async void SaveAllFiles()
    {
        if (IsDirty)
        {
            switch (MessageBox.Show("Do you want to commit your Level Editor changes before saving all files?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChanges();
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        foreach (var file in OpenFiles)
        {
            if (!file.IsReadOnly && file.Package.IsModified)
            {
                await file.Package.SaveAsync();
            }
        }
    }

    private async void SaveSingleFileExecute(object parameter)
    {
        OpenLevelFile file = ResolveFileParameter(parameter);
        if (file is null || file.IsReadOnly) return;

        if (file.IsDirty)
        {
            switch (MessageBox.Show($"Do you want to commit changes to {file.FileName} before saving?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChangesForFile(file);
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }
        await file.Package.SaveAsync();
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
            switch (MessageBox.Show($"Do you want to commit changes to {fileToSave.FileName} before saving?", "Uncommitted changes", MessageBoxButton.YesNoCancel))
            {
                case MessageBoxResult.Yes:
                    CommitChangesForFile(fileToSave);
                    break;
                case MessageBoxResult.No:
                    break;
                case MessageBoxResult.Cancel:
                default:
                    return;
            }
        }

        string fileFilter;
        switch (fileToSave.Package.Game)
        {
            case MEGame.ME1:
                fileFilter = GameFileFilters.ME1SaveFileFilter;
                break;
            case MEGame.ME2:
            case MEGame.ME3:
                fileFilter = GameFileFilters.ME3ME2SaveFileFilter;
                break;
            default:
                string extension = Path.GetExtension(fileToSave.FilePath);
                fileFilter = $"*{extension}|*{extension}";
                break;
        }
        var d = new SaveFileDialog { Filter = fileFilter };
        if (d.ShowDialog() == true)
        {
            SetBusy("Saving...");
            try
            {
                await fileToSave.Package.SaveAsync(d.FileName);
            }
            finally
            {
                EndBusy();
            }
        }
    }

    #endregion

    #region HandleUpdate

    private readonly PreviewUpdateQueue PendingPreviewUpdates = new();
    private readonly DispatcherTimer PreviewUpdateTimer;
    private bool PreviewRefreshInProgress;
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
        // Both the open level and the dependency observer may notify us; the queue deduplicates indices.
        PendingPreviewUpdates.Add(package, updates);
        if (PendingPreviewUpdates.HasUpdates) PreviewUpdateTimer.Start();
    }

    private async void ProcessPreviewUpdates(object sender, EventArgs e)
    {
        // Package loading and shader preparation also read the scene on background threads.
        // Keep edits queued until those readers finish, and suspend rendering while replacing GPU resources. (Package saves aren't readers of the scene)
        if (_busyCount > 0 || PreviewRefreshInProgress || RenderContext.TransformWidget.IsDragging || RenderContext.LightGizmo.IsDragging) return;
        PreviewUpdateTimer.Stop();
        if ((!PendingPreviewUpdates.HasUpdates && !PreviewShaderPreparationRequested) || OpenFiles.Count == 0 || PreviewClosed) return;
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
            foreach (var (package, exports) in PendingPreviewUpdates.Drain())
                changed |= TryRefresh(Path.GetFileName(package.FilePath), errors, () => ApplyPreviewUpdate(package, exports, errors));
            var mode = LightingMode;
            if (changed && UseGameShaders)
                await Task.Run(() => RenderContext.LoadPendingGameShaders(ReportGameShaderLoadProgress, mode));
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
            RenderContext.UseGameShaders = _useGameShaders;
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
        bool changed = RenderContext.InvalidatePreviewResources(package, updatedExports, out PreviewRebuild rebuild);
        // Meshes that must be rebuilt to reacquire released resources, whether or not they depend on the edited exports
        bool MustRebuild(ActorProxy actor) => rebuild is PreviewRebuild.AllMeshes
            || rebuild is PreviewRebuild.LightEnvironmentMeshes && MeshComponents(actor).Any(mesh => mesh.IsLitByLightEnvironment);
        var file = OpenFiles.FirstOrDefault(open => open.Package == package);
        bool reloadLevel = file is not null && updatedExports.Contains(file.LevelExport.UIndex);
        // Decided against the old dependency snapshots, but reloaded after the actors, so rebuilt light proxies' discarded edits aren't reapplied.
        var lightFiles = OpenFiles.Where(openFile => !(openFile == file && reloadLevel)
            && openFile.Lighting?.IsAffectedBy(package, updatedExports) == true).ToList();
        changed |= lightFiles.Count > 0;
        foreach (var actor in Actors.Where(actor => actor.OwningFile != file).ToList())
            changed |= TryRefresh(actor.DisplayText, errors, () => actor.TryRefreshPreview(package, updatedExports, MustRebuild(actor)));
        if (file is not null)
            changed |= ApplyLevelActorUpdate(file, reloadLevel, updatedExports, MustRebuild, errors);
        if (lightFiles.Count > 0) RenderContext.InvalidateLightFunctions();
        foreach (var openFile in lightFiles)
            TryRefresh($"{openFile.FileName} lights", errors, () =>
            {
                openFile.ReloadLights(openFile.LevelExport.GetBinaryData<Level>(), RenderContext);
                return true;
            });
        return changed;
    }

    private bool ApplyLevelActorUpdate(OpenLevelFile file, bool reloadLevel, HashSet<int> updatedExports, Func<ActorProxy, bool> mustRebuild, List<string> errors)
    {
        bool changed = false;
        if (reloadLevel)
        {
            changed = true;
            ReloadFile(file);
        }
        else
        {
            bool updated = false;
            int reselectUIndex = 0;
            (Vector3, float, float) savedCamPOV = default;
            Vector3 savedActorPos = default;
            HashSet<ExportEntry> collectionActorsToUpdate = [];
            for (int i = file.Actors.Count - 1; i >= 0; i--)
            {
                ActorProxy alteredActor = file.Actors[i];
                if (TryRefresh(alteredActor.DisplayText, errors, () => alteredActor.TryRefreshPreview(file.Package, updatedExports, mustRebuild(alteredActor))))
                {
                    changed = true;
                    continue;
                }
                if (alteredActor.TestUIndexes(updatedExports))
                {
                    changed = updated = true;
                    if (alteredActor == SelectedActor)
                    {
                        reselectUIndex = alteredActor.Export.UIndex;
                        savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
                        savedActorPos = SelectedActor.Location;
                    }
                    if (alteredActor is CollectionActorComponentProxy cacp)
                    {
                        //the whole collection actor gets reloaded, since its components' transforms all live in its binary
                        collectionActorsToUpdate.Add(cacp.CollectionActorExport);
                        continue;
                    }
                    RemoveActor(alteredActor);
                    if (file.Package.GetEntry(alteredActor.Export.UIndex) is ExportEntry actorExport
                        && ActorProxy.Create(this, actorExport) is { } actorProxy)
                    {
                        actorProxy.OwningFile = file;
                        AddActor(actorProxy);
                    }
                }
            }
            foreach (var collectionActor in collectionActorsToUpdate)
            {
                for (int i = file.Actors.Count - 1; i >= 0; i--)
                {
                    if (file.Actors[i] is CollectionActorComponentProxy cacp && cacp.CollectionActorExport == collectionActor)
                    {
                        RemoveActor(file.Actors[i]);
                    }
                }
                if (file.Package.GetEntry(collectionActor.UIndex) is ExportEntry newCollectionActor)
                {
                    string className = newCollectionActor.ClassName;
                    if (className is "StaticMeshCollectionActor")
                    {
                        var smca = newCollectionActor.GetBinaryData<StaticMeshCollectionActor>();
                        for (int i = 0; i < smca.Components.Count; i++)
                        {
                            if (file.Package.TryGetUExport(smca.Components[i], out ExportEntry smcExport))
                            {
                                var smcActor = new StaticMeshComponentActorProxy(this, smcExport, smca, i);
                                smcActor.OwningFile = file;
                                AddActor(smcActor, false);
                            }
                        }
                    }
                    else if (className is "StaticLightCollectionActor")
                    {
                        foreach (var light in CreateCollectionLights(newCollectionActor, file))
                        {
                            AddActor(light, false);
                        }
                    }
                }
            }
            if (updated)
            {
                Actors.Sort(a => a.Export.UIndex);
                UpdateGlobalDirtyState();
            }
            if (reselectUIndex is not 0)
            {
                SelectedActor = Actors.FirstOrDefault(a => a.Export.UIndex == reselectUIndex && a.Export.FileRef == file.Package);
                if (SelectedActor is not null)
                {
                    (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw)
                    = (savedCamPOV.Item1 + SelectedActor.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
                }
            }
        }
        return changed;
    }

    private void ReloadFile(OpenLevelFile file)
    {
        // Remove all actors for this file, then re-load
        (Vector3, float, float) savedCamPOV = default;
        Vector3 savedActorPos = default;
        int reselectUIndex = 0;
        if (SelectedActor is not null && file.Actors.Contains(SelectedActor))
        {
            savedCamPOV = (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw);
            savedActorPos = SelectedActor.Location;
            reselectUIndex = SelectedActor.Export.UIndex;
            SelectedActor = null;
        }

        foreach (var actor in file.Actors.ToList())
        {
            Actors.Remove(actor);
            RenderContext.RemoveActor(actor);
            actor.Dispose();
        }
        file.Actors.Clear();

        Level levelBin = file.LevelExport.GetBinaryData<Level>();
        var (actors, _) = LoadActors(levelBin, file);
        var sorted = actors.OrderBy(a => a.Export.UIndex).ToList();
        file.Actors.AddRange(sorted);
        Actors.AddRange(sorted);
        RenderContext.LoadActors(sorted);

        if (reselectUIndex is not 0)
        {
            var reselect = Actors.FirstOrDefault(a => a.Export.UIndex == reselectUIndex && a.Export.FileRef == file.Package);
            if (reselect is not null)
            {
                SelectedActor = reselect;
                (RenderContext.Camera.Position, RenderContext.Camera.Pitch, RenderContext.Camera.Yaw)
                = (savedCamPOV.Item1 + reselect.Location - savedActorPos, savedCamPOV.Item2, savedCamPOV.Item3);
            }
        }

        file.IsDirty = false;
    }

    #endregion

    public void UpdateGlobalDirtyState()
    {
        IsDirty = OpenFiles.Any(f => f.IsDirty);
    }

    private bool PackageIsLoaded() => OpenFiles.Count > 0;

    private void OnActorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ActorProxy.Location) or nameof(ActorProxy.Rotation) or nameof(ActorProxy.DrawScale) or nameof(ActorProxy.DrawScale3D))) return;
        // Which lights reach the selection depends on its bounds. Refreshed once a drag ends
        LightDiagnosticsStale = true;
        if (_isApplyingUndoRedo || RenderContext.TransformWidget.IsDragging) return;

        if (sender is ActorProxy actor && _preEditSnapshot is { } before)
        {
            var after = actor.SnapshotTransform();
            if (!before.Equals(after))
            {
                UndoHistory.Push(new TransformAction(actor, before, after, $"Edit {actor.Export.ObjectName.Instanced}"));
                _preEditSnapshot = after;
            }
        }
    }

    private void OnWidgetDragComplete(ActorProxy actor, TransformSnapshot before, TransformSnapshot after)
    {
        if (before.Equals(after)) return;
        UndoHistory.Push(new TransformAction(actor, before, after, $"Drag {actor.Export.ObjectName.Instanced}"));
        _preEditSnapshot = after;
    }

    #region Lights

    public ObservableCollectionExtended<EditableActorLighting> SelectedActorLighting { get; } = [];
    private readonly HashSet<MeshComponentProxy> PendingActorLightingEdits = [];

    public void OnActorLightingEdited(MeshComponentProxy mesh, ActorLightingSettings before, ActorLightingSettings after)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(new ActorLightingAction(mesh.Lighting, before, after));
        PendingActorLightingEdits.Add(mesh);
        LightDiagnosticsStale = true;
    }

    private void ApplyActorLightingEdits()
    {
        if (PendingActorLightingEdits.Count == 0 || PreviewRefreshInProgress) return;
        foreach (var mesh in PendingActorLightingEdits)
        {
            if (mesh.Actor is { IsDisposed: false, OwningFile: { } file } actor && OpenFiles.Contains(file) && file.Actors.Contains(actor))
                mesh.RefreshPreview(mesh.Export.FileRef, new HashSet<int> { mesh.Export.UIndex }, force: true);
        }
        PendingActorLightingEdits.Clear();
        LightDiagnosticsStale = true;
    }

    private LightSettings? _preEditLightSettings;

    /// <summary>
    /// The selected light's editable settings. Null if the selection isn't a light
    /// </summary>
    public EditableLight SelectedLight => (selectedActor as ILightProxy)?.Light;
    public bool IsLightSelected => SelectedLight is not null;

    // Lights whose uncommitted edits haven't been rebuilt into the scene yet. Rebuilt once per frame, however many edits there were.
    private readonly HashSet<ILightProxy> PendingLightEdits = [];

    public void OnLightEdited(ILightProxy light)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => OnLightEdited(light));
            return;
        }
        PendingLightEdits.Add(light);
    }

    private void ApplyLightEdits()
    {
        if (PendingLightEdits.Count == 0 || PreviewRefreshInProgress) return;
        foreach (ILightProxy light in PendingLightEdits)
        {
            // Proxies replaced by a reload or closed with their file are skipped.
            if (light is ActorProxy { OwningFile: { } file } actor && OpenFiles.Contains(file) && file.Actors.Contains(actor))
            {
                file.UpdateLight(light, RenderContext);
            }
        }
        PendingLightEdits.Clear();
        LightDiagnosticsStale = true;
    }

    private void OnSelectedLightChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditableLight.Settings) || sender is not EditableLight light) return;
        // Undo, redo and handle drags record their own history.
        if (!_isApplyingUndoRedo && !RenderContext.LightGizmo.IsDragging && _preEditLightSettings is { } before && before != light.Settings
            && selectedActor is not null)
        {
            UndoHistory.Push(new LightSettingsAction(light, before, light.Settings, selectedActor.Export.ObjectName.Instanced));
        }
        _preEditLightSettings = light.Settings;
    }

    private void OnLightGizmoDragComplete(ILightProxy light, LightSettings before, LightSettings after)
    {
        if (light is not ActorProxy actor) return;
        UndoHistory.Push(new LightSettingsAction(light.Light, before, after, actor.Export.ObjectName.Instanced));
        _preEditLightSettings = after;
        LightDiagnosticsStale = true;
    }

    // Uses the active viewport's rules: Level lighting includes the last build's records; the other modes ignore them, like Preview lighting.
    private bool IgnorePrecomputedLighting => LightingMode != ViewportLightingMode.Level;

    private static IEnumerable<MeshComponentProxy> MeshComponents(ActorProxy actor) => actor is PrefabInstanceProxy prefab
        ? prefab.Children.SelectMany(MeshComponents)
        : actor.Components.OfType<MeshComponentProxy>();

    private SceneLight SelectedSceneLight => selectedActor is ILightProxy proxy ? selectedActor.OwningFile?.Lighting?.Find(proxy.Light.Component) : null;

    private bool _showLightReach;
    /// <summary>
    /// Highlight the meshes the selected light is eligible to light
    /// </summary>
    public bool ShowLightReach
    {
        get => _showLightReach;
        set
        {
            if (SetProperty(ref _showLightReach, value)) LightDiagnosticsStale = true;
        }
    }

    /// <summary>
    /// Highlights the meshes the selected light is eligible to light, if <see cref="ShowLightReach"/>. Cheap enough to do every frame, so it follows drags
    /// </summary>
    private void UpdateLightHighlights()
    {
        RenderContext.HighlightedComponents.Clear();
        if (ShowLightReach && SelectedSceneLight is { } light)
        {
            FindLightReach(light, RenderContext.HighlightedComponents);
        }
    }

    /// <summary>
    /// Counts the meshes a light is eligible to light, directly and through light environments, optionally collecting them
    /// </summary>
    private (int Direct, int Environments) FindLightReach(SceneLight light, HashSet<PrimitiveComponentProxy> meshes)
    {
        int direct = 0, environments = 0;
        bool ignorePrecomputed = IgnorePrecomputedLighting;
        foreach (ActorProxy actor in Actors)
        {
            if (actor is ILightProxy) continue;
            foreach (MeshComponentProxy mesh in MeshComponents(actor))
            {
                if (mesh.GetLightEligibility(light, ignorePrecomputed) is { } eligibility && eligibility.IsAffecting())
                {
                    meshes?.Add(mesh);
                    if (eligibility is LightEligibility.LightEnvironment) environments++;
                    else direct++;
                }
            }
        }
        return (direct, environments);
    }

    private void RefreshSelectedLightReach()
    {
        if (selectedActor is not ILightProxy proxy)
        {
            SelectedLightReach = null;
            return;
        }
        SceneLight light = SelectedSceneLight;
        if (!proxy.Light.Settings.Enabled)
        {
            SelectedLightReach = "Disabled: lights nothing.";
        }
        else if (light is null)
        {
            SelectedLightReach = "Not in the preview (unsupported or unreadable settings).";
        }
        else
        {
            (int direct, int environments) = FindLightReach(light, null);
            SelectedLightReach = $"Eligible to light {direct} mesh{(direct == 1 ? "" : "es")} directly and {environments} through light environments"
                                 + (ShowLightReach ? " (highlighted)." : ".");
        }
    }

    private string _selectedLightReach;
    public string SelectedLightReach
    {
        get => _selectedLightReach;
        private set => SetProperty(ref _selectedLightReach, value);
    }

    public string LightingRulesNote => LightingMode switch
    {
        ViewportLightingMode.Preview => "Preview rules: current light settings and positions; the last bake's light-map and irrelevance records are ignored. Preview lighting is direct light only, with no bounced light.",
        ViewportLightingMode.Level => "Level rules: includes the last bake's light-map and irrelevance records. Bounced light comes only from the existing light-maps, so it doesn't follow edits.",
        _ => "This lighting mode doesn't show the level's lights. Eligibility uses preview rules."
    };

    private bool LightDiagnosticsStale;
    private readonly Stopwatch LightDiagnosticsThrottle = Stopwatch.StartNew();

    public ObservableCollectionExtended<LightRelevance> SelectedMeshLights { get; } = [];
    public ObservableCollectionExtended<string> SelectedMeshNotes { get; } = [];

    private bool _isMeshSelected;
    public bool IsMeshSelected
    {
        get => _isMeshSelected;
        private set => SetProperty(ref _isMeshSelected, value);
    }

    /// <summary>
    /// Lists the lights relevant to the selected mesh: those eligible to light it, and those that reach it but are excluded, with the reason.
    /// Also notes where the mesh's appearance doesn't come from the level's lights
    /// </summary>
    private void RefreshLightingDiagnostics()
    {
        LightDiagnosticsStale = false;
        LightDiagnosticsThrottle.Restart();
        RefreshSelectedLightReach();
        RenderContext.HighlightedLights.Clear();
        var rows = new List<LightRelevance>();
        var notes = new List<string>();
        List<MeshComponentProxy> meshes = selectedActor is null or ILightProxy ? [] : MeshComponents(selectedActor).ToList();
        IsMeshSelected = meshes.Count > 0;
        var lighting = meshes.Select(mesh => mesh.Lighting).ToList();
        if (!SelectedActorLighting.SequenceEqual(lighting))
        {
            foreach (var settings in lighting) settings.RefreshEnvironments();
            SelectedActorLighting.ReplaceAll(lighting);
        }
        if (meshes.Count > 0)
        {
            bool ignorePrecomputed = IgnorePrecomputedLighting;
            var proxies = new Dictionary<ExportEntry, ActorProxy>();
            foreach (ActorProxy actor in Actors)
            {
                if (actor is ILightProxy proxy) proxies[proxy.Light.Component] = actor;
            }
            foreach (SceneLight light in RenderContext.GetLights(out _))
            {
                LightEligibility? best = null;
                foreach (MeshComponentProxy mesh in meshes)
                {
                    if (mesh.GetLightEligibility(light, ignorePrecomputed) is { } eligibility
                        && (best is null || eligibility.IsMoreRelevantThan(best.Value)))
                    {
                        best = eligibility;
                    }
                }
                // Lights that don't reach the mesh at all aren't worth listing.
                if (best is not { } result || !result.Reaches()) continue;
                proxies.TryGetValue(light.Export, out ActorProxy lightActor);
                rows.Add(new LightRelevance(lightActor?.DisplayText ?? light.Export.ObjectName.Instanced, light.LevelName, result.Describe(),
                    result.IsAffecting(), lightActor));
                if (result.IsAffecting()) RenderContext.HighlightedLights.Add(light.Export);
            }
            if (meshes.All(mesh => !mesh.IsLitByLevelLights))
            {
                notes.Add(Game.IsLEGame()
                    ? "The level's lights don't light this mesh in the viewport (its lighting couldn't be read), so it's shown with neutral lighting."
                    : "Level lighting previews need a Legendary Edition game; this mesh is shown with neutral lighting.");
            }
            else if (meshes.Any(mesh => mesh.IsLitByLightEnvironment))
            {
                notes.Add("Lit by a light environment, which composites the lights below into a few lights. Whether each light is blocked from it isn't shown here.");
            }
            if (!UseGameShaders)
            {
                notes.Add("Game shaders are off, so every mesh uses LEX's neutral shader.");
            }
            else
            {
                foreach (var material in meshes.SelectMany(mesh => mesh.GameShaderMaterials).Distinct())
                {
                    if (material.LoadedGameShaderError is { } error)
                    {
                        notes.Add($"{material.InstancedFullPath} uses LEX's neutral shader: {error}");
                    }
                }
            }
        }
        rows.Sort((a, b) => a.IsAffecting != b.IsAffecting ? b.IsAffecting.CompareTo(a.IsAffecting) : string.Compare(a.LightName, b.LightName, StringComparison.OrdinalIgnoreCase));
        SelectedMeshLights.ReplaceAll(rows);
        SelectedMeshNotes.ReplaceAll(notes);
    }

    #endregion

    private bool ActorFilter(object obj)
    {
        if (string.IsNullOrEmpty(_actorFilterText)) return true;
        return obj is ActorProxy actor &&
               (actor.DisplayText.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.Export.ClassName.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || (int.TryParse(_actorFilterText.TrimStart('#'), out int index) && actor.Export.UIndex == index));
    }

    private void ActorFilter_TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _actorFilterText = ((TextBox)sender).Text.Trim();
        ActorsView.Refresh();
    }

    private void SelectAndRevealActor(ActorProxy actor, bool focus = true)
    {
        if (!ActorFilter(actor)) ActorFilter_TextBox.Clear();
        SelectActor(actor, focus);
        RevealActorInOutliner(actor);
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
            SelectAndRevealActor(actor);
        }
    }

    #region Open / Drag-Drop

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

    private async void AddFile()
    {
        var d = AppDirectories.GetOpenPackageDialog();
        if (d.ShowDialog() == true)
        {
#if !DEBUG
            try
            {
#endif

            using var guard = new RenderGuard(this);
            await AddLevelFile(d.FileName).ConfigureAwait(true);
#if !DEBUG
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open file:\n" + ex.Message);
            }
#endif
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
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length is 0) return;
            if (PackageIsLoaded())
            {
                string q = files.Length is 1 ? "these files" : "";
                var result = MessageBox.Show("Do you want to add" + q + "to the existing level view? Select no to unload all open files first.", "Add to files?", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Cancel) return;
                //a refresh can start while the question is shown
                if (!CanChangeFiles)
                {
                    MessageBox.Show(this, "The Level Editor started refreshing its preview. Drop the files again once it has finished.", "Level Editor is busy");
                    return;
                }
                isFirst = result == MessageBoxResult.No;
            }
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext is not (".upk" or ".pcc" or ".sfm")) continue;

                if (isFirst && OpenFiles.Count == 0)
                {
                    await LoadFileAsync(file);
                    isFirst = false;
                }
                else
                {
                    using var guard = new RenderGuard(this);

                    await AddLevelFile(file).ConfigureAwait(true);
                    isFirst = false;
                }
            }
        }
    }

    #endregion

    #region Window Lifecycle

    private void LevelEditor_Closing(object sender, CancelEventArgs e)
    {
        if (e.Cancel) return;
        if (IsBusy || PreviewRefreshInProgress || loadingBakePreview)
        {
            // Background loads and saves are still using the scene and its packages
            e.Cancel = true;
            MessageBox.Show(this, "The Level Editor can't close until it has finished" + (string.IsNullOrEmpty(BusyText) ? "." : $": {BusyText}"),
                "Level Editor is busy");
            return;
        }

        var dirtyFiles = OpenFiles.Where(f => f.IsDirty || f.Package.IsModified).ToList();
        if (!IsBakePreview && dirtyFiles.Count > 0)
        {
            string fileNames = string.Join(",\n", dirtyFiles.Select(f => f.FileName));
            var result = MessageBox.Show(this,
                $"The following files have unsaved changes:\n{fileNames}\n\nClose anyway?",
                "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        CloseAllFiles();

        PreviewClosed = true;
        PreviewUpdateTimer.Stop();
        PendingPreviewUpdates.Clear();
        RenderContext.PackageCache.PackageUpdated -= QueuePreviewUpdate;
        RenderContext.UpdateScene -= UpdateScene;
        RenderContext.RenderScene -= RenderScene;
        RenderContext.SelectActor -= ViewportActorSelect;

        UndoHistory.PropertyChanged -= UndoHistory_PropertyChanged;
        UndoHistory.Clear();

        SceneViewer.Dispose();
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
                LoadFileAsync(FileQueuedForLoad);
                FileQueuedForLoad = null;

                Activate();
            }));
        }
    }

    #endregion

    #region Recent File Sets

    private void LoadRecentSets()
    {
        if (!File.Exists(RecentSetsFile)) return;
        try
        {
            var json = File.ReadAllText(RecentSetsFile);
            var sets = JsonConvert.DeserializeObject<List<RecentFileSet>>(json);
            if (sets is null) return;
            foreach (var set in sets)
            {
                set.FilePaths.RemoveAll(p => !File.Exists(p));
                if (set.FilePaths.Count > 0)
                    RecentSets.Add(set);
            }
        }
        catch { /* corrupt file, ignore */ }
        RefreshRecentsMenu();
    }

    private void SaveRecentSets()
    {
        var json = JsonConvert.SerializeObject(RecentSets.ToList(), Formatting.Indented);
        File.WriteAllText(RecentSetsFile, json);
        RefreshRecentsMenu();
    }

    private void RecordCurrentFilesAsRecent()
    {
        if (OpenFiles.Count == 0) return;
        var currentPaths = OpenFiles.Select(f => f.FilePath).ToList();

        for (int i = 0; i < RecentSets.Count; i++)
        {
            var existing = RecentSets[i].FilePaths;
            if (existing.Count > 0 && existing[0] == currentPaths[0])
            {
                RecentSets.RemoveAt(i);
            }
        }

        RecentSets.Insert(0, new RecentFileSet
        {
            Game = Game,
            FilePaths = currentPaths,
            ReadOnlyFilePaths = OpenFiles.Where(f => f.IsReadOnly).Select(f => f.FilePath).ToList()
        });

        while (RecentSets.Count > 10)
            RecentSets.RemoveAt(RecentSets.Count - 1);

        SaveRecentSets();
    }

    private async void OpenRecentFileSet(RecentFileSet set)
    {
        CloseAllFiles();

        using var guard = new RenderGuard(this);

        foreach (string path in set.FilePaths)
        {
            if (File.Exists(path))
            {
                await AddLevelFile(path).ConfigureAwait(true);
                var openFile = OpenFiles.LastOrDefault(f => f.FilePath == path);
                if (openFile is not null && set.ReadOnlyFilePaths.Contains(path))
                    openFile.IsReadOnly = true;
            }
        }
    }

    private void RefreshRecentsMenu()
    {
        Recents_MenuItem.Items.Clear();
        Recents_MenuItem.IsEnabled = RecentSets.Count > 0;
        foreach (var set in RecentSets)
        {
            var mi = new MenuItem
            {
                Header = set.DisplayName.Replace("_", "__"),
                ToolTip = set.TooltipText,
                Tag = set
            };
            mi.Click += (_, _) => OpenRecentFileSet((RecentFileSet)mi.Tag);
            Recents_MenuItem.Items.Add(mi);
        }
    }

    #endregion

    #region UI Properties

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

    private string _currentModeName = "Translate";
    public string CurrentModeName { get => _currentModeName; set => SetProperty(ref _currentModeName, value); }

    private bool _isOrthographicView;
    public bool IsOrthographicView
    {
        get => _isOrthographicView;
        set
        {
            if (SetProperty(ref _isOrthographicView, value))
            {
                if (value)
                {
                    RenderContext.Camera.SavePerspectiveState();
                    RenderContext.Camera.IsOrthographic = true;
                    var pos = RenderContext.Camera.Position;
                    RenderContext.Camera.Position = new Vector3(pos.X, pos.Y, RenderContext.Camera.ZFar * 0.4f);
                    float focusDepth = RenderContext.Camera.FocusDepth;
                    RenderContext.Camera.OrthoWidth = MathF.Max(focusDepth * 4f, 500f);
                }
                else
                {
                    RenderContext.Camera.IsOrthographic = false;
                    RenderContext.Camera.RestorePerspectiveState();
                }
            }
        }
    }

    private void ResetUncommittedChanges_Click(object sender, RoutedEventArgs e)
    {
        if (IsDirty && MessageBox.Show("Are you sure you want to reset uncommitted changes?", "Reset confirmation", MessageBoxButton.YesNo) is MessageBoxResult.Yes)
        {
            foreach (var file in OpenFiles)
            {
                ReloadFile(file);
            }
            UndoHistory.RemoveOrphaned();
        }
    }

    #endregion



    #region Busy variables

    private bool _isBusy;
    // Loading, Save As and preview refreshes can overlap, so the editor is busy until each has ended. (Package saves are tracked by SavingFiles)
    private int _busyCount;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    private bool _isBusyTaskbar;

    public bool IsBusyTaskbar
    {
        get => _isBusyTaskbar;
        set => SetProperty(ref _isBusyTaskbar, value);
    }

    private string _busyText;

    public string BusyText
    {
        get => _busyText;
        set => SetProperty(ref _busyText, value);
    }

    /// <summary>
    /// Marks the start of an operation the editor must wait for. Pair each call with <see cref="EndBusy"/>
    /// </summary>
    public virtual void SetBusy(string text = null)
    {
        //a nested operation without its own text keeps its outer operation's
        if (!IsBusy || text is not null) BusyText = text;
        _busyCount++;
        UpdateIsBusy();
    }
    public virtual void EndBusy()
    {
        if (_busyCount > 0) _busyCount--;
        UpdateIsBusy();
    }

    // Files whose packages are being saved. Tracked by file rather than counted, so that a save whose end is never reported
    // (because its file was closed, and so stopped being a user of the package) can't leave the editor busy for good
    private readonly HashSet<OpenLevelFile> SavingFiles = [];

    public void HandleSaveStateChange(OpenLevelFile file, bool isSaving)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => HandleSaveStateChange(file, isSaving));
            return;
        }
        if (isSaving ? SavingFiles.Add(file) : SavingFiles.Remove(file))
        {
            if (isSaving) BusyText = "Saving";
            UpdateIsBusy();
        }
    }

    private void StopTrackingSave(OpenLevelFile file)
    {
        if (SavingFiles.Remove(file)) UpdateIsBusy();
    }

    private void UpdateIsBusy() => IsBusy = _busyCount > 0 || SavingFiles.Count > 0;

    #endregion

    private readonly struct RenderGuard : IDisposable
    {
        private readonly LevelEditor levelEditor;

        public RenderGuard(LevelEditor levEd)
        {
            levelEditor = levEd;
            levelEditor.SceneViewer.SetShouldRender(false);
            levelEditor.SetBusy();
        }

        public readonly void Dispose()
        {
            levelEditor.SceneViewer.SetShouldRender(true);
            levelEditor.EndBusy();
        }
    }
}
