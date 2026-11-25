using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public enum ViewportLightingMode
{
    Level,
    Neutral,
    Unlit,
    Preview
}

/// <summary>
/// A text label to be drawn at a screen-space position as a D2D overlay.
/// </summary>
public struct ScreenLabel(float x, float y, string text)
{
    public float X = x;
    public float Y = y;
    public string Text = text;
}

/// <summary>
/// Handles rendering of mesh data
/// </summary>
public partial class MeshRenderContext : RenderContext
{
    // Set by views that opt into culling; null keeps the existing preview behavior.
    public ViewFrustum CullingFrustum { get; set; }

    /// <summary>
    /// The current flags for rendering textures. This renderer does not support 'SetAlphaAsBlack' or 'ReconstructZ'
    /// </summary>
    public ShaderFlags RenderFlags = ShaderFlags.EnableRedChannel | ShaderFlags.EnableGreenChannel | ShaderFlags.EnableBlueChannel | ShaderFlags.EnableAlphaChannel;

    /// <summary>The flags that tint what's drawn, which <see cref="SelectionColor"/> follows</summary>
    public const ShaderFlags SelectionTintFlags = ShaderFlags.Selected | ShaderFlags.Highlighted;
    //UE3's DefaultSelectedMaterialColor (BaseEngine.ini)
    private static readonly Vector4 SelectedMaterialColor = new(0.04f, 0.02f, 0.24f, 1);
    //LevelEditorRenderContext.HighlightColor, linear, at 35%
    private static readonly Vector4 HighlightedMaterialColor = new(0.35f, 0.122f, 0.0033f, 1);

    /// <summary>
    /// UE3's SelectionColor material parameter for what's being drawn: every game material adds it to its emissive and takes it off its diffuse
    /// (FMaterialResource::CompileProperty), so it shows on dark surfaces and in every light pass. LevelEditorShader.hlsl's SelectionColor() matches it
    /// </summary>
    public Vector4 SelectionColor => (RenderFlags & ShaderFlags.Selected) != 0 ? SelectedMaterialColor
        : (RenderFlags & ShaderFlags.Highlighted) != 0 ? HighlightedMaterialColor
        : Vector4.Zero;

    /// <summary>The hit proxy ID whose pixels the resolve outlines; -1 for none</summary>
    public int OutlinedHitID { get; set; } = -1;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WorldConstants
    {
        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 Model;
        public Vector3 HitTestID;
        public ShaderFlags Flags;

        public WorldConstants(Matrix4x4 Projection, Matrix4x4 View, Matrix4x4 Model, ShaderFlags flags, Vector3 hitTestId)
        {
            this.Projection = Projection;
            this.View = View;
            this.Model = Model;
            this.Flags = flags;
            this.HitTestID = hitTestId;
        }
    }

    public Color BackgroundColor = Color.FromArgb(255,255,255,255); //Default

    public readonly SceneCamera Camera = new();
    private bool wireframe;
    public bool Wireframe
    {
        get => wireframe;
        set
        {
            wireframe = value;
            if (Device != null)
            {
                if (wireframe)
                {
                    ImmediateContext.Rasterizer.State = WireframeRasterizerState;
                    RenderFlags |= ShaderFlags.Wireframe;
                }
                else
                {
                    ImmediateContext.Rasterizer.State = FillRasterizerState;
                    RenderFlags &= ~ShaderFlags.Wireframe;
                }
            }
        }
    }

    public float Time { get; private set; }
    public uint NumFrames { get; private set; }

    private float FPS;
    private float lastFPSTime;
    private float lastFPSFrame;
    public string ErrorText;

    /// <summary>
    /// Screen-space labels to be rendered as a D2D text overlay after 3D rendering.
    /// Populated by scene renderers, cleared each frame after drawing.
    /// </summary>
    public List<ScreenLabel> ScreenLabels { get; } = [];

    public Vector3 CurrentHitTestId;

    public event EventHandler<float> UpdateScene;
    public event EventHandler RenderScene;

    private readonly Dictionary<RenderTargetBlendDescription, BlendState> BlendStateCache = new(new BlendDescComparer());

    public readonly PreviewTextureCache TextureCache;
    public readonly PreviewPackageCache PackageCache;
    internal readonly StaticMeshPreviewCache StaticMeshes = new();

    /// <summary>
    /// A read-only parsed snapshot. A load shares one until it ends or this mesh export is invalidated.
    /// Outside a load, every call parses it, so a caller that repeats should keep the result.
    /// </summary>
    public StaticMesh GetStaticMesh(ExportEntry export) => StaticMeshes.GetMesh(export);

    /// <summary>
    /// The mesh's collision geometry, sharing its buffers with other users of the mesh. Null if it has none. The caller disposes it.
    /// </summary>
    internal Mesh<WorldVertex> GetStaticMeshCollision(StaticMesh mesh) =>
        StaticMeshes.GetCollision(mesh, () => this.GetMeshFromAggGeom(mesh.GetCollisionMeshProperty(mesh.Export.FileRef)));

    /// <summary>
    /// Lighting for meshes rendered with the game's shaders
    /// </summary>
    public PreviewLighting Lighting { get; } = new();

    /// <summary>
    /// Whether the actor is shown in this view, so that its meshes cast their dynamic shadows
    /// </summary>
    public virtual bool IsActorVisible(ActorProxy actor) => true;

    /// <summary>Whether a component is shown, including its owning actor's visibility.</summary>
    public virtual bool IsComponentVisible(PrimitiveComponentProxy component)
        => component.IsVisible && (component.Actor is null || IsActorVisible(component.Actor));

    /// <summary>
    /// Forgets what's cached for a level that's been closed: its WorldInfo's light environment settings, and its own meshes' collision triangles, in case it's
    /// edited and reopened. (Other packages' meshes stay cached while other levels use them)
    /// </summary>
    public void ForgetLevel(IMEPackage level)
    {
        ForgetPackage(level);
        //(what's read through its imports is forgotten too, in case it's reopened changed)
        EffectiveLightingProperties.InvalidateDefaults(PackageCache);
        lock (LightsLock)
        {
            LightsVersion++;
        }
        //its lights' light function materials and failures, and the meshes that cast shadows from them, which would keep the level loaded
        LightAttenuations.ClearMaterials();
        ModulatedShadowsFailed = false;
        lock (LevelGeometryLock)
        {
            //it may hold the level's meshes and BSP. Rebuilt the next time it's needed
            LevelGeometry = null;
        }
    }

    /// <summary>
    /// Forgets what's cached of a package, but not what the scene's lights and geometry hold of it, which <see cref="ForgetLevel"/> resets too: enough
    /// for one that was never in the scene, a copied actor's level (<see cref="ActorCapture"/>), whose views stayed out of it
    /// </summary>
    internal void ForgetPackage(IMEPackage level)
    {
        StaticMeshes.Forget(level);
        //the level's classes and imports key class defaults, and its materials' shaders its shader cache (or none), which would keep it loaded
        EffectiveLightingProperties.ForgetDefaults(PackageCache, level);
        lock (SeekFreeShaderCaches)
        {
            SeekFreeShaderCaches.Remove(level);
        }
        lock (LightsLock)
        {
            Volatile.Write(ref assignmentHistory, assignmentHistory.Without(level));
            var state = LevelCaptures.GetValue(level, static _ => new LevelCaptureState());
            state.Forgotten = true;
            state.Generation++;
        }
        lock (LightEnvironments)
        {
            CachedWorldLightEnvironmentSettings.Remove(level);
        }
        string keyPrefix = $"{level.FilePath}|";
        lock (LevelGeometryLock)
        {
            LevelModels.Remove(level);
            MeshesWithoutLevelGeometry.RemoveWhere(key => key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase));
            foreach (string key in LevelGeometryMeshes.Keys.Where(key => key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                LevelGeometryMeshes.Remove(key);
            }
            PruneLevelGeometryMeshes();
        }
    }

    public MeshRenderContext(bool sharePackages = true)
    {
        this.Camera.FocusDepth = 100.0f;
        TextureCache = new PreviewTextureCache(this);
        PackageCache = new PreviewPackageCache(sharePackages);
        LightAttenuations = new LightAttenuationRenderer(this);
    }

    public override void Update(float timestep)
    {
        Time += timestep;
        float fpsDelta = Time - lastFPSTime;
        if (fpsDelta >= 1f)
        {
            float frameDelta = NumFrames - lastFPSFrame;
            lastFPSTime = Time;
            lastFPSFrame = NumFrames;

            FPS = MathF.Round(frameDelta / fpsDelta);
        }

        if (!Camera.FirstPerson || Camera.IsOrthographic)
        {
            CameraVelocity = Vector3.Zero;
        }
        if (Camera.IsOrthographic)
        {
            float panSpeed = Camera.OrthoWidth * 0.5f;
            var (right, up) = Camera.OrthoScreenAxes;
            if (PressedKeys.HasFlag(KeyStates.W))
                Camera.Position += up * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.S))
                Camera.Position -= up * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.A))
                Camera.Position -= right * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.D))
                Camera.Position += right * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.Q))
            {
                Camera.OrthoWidth *= 1 + timestep;
            }
            if (PressedKeys.HasFlag(KeyStates.E))
            {
                Camera.OrthoWidth *= 1 - timestep;
                Camera.OrthoWidth = MathF.Max(Camera.OrthoWidth, 1f);
            }
        }
        else if (Camera.FirstPerson)
        {
            Vector3 direction = Vector3.Zero;
            if (PressedKeys.HasFlag(KeyStates.W)) direction += Camera.CameraForward;
            if (PressedKeys.HasFlag(KeyStates.S)) direction -= Camera.CameraForward;
            if (PressedKeys.HasFlag(KeyStates.A)) direction -= Camera.CameraRight;
            if (PressedKeys.HasFlag(KeyStates.D)) direction += Camera.CameraRight;
            if (PressedKeys.HasFlag(KeyStates.Q)) direction -= Vector3.UnitZ;
            if (PressedKeys.HasFlag(KeyStates.E)) direction += Vector3.UnitZ;
            Vector3 target = direction * (CameraSpeed * (PressedKeys.HasFlag(KeyStates.Shift) ? FastCameraMultiplier : 1));

            //UE3 steps dv/dt = Damping * (target - v) once a frame; this is its exact solution, so the easing doesn't depend on the frame rate
            float decay = MathF.Exp(-CameraVelocityDamping * timestep);
            Vector3 lag = CameraVelocity - target;
            Camera.Position += target * timestep + lag * ((1 - decay) / CameraVelocityDamping);
            CameraVelocity = target + lag * decay;
            if (CameraVelocity.LengthSquared() < 1e-4f)
            {
                CameraVelocity = Vector3.Zero;
            }
        }

        UpdateScene?.Invoke(null, timestep);
    }

    public override void Render()
    {
        TextureCache.BeginStreamingFrame();
        NumFrames++;
        // Clear the color and depth buffers
        if (BackbufferView != null)
        {
            ImmediateContext.OutputMerger.SetRenderTargets(DepthBufferView, SceneColorView, HitBufferView);
            ImmediateContext.OutputMerger.SetDepthStencilState(DefaultDepthState);
            ClearDepthBuffer();
            ImmediateContext.ClearRenderTargetView(SceneColorView, new RawColor4(
                MathF.Pow(BackgroundColor.R / 255.0f, DisplayGamma), MathF.Pow(BackgroundColor.G / 255.0f, DisplayGamma), MathF.Pow(BackgroundColor.B / 255.0f, DisplayGamma),
                0)); //alpha marks pixels rendered with game shaders, see PSMainResolve
            if (HitBufferView is not null) ImmediateContext.ClearRenderTargetView(HitBufferView, new RawColor4(1f, 1f, 1f, 1f));

            if (ErrorText is null)
            {
                try
                {
                    if (UseGameShaders)
                    {
                        //other users of the device may have changed them since the last frame
                        LEEffect.PixelShaderResources.Reset();
                        LEEffect.BeginFrame(this);
                    }
                    RenderScene?.Invoke(null, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    ErrorText = e.FlattenException();
                }
                //left over if the lighting pass failed, or the scene renderer doesn't call EndLightingPass. They'd draw out of order, so they're dropped
                System.Diagnostics.Debug.Assert(PendingLightPasses.Count + PendingPostModulatedShadowPasses.Count == 0 || ErrorText is not null,
                    "Light passes were queued, but EndLightingPass wasn't called");
                PendingLightPasses.Clear();
                PendingPostModulatedShadowPasses.Clear();
            }

            ResolveSceneColor();

            if (ErrorText is not null)
            {
                RenderTarget2D.BeginDraw();
                {
                    var size = RenderTarget2D.Size;
                    RenderTarget2D.DrawText($"{ErrorText}", errorTextFormat, new RawRectangleF(0, 0, size.Width, size.Height), errorTextBrush);
                }
                RenderTarget2D.EndDraw();
            }

            //render D2D overlay
            RenderTarget2D.BeginDraw();
            {
                if (App.IsDebug)
                {
                    var size = RenderTarget2D.Size;
                    string stats = $"{FPS} fps\n{Camera.Position}";
                    // White text with a dark shadow stays readable over both the scene and background.
                    RenderTarget2D.DrawText(stats, statsTextFormat, new RawRectangleF(5, 5, size.Width - 3, size.Height - 3), statsShadowBrush);
                    RenderTarget2D.DrawText(stats, statsTextFormat, new RawRectangleF(4, 4, size.Width - 4, size.Height - 4), statsTextBrush);
                }

                foreach (ref readonly var label in CollectionsMarshal.AsSpan(ScreenLabels))
                {
                    const float labelW = 20;
                    const float labelH = 12;
                    var rect = new RawRectangleF(label.X - labelW * 0.5f, label.Y - labelH * 0.5f,
                                                 label.X + labelW * 0.5f, label.Y + labelH * 0.5f);
                    RenderTarget2D.FillRectangle(rect, labelBackgroundBrush);
                    RenderTarget2D.DrawText(label.Text, labelTextFormat, rect, labelTextBrush);
                }
                ScreenLabels.Clear();
            }
            RenderTarget2D.EndDraw();
        }

        TextureCache.EndStreamingFrame();
        base.Render();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResolveConstants
    {
        public int OutlinedHitID;
        private readonly int Padding0, Padding1, Padding2;
    }

    /// <summary>
    /// Resolves the multisampled linear scene color into the gamma-encoded backbuffer, outlining <see cref="OutlinedHitID"/>'s pixels, and copies the hit proxy
    /// IDs into <see cref="HitBuffer"/>
    /// </summary>
    private void ResolveSceneColor()
    {
        ImmediateContext.Rasterizer.State = FillRasterizerState;
        ImmediateContext.OutputMerger.SetRenderTargets((DepthStencilView)null, BackbufferView, HitBufferResolvedView);
        ImmediateContext.OutputMerger.SetBlendState(null);
        ImmediateContext.InputAssembler.InputLayout = null;
        ImmediateContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
        ImmediateContext.VertexShader.Set(ResolveVertexShader);
        ImmediateContext.PixelShader.Set(ResolvePixelShader);
        ImmediateContext.PixelShader.SetShaderResource(0, SceneColorResourceView);
        ImmediateContext.PixelShader.SetShaderResource(1, HitBufferMSResourceView);
        var constants = new ResolveConstants { OutlinedHitID = OutlinedHitID };
        ImmediateContext.UpdateSubresource(ref constants, ResolveConstantBuffer);
        ImmediateContext.PixelShader.SetConstantBuffer(RESOLVE_CONSTANT_BUFFER_SLOT, ResolveConstantBuffer);

        ImmediateContext.Draw(3, 0);

        //these will be bound as render targets next frame, so they can't stay bound as shader resources
        ImmediateContext.PixelShader.SetShaderResource(0, null);
        ImmediateContext.PixelShader.SetShaderResource(1, null);
        RestoreRasterizerState();
    }

    public void ClearDepthBuffer()
    {
        if (DepthBufferView != null)
        {
            //reversed depth: 0 is the far plane
            ImmediateContext.ClearDepthStencilView(DepthBufferView, DepthStencilClearFlags.Depth, 0f, 0);
        }
    }

    public void RenderMeshAsWireframe(Mesh<WorldVertex> mesh)
    {
        bool wireframeBackup = Wireframe;
        Wireframe = true;
        DefaultEffect.PrepDraw(ImmediateContext, AlphaBlendState, GetWorldConstants(mesh.LocalToWorld));
        DefaultEffect.RenderObject(ImmediateContext, mesh, null);
        Wireframe = wireframeBackup;
    }

    public void RenderMeshAsWireframe(Mesh<WorldVertex> mesh, ModelPreviewSection section)
    {
        bool wireframeBackup = Wireframe;
        Wireframe = true;
        DefaultEffect.PrepDraw(ImmediateContext, AlphaBlendState, GetWorldConstants(mesh.LocalToWorld));
        DefaultEffect.RenderObject(ImmediateContext, mesh, (int)section.StartIndex, (int)section.TriangleCount * 3, null);
        Wireframe = wireframeBackup;
    }

    public WorldConstants GetWorldConstants(Matrix4x4 localToWorld)
    {
        ShaderFlags flags = IsUnlit ? RenderFlags | ShaderFlags.Unlit : RenderFlags;
        return new WorldConstants(Matrix4x4.Transpose(Camera.ProjectionMatrix), Matrix4x4.Transpose(Camera.ViewMatrix), Matrix4x4.Transpose(localToWorld), flags, CurrentHitTestId);
    }

    /// <summary>Draws a terrain section with LEX's terrain shaders: neutral material, hit proxy and selection flags as for other meshes</summary>
    internal void RenderTerrainSection(SharpDX.Direct3D11.Buffer vertexBuffer, SharpDX.Direct3D11.Buffer indexBuffer, Format indexFormat, int indexCount, Matrix4x4 localToWorld)
    {
        DefaultEffect.PrepDraw(ImmediateContext, AlphaBlendState, GetWorldConstants(localToWorld));
        ImmediateContext.InputAssembler.InputLayout = TerrainInputLayout;
        ImmediateContext.VertexShader.Set(TerrainVertexShader);
        ImmediateContext.PixelShader.Set(TerrainPixelShader);
        ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, TerrainVertex.Stride, 0));
        ImmediateContext.InputAssembler.SetIndexBuffer(indexBuffer, indexFormat, 0);
        ImmediateContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
        ImmediateContext.DrawIndexed(indexCount, 0, 0);
    }

    public void RenderMeshWithDefaultEffect(Mesh<LEVertex> mesh, ModelPreviewSection section, ShaderResourceView diffuseTexture)
    {
        DefaultEffect.PrepDraw(ImmediateContext, AlphaBlendState, GetWorldConstants(mesh.LocalToWorld));
        ImmediateContext.InputAssembler.InputLayout = LEVertexDefaultInputLayout;
        ImmediateContext.VertexShader.Set(LEVertexDefaultVertexShader);
        DefaultEffect.RenderObject(ImmediateContext, mesh, (int)section.StartIndex, (int)section.TriangleCount * 3,
            Wireframe ? null : diffuseTexture ?? DefaultTextureView);
    }

    /// <summary>
    /// Invalidates package-backed resources between frames. Returns true if any shared resource (textures, shader bytecode,
    /// or light-environment settings) changed.
    /// </summary>
    public bool InvalidatePreviewResources(IMEPackage package, IReadOnlySet<int> exports) => InvalidatePreviewResources(package, exports, out _);

    /// <param name="rebuild">The mesh previews that must be rebuilt to reacquire them. (Textures are reloaded in place, so they need none)</param>
    public bool InvalidatePreviewResources(IMEPackage package, IReadOnlySet<int> exports, out PreviewRebuild rebuild)
    {
        //before the affected meshes are refreshed, so that they follow their materials' new dependencies
        PackageCache.MaterialDependencies.Invalidate(package, exports);
        StaticMeshes.Invalidate(package, exports);
        InvalidateLevelGeometryMeshes(package, exports);
        bool textures = TextureCache.Invalidate(package, exports);
        bool shaders = exports.Any(index => package.GetEntry(index)?.ClassName == "ShaderCache");
        lock (SeekFreeShaderCaches)
            shaders |= SeekFreeShaderCaches.TryGetValue(package, out var previousCache)
                       && previousCache?.Export is { } shaderExport && exports.Contains(shaderExport.UIndex);
        int? modelToRefresh = null;
        lock (LevelGeometryLock)
            if (LevelModels.TryGetValue(package, out var model) && exports.Contains(model.Export.UIndex))
                modelToRefresh = model.Export.UIndex;
        if (modelToRefresh is { } modelIndex) SetLevelModel(package, modelIndex);
        bool environments = exports.Any(index => package.GetEntry(index) is ExportEntry entry
            && (entry.ClassName.Contains("LightEnvironment", StringComparison.Ordinal)
                || entry.ClassName == "WorldInfo"
                || (entry.Game is MEGame.LE1 && entry.IsDefaultObject && entry.IsA("LightComponent"))));
        if (shaders)
        {
            DepthPixelShaderByteCodes.Clear();
            LightAttenuations.ClearDepthPixelShaders();
            lock (SeekFreeShaderCaches) SeekFreeShaderCaches.Remove(package);
            lock (VertexShaderCache)
            {
                foreach (var cached in VertexShaderCache.Values)
                {
                    cached.Shader.Dispose();
                    cached.InputLayout.Dispose();
                }
                VertexShaderCache.Clear();
            }
            lock (PixelShaderCache) PixelShaderCache.DisposeValuesAndClear();
        }
        if (environments)
        {
            lock (LightEnvironments)
            {
                LightEnvironments.Clear();
                CachedWorldLightEnvironmentSettings.Clear();
                InstalledEnvironmentDefaults.Clear();
            }
        }
        if (textures || shaders || environments)
        {
            LightAttenuations.ClearMaterials();
            ModulatedShadowsFailed = false;
        }
        rebuild = shaders ? PreviewRebuild.AllMeshes : environments ? PreviewRebuild.LightEnvironmentMeshes : PreviewRebuild.None;
        return textures || shaders || environments;
    }

    public override void EmptyCaches()
    {
        StaticMeshes.Clear();
        PackageCache?.ReleasePackages();
        //they're keyed by the levels' imports, and hold the released packages' class default objects
        EffectiveLightingProperties.InvalidateDefaults(PackageCache);
        LightAttenuations?.ClearMaterials();
        LightAttenuations?.ClearDepthPixelShaders();
        ModulatedShadowsFailed = false;
        lock (SeekFreeShaderCaches)
        {
            SeekFreeShaderCaches.Clear();
        }
        lock (PendingGameShaderLoads)
        {
            foreach (MaterialRenderProxy material in PendingGameShaderLoads) material.Dequeued();
            PendingGameShaderLoads.Clear();
        }
        DepthPixelShaderByteCodes.Clear();
        //light environments themselves belong to their meshes' components, which remove them when disposed
        lock (LightEnvironments)
        {
            CachedWorldLightEnvironmentSettings.Clear();
            InstalledEnvironmentDefaults.Clear();
        }
        lock (LevelGeometryLock)
        {
            LevelGeometryMeshes.Clear();
            MeshesWithoutLevelGeometry.Clear();
            LevelModels.Clear();
            LevelGeometry = null;
        }
        InvalidateLevelGeometry();
        TextureCache?.ExpungeStaleCacheItems();
        BlendStateCache.DisposeValuesAndClear();
        lock (VertexShaderCache)
        {
            foreach (var cached in VertexShaderCache.Values)
            {
                cached.Shader.Dispose();
                cached.InputLayout.Dispose();
            }
            VertexShaderCache.Clear();
        }
        lock (PixelShaderCache)
        {
            PixelShaderCache.DisposeValuesAndClear();
        }
    }

    public Vector4 WorldToScreen(Vector3 point)
    {
        return Vector4.Transform(point, Camera.ViewProjectionMatrix);
    }

    public bool ScreenToPixel(Vector4 point, out Vector2 pixel)
    {
        if (point.W <= 0f)
        {
            pixel = Vector2.Zero;
            return false;
        }

        float invW = 1f / point.W;
        pixel = new Vector2((0.5f + point.X * 0.5f * invW) * Width, (0.5f - point.Y * 0.5f * invW) * Height);
        return true;
    }

    public bool WorldToPixel(Vector3 point, out Vector2 pixel) => ScreenToPixel(WorldToScreen(point), out pixel);
}

file class BlendDescComparer : IEqualityComparer<RenderTargetBlendDescription>
{
    public bool Equals(RenderTargetBlendDescription x, RenderTargetBlendDescription y)
    {
        return x.IsBlendEnabled.Equals(y.IsBlendEnabled)
               && x.SourceBlend == y.SourceBlend 
               && x.DestinationBlend == y.DestinationBlend 
               && x.BlendOperation == y.BlendOperation
               && x.SourceAlphaBlend == y.SourceAlphaBlend
               && x.DestinationAlphaBlend == y.DestinationAlphaBlend
               && x.AlphaBlendOperation == y.AlphaBlendOperation
               && x.RenderTargetWriteMask == y.RenderTargetWriteMask;
    }

    public int GetHashCode(RenderTargetBlendDescription obj)
    {
        return HashCode.Combine(obj.IsBlendEnabled, (int)obj.SourceBlend,
            (int)obj.DestinationBlend, (int)obj.BlendOperation, 
            (int)obj.SourceAlphaBlend, (int)obj.DestinationAlphaBlend,
            (int)obj.AlphaBlendOperation, (int)obj.RenderTargetWriteMask);
    }
}
