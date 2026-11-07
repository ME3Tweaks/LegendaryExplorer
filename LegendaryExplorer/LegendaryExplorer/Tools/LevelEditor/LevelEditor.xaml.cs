using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    // Pending deletions, which have left Actors (and so PendingChangesView)
    private readonly ObservableCollectionExtended<ActorProxy> pendingDeletionActors = [];
    /// <summary>The change list: changed actors in the scene, then those pending deletion</summary>
    public CompositeCollection PendingChanges { get; }
    public int PendingChangeCount => PendingChangesView.Count + pendingDeletionActors.Count;
    public bool HasPendingChanges => PendingChangeCount != 0;
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

    private void CollapseOutlinerGroups_Clicked(object sender, RoutedEventArgs e)
    {
        // Include cached groups hidden by the current search as well as unrealized groups.
        foreach (var state in _outlinerGroupStates.Values) state.IsExpanded = false;
        foreach (CollectionViewGroup group in ActorsView.Groups) CollapseGroup(group);

        void CollapseGroup(CollectionViewGroup group)
        {
            GetOutlinerGroupState(group).IsExpanded = false;
            if (!group.IsBottomLevel)
            {
                foreach (CollectionViewGroup child in group.Items) CollapseGroup(child);
            }
        }
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
                OnPropertyChanged(nameof(LightingRulesNote));
                LightDiagnosticsStale = true;
                if (value is ViewportLightingMode.Level or ViewportLightingMode.Preview)
                {
                    PrepareGameShaders();
                }
                else
                {
                    RenderContext.LightingMode = value;
                }
            }
        }
    }

    public LightAssignmentPolicy AssignmentPolicy
    {
        get => RenderContext.AssignmentPolicy;
        set
        {
            if (RenderContext.AssignmentPolicy == value) return;
            RenderContext.AssignmentPolicy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreserveAssignments));
            OnPropertyChanged(nameof(RecomputeAssignments));
            OnPropertyChanged(nameof(LightingRulesNote));
            LightDiagnosticsStale = true;
            if (IsPreviewLighting) PrepareGameShaders();
        }
    }
    public bool PreserveAssignments => AssignmentPolicy == LightAssignmentPolicy.PreserveExistingAssignments;
    public bool RecomputeAssignments => AssignmentPolicy == LightAssignmentPolicy.RecomputeAssignments;
    public ICommand SetAssignmentPolicyCommand { get; private set; }

    public bool IsLevelLighting => LightingMode == ViewportLightingMode.Level;
    public bool IsNeutralLighting => LightingMode == ViewportLightingMode.Neutral;
    public bool IsPreviewLighting => LightingMode == ViewportLightingMode.Preview;
    public bool IsUnlit => LightingMode == ViewportLightingMode.Unlit;
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

    /// <summary>
    /// Nothing else is changing the scene or reading it in the background: no preview refresh, file load or save (which read packages and the scene
    /// on other threads while the window stays responsive), history step or scene edit is in progress. Undo, redo and commit wait for this
    /// </summary>
    private bool CanMutateScene => CanEditScene && !IsBusy && !PreviewRefreshInProgress && !loadingBakePreview && !_isApplyingUndoRedo
        && !UndoHistory.IsExecuting && !RenderContext.IsEditingScene;
    private bool loadingBakePreview;

    public LevelEditor() : this(false) { }

    public LevelEditor(bool readOnlyPreview)
    {
        IsBakePreview = readOnlyPreview;
        RenderContext = new LevelEditorRenderContext(readOnlyPreview)
        {
            UseGameShaders = true,
            TextureStreamingEnabled = true
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
        RenderContext.TransformWidget.OnDuplicateDragStart = BeginDuplicateDrag;
        RenderContext.TransformWidget.OnDuplicateDragComplete = OnDuplicateDragComplete;
        RenderContext.LightGizmo.OnDragComplete = OnLightGizmoDragComplete;
        RenderContext.SplineGizmo.OnDragComplete = OnSplineGizmoDragComplete;
        ActorsView =CollectionViewSource.GetDefaultView(Actors);
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
            Filter = actor => actor is ActorProxy { IsPendingChange: true }
        };
        PendingChangesView.LiveFilteringProperties.Add(nameof(ActorProxy.IsPendingChange));
        PendingChangesView.IsLiveFiltering = true;
        PendingChanges = [new CollectionContainer { Collection = PendingChangesView }, new CollectionContainer { Collection = pendingDeletionActors }];
        ((INotifyCollectionChanged)PendingChangesView).CollectionChanged += (_, _) => OnPendingChangeCountChanged();
        pendingDeletionActors.CollectionChanged += (_, _) => OnPendingChangeCountChanged();
        OpenFiles.CollectionChanged += (_, _) =>
        {
            UpdatePlacementOptions();
            RefreshCommandStates();
        };
        RenderContext.SceneEditingChanged += RefreshCommandStates;

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
        // Meshes created outside a load or refresh (Matinee's actors, a reset) queue their materials too.
        // They use LEX's shader until the next preparation batch, which suspends rendering and shows the busy overlay while loading shaders.
        if (_busyCount == 0 && !PreviewRefreshInProgress && !PreviewShaderPreparationRequested && RenderContext.HasPendingGameShaderLoads)
        {
            PrepareGameShaders();
        }
        if (LightDiagnosticsStale && !RenderContext.LightGizmo.IsDragging && !RenderContext.TransformWidget.IsDragging
            && LightDiagnosticsThrottle.ElapsedMilliseconds > 250)
        {
            RefreshLightingDiagnostics();
        }
        // Match MeshRenderContext.CreateRasterizerState, which disables near/far depth clipping.
        if (UseFrustumCulling) viewFrustum.Update(RenderContext.Camera.ViewProjectionMatrix, depthClipEnabled: false);
        RenderContext.CullingFrustum = UseFrustumCulling ? viewFrustum : null;
        RenderContext.ShowHidden = ShowHidden;
        visibleActors.Clear();
        foreach (ActorProxy actor in RenderContext.DrawList_3D)
        {
            if (RenderContext.IsActorVisible(actor)) visibleActors.Add(actor);
        }
        Span<RenderPass> passes = ShowCollision
            ? [RenderPass.Base, RenderPass.Hair, RenderPass.Lighting, RenderPass.Decals, RenderPass.Translucent, RenderPass.Collision]
            : [RenderPass.Base, RenderPass.Hair, RenderPass.Lighting, RenderPass.Decals, RenderPass.Translucent];

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

    //the frame's actors, which every render pass goes through
    private readonly List<ActorProxy> visibleActors = [];

    void DoRenderPass(RenderPass pass)
    {
        var actors = pass == RenderPass.Decals
            ? visibleActors.OrderBy(a => (a as DecalActorProxy)?.SortOrder ?? 0)
            : (IEnumerable<ActorProxy>)visibleActors;
        foreach (ActorProxy actor in actors)
        {
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
        if (actor != selectedActor)
        {
            RenderContext.LightGizmo.EndDrag();
            RenderContext.SplineGizmo.EndDrag();
        }
        var prev = selectedActor;
        RenderContext.SelectedActor = actor;
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
            OnPropertyChanged(nameof(SelectedDecal));
            OnPropertyChanged(nameof(IsDecalSelected));
            SelectedDecal?.RefreshFilterCandidates();
            RenderContext.SplineGizmo.Attach = selectedActor as SplineActorProxy;
            OnPropertyChanged(nameof(SelectedSpline));
            OnPropertyChanged(nameof(IsSplineSelected));
            RefreshLightingDiagnostics();
            RefreshCommandStates();
        }
        RenderContext.TransformWidget.Attach = selectedActor is { CanEditTransform: true, HasSpatialRepresentation: true }
            ? selectedActor : null;
        if (focus && selectedActor is { HasSpatialRepresentation: true })
        {
            FocusOnBounds(selectedActor.GetBounds());
        }
    }

    private void CenterView()
    {
        if (GetSceneBounds() is BoxSphereBounds fullBounds)
        {
            FocusOnBounds(fullBounds);
        }
        else
        {
            RenderContext.Camera.Position = Vector3.Zero;
            RenderContext.Camera.Pitch = -MathF.PI / 5.0f;
            RenderContext.Camera.Yaw = MathF.PI / 4.0f;
        }
    }

    private BoxSphereBounds? GetSceneBounds()
    {
        BoxSphereBounds? fullBounds = null;
        foreach (ActorProxy actor in Actors.Where(a => a is not EditorActorProxy && a.HasSpatialRepresentation))
        {
            BoxSphereBounds bounds = actor.GetBounds();
            fullBounds = fullBounds?.Union(bounds) ?? bounds;
        }
        return fullBounds;
    }

    /// <summary>
    /// Puts the orthographic camera above everything in the scene, with a depth range that reaches below all of it
    /// </summary>
    private void PlaceOrthographicCamera(float x, float y)
    {
        const float margin = 1000;
        float top = 40_000, bottom = -60_000;
        if (GetSceneBounds() is BoxSphereBounds bounds)
        {
            top = bounds.Origin.Z + bounds.BoxExtent.Z;
            bottom = bounds.Origin.Z - bounds.BoxExtent.Z;
        }
        RenderContext.Camera.Position = new Vector3(x, y, top + margin);
        RenderContext.Camera.OrthoDepthRange = MathF.Max(top - bottom + 2 * margin, 100_000);
    }

    private void FocusOnBounds(BoxSphereBounds fullBounds)
    {
        Vector3 origin = fullBounds.Origin;
        if (RenderContext.Camera.IsOrthographic)
        {
            PlaceOrthographicCamera(origin.X, origin.Y);
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
            CenterView();
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
        CancelViewportDrag();
        Game = MEGame.Unknown;
        // Through SelectActor, so that the details panes stop showing (and offering to select) the closed levels' actors
        SelectActor(null, false);
        PendingLightEdits.Clear();
        SceneViewer.SetShouldRender(false);
        PendingPreviewUpdates.Clear();
        PreviewShaderPreparationRequested = false;
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
        UndoHistory.RemoveOrphaned();
        HasAnyFileOpen = OpenFiles.Count > 0;
        UpdateGlobalDirtyState();
        UpdateTitle();

        if (OpenFiles.Count == 0)
        {
            SceneViewer.SetShouldRender(false);
            // No scene still references the caches. This also joins the texture worker and releases its uploads/readers.
            RenderContext.UnloadLevel();
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
        var actors = level.Actors.Where(level.Export.FileRef.IsUExport)
            .SelectMany(index => CreateActorViews(level.Export.FileRef.GetUExport(index), owningFile)).ToList();
        HashSet<string> ignoredActorClasses = actors.Where(a => a.Coverage.Representation == ActorRepresentation.Unsupported)
            .Select(a => a.Export.ClassName).ToHashSet();
        foreach (var actor in actors)
        {
            actor.ResolveAttachment(actors);
        }
        if (CreateLevelModelView(level, owningFile) is { } bsp) actors.Add(bsp);

        //The level's lights, for rendering static meshes the way the game lights them. (Light actors aren't otherwise shown)
        owningFile.ReloadLights(level, RenderContext);
        //The level's BSP blocks light environments' visibility traces
        RenderContext.SetLevelModel(level.Export.FileRef, level.Model);
        return new(actors, ignoredActorClasses);
    }

    /// <summary>
    /// The views of one of a level's actors: a collection actor's are its components', but not those whose deletion is pending (their views are out of
    /// the scene, which undo can bring back)
    /// </summary>
    private List<ActorProxy> CreateActorViews(ExportEntry actorExport, OpenLevelFile owningFile)
    {
        List<ActorProxy> views = actorExport.ClassName is "StaticMeshCollectionActor" or "StaticLightCollectionActor"
            ? ActorProxy.CreateCollection(this, actorExport).ToList()
            : ActorProxy.Create(this, actorExport) is { } actor ? [actor] : [];
        var pending = owningFile.PendingDeletions.Select(d => d.Actor.Export).ToHashSet();
        foreach (var view in views.Where(v => v.CollectionOwner is not null && pending.Contains(v.Export)).ToList())
        {
            views.Remove(view);
            view.Dispose();
        }
        foreach (var view in views) view.OwningFile = owningFile;
        return views;
    }

    /// <summary>
    /// Replaces the views of a collection and its members with views of them as the package has them, if it still has it. Members' uncommitted edits
    /// and undo steps go with their views
    /// </summary>
    /// <returns>The new views</returns>
    private List<ActorProxy> RebuildCollection(ExportEntry collection, OpenLevelFile file)
    {
        for (int i = file.Actors.Count - 1; i >= 0; i--)
        {
            if (file.Actors[i].Export == collection || file.Actors[i].CollectionOwner == collection)
            {
                //(a caller that reselects it does so by its export)
                if (file.Actors[i] == SelectedActor) SelectedActor = null;
                RenderContext.Registry.Invalidate(file.Actors[i].Export);
                RemoveActor(file.Actors[i]);
            }
        }
        var views = file.Package.GetEntry(collection.UIndex) is ExportEntry newCollection ? CreateActorViews(newCollection, file) : [];
        foreach (var view in views) AddActor(view, false);
        return views;
    }

    /// <summary>
    /// The level's BSP surfaces, as one world-geometry entry, or null if it has none (which the file notes, so that a change to the Level that
    /// leaves its model as it was doesn't read it again: <see cref="OpenLevelFile.UndrawnModel"/>)
    /// </summary>
    private LevelModelProxy CreateLevelModelView(Level level, OpenLevelFile owningFile)
    {
        owningFile.UndrawnModel = null;
        if (!level.Export.FileRef.TryGetUExport(level.Model, out ExportEntry modelExport)) return null;
        var bsp = new LevelModelProxy(this, modelExport, level.Export) { OwningFile = owningFile };
        if (bsp.Components.Count > 0) return bsp;
        bsp.Dispose();
        owningFile.UndrawnModel = (level.Model, level.ModelComponents.ToArray());
        return null;
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
                Actors.Sort(a => a.SortIndex);
            }
        }
    }

    //a scene edit has already drawn the view and made it one of its file's actors
    void IActorEditorContext.OnViewAttached(ActorProxy view)
    {
        if (Actors.Contains(view)) return;
        Actors.Add(view);
        view.ResolveAttachment(Actors);
        Actors.Sort(a => a.SortIndex);
        //back in the scene, its light is too
        if (view is ILightProxy light) OnLightEdited(light);
        UpdateGlobalDirtyState();
    }

    //the view isn't disposed: the scene edit's caller (an undo step) can attach it again
    void IActorEditorContext.OnViewDetached(ActorProxy view)
    {
        if (!Actors.Remove(view)) return;
        if (SelectedActor == view) SelectedActor = null;
        view.Detach();
        //out of the scene (pending deletion), its light no longer lights it
        if (view is ILightProxy light) OnLightEdited(light);
        UpdateGlobalDirtyState();
    }

    //without focusing the camera, which would also throw off a drag that's moving the selection
    void IActorEditorContext.Select(ActorProxy actor) => ViewportActorSelect(actor);

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
    public ICommand LoadRelatedLevelsCommand { get; set; }
    public ICommand FocusSelectedCommand { get; set; }
    public ICommand ToggleLocalCoordsCommand { get; set; }
    public ICommand OpenInPackageEditorCommand { get; set; }
    public ICommand OpenRecentSetCommand { get; set; }
    public ICommand UndoCommand { get; set; }
    public ICommand RedoCommand { get; set; }
    public ICommand DeleteActorCommand { get; set; }
    public ICommand UpgradeActorCommand { get; set; }
    public ICommand ToggleOrthoViewCommand { get; set; }
    public ICommand FocusFileCommand { get; set; }
    public ICommand SelectChangedActorCommand { get; set; }
    public ICommand SelectLightCommand { get; set; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Handle unmodified shortcuts after controls have had a chance to process the key.
        // Window InputBindings consume these keys even when a text editor has focus.
        if (e.Handled || Keyboard.Modifiers != ModifierKeys.None) return;
        //wherever focus is, if no control used the key
        if (e.Key is Key.Escape && placingClass is not null)
        {
            EndPlacement();
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox { IsEditable: true }) return;

        ICommand command = e.Key switch
        {
            Key.F => FocusSelectedCommand,
            Key.L => ToggleLocalCoordsCommand,
            Key.NumPad5 => ToggleOrthoViewCommand,
            Key.Delete => DeleteActorCommand,
            _ => null
        };
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    //a drag works in the mode it started in
    private void SetWidgetMode(EWidgetMode mode, string name)
    {
        CancelViewportDrag();
        RenderContext.TransformWidget.Mode = mode;
        CurrentModeName = name;
    }

    /// <summary>
    /// Tells the commands to ask again whether they can run: call when the scene's state they depend on changes (whether it can be changed, the open
    /// files, the history, the selection). WPF asks again by itself only after input or a change of focus, which a background refresh ends without
    /// </summary>
    private void RefreshCommandStates()
    {
        //CommandManager is the UI thread's
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshCommandStates);
            return;
        }
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadCommands()
    {
        SetAssignmentPolicyCommand = new RelayCommand(policy => AssignmentPolicy = (LightAssignmentPolicy)policy);
        SetLightingModeCommand = new RelayCommand(mode => LightingMode = (ViewportLightingMode)mode);
        OpenFileCommand = new GenericCommand(OpenFile, () => CanEditScene);
        AddFileCommand = new GenericCommand(AddFile, () => CanEditScene);
        SaveAllCommand = new GenericCommand(SaveAllFiles, () => CanEditScene && PackageIsLoaded());
        SaveAsCommand = new GenericCommand(SaveFileAs, () => CanEditScene && PackageIsLoaded());
        SaveSingleFileCommand = new RelayCommand(SaveSingleFileExecute, _ => CanEditScene && PackageIsLoaded());
        CloseFileCommand = new RelayCommand(CloseFileExecute, _ => CanEditScene);
        ToggleTranslateCommand = new GenericCommand(() => SetWidgetMode(EWidgetMode.Translate, "Translate"), PackageIsLoaded);
        ToggleRotateCommand = new GenericCommand(() => SetWidgetMode(EWidgetMode.Rotate, "Rotate"), PackageIsLoaded);
        ToggleScaleCommand = new GenericCommand(() => SetWidgetMode(EWidgetMode.Scale, "Scale"), PackageIsLoaded);
        ToggleUniformScaleCommand = new GenericCommand(() => SetWidgetMode(EWidgetMode.UniformScale, "Uniform Scale"), PackageIsLoaded);
        // Commit, undo and redo are enabled by notifying properties (IsDirty, CanUndo, CanRedo), which a command's CanExecute would override: WPF asks it
        // only when it chooses to, and leaves the buttons as it last found them. What they run checks that the scene can change
        CommitChangesCommand = new GenericCommand(() => CommitChanges());
        LoadRelatedLevelsCommand = new GenericCommand(LoadRelatedLevels, () => CanEditScene && PackageIsLoaded());
        FocusSelectedCommand = new GenericCommand(() =>
        {
            if (SelectedActor is not null)
            {
                SelectAndRevealActor(SelectedActor, false);
                FocusOnBounds(SelectedActor.GetBounds());
            }
        }, () => PackageIsLoaded() && SelectedActor is { HasSpatialRepresentation: true });
        ToggleLocalCoordsCommand = new GenericCommand(() => { CancelViewportDrag(); UseLocalCoordsForWidget = !UseLocalCoordsForWidget; }, PackageIsLoaded);
        OpenInPackageEditorCommand = new GenericCommand(() =>
        {
            if (SelectedActor?.Export is { } export)
            {
                var p = new PackageEditorWindow();
                p.Show();
                p.LoadPackage(export.FileRef, export.UIndex);
                p.Activate();
            }
        }, () => CanEditScene && PackageIsLoaded() && SelectedActor is { IsDraft: false });
        OpenRecentSetCommand = new RelayCommand(obj => { if (obj is RecentFileSet set) OpenRecentFileSet(set); });
        UndoCommand = new GenericCommand(Undo);
        RedoCommand = new GenericCommand(Redo);
        DeleteActorCommand = new RelayCommand(DeleteActor, parameter => CanMutateScene
            && (parameter as ActorProxy ?? SelectedActor) is { DeleteRefusal: null });
        UpgradeActorCommand = new RelayCommand(UpgradeActor, parameter => CanMutateScene
            && (parameter as ActorProxy ?? SelectedActor) is CollectionActorComponentProxy { UpgradeRefusal: null });
        ToggleOrthoViewCommand = new GenericCommand(() => IsOrthographicView = !IsOrthographicView);
        FocusFileCommand = new RelayCommand(parameter =>
        {
            if (parameter is OpenLevelFile file && file.Actors.FirstOrDefault(a => a is not EditorActorProxy && a.HasSpatialRepresentation) is { } actor)
            {
                SelectAndRevealActor(actor);
            }
        });
        SelectChangedActorCommand = new RelayCommand(parameter =>
        {
            if (parameter is ActorProxy actor)
            {
                //a pending deletion isn't in the scene to select
                if (Actors.Contains(actor)) SelectAndRevealActor(actor);
            }
        });
        SelectLightCommand = new RelayCommand(parameter =>
        {
            if (parameter is ActorProxy light)
            {
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
        RefreshCommandStates();
    }

    /// <summary>
    /// Abandons a viewport drag, restoring what it started from, before something that can't happen mid-drag (undo, a mode change, closing a file)
    /// </summary>
    private void CancelViewportDrag()
    {
        if (!RenderContext.IsDragging) return;
        RenderContext.EndDrag(DragEndReason.Cancelled);
        SceneViewer.SyncMouseCapture();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        CancelViewportDrag();
        base.OnDeactivated(e);
    }

    public void Undo_Clicked(object sender, RoutedEventArgs e) => Undo();
    public void Undo()
    {
        if (!CanMutateScene) return;
        CancelViewportDrag();
        if (UndoHistory.CanUndo) ApplyHistoryStep(UndoHistory.Undo, "Undo");
    }

    public void Redo_Clicked(object sender, RoutedEventArgs e) => Redo();
    void Redo()
    {
        if (!CanMutateScene) return;
        CancelViewportDrag();
        if (UndoHistory.CanRedo) ApplyHistoryStep(UndoHistory.Redo, "Redo");
    }

    /// <summary>
    /// Undoes or redoes one step. A step that fails (such as one whose actor isn't drawn) stays in the history where it was, and is reported
    /// </summary>
    private void ApplyHistoryStep(Action step, string name)
    {
        _isApplyingUndoRedo = true;
        try
        {
            step();
        }
        catch (InvalidOperationException e)
        {
            MessageBox.Show(this, e.Message, $"{name} failed");
        }
        finally
        {
            _isApplyingUndoRedo = false;
            RefreshCommandStates();
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

            for (int i = 0; i < selectedPaths.Count; i++)
            {
                await AddLevelFile(selectedPaths[i], i + 1, selectedPaths.Count).ConfigureAwait(true);
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
        new LightmassBaker.LightmassBakerWindow(OpenFiles.Select(f => (f.FilePath, !f.IsReadOnly))) { AssignmentPolicy = AssignmentPolicy }.Show();
    }

    #endregion

    #region Commit & Save

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
            var decals = await ChooseEmptiedDecals(actor, plan);
            if (decals is null) return;
            if (decals.Count > 0)
            {
                // Nothing needs taking out of a decal that's going too, which what the deletion says should show
                var repairs = actor.GetDeletionRepairs();
                repairs = repairs with { Trashed = repairs.Trashed.Concat(decals.SelectMany(d => d.Plan.Entries)).ToHashSet() };
                SetBusy($"Checking references to {actor.ActorName}...");
                try { plan = await ActorDeletionPlan.PrepareAsync(actor.Export, actor.OwningFile.LevelExport, repairs); }
                finally { EndBusy(); }
            }
            if (!CanMutateScene) throw new InvalidOperationException("The editor state changed during reference inspection. Retry deletion.");
            UndoHistory.Push(decals.Count == 0 ? actor.Delete(plan) : PendingActorDeletion.DeleteTogether([(actor, plan), .. decals]));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, "Delete actor");
        }
    }

    /// <summary>
    /// Asks whether to delete the decals that deleting <paramref name="actor"/> would leave with nothing to project onto, if there are any that can be
    /// </summary>
    /// <returns>The decals to delete with it, with their plans, or null if its deletion was cancelled</returns>
    private async Task<List<(ActorProxy Actor, ActorDeletionPlan Plan)>> ChooseEmptiedDecals(ActorProxy actor, ActorDeletionPlan plan)
    {
        var deletable = new List<(ActorProxy Actor, ActorDeletionPlan Plan)>();
        var emptied = ActorDeletionPlan.EmptiedDecals(actor, plan);
        if (emptied.Count == 0) return deletable;
        SetBusy($"Checking references to {actor.ActorName}'s decals...");
        try
        {
            foreach (var decal in emptied)
            {
                var decalPlan = await ActorDeletionPlan.PrepareAsync(decal.Export, decal.OwningFile.LevelExport, decal.GetDeletionRepairs());
                if (decalPlan.Blockers.Count == 0) deletable.Add((decal, decalPlan));
            }
        }
        finally { EndBusy(); }
        if (deletable.Count == 0) return deletable;
        string decals = deletable.Count == 1 ? $"the decal {deletable[0].Actor.ActorName} with nothing to project onto. Delete it too?"
            : $"{deletable.Count} decals with nothing to project onto. Delete them too?\n\n{string.Join("\n", deletable.Select(d => d.Actor.ActorName))}";
        return MessageBox.Show(this, $"Deleting {actor.ActorName} leaves {decals}", "Delete actor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No) switch
        {
            MessageBoxResult.Yes => deletable,
            MessageBoxResult.No => [],
            _ => null
        };
    }

    private void UpgradeActor(object parameter)
    {
        if (!CanMutateScene || (parameter as ActorProxy ?? SelectedActor) is not CollectionActorComponentProxy member) return;
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
            migration = DecalPackageMigration.Prepare(files.SelectMany(f => f.Actors).OfType<DecalActorProxy>());
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
            try { ActorCommit.Commit(OpenFiles.Where(files.Contains).ToList(), migration.Apply, migration.Moves.Select(m => (ActorProxy)m.Actor).ToHashSet()); }
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
        ClearUndoHistory();
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

    #endregion

    #region HandleUpdate

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
        if (_busyCount > 0 || PreviewRefreshInProgress || RenderContext.TransformWidget.IsDragging || RenderContext.LightGizmo.IsDragging
            || RenderContext.SplineGizmo.IsDragging || RenderContext.IsEditingScene || _isApplyingUndoRedo || UndoHistory.IsExecuting) return;
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
            changed |= CancelDeletions(file, cancelled, errors);
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
                    foreach (var view in CreateActorViews(actorExport, file)) AddActor(view);
                }
            }
        }
        foreach (var collectionActor in collectionActorsToUpdate) RebuildCollection(collectionActor, file);
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
    /// Cancels the file's pending deletions that another tool's change overtook (<see cref="PendingActorDeletion.Cancel"/>): the
    /// <paramref name="overtaken"/> deletions of actors it changed, and those whose undo step's other actors (a spline point's neighbours, still
    /// referring to it in the package) it rebuilt, which orphans that step. Each actor is shown as the package now has it, if its Level still lists it.
    /// The other committed actors their undo steps changed are shown as the package has them too, as when another tool changes them: with the
    /// cancelled step, nothing else would take back what it did to them. Rebuilding them can overtake another deletion in turn
    /// </summary>
    /// <returns>Whether any was cancelled</returns>
    private bool CancelDeletions(OpenLevelFile file, List<PendingActorDeletion> overtaken, List<string> errors)
    {
        var queue = new Queue<PendingActorDeletion>(overtaken);
        HashSet<int> listed = null;
        bool cancelled = false;
        while (queue.TryDequeue(out var deletion) || (deletion = file.PendingDeletions.FirstOrDefault(d => d.AlsoChanged.Any(a => a.IsDisposed))) is not null)
        {
            if (!file.PendingDeletions.Contains(deletion)) continue;
            cancelled = true;
            var (export, collection) = (deletion.Actor.Export, deletion.Actor.CollectionOwner);
            deletion.Cancel();
            listed ??= file.LevelExport.GetBinaryData<Level>().Actors.ToHashSet();
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
            foreach (var other in deletion.AlsoChanged.Where(a => a.Export is not null && Actors.Contains(a)))
            {
                var (otherExport, owner) = (other.Export, other.OwningFile);
                if (other == SelectedActor) SelectedActor = null;
                RemoveActor(other);
                if (owner.Package.GetEntry(otherExport.UIndex) == otherExport) Rebuild(otherExport, owner);
                owner.RecalculateDirty();
            }
        }
        if (cancelled) UpdateGlobalDirtyState();
        return cancelled;

        void Rebuild(ExportEntry export, OpenLevelFile owner) => TryRefresh(export.ObjectName.Instanced, errors, () =>
        {
            RenderContext.Registry.Invalidate(export);
            foreach (var view in CreateActorViews(export, owner))
            {
                AddActor(view);
                //a light whose deletion was pending had left the scene's lights
                if (view is ILightProxy light) OnLightEdited(light);
            }
            return true;
        });
    }

    /// <summary>
    /// Takes out the views of the actors <paramref name="level"/> no longer lists, and of its BSP if it isn't what the level has (which
    /// <see cref="AddActorsNewToLevel"/> adds). Not drafts, which it doesn't list until they're committed, nor pending deletions, which are already
    /// out of the scene and checked again when committed
    /// </summary>
    /// <returns>Whether any were taken out</returns>
    private bool RemoveActorsGoneFromLevel(OpenLevelFile file, Level level)
    {
        var listed = level.Actors.ToHashSet();
        var gone = file.Actors.Where(actor => actor is LevelModelProxy bsp
            ? !bsp.IsReadFrom(level)
            : actor.Export is { } export && !listed.Contains((actor.CollectionOwner ?? export).UIndex)).ToList();
        foreach (var actor in gone)
        {
            if (actor == SelectedActor) SelectedActor = null;
            RemoveActor(actor);
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
        if (added.Count > 0)
        {
            foreach (var actor in file.Actors.Where(actor => actor.Base is null)) actor.ResolveAttachment(added);
        }
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

    #endregion

    public void UpdateGlobalDirtyState()
    {
        IsDirty = OpenFiles.Any(f => f.IsDirty);
        RefreshCommandStates();
    }

    private void ActorContextMenu_Opened(object sender, RoutedEventArgs e) => ((sender as FrameworkElement)?.DataContext as ActorProxy)?.NotifyDeleteRefusal();

    /// <summary>Lists or unlists an actor pending deletion in the change list</summary>
    internal void ShowPendingDeletion(ActorProxy actor, bool pending)
    {
        if (pending) pendingDeletionActors.Add(actor);
        else pendingDeletionActors.Remove(actor);
    }

    private void OnPendingChangeCountChanged()
    {
        OnPropertyChanged(nameof(PendingChangeCount));
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    private bool PackageIsLoaded() => OpenFiles.Count > 0;

    private void OnActorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ActorProxy.Location) or nameof(ActorProxy.Rotation) or nameof(ActorProxy.DrawScale) or nameof(ActorProxy.DrawScale3D))) return;
        // Which lights reach the selection depends on its bounds. Refreshed once a drag ends
        LightDiagnosticsStale = true;
        if (PreviewRefreshInProgress)
        {
            _preEditSnapshot = selectedActor?.SnapshotTransform();
            return;
        }
        if (_isApplyingUndoRedo || RenderContext.TransformWidget.IsDragging) return;

        if (sender is ActorProxy actor && _preEditSnapshot is { } before)
        {
            var after = actor.SnapshotTransform();
            if (!before.Equals(after))
            {
                UndoHistory.Push(new TransformAction(actor, before, after, $"Edit {actor.ActorName}"));
                _preEditSnapshot = after;
            }
        }
    }

    private void OnWidgetDragComplete(ActorProxy actor, TransformSnapshot before, TransformSnapshot after)
    {
        if (before.Equals(after)) return;
        UndoHistory.Push(new TransformAction(actor, before, after, $"Drag {actor.ActorName}"));
        _preEditSnapshot = after;
    }

    private SplineActorProxy.Duplication BeginDuplicateDrag(SplineActorProxy point)
    {
        string refusal = CanMutateScene ? point.DuplicateRefusal
            : "Wait for the current operation (such as saving or refreshing the preview) to finish, then try again.";
        if (refusal is null) return point.BeginDuplicate();
        //once the drag has ended, so the message doesn't arrive in the middle of its mouse input
        Dispatcher.InvokeAsync(() => MessageBox.Show(this, $"{point.ActorName} can't be duplicated. {refusal}", "Duplicate spline point"));
        return null;
    }

    private void OnDuplicateDragComplete(IUndoAction step)
    {
        UndoHistory.Push(step);
        //the copy is selected, and its drag is part of the step
        _preEditSnapshot = selectedActor?.SnapshotTransform();
        LightDiagnosticsStale = true;
    }

    #region Lights

    public ObservableCollectionExtended<EditableActorLighting> SelectedActorLighting { get; } = [];
    private readonly HashSet<EditableActorLighting> PendingActorLightingEdits = [];

    public void OnActorLightingEdited(MeshComponentProxy mesh, ActorLightingSettings before, ActorLightingSettings after)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(new ActorLightingAction(mesh.Lighting, before, after));
        PendingActorLightingEdits.Add(mesh.Lighting);
        LightDiagnosticsStale = true;
    }

    public EditableDecal SelectedDecal => (selectedActor as DecalActorProxy)?.Decal;
    public bool IsDecalSelected => SelectedDecal is not null;
    public void OnDecalEdited(DecalActorProxy actor, DecalSettings before, DecalSettings after, string property)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(new DecalSettingsAction(actor, before, after, property));
    }

    public SplineActorProxy SelectedSpline => selectedActor as SplineActorProxy;
    public bool IsSplineSelected => SelectedSpline is not null;
    public void OnSplineConnectionsEdited(IUndoAction action)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(action);
        LightDiagnosticsStale = true;
    }
    private void SplineConnection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SplineConnectionItem item, Tag: string operation }) return;
        switch (operation)
        {
            case "Break": item.Break(); break;
            case "Reverse": item.Reverse(); break;
            case "Straight": item.Straighten(); break;
        }
    }
    private void SplineBreakAll_Click(object sender, RoutedEventArgs e) => SelectedSpline?.BreakAllConnections();
    private void SplineReverseAll_Click(object sender, RoutedEventArgs e) => SelectedSpline?.ReverseWholeSpline();
    private void SplineMesh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is not { IsLoft: true } point) return;
        var mesh = EntrySelector.GetEntry<IEntry>(this, point.Package, "Select DeformMesh", entry => entry.IsA("StaticMesh"));
        if (mesh is null || point.SetDeformMesh(mesh.UIndex, point.DeformMeshMaterials) is null) return;
        //allowed, since its sections can be given overrides next
        var problems = point.SplineMeshMaterialProblems(mesh.UIndex, [.. point.DeformMeshMaterials]).ToList();
        if (problems.Count > 0)
        {
            MessageBox.Show(this, $"In game, some of {mesh.ObjectName.Instanced}'s sections won't be drawn with their materials on a spline. " +
                $"Give them DeformMeshMaterials overrides that can be:\n\n{string.Join("\n", problems)}", "DeformMesh");
        }
    }

    //the game draws a material it can't draw on a spline mesh with its default material instead
    private bool RefuseSplineMaterial(SplineActorProxy point, IEntry material)
    {
        if (point.SplineMeshMaterialProblem(material.UIndex) is not { } problem) return false;
        MessageBox.Show(this, problem, "DeformMeshMaterials");
        return true;
    }
    private void SplineClearMesh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is { } point) point.SetDeformMesh(0, point.DeformMeshMaterials);
    }
    private void SplineMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is not { IsLoft: true } point) return;
        var material = EntrySelector.GetEntry<IEntry>(this, point.Package, "Add DeformMeshMaterials override", entry => entry.IsA("MaterialInterface"));
        if (material is not null && !RefuseSplineMaterial(point, material)) point.SetDeformMesh(point.DeformMesh, point.DeformMeshMaterials.Append(material.UIndex));
    }
    private void SplineClearMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is { } point) point.SetDeformMesh(point.DeformMesh, []);
    }
    private void SplineMaterialSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SplineMaterialSlot slot, Tag: string operation }) return;
        var values = slot.Point.DeformMeshMaterials.ToList();
        switch (operation)
        {
            case "Choose":
                var material = EntrySelector.GetEntry<IEntry>(this, slot.Point.Package, $"Material override for slot {slot.Index}", entry => entry.IsA("MaterialInterface"));
                if (material is null || RefuseSplineMaterial(slot.Point, material)) return;
                values[slot.Index] = material.UIndex;
                break;
            case "Clear": values[slot.Index] = 0; break;
            case "Remove": values.RemoveAt(slot.Index); break;
        }
        slot.Point.SetDeformMesh(slot.Point.DeformMesh, values);
    }
    public void OnSplineEdited(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property)
    {
        //a handle drag is recorded once, when it ends
        if (!_isApplyingUndoRedo && !RenderContext.SplineGizmo.IsDragging) UndoHistory.Push(new SplineSettingsAction(actor, before, after, property));
    }
    private void OnSplineGizmoDragComplete(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after) =>
        UndoHistory.Push(new SplineSettingsAction(actor, before, after, "tangent"));

    private void RefreshDecalFilter_Click(object sender, RoutedEventArgs e) => SelectedDecal?.RefreshFilterCandidates();
    private void ClearDecalFilter_Click(object sender, RoutedEventArgs e) => SelectedDecal?.ClearFilter();

    private void DecalMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDecal is not { IsEditable: true } decal) return;
        var material = EntrySelector.GetEntry<IEntry>(this, decal.Package, "Select DecalMaterial", entry => entry.IsA("MaterialInterface"));
        if (material is not null && decal.SetMaterial(material) is { } problem) MessageBox.Show(this, problem, "DecalMaterial");
    }

    private void EditLightEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditScene || sender is not FrameworkElement { DataContext: EditableActorLighting { CanEditEnvironment: true } lighting }) return;
        ShowLightEnvironmentProperties(lighting.EditableEnvironment);
    }

    private void CreateLightEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditScene || sender is not Button { DataContext: EditableActorLighting { CanCreateEnvironment: true } lighting } button) return;
        try
        {
            var environment = lighting.CreateEnvironment(button.Tag as string == "Bio");
            foreach (var settings in SelectedActorLighting) settings.RefreshEnvironments();
            ShowLightEnvironmentProperties(environment);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not create the light environment: {ex.Message}", "Light environment");
        }
    }

    private void ShowLightEnvironmentProperties(ExportEntry environment)
    {
        if (environment is null) return;
        // Keep package read-only state and selection fixed while this editor can write properties.
        new ExportLoaderHostedWindow(new InterpreterExportLoader(), environment)
        {
            Owner = this,
            Title = $"Light environment properties — {environment.InstancedFullPath}"
        }.ShowDialog();
        foreach (var settings in SelectedActorLighting) settings.RefreshEnvironments();
        LightDiagnosticsStale = true;
    }

    private void ApplyActorLightingEdits()
    {
        if (PendingActorLightingEdits.Count == 0 || PreviewRefreshInProgress) return;
        using var load = RenderContext.BeginLoad();
        foreach (var lighting in PendingActorLightingEdits)
        {
            var mesh = lighting.Mesh;
            if (mesh.Actor is { IsDisposed: false, OwningFile: { } file } actor && actor.Components.Contains(mesh)
                && OpenFiles.Contains(file) && file.Actors.Contains(actor))
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
            // Proxies replaced by a reload or closed with their file are skipped. One out of the scene that undo can bring back (a pending deletion) isn't
            if (light is ActorProxy { OwningFile: { } file, IsDisposed: false } actor && OpenFiles.Contains(file)
                && (file.Actors.Contains(actor) || file.RetainedActors.Contains(actor)))
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
            UndoHistory.Push(new LightSettingsAction(light, before, light.Settings, selectedActor.ActorName));
        }
        _preEditLightSettings = light.Settings;
    }

    private void OnLightGizmoDragComplete(ILightProxy light, LightSettings before, LightSettings after)
    {
        if (light is not ActorProxy actor) return;
        UndoHistory.Push(new LightSettingsAction(light.Light, before, after, actor.ActorName));
        _preEditLightSettings = after;
        LightDiagnosticsStale = true;
    }

    // Uses the active viewport's rules: Level lighting includes the last build's records; the other modes ignore them, like Preview lighting.
    private bool IgnorePrecomputedLighting => LightingMode != ViewportLightingMode.Level;

    private static IEnumerable<MeshComponentProxy> MeshComponents(ActorProxy actor) => actor.Components.OfType<MeshComponentProxy>();

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
        ViewportLightingMode.Preview => $"{AssignmentPolicy.Describe()}: shared preview/bake eligibility. Direct lighting only; occlusion is approximate and bounced light is absent." + (PreserveAssignments ? " Historical exclusions remain until you select Recompute assignments." : ""),
        ViewportLightingMode.Level => "Level rules: includes the last bake's light-map and irrelevance records. Bounced light comes only from the existing light-maps, so it doesn't follow edits.",
        _ => $"This mode does not show level lights. Eligibility uses {AssignmentPolicy.Describe()}."
    };

    private bool LightDiagnosticsStale;

    /// <summary>
    /// A level's bake occluders that the preview has no shadow geometry for. Whether it has BSP, and which actors are terrains, come from the level's own
    /// binaries, read again when its Level or Model export is updated (or, as a backstop, when its export count changes). The terrain sections that cast also depend on
    /// class defaults, so they're counted again after any update
    /// </summary>
    private sealed class UnshadowedGeometryEntry
    {
        public int ExportCount;
        //the exports they were read from
        public int LevelUIndex, ModelUIndex;
        public bool HasBsp;
        public ExportEntry[] TerrainActors = [];
        public int? TerrainSections;
    }

    private readonly Dictionary<IMEPackage, UnshadowedGeometryEntry> UnshadowedGeometry = [];

    /// <summary>
    /// The level's occluders in a Lightmass bake that the preview has no shadow geometry for: its BSP, and the terrain sections that take part in
    /// static lighting and cast shadows (as LightmassSceneBuilder exports them). Spline meshes aren't baked at all
    /// </summary>
    private Dictionary<string, int> UnshadowedGeometryCounts(IMEPackage package)
    {
        //what can't be read counts as none until it's read again: when the Level export (or for terrain sections, anything) is updated, or exports are added
        if (!UnshadowedGeometry.TryGetValue(package, out var entry) || entry.ExportCount != package.ExportCount)
        {
            UnshadowedGeometry[package] = entry = new UnshadowedGeometryEntry { ExportCount = package.ExportCount };
            try
            {
                if (package.Exports.FirstOrDefault(e => e.ClassName == "Level") is { } levelExport)
                {
                    //before it's parsed, so that one that can't be is read again when it's updated
                    entry.LevelUIndex = levelExport.UIndex;
                    var level = levelExport.GetBinaryData<Level>();
                    entry.ModelUIndex = level.Model;
                    entry.HasBsp = package.TryGetUExport(level.Model, out var modelExport) && modelExport.GetBinaryData<Model>().Nodes.Length > 0;
                    entry.TerrainActors = Lightmaps.Lightmass.LightmassSceneBuilder.TerrainActors(package, level);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Could not read {package.FileNameNoExtension}'s BSP and terrain actors: {e.Message}");
            }
        }
        if (entry.TerrainSections is null)
        {
            try
            {
                entry.TerrainSections = entry.TerrainActors.Length == 0 ? 0
                    : Lightmaps.Lightmass.LightmassSceneBuilder.ShadowCastingTerrainSections(entry.TerrainActors, RenderContext.PackageCache);
            }
            catch (Exception e)
            {
                entry.TerrainSections = 0;
                Debug.WriteLine($"Could not count {package.FileNameNoExtension}'s terrain sections: {e.Message}");
            }
        }
        var counts = new Dictionary<string, int>();
        if (entry.HasBsp) counts["level(s) of BSP"] = 1;
        if (entry.TerrainSections > 0) counts["terrain section(s)"] = entry.TerrainSections.Value;
        return counts;
    }
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
        //the preview's limits only matter in Preview lighting, and only for a selected mesh
        if (IsPreviewLighting && meshes.Count > 0)
        {
            notes.AddRange(meshes.SelectMany(mesh => mesh.ShadowNotes).Distinct());
            var totals = new SortedDictionary<string, int>();
            foreach (var file in OpenFiles)
                foreach (var (className, count) in UnshadowedGeometryCounts(file.Package))
                    totals[className] = totals.GetValueOrDefault(className) + count;
            var unsupported = totals.Select(total => $"{total.Value} {total.Key}").ToArray();
            if (unsupported.Length > 0)
                notes.Add("Preview has no shadow geometry for BSP or terrain, which block light in a Lightmass bake: " + string.Join(", ", unsupported) + ".");
            if (PreserveAssignments && RenderContext.AssignmentHistory.Problems is { Count: > 0 } problems)
                notes.Add("Some source assignments could not be read; preservation is incomplete: " + string.Join("; ", problems.Take(3)));
            //meshes without static lighting (skeletal meshes, movable meshes) have no bake mapping to lack
            if (meshes.Any(mesh => mesh.ReceivesStaticLighting && !mesh.HasCompatibleBakeMapping))
                notes.Add("One or more receivers need a new bake mapping. The baker checks geometry and cooked shader support before allocating one; see its report for unsupported receivers.");
        }
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
                string reason = result.Describe();
                if (ignorePrecomputed && result.IsAffecting() && !light.HasStaticLighting && !light.HasStaticShadowing)
                    reason += " (runtime only; no bake contribution)";
                rows.Add(new LightRelevance(lightActor?.DisplayText ?? light.Export.ObjectName.Instanced, light.LevelName, reason,
                    result.IsAffecting(), lightActor));
                if (result.IsAffecting()) RenderContext.HighlightedLights.Add(light.Export);
            }
            foreach (var group in meshes.Where(mesh => mesh.UsesUnsupportedLightEnvironment)
                         .GroupBy(mesh => (mesh.Export.Game, mesh.UnsupportedLightEnvironmentReason)))
            {
                notes.Add($"This {group.Key.Game} light environment isn't previewed: {group.Key.UnsupportedLightEnvironmentReason ?? "its mode isn't implemented"}. "
                          + "Lighting and shadows it would supply are missing; the mesh uses neutral lighting.");
            }
            if (meshes.Any(mesh => mesh.IsLitByLightEnvironment && mesh.Export.Game is MEGame.LE1 or MEGame.LE2))
            {
                notes.Add("LE1/LE2 light environments show their settled synthesis. Transition timing, Kismet-driven runtime changes and in-game image parity aren't verified.");
            }
            if (meshes.Any(mesh => mesh.LightEnvironmentSettings is { } s && (s.WrapLightingDirectMultiplier != 1 || s.WrapLightingIndirectMultiplier != 1)))
            {
                notes.Add("This light environment's wrap-lighting multipliers aren't applied; the viewport uses the game's stock wrap-lighting constants.");
            }
            if (meshes.All(mesh => !mesh.IsLitByLevelLights) && meshes.Any(mesh => !mesh.UsesUnsupportedLightEnvironment))
            {
                notes.Add(Game.IsLEGame()
                    ? "The level's lights don't light this mesh in the viewport (its lighting couldn't be read), so it's shown with neutral lighting."
                    : "Level lighting previews need a Legendary Edition game; this mesh is shown with neutral lighting.");
            }
            else if (meshes.Any(mesh => mesh.IsLitByLightEnvironment))
            {
                notes.Add("Lit by a light environment, which composites the lights below into a few lights. Whether each light is blocked from it isn't shown here.");
            }
            if (selectedActor?.OwningFile?.IsReadOnly == true)
                notes.Add("Context-only level: it contributes geometry and lights, but its mappings are not baked in the editor handoff.");
            foreach (var material in meshes.SelectMany(mesh => mesh.GameShaderMaterials).Distinct())
            {
                if (material.LoadedGameShaderError is { } error)
                {
                    notes.Add($"{material.InstancedFullPath} uses LEX's neutral shader: {error}");
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
                || actor.ClassName.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.CoverageLabel.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.Category.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || (int.TryParse(_actorFilterText.TrimStart('#'), out int index) && actor.Export?.UIndex == index));
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
            && Actors.FirstOrDefault(a => a.Export?.UIndex == uIdx) is ActorProxy actor)
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
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Where(file => Path.GetExtension(file).ToLowerInvariant() is ".upk" or ".pcc" or ".sfm").ToArray();
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
            if (isFirst && !ConfirmCloseFiles("Open the new files anyway?")) return;
            using var guard = new RenderGuard(this);
            if (isFirst) CloseAllFiles();
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    await AddLevelFile(files[i], i + 1, files.Length).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    StatusBar_LeftMostText.Text = "Failed to load " + Path.GetFileName(files[i]);
                    MessageBox.Show(this, $"Error loading {Path.GetFileName(files[i])}:\n{ex.Message}", "Load level");
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

        if (!ConfirmCloseFiles("Close anyway?"))
        {
            e.Cancel = true;
            return;
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
        if (!ConfirmCloseFiles("Open the new files anyway?")) return;
        CloseAllFiles();

        using var guard = new RenderGuard(this);

        var paths = set.FilePaths.Where(File.Exists).ToList();
        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            await AddLevelFile(path, i + 1, paths.Count).ConfigureAwait(true);
            var openFile = OpenFiles.LastOrDefault(f => f.FilePath == path);
            if (openFile is not null && set.ReadOnlyFilePaths.Contains(path))
                openFile.IsReadOnly = true;
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
                    PlaceOrthographicCamera(pos.X, pos.Y);
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
        private set
        {
            if (SetProperty(ref _isBusy, value)) RefreshCommandStates();
        }
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
