using System;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal.Animation;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SkeletalMesh = LegendaryExplorerCore.Unreal.BinaryConverters.SkeletalMesh;

namespace LegendaryExplorer.UserControls.SharedToolControls;

/// <summary>
/// Self-contained animation preview control with 3D viewport, CPU skinning, and playback controls.
/// </summary>
public partial class AnimationPreviewControl : NotifyPropertyChangedControlBase
{
    private MeshRenderContext _meshContext;
    private ModelPreview<WorldVertex> _meshPreview;
    private SkinnedMeshRenderer _skinnedRenderer;
    private SkeletonAnimPlayer _animPlayer;
    private bool _previewInitialized;
    private ExportEntry _lastAnimExport;

    #region Bindable Properties

    private int _animCurrentFrame;
    public int AnimCurrentFrame
    {
        get => _animCurrentFrame;
        set
        {
            if (SetProperty(ref _animCurrentFrame, value) && _animPlayer != null)
            {
                _animPlayer.CurrentFrame = value;
                if (!_animPlayer.IsPlaying)
                {
                    UpdateSkinningOneShot();
                }
            }
        }
    }

    private int _animFrameCount;
    public int AnimFrameCount
    {
        get => _animFrameCount;
        set => SetProperty(ref _animFrameCount, value);
    }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (SetProperty(ref _playbackSpeed, value) && _animPlayer != null)
            {
                _animPlayer.PlaybackSpeed = (float)value;
            }
        }
    }

    private bool removeOffset = true;
    public bool RemoveOffset
    {
        get => removeOffset;
        set => SetProperty(ref removeOffset, value);
    }

    #endregion

    #region Commands

    public ICommand PlayPauseCommand { get; }

    #endregion

    public AnimationPreviewControl()
    {
        PlayPauseCommand = new GenericCommand(TogglePlayPause);
        InitializeComponent();

        _meshContext = new MeshRenderContext
        {
            BackgroundColor = Colors.Gray
        };
        SceneViewer.Context = _meshContext;
        SceneViewer.Loaded += SceneViewer_Loaded;
    }

    private void SceneViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (_meshContext.IsReady && !_previewInitialized)
        {
            _meshContext.UpdateScene += OnUpdateScene;
            _meshContext.RenderScene += OnRenderScene;
            _previewInitialized = true;
            SceneViewer.SetShouldRender(true);
        }
    }

    #region Public API

    public void LoadSkeletalMesh(ExportEntry skeletalMeshExport)
    {
        if (!_meshContext.IsReady) return;

        // Dispose old preview
        _meshPreview?.Dispose();
        _meshPreview = null;
        _skinnedRenderer = null;
        _animPlayer = null;

        try
        {
            var skm = ObjectBinary.From<SkeletalMesh>(skeletalMeshExport);

            if (skm.LODModels.Length is 0)
            {
                throw new Exception("Mesh has no LODs!");
            }
            _meshPreview = new ModelPreview<WorldVertex>(_meshContext, skm);

            _skinnedRenderer = new SkinnedMeshRenderer();
            _skinnedRenderer.BuildFromSkeletalMesh(skeletalMeshExport.FileRef.Game, skm.LODModels[0]);

            _animPlayer = new SkeletonAnimPlayer();
            _animPlayer.SetSkeleton(skm);
            _animPlayer.PlaybackSpeed = (float)PlaybackSpeed;

            Mesh<WorldVertex> mesh = _meshPreview.LODs[0].Mesh;
            // Center camera on mesh
            _meshContext.Camera.FocusDepth = mesh.TransformedBounds.SphereRadius * 1.75f;
            _meshContext.Camera.Position = mesh.TransformedBounds.Origin;
            _meshContext.Camera.Pitch = -MathF.PI / 7.0f;

            // If we had a previously loaded animation, reload it
            if (_lastAnimExport != null)
            {
                LoadAnimSequence(_lastAnimExport);
            }
        }
        catch (Exception ex)
        {
            _meshContext.ErrorText = $"Error loading mesh: {ex.Message}";
        }
    }

    public void LoadAnimSequence(ExportEntry animSequenceExport)
    {
        _lastAnimExport = animSequenceExport;
        if (_animPlayer == null) return;

        try
        {
            var animSequence = ObjectBinary.From<AnimSequence>(animSequenceExport);
            animSequence.DecompressAnimationData();

            _animPlayer.SetAnimation(animSequence);

            AnimFrameCount = Math.Max(0, _animPlayer.TotalFrames - 1);
            AnimCurrentFrame = 0;

            UpdateSkinningOneShot();
        }
        catch (Exception)
        {
            // Animation might not be compatible with this skeleton
        }
    }

    public void ClearAnimation()
    {
        _lastAnimExport = null;
        _animPlayer?.SetAnimation(null);
        AnimFrameCount = 0;
        AnimCurrentFrame = 0;
    }

    public void Clear()
    {
        _lastAnimExport = null;
        _meshPreview?.Dispose();
        _meshPreview = null;
        _skinnedRenderer = null;
        _animPlayer = null;
        AnimFrameCount = 0;
        AnimCurrentFrame = 0;
    }

    public void TogglePlayPause()
    {
        if (_animPlayer == null) return;
        _animPlayer.IsPlaying = !_animPlayer.IsPlaying;
    }

    public void Play()
    {
        if (_animPlayer == null) return;
        _animPlayer.IsPlaying = true;
    }

    public void Pause()
    {
        if (_animPlayer == null) return;
        _animPlayer.IsPlaying = false;
    }

    public void Dispose()
    {
        if (_previewInitialized)
        {
            _meshContext.UpdateScene -= OnUpdateScene;
            _meshContext.RenderScene -= OnRenderScene;
        }
        _meshPreview?.Dispose();
        SceneViewer?.Dispose();
    }

    #endregion

    #region Internal Rendering

    private void UpdateSkinningOneShot()
    {
        if (_skinnedRenderer == null || _meshPreview == null || _animPlayer == null) return;
        if (!_meshContext.IsReady || _meshPreview.LODs.Count == 0) return;

        _skinnedRenderer.UpdateSkinning(_meshContext.Device, _meshPreview.LODs[0].Mesh, _animPlayer);
    }

    private void OnUpdateScene(object sender, float timestep)
    {
        if (_animPlayer is { IsPlaying: true } && _skinnedRenderer != null && _meshPreview is { LODs.Count: > 0 })
        {
            _animPlayer.AdvanceTime(timestep);
            _skinnedRenderer.UpdateSkinning(_meshContext.Device, _meshPreview.LODs[0].Mesh, _animPlayer);

            // Update frame slider without triggering setter logic
            _animCurrentFrame = _animPlayer.CurrentFrame;
            OnPropertyChanged(nameof(AnimCurrentFrame));
        }
    }

    private void OnRenderScene(object sender, EventArgs e)
    {
        if (_meshPreview is not { LODs.Count: > 0 })
        {
            return;
        }
        if (RemoveOffset)
        {
            _meshContext.Camera.Position = _meshPreview.LODs[0].Mesh.TransformedBounds.Origin;
        }
        foreach (RenderPass renderPass in Enum.GetValues<RenderPass>())
        {
            _meshPreview.Render(renderPass, _meshContext, 0);
        }
    }

    #endregion
}
