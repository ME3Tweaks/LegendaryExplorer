using LegendaryExplorer.Tools.LevelEditor;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using System;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class ActorPreviewControl : ExportLoaderControl, IActorEditorContext
{
    public LevelEditorRenderContext RenderContext { get; } = new();
    public bool IsApplyingUndoRedo => false;

    private ActorProxy _actor;
    private bool _controlIsLoaded;

    private bool _showWireframe;
    public bool ShowWireframe
    {
        get => _showWireframe;
        set
        {
            SetProperty(ref _showWireframe, value);
            RenderContext.Wireframe = value;
        }
    }

    private bool _showCollision;
    public bool ShowCollision
    {
        get => _showCollision;
        set => SetProperty(ref _showCollision, value);
    }

    public ActorPreviewControl() : base("Actor Preview")
    {
        DataContext = this;
        InitializeComponent();
        SceneViewer.Context = RenderContext;
        RenderContext.Camera.FirstPerson = false;
        SceneViewer.Loaded += SceneViewer_Loaded;
        SceneViewer.Unloaded += SceneViewer_Unloaded;
    }

    private void SceneViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (_controlIsLoaded)
        {
            if (CurrentLoadedExport is not null && _actor is null)
            {
                LoadActor();
            }
            return;
        }

        _controlIsLoaded = true;
        RenderContext.UpdateScene += OnUpdateScene;
        RenderContext.RenderScene += OnRenderScene;
        if (Parent is TabItem { Parent: TabControl tc })
            tc.SelectionChanged += HostingTabSelectionChanged;
    }

    private void SceneViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Parent is TabItem { Parent: TabControl tc })
            tc.SelectionChanged -= HostingTabSelectionChanged;
    }

    private void HostingTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Parent is TabItem ti)
        {
            bool shouldRender = e.AddedItems.Contains(ti);
            SceneViewer?.SetShouldRender(shouldRender);
        }
    }

    private void OnUpdateScene(object sender, float deltaTime)
    {
        _actor?.UpdateScene(RenderContext, deltaTime);
    }

    private void OnRenderScene(object sender, EventArgs e)
    {
        Span<RenderPass> passes = ShowCollision
            ? [RenderPass.Base, RenderPass.Hair, RenderPass.Collision]
            : [RenderPass.Base, RenderPass.Hair];
        foreach (RenderPass pass in passes)
            _actor?.Render(RenderContext, pass);
        RenderContext.DrawUI();
    }

    public override bool CanParse(ExportEntry exportEntry) =>
        !exportEntry.IsDefaultObject && ActorProxy.CanCreate(exportEntry);

    public override void LoadExport(ExportEntry exportEntry)
    {
        UnloadExport();
        CurrentLoadedExport = exportEntry;
        if (IsLoaded)
        {
            LoadActor();
        }
    }

    private void LoadActor()
    {
        IsBusy = true;
        try
        {
            _actor = ActorProxy.Create(this, CurrentLoadedExport);
            if (_actor is null)
            {
                RenderContext.ErrorText = $"Could not create preview object of type: '{CurrentLoadedExport.ClassName}'";
            }
            else
            {
                RenderContext.ErrorText = null;
                RenderContext.LoadActors([_actor]);
                RenderContext.Camera.Position = _actor.GetBounds().Origin;
            }
        }
        catch (Exception ex)
        {
            RenderContext.ErrorText = ex.FlattenException();
        }
        IsBusy = false;
    }

    public override void UnloadExport()
    {
        if (_actor is not null)
        {
            RenderContext.UnloadActors([_actor]);
            _actor.Dispose();
            _actor = null;
        }
        RenderContext.EmptyCaches();
        CurrentLoadedExport = null;
    }

    public override void PopOut() =>
        new ExportLoaderHostedWindow(new ActorPreviewControl(), CurrentLoadedExport).Show();

    public override void Dispose()
    {
        RenderContext.UpdateScene -= OnUpdateScene;
        RenderContext.RenderScene -= OnRenderScene;
        _actor?.Dispose();
        _actor = null;
        SceneViewer?.Dispose();
    }


    #region Busy variables
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _busyText;

    public string BusyText
    {
        get => _busyText;
        set => SetProperty(ref _busyText, value);
    }

    #endregion
}
