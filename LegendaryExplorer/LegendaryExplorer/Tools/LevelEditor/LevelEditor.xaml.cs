using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
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
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor : TrackingNotifyPropertyChangedWindowBase, IActorEditorContext
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

    // Level Editor has its own dark styling (LevelEditorStyles.xaml), which the app theme would override.
    public LevelEditor(bool readOnlyPreview) : base("Level Editor", true, followAppTheme: false)
    {
        IsBakePreview = readOnlyPreview;
        RenderContext = new LevelEditorRenderContext(readOnlyPreview)
        {
            UseGameShaders = true,
            TextureStreamingEnabled = true,
            CameraSpeed = Math.Max(1, Settings.LevelEditor_CameraSpeed)
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
        //once the drag has ended, so the message doesn't arrive in the middle of its mouse input
        RenderContext.TransformWidget.OnDuplicateDragFailed = error => Dispatcher.InvokeAsync(() => MessageBox.Show(this, error.Message, "Duplicate"));
        RenderContext.LightGizmo.OnDragComplete = OnLightGizmoDragComplete;
        RenderContext.SplineGizmo.OnDragComplete = OnSplineGizmoDragComplete;
        InitTerrainSculpting();
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
            //(with no level open, the Terrain tab is disabled, so its tool couldn't be clicked off)
            if (OpenFiles.Count == 0) EndSculpting();
            RefreshCommandStates();
        };
        RenderContext.SceneEditingChanged += RefreshCommandStates;

        LoadCommands();
        InitializeComponent();
        EditorTitleBar.Apply(this);
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
        //(an undone paste a dropped step held may have been the last user of its copied actor)
        UndoHistory.Dropped += ReleaseUnused;
        assetPackages = new AssetPackages(this);
        // What this window has of the copied actors is its own. It shares the clipboard with the other windows from when it's shown until it has closed
        // (a window that's never shown never closes)
        copiedActors = new CopiedActors(this);
        IDisposable clipboard = null;
        SourceInitialized += (_, _) => clipboard ??= ActorClipboard.Join(ClipboardChanged);
        Closed += (_, _) => clipboard?.Dispose();
        OpenFiles.CollectionChanged += (_, _) =>
        {
            foreach (var key in _outlinerGroupStates.Keys
                         .Where(key => key.Name is OpenLevelFile file && !OpenFiles.Contains(file)).ToArray())
            {
                _outlinerGroupStates.Remove(key);
            }
            OnScannedFilesChanged();
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
        RenderContext.OutlinedHitID = ShowSelectionOutline && selectedActor is not null ? selectedActor.HitID : -1;
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

    /// <summary>Opens the outliner's actor menu over the viewport, for an actor right-clicked there</summary>
    private void ViewportActorMenu(ActorProxy actor)
    {
        //(after the release has finished routing, so the menu taking the mouse doesn't interrupt the viewport's handling of it)
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (actor != selectedActor) return;
            //where its Paste puts a copy
            viewportMenuPoint = Mouse.GetPosition(SceneViewer);
            var menu = (ContextMenu)FindResource("ActorContextMenu");
            menu.DataContext = actor;
            menu.PlacementTarget = SceneViewer;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        });
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
            RefreshMeshChoices();
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

    //UE3's editor viewports are top, front, side and perspective. Shipping levels with views mostly have a camera only in the perspective one
    private const int PerspectiveEditorView = 3;

    /// <summary>
    /// Where the camera starts when <paramref name="levelExport"/> is the first file opened: where it was when the recent set being opened was last closed,
    /// else the level's own perspective editor view, else looking over the whole scene
    /// </summary>
    private void PlaceStartingCamera(ExportEntry levelExport)
    {
        if (pendingRecentCamera is { } recent)
        {
            PlaceCamera(new Vector3(recent.X, recent.Y, recent.Z), recent.Pitch, recent.Yaw);
        }
        else if (levelExport.Parent is ExportEntry { ClassName: "World" } worldExport
                 && worldExport.GetBinaryData<World>().EditorViews[PerspectiveEditorView] is { } view
                 && (view.CamPosition != Vector3.Zero || view.CamRotation != new Rotator(0, 0, 0)))
        {
            //(stored rotators aren't normalized: a pitch of 60000 is -5536)
            float pitch = ((int)unchecked((short)view.CamRotation.Pitch)).UnrealRotationUnitsToRadians();
            PlaceCamera(view.CamPosition, pitch, view.CamRotation.Yaw.UnrealRotationUnitsToRadians());
        }
        else
        {
            CenterView();
        }
    }

    /// <summary>
    /// Puts the perspective camera at <paramref name="position"/>, looking along <paramref name="pitch"/> and <paramref name="yaw"/> (radians).
    /// The top view is placed over it, clipped at its height, and switching back to perspective returns to it
    /// </summary>
    private void PlaceCamera(Vector3 position, float pitch, float yaw)
    {
        SceneCamera camera = RenderContext.Camera;
        camera.Position = position;
        camera.Pitch = pitch.Clamp(-MathF.PI / 2 + 0.01f, MathF.PI / 2 - 0.01f);
        camera.Yaw = yaw;
        if (camera.IsOrthographic)
        {
            camera.SavePerspectiveState();
            PlaceOrthographicCamera(position.X, position.Y, position.Z);
        }
    }

    private void CenterView()
    {
        if (GetSceneBounds() is BoxSphereBounds fullBounds)
        {
            FocusOnBounds(fullBounds);
        }
        else if (RenderContext.Camera.IsOrthographic)
        {
            PlaceOrthographicCamera(0, 0, RenderContext.Camera.Position.Z);
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

    private const float OrthoDepthMargin = 1000;
    //the scene's lowest point when the top view was last placed, which its depth range reaches below
    private float _orthoSceneBottom;

    /// <summary>
    /// Puts the orthographic camera over (x, y) at <paramref name="clipHeight"/>, the top view's near plane, with a depth range that reaches below everything in the scene
    /// </summary>
    private void PlaceOrthographicCamera(float x, float y, float clipHeight)
    {
        _orthoSceneBottom = GetSceneBounds() is BoxSphereBounds bounds ? bounds.Origin.Z - bounds.BoxExtent.Z : -60_000;
        RenderContext.Camera.Position = new Vector3(x, y, clipHeight);
        UpdateOrthoDepthRange();
        OnPropertyChanged(nameof(TopDownClipHeight));
    }

    private void UpdateOrthoDepthRange() =>
        RenderContext.Camera.OrthoDepthRange = MathF.Max(RenderContext.Camera.Position.Z - _orthoSceneBottom + OrthoDepthMargin, 100_000);

    /// <summary>
    /// The height the top view is clipped at: what's above it isn't drawn, so a level can be seen under its ceilings. It's the top view's camera height,
    /// which starts at the perspective camera's
    /// </summary>
    public float TopDownClipHeight
    {
        get => RenderContext.Camera.IsOrthographic ? RenderContext.Camera.Position.Z : 0;
        set
        {
            if (!RenderContext.Camera.IsOrthographic || RenderContext.Camera.Position.Z == value) return;
            RenderContext.Camera.Position = RenderContext.Camera.Position with { Z = value };
            UpdateOrthoDepthRange();
            OnPropertyChanged();
        }
    }

    private void FocusOnBounds(BoxSphereBounds fullBounds)
    {
        Vector3 origin = fullBounds.Origin;
        if (RenderContext.Camera.IsOrthographic)
        {
            //(focusing keeps the clip height chosen)
            PlaceOrthographicCamera(origin.X, origin.Y, RenderContext.Camera.Position.Z);
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
        view.OwningFile?.ReattachTo([view]);
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
    public ICommand AddFilesCommand { get; set; }
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
    public ICommand DuplicateActorCommand { get; set; }
    public ICommand CopyActorCommand { get; set; }
    /// <summary>Pastes the copied actor (<see cref="PasteActor"/>). Its parameter is the menu it's chosen from, if any</summary>
    public ICommand PasteActorCommand { get; set; }
    public ICommand ToggleOrthoViewCommand { get; set; }
    public ICommand FocusFileCommand { get; set; }
    public ICommand SelectChangedActorCommand { get; set; }
    public ICommand SelectLightCommand { get; set; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Handle unmodified shortcuts after controls have had a chance to process the key.
        // Window InputBindings consume these keys even when a text editor has focus.
        if (e.Handled) return;
        // Copy and paste, but not a text editor's
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.V)
        {
            //(the window's only text editors)
            if (Keyboard.FocusedElement is TextBoxBase or PasswordBox or ComboBox { IsEditable: true }) return;
            // One copy per press, however long it's held. Ctrl+C with nothing selected is left to whatever else uses it
            if (e.IsRepeat || (e.Key is Key.C ? CopyActor(null) : PasteActor(null))) e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        //wherever focus is, if no control used the key
        if (e.Key is Key.Escape && (placing is not null || sculpting is not null))
        {
            EndPlacement();
            EndSculpting();
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
        AddFilesCommand = new GenericCommand(AddFiles, () => CanEditScene);
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
            if (SelectedActor?.Export is { } export) OpenInPackageEditor(export);
        }, () => CanEditScene && PackageIsLoaded() && SelectedActor is { PackageEditorRefusal: null });
        OpenRecentSetCommand = new RelayCommand(obj => { if (obj is RecentFileSet set) OpenRecentFileSet(set); });
        UndoCommand = new GenericCommand(Undo);
        RedoCommand = new GenericCommand(Redo);
        DeleteActorCommand = new RelayCommand(DeleteActor, parameter => CanMutateScene
            && (parameter as ActorProxy ?? SelectedActor) is { DeleteRefusal: null });
        DuplicateActorCommand = new RelayCommand(DuplicateActor, parameter => CanMutateScene
            && (parameter as ActorProxy ?? SelectedActor) is { DuplicateRefusal: null });
        // (enabled by CanCopy and CanPaste, which their refusals notify)
        CopyActorCommand = new RelayCommand(parameter => CopyActor(parameter));
        PasteActorCommand = new RelayCommand(parameter => PasteActor(parameter as ContextMenu));
        UpgradeActorCommand = new RelayCommand(UpgradeActor, parameter => CanMutateScene
            && (parameter as ActorProxy ?? SelectedActor) is { UpgradeRefusal: null });
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
        //every edit is a step, and undo or redo moves one
        MarkScanStale();
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

    private LevelEditorHelpWindow helpWindow;

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        if (helpWindow is null)
        {
            //owned, so it stays over this window while it's read, and closes with it
            helpWindow = new LevelEditorHelpWindow { Owner = this };
            helpWindow.Closed += (_, _) => helpWindow = null;
            helpWindow.Show();
        }
        else
        {
            if (helpWindow.WindowState == WindowState.Minimized) helpWindow.WindowState = WindowState.Normal;
            helpWindow.Activate();
        }
    }

    public void UpdateGlobalDirtyState()
    {
        IsDirty = OpenFiles.Any(f => f.IsDirty);
        RefreshCommandStates();
    }

    private void ActorContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        ((sender as FrameworkElement)?.DataContext as ActorProxy)?.NotifyRefusals();
        NotifyPasteRefusal();
    }

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
        ReleaseAll();
        //a search of the game's files for missing assets
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
        scanCancellation = null;

        PreviewClosed = true;
        PreviewUpdateTimer.Stop();
        PendingPreviewUpdates.Clear();
        RenderContext.PackageCache.PackageUpdated -= QueuePreviewUpdate;
        RenderContext.UpdateScene -= UpdateScene;
        RenderContext.RenderScene -= RenderScene;
        RenderContext.SelectActor -= ViewportActorSelect;
        RenderContext.ActorMenuRequested -= ViewportActorMenu;

        UndoHistory.PropertyChanged -= UndoHistory_PropertyChanged;
        UndoHistory.Dropped -= ReleaseUnused;
        UndoHistory.Clear();

        SceneViewer.Dispose();

        Settings.LevelEditor_CameraSpeed = (int)MathF.Round(CameraSpeed);
    }

    private void LevelEditor_Loaded(object sender, RoutedEventArgs e)
    {
        RenderContext.UpdateScene += UpdateScene;
        RenderContext.RenderScene += RenderScene;
        RenderContext.SelectActor += ViewportActorSelect;
        RenderContext.ActorMenuRequested += ViewportActorMenu;

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

    /// <summary>Outline the selected actor in the viewport, kept between sessions</summary>
    public bool ShowSelectionOutline
    {
        get => Settings.LevelEditor_ShowSelectionOutline;
        set
        {
            if (Settings.LevelEditor_ShowSelectionOutline == value) return;
            Settings.LevelEditor_ShowSelectionOutline = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Units per second the camera flies at, kept between sessions</summary>
    public float CameraSpeed
    {
        get => RenderContext.CameraSpeed;
        set
        {
            if (RenderContext.CameraSpeed == value) return;
            RenderContext.CameraSpeed = value;
            OnPropertyChanged();
        }
    }

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
                    //clipped where the camera is, so the view from inside a room is of that room, not its ceiling
                    float eyeHeight = RenderContext.Camera.EyePosition.Z;
                    RenderContext.Camera.SavePerspectiveState();
                    RenderContext.Camera.IsOrthographic = true;
                    var pos = RenderContext.Camera.Position;
                    PlaceOrthographicCamera(pos.X, pos.Y, eyeHeight);
                    float focusDepth = RenderContext.Camera.FocusDepth;
                    RenderContext.Camera.OrthoWidth = MathF.Max(focusDepth * 4f, 500f);
                }
                else
                {
                    RenderContext.Camera.IsOrthographic = false;
                    RenderContext.Camera.RestorePerspectiveState();
                    OnPropertyChanged(nameof(TopDownClipHeight));
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
            ReleaseUnused();
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
}
