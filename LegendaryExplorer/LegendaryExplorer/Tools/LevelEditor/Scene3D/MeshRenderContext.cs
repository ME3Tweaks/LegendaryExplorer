using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

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
public class MeshRenderContext : RenderContext
{
    /// <summary>
    /// The current flags for rendering textures. This renderer does not support 'SetAlphaAsBlack' or 'ReconstructZ'
    /// </summary>
    public ShaderFlags RenderFlags = ShaderFlags.EnableRedChannel | ShaderFlags.EnableGreenChannel | ShaderFlags.EnableBlueChannel | ShaderFlags.EnableAlphaChannel;

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

    [Flags]
    private enum KeyStates
    {
        None = 0,
        W = 0b1,
        A = 0b10,
        S = 0b100,
        D = 0b1000,
        Q = 0b10000,
        E = 0b100000
    }

    public Color BackgroundColor = Color.FromArgb(255,255,255,255); //Default

    #region Size-Dependent Resources
    public RenderTargetView BackbufferView { get; private set; }
    //The scene is rendered into this linear HDR target, then gamma-encoded into the backbuffer, as the game does
    protected Texture2D SceneColor;
    private RenderTargetView SceneColorView;
    private ShaderResourceView SceneColorResourceView;
    public Texture2D DepthBuffer { get; private set; } // also called Depth-Stencil, but we don't use stencil at the moment.
    public DepthStencilView DepthBufferView { get; private set; }
    //The scene is rendered with MSAA, so the hit proxy IDs are rendered into a multisampled target, then copied (not averaged!) into HitBuffer for reading
    private Texture2D HitBufferMS;
    private ShaderResourceView HitBufferMSResourceView;
    protected RenderTargetView HitBufferView;
    protected Texture2D HitBuffer;
    private RenderTargetView HitBufferResolvedView;

    protected D2D.RenderTarget RenderTarget2D;
    private DW.TextFormat statsTextFormat;
    private DW.TextFormat errorTextFormat;
    private DW.TextFormat labelTextFormat;
    private D2D.SolidColorBrush statsTextBrush;
    private D2D.SolidColorBrush errorTextBrush;
    private D2D.SolidColorBrush labelTextBrush;
    private D2D.SolidColorBrush labelBackgroundBrush;
    #endregion
    public GenericEffect<WorldConstants> DefaultEffect { get; private set; }
    //lets LEVertex meshes be drawn with DefaultEffect's pixel shader
    private VertexShader LEVertexDefaultVertexShader;
    private InputLayout LEVertexDefaultInputLayout;
    private VertexShader ResolveVertexShader;
    //tonemaps pixels rendered with game shaders, to match how the game displays HDR scene color
    private PixelShader ResolvePixelShader;
    private const float DisplayGamma = 2.2f;
    public LEEffect LEEffect { get; private set; }
    private Texture2D DefaultTexture;
    private Texture2D WhiteTextureCube;
    private Texture2D WhiteTex;
    public ShaderResourceView DefaultTextureView { get; private set; }
    public ShaderResourceView WhiteTextureCubeView { get; private set; }
    public ShaderResourceView WhiteTexView { get; private set; }
    private RasterizerState FillRasterizerState;
    private RasterizerState WireframeRasterizerState;
    private RasterizerState CullBackRasterizerState;
    private RasterizerState CullFrontRasterizerState;
    public SamplerState SampleState { get; private set; }

    /// <summary>
    /// MSAA sample count of the scene's render targets. 4 if the device supports it.
    /// </summary>
    public int SampleCount { get; private set; } = 1;
    private const int PreferredSampleCount = 4;

    //The depth buffer is reversed (near is 1, far is 0, see SceneCamera.ProjectionMatrix), so depth tests are Greater instead of Less
    /// <summary>
    /// Depth tested and written. Should be set whenever another depth state is done being used.
    /// </summary>
    public DepthStencilState DefaultDepthState { get; private set; }
    /// <summary>
    /// Depth tested, but not written. For translucent materials
    /// </summary>
    public DepthStencilState TranslucentDepthState { get; private set; }
    /// <summary>
    /// For drawing over geometry that's already been drawn, such as additive light passes: depth tested against itself (GreaterEqual), but not written
    /// </summary>
    public DepthStencilState LightPassDepthState { get; private set; }

    /// <summary>
    /// Sets the rasterizer state for a mesh drawn with the game's shaders, matching UE3's FMeshDrawingPolicy::SetMeshRenderState.
    /// Two-sided materials aren't culled. Otherwise backfaces are culled, with the winding flipped for meshes whose transform mirrors them.
    /// </summary>
    public void SetMeshRasterizerState(bool isTwoSided, bool reverseCulling)
    {
        if (Wireframe) return;
        ImmediateContext.Rasterizer.State = isTwoSided ? FillRasterizerState
            : reverseCulling ? CullFrontRasterizerState
            : CullBackRasterizerState;
    }

    public void RestoreRasterizerState()
    {
        ImmediateContext.Rasterizer.State = Wireframe ? WireframeRasterizerState : FillRasterizerState;
    }

    private const int NUM_SAMPLER_SLOTS = 16;
    private SamplerState[] DefaultSamplers;

    /// <summary>
    /// Sets every pixel shader sampler slot to <see cref="SampleState"/>, which LEX's shaders expect
    /// </summary>
    public void RestoreDefaultSamplers()
    {
        if (DefaultSamplers is null)
        {
            DefaultSamplers = new SamplerState[NUM_SAMPLER_SLOTS];
            Array.Fill(DefaultSamplers, SampleState);
        }
        ImmediateContext.PixelShader.SetSamplers(0, DefaultSamplers);
    }

    private readonly Dictionary<(TextureAddressMode, TextureAddressMode), SamplerState> SamplerStateCache = [];

    /// <summary>
    /// Gets a sampler like <see cref="SampleState"/>, with the given address modes
    /// </summary>
    public SamplerState GetSamplerState(TextureAddressMode addressU, TextureAddressMode addressV)
    {
        if (addressU is TextureAddressMode.Wrap && addressV is TextureAddressMode.Wrap)
        {
            return SampleState;
        }
        if (!SamplerStateCache.TryGetValue((addressU, addressV), out SamplerState samplerState))
        {
            samplerState = new SamplerState(Device, new SamplerStateDescription
            {
                AddressU = addressU,
                AddressV = addressV,
                AddressW = TextureAddressMode.Wrap,
                Filter = Filter.Anisotropic,
                MaximumAnisotropy = 8
            });
            SamplerStateCache.Add((addressU, addressV), samplerState);
        }
        return samplerState;
    }
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
    private KeyStates PressedKeys;
    private MouseButtons PressedMouseButton;
    public float CameraSpeed { get; set; } = 500.0f; // Units per second
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
    private readonly Dictionary<Guid, VertexShader> VertexShaderCache = [];
    private readonly Dictionary<Guid, InputLayout> InputLayoutCache = [];
    private readonly Dictionary<Guid, PixelShader> PixelShaderCache = [];
    //parsing a package's ShaderCache is expensive, and every material in the package needs it
    private readonly Dictionary<IMEPackage, ShaderCache> SeekFreeShaderCaches = [];
    public readonly PreviewTextureCache TextureCache;
    public readonly PackageCache PackageCache;

    /// <summary>
    /// Render meshes with the game's shaders. Materials that can't be rendered with them fall back to LEX's shader.
    /// Only applies to meshes made of <see cref="LEVertex"/>.
    /// </summary>
    public bool UseGameShaders { get; set; }

    /// <summary>
    /// Lighting for meshes rendered with the game's shaders
    /// </summary>
    public PreviewLighting Lighting { get; } = new();

    //Materials whose game shaders haven't been loaded yet
    private readonly List<MaterialRenderProxy> PendingGameShaderLoads = [];

    private readonly Lock LightsLock = new();
    //replaced rather than modified, so it can be read without locking
    private SceneLight[] Lights = [];
    private int LightsVersion;

    /// <summary>
    /// Render static meshes lit by the level's lights (<see cref="AddLights"/>), as the game does. Otherwise, or if there are no lights,
    /// meshes without a light-map are lit by <see cref="Lighting"/>.
    /// </summary>
    public bool UseLevelLighting { get; set; } = true;

    /// <summary>
    /// See <see cref="UseLevelLighting"/>
    /// </summary>
    public bool IsLevelLightingActive => UseLevelLighting && Lights.Length > 0;

    /// <summary>
    /// The level lights in the scene, and a number that changes whenever they do. Thread-safe
    /// </summary>
    public SceneLight[] GetLights(out int version)
    {
        lock (LightsLock)
        {
            version = LightsVersion;
            return Lights;
        }
    }

    /// <summary>
    /// Adds lights from a level. Thread-safe
    /// </summary>
    public void AddLights(IEnumerable<SceneLight> lights)
    {
        lock (LightsLock)
        {
            Lights = [.. Lights, .. lights];
            LightsVersion++;
        }
    }

    /// <summary>
    /// Thread-safe
    /// </summary>
    public void ClearLights()
    {
        lock (LightsLock)
        {
            Lights = [];
            LightsVersion++;
        }
    }

    /// <summary>
    /// Thread-safe
    /// </summary>
    public void RemoveLights(IEnumerable<SceneLight> lights)
    {
        var toRemove = new HashSet<SceneLight>(lights);
        lock (LightsLock)
        {
            Lights = Lights.Where(light => !toRemove.Contains(light)).ToArray();
            LightsVersion++;
        }
    }

    //every mesh with static lighting, so their light shaders can be loaded ahead of time
    private readonly HashSet<MeshStaticLighting> StaticLightings = [];

    internal void RegisterStaticLighting(MeshStaticLighting staticLighting)
    {
        lock (StaticLightings)
        {
            StaticLightings.Add(staticLighting);
        }
    }

    internal void UnregisterStaticLighting(MeshStaticLighting staticLighting)
    {
        lock (StaticLightings)
        {
            StaticLightings.Remove(staticLighting);
        }
    }

    internal void AddPendingGameShaderLoad(MaterialRenderProxy material)
    {
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Add(material);
        }
    }

    /// <summary>
    /// Whether any materials have been created whose game shaders haven't been loaded by <see cref="LoadPendingGameShaders"/>
    /// </summary>
    public bool HasPendingGameShaderLoads
    {
        get
        {
            lock (PendingGameShaderLoads)
            {
                return PendingGameShaderLoads.Count > 0;
            }
        }
    }

    /// <summary>
    /// Loads the game shaders of every material created since the last call, and creates their D3D shaders.
    /// Then loads the light shaders that the level's lights need (see <see cref="MeshStaticLighting.GetLightInteractions"/>).
    /// Loading them can take seconds for a whole level, so call this on a background thread before turning on <see cref="UseGameShaders"/>,
    /// and after adding a level's lights. Otherwise shaders are loaded the first time they're needed.
    /// </summary>
    /// <param name="reportProgress">Called with the number of items loaded so far, and the total</param>
    /// <param name="prepareLevelLighting">Also load the light shaders, even if <see cref="UseLevelLighting"/> is off (for before turning it on)</param>
    public void LoadPendingGameShaders(Action<int, int> reportProgress = null, bool prepareLevelLighting = false)
    {
        MaterialRenderProxy[] materials;
        lock (PendingGameShaderLoads)
        {
            materials = [.. PendingGameShaderLoads];
            PendingGameShaderLoads.Clear();
        }
        MeshStaticLighting[] staticLightings = [];
        if (UseLevelLighting || prepareLevelLighting)
        {
            lock (StaticLightings)
            {
                staticLightings = [.. StaticLightings];
            }
        }
        int total = materials.Length + staticLightings.Length;
        for (int i = 0; i < materials.Length; i++)
        {
            MaterialRenderProxy material = materials[i];
            material.LoadGameShaders();
            if (Device is not null && material.CanRenderWithGameShaders)
            {
                GetCachedVertexShader(material.UnrealVertexShader.Guid, material.UnrealVertexShader.ShaderByteCode);
                GetCachedPixelShader(material.UnrealPixelShader.Guid, material.UnrealPixelShader.ShaderByteCode);
                if (material.NoLightMapVertexShader is not null)
                {
                    GetCachedVertexShader(material.NoLightMapVertexShader.Guid, material.NoLightMapVertexShader.ShaderByteCode);
                    GetCachedPixelShader(material.NoLightMapPixelShader.Guid, material.NoLightMapPixelShader.ShaderByteCode);
                }
            }
            reportProgress?.Invoke(i + 1, total);
        }
        for (int i = 0; i < staticLightings.Length; i++)
        {
            staticLightings[i].PrepareLightShaders();
            reportProgress?.Invoke(materials.Length + i + 1, total);
        }
    }

    /// <summary>
    /// Gets the parsed SeekFreeShaderCache of a package, or null if it doesn't have one. Results are cached until <see cref="EmptyCaches"/>.
    /// </summary>
    public ShaderCache GetSeekFreeShaderCache(IMEPackage pcc)
    {
        lock (SeekFreeShaderCaches)
        {
            if (!SeekFreeShaderCaches.TryGetValue(pcc, out ShaderCache shaderCache))
            {
                if (pcc.FindExport("SeekFreeShaderCache", "ShaderCache") is { } seekFreeShaderCacheExport)
                {
                    shaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                }
                SeekFreeShaderCaches.Add(pcc, shaderCache);
            }
            return shaderCache;
        }
    }

    public MeshRenderContext()
    {
        this.Camera.FocusDepth = 100.0f;
        TextureCache = new PreviewTextureCache(this);
        PackageCache = new PackageCache();
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

        if (Camera.IsOrthographic)
        {
            float panSpeed = Camera.OrthoWidth * 0.5f;
            if (PressedKeys.HasFlag(KeyStates.W))
                Camera.Position += Vector3.UnitY * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.S))
                Camera.Position -= Vector3.UnitY * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.A))
                Camera.Position -= Vector3.UnitX * timestep * panSpeed;
            if (PressedKeys.HasFlag(KeyStates.D))
                Camera.Position += Vector3.UnitX * timestep * panSpeed;
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
            if (PressedKeys.HasFlag(KeyStates.W))
            {
                Camera.Position += Camera.CameraForward * timestep * CameraSpeed;
            }
            if (PressedKeys.HasFlag(KeyStates.S))
            {
                Camera.Position -= Camera.CameraForward * timestep * CameraSpeed;
            }
            if (PressedKeys.HasFlag(KeyStates.A))
            {
                Camera.Position -= Camera.CameraRight * timestep * CameraSpeed;
            }
            if (PressedKeys.HasFlag(KeyStates.D))
            {
                Camera.Position += Camera.CameraRight * timestep * CameraSpeed;
            }
            if (PressedKeys.HasFlag(KeyStates.Q))
            {
                Camera.Position -= Vector3.UnitZ * timestep * CameraSpeed;
            }
            if (PressedKeys.HasFlag(KeyStates.E))
            {
                Camera.Position += Vector3.UnitZ * timestep * CameraSpeed;
            }
        }

        UpdateScene?.Invoke(null, timestep);
    }

    public override void Render()
    {
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
                    RenderScene?.Invoke(null, EventArgs.Empty);
                }
                catch (Exception e)
                {
                    ErrorText = e.FlattenException();
                }
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
                    RenderTarget2D.DrawText($"{FPS} fps\n{Camera.Position}", statsTextFormat, new RawRectangleF(0, 0, size.Width, size.Height), statsTextBrush);
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

        base.Render();
    }

    /// <summary>
    /// Resolves the multisampled linear scene color into the gamma-encoded backbuffer, and copies the hit proxy IDs into <see cref="HitBuffer"/>
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

    //Picks the highest MSAA sample count (up to PreferredSampleCount) that every scene render target format supports
    private int GetSupportedSampleCount()
    {
        for (int count = PreferredSampleCount; count > 1; count /= 2)
        {
            if (Device.CheckMultisampleQualityLevels(Format.R16G16B16A16_Float, count) > 0
                && Device.CheckMultisampleQualityLevels(Format.D32_Float, count) > 0
                && Device.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, count) > 0)
            {
                return count;
            }
        }
        return 1;
    }

    private RasterizerState CreateRasterizerState(CullMode cullMode, FillMode fillMode = FillMode.Solid, int depthBias = 0)
    {
        return new RasterizerState(Device, new RasterizerStateDescription
        {
            CullMode = cullMode,
            FillMode = fillMode,
            //matches UE3's D3D10/11 RHI, whose CM_CW cull mode is D3D11_CULL_BACK
            IsFrontCounterClockwise = true,
            IsMultisampleEnabled = SampleCount > 1,
            IsAntialiasedLineEnabled = false,
            DepthBias = depthBias
        });
    }

    public override void CreateResources()
    {
        base.CreateResources();

        SampleCount = GetSupportedSampleCount();

        //LEX's own shader, and two-sided materials, don't cull backfaces
        FillRasterizerState = CreateRasterizerState(CullMode.None);
        ImmediateContext.Rasterizer.State = FillRasterizerState;
        //pulls wireframes towards the camera, so they draw over the surfaces they outline. (Positive, since depth is reversed)
        WireframeRasterizerState = CreateRasterizerState(CullMode.None, FillMode.Wireframe, depthBias: 10);
        CullBackRasterizerState = CreateRasterizerState(CullMode.Back);
        CullFrontRasterizerState = CreateRasterizerState(CullMode.Front);

        DefaultDepthState = new DepthStencilState(Device, new DepthStencilStateDescription
        {
            IsDepthEnabled = true,
            DepthWriteMask = DepthWriteMask.All,
            DepthComparison = Comparison.Greater,
            IsStencilEnabled = false
        });
        ImmediateContext.OutputMerger.SetDepthStencilState(DefaultDepthState);

        // Set texture sampler state
        var ssd = new SamplerStateDescription
        {
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            Filter = Filter.Anisotropic,
            MaximumAnisotropy = 8
        };
        SampleState = new SamplerState(Device, ssd);
        RestoreDefaultSamplers();

        TranslucentDepthState = new DepthStencilState(Device, new DepthStencilStateDescription
        {
            IsDepthEnabled = true,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthComparison = Comparison.Greater,
            IsStencilEnabled = false
        });
        LightPassDepthState = new DepthStencilState(Device, new DepthStencilStateDescription
        {
            IsDepthEnabled = true,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthComparison = Comparison.GreaterEqual,
            IsStencilEnabled = false
        });

        // Load the default texture
        DefaultTexture = this.LoadTextureFromFile(Path.Combine(AppDirectories.ExecFolder, "Default.png"));
        DefaultTextureView = new ShaderResourceView(Device, DefaultTexture);

        // Load the default position-texture shader
        DefaultEffect = new GenericEffect<WorldConstants>(Device, EmbeddedResources.LevelEditorShader);
        using (ShaderBytecode leVertexVSBytecode = ShaderBytecode.Compile(EmbeddedResources.LevelEditorShader, "VSMainLEVertex", "vs_5_0").Bytecode)
        {
            LEVertexDefaultVertexShader = new VertexShader(Device, leVertexVSBytecode);
            LEVertexDefaultInputLayout = new InputLayout(Device, leVertexVSBytecode, LEVertex.InputElements);
        }
        using (ShaderBytecode resolveVSBytecode = ShaderBytecode.Compile(EmbeddedResources.LevelEditorShader, "VSMainResolve", "vs_5_0").Bytecode)
        {
            ResolveVertexShader = new VertexShader(Device, resolveVSBytecode);
        }
        using (ShaderBytecode resolvePSBytecode = ShaderBytecode.Compile($"#define MSAA_SAMPLES {SampleCount}\n" + EmbeddedResources.LevelEditorShader, "PSMainResolve", "ps_5_0").Bytecode)
        {
            ResolvePixelShader = new PixelShader(Device, resolvePSBytecode);
        }

        //create fallback textures
        WhiteTextureCube = CreateWhiteTextureCube();
        WhiteTextureCubeView = new ShaderResourceView(Device, WhiteTextureCube);
        WhiteTex = CreateWhiteTexture();
        WhiteTexView = new ShaderResourceView(Device, WhiteTex);

        LEEffect = new LEEffect(Device, EmbeddedResources.LevelEditorShader);
    }

    public override void CreateSizeDependentResources(int width, int height, Texture2D newBackBuffer)
    {
        base.CreateSizeDependentResources(width, height, newBackBuffer);
        BackbufferView = new RenderTargetView(Device, Backbuffer);

        Texture2D CreateTarget(Format format, BindFlags bindFlags, int sampleCount) => new(Device, new Texture2DDescription
        {
            ArraySize = 1,
            BindFlags = bindFlags,
            CpuAccessFlags = CpuAccessFlags.None,
            Format = format,
            Height = height,
            Width = width,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(sampleCount, 0),
            Usage = ResourceUsage.Default
        });

        //multisampled scene targets. Their views' dimensions (Texture2DMS or Texture2D) are inferred from the sample count
        SceneColor = CreateTarget(Format.R16G16B16A16_Float, BindFlags.RenderTarget | BindFlags.ShaderResource, SampleCount);
        SceneColorView = new RenderTargetView(Device, SceneColor);
        SceneColorResourceView = new ShaderResourceView(Device, SceneColor);
        DepthBuffer = CreateTarget(Format.D32_Float, BindFlags.DepthStencil, SampleCount);
        DepthBufferView = new DepthStencilView(Device, DepthBuffer);
        HitBufferMS = CreateTarget(Format.B8G8R8A8_UNorm, BindFlags.RenderTarget | BindFlags.ShaderResource, SampleCount);
        HitBufferView = new RenderTargetView(Device, HitBufferMS);
        HitBufferMSResourceView = new ShaderResourceView(Device, HitBufferMS);

        //single-sampled, so that it can be copied for reading on the CPU
        HitBuffer = CreateTarget(Format.B8G8R8A8_UNorm, BindFlags.RenderTarget, 1);
        HitBufferResolvedView = new RenderTargetView(Device, HitBuffer);

        ImmediateContext.OutputMerger.SetRenderTargets(DepthBufferView, SceneColorView, HitBufferView);
        ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);

        Camera.aspect = (float)Width / Height;


        using var factory = new D2D.Factory(D2D.FactoryType.SingleThreaded, App.IsDebug ? D2D.DebugLevel.Information : D2D.DebugLevel.None);
        RenderTarget2D = new D2D.RenderTarget(factory, newBackBuffer.QueryInterface<Surface>(), new D2D.RenderTargetProperties(new D2D.PixelFormat(Format.Unknown, D2D.AlphaMode.Premultiplied)));
        statsTextBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(0, 0, 0, 1), new D2D.BrushProperties { Opacity = 1 });
        errorTextBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(0.2f, 0, 0, 1), new D2D.BrushProperties { Opacity = 1 });
        using var dwFactory = new DW.Factory(DW.FactoryType.Shared);
        statsTextFormat = new DW.TextFormat(dwFactory, "Verdana", 12)
        {
            TextAlignment = DW.TextAlignment.Trailing,
            ParagraphAlignment = DW.ParagraphAlignment.Near
        };
        errorTextFormat = new DW.TextFormat(dwFactory, "Verdana", 18)
        {
            TextAlignment = DW.TextAlignment.Leading,
            ParagraphAlignment = DW.ParagraphAlignment.Center
        };
        labelTextFormat = new DW.TextFormat(dwFactory, "Verdana", 8)
        {
            TextAlignment = DW.TextAlignment.Center,
            ParagraphAlignment = DW.ParagraphAlignment.Center
        };
        labelTextBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(1, 1, 1, 1), new D2D.BrushProperties { Opacity = 1 });
        labelBackgroundBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(0, 0, 0, 0.65f), new D2D.BrushProperties { Opacity = 1 });
    }

    public override void DisposeSizeDependentResources()
    {
        ImmediateContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
        BackbufferView.Dispose();
        BackbufferView = null;
        SceneColorResourceView?.Dispose();
        SceneColorResourceView = null;
        SceneColorView?.Dispose();
        SceneColorView = null;
        SceneColor?.Dispose();
        SceneColor = null;
        DepthBufferView.Dispose();
        DepthBufferView = null;
        DepthBuffer.Dispose();
        DepthBuffer = null;
        HitBufferView?.Dispose();
        HitBufferView = null;
        HitBufferMSResourceView?.Dispose();
        HitBufferMSResourceView = null;
        HitBufferMS?.Dispose();
        HitBufferMS = null;
        HitBufferResolvedView?.Dispose();
        HitBufferResolvedView = null;
        HitBuffer?.Dispose();
        HitBuffer = null;
        RenderTarget2D.Dispose();
        statsTextFormat?.Dispose();
        errorTextFormat?.Dispose();
        labelTextFormat?.Dispose();
        statsTextBrush?.Dispose();
        errorTextBrush?.Dispose();
        labelTextBrush?.Dispose();
        labelBackgroundBrush?.Dispose();
        base.DisposeSizeDependentResources();
    }

    public override void DisposeResources()
    {
        if (!IsReady)
            return;

        TextureCache?.Dispose();
        DefaultTextureView?.Dispose();
        WhiteTextureCubeView?.Dispose();
        WhiteTexView?.Dispose();
        DefaultTexture?.Dispose();
        WhiteTextureCube?.Dispose();
        WhiteTex?.Dispose();
        SampleState?.Dispose();
        DefaultSamplers = null;
        SamplerStateCache.DisposeValuesAndClear();
        TranslucentDepthState?.Dispose();
        LightPassDepthState?.Dispose();
        DefaultDepthState?.Dispose();
        CullBackRasterizerState?.Dispose();
        CullFrontRasterizerState?.Dispose();
        DefaultEffect?.Dispose();
        LEVertexDefaultVertexShader?.Dispose();
        LEVertexDefaultInputLayout?.Dispose();
        ResolveVertexShader?.Dispose();
        ResolvePixelShader?.Dispose();
        LEEffect?.Dispose();
        FillRasterizerState?.Dispose();
        WireframeRasterizerState?.Dispose();
        EmptyCaches();
        base.DisposeResources();
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
        return new WorldConstants(Matrix4x4.Transpose(Camera.ProjectionMatrix), Matrix4x4.Transpose(Camera.ViewMatrix), Matrix4x4.Transpose(localToWorld), RenderFlags, CurrentHitTestId);
    }

    public BlendState GetCachedBlendState(RenderTargetBlendDescription renderTargetBlendDesc)
    {
        if (!BlendStateCache.TryGetValue(renderTargetBlendDesc, out BlendState blendState))
        {
            blendState = new BlendState(Device, new BlendStateDescription
            {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = true,
                RenderTarget =
                {
                    [0] = renderTargetBlendDesc
                }
            });
            BlendStateCache.Add(renderTargetBlendDesc, blendState);
        }
        return blendState;
    }

    //Locked, since shaders can be created on a background thread by LoadPendingGameShaders. (Creating D3D11 resources is thread-safe)
    public (VertexShader, InputLayout) GetCachedVertexShader(Guid id, byte[] shaderBytecode)
    {
        lock (VertexShaderCache)
        {
            InputLayout inputLayout;
            if (VertexShaderCache.TryGetValue(id, out VertexShader shader))
            {
                inputLayout = InputLayoutCache[id];
            }
            else
            {
                shader = new VertexShader(Device, shaderBytecode);
                VertexShaderCache.Add(id, shader);
                inputLayout = new InputLayout(Device, shaderBytecode, [.. LEVertex.InputElements, .. MeshStaticLighting.InputElements]);
                InputLayoutCache.Add(id, inputLayout);
            }
            return (shader, inputLayout);
        }
    }

    public PixelShader GetCachedPixelShader(Guid id, byte[] shaderBytecode)
    {
        lock (PixelShaderCache)
        {
            if (!PixelShaderCache.TryGetValue(id, out PixelShader shader))
            {
                //The game's bytecode is used as-is. Base pass shaders write 0 to alpha; materials mask that out with their blend state.
                shader = new PixelShader(Device, shaderBytecode);
                PixelShaderCache.Add(id, shader);
            }
            return shader;
        }
    }

    /// <summary>
    /// Renders a section of a game-shader mesh with LEX's default shader. Used for materials that can't be rendered with game shaders.
    /// </summary>
    public void RenderMeshWithDefaultEffect(Mesh<LEVertex> mesh, ModelPreviewSection section, ShaderResourceView diffuseTexture)
    {
        DefaultEffect.PrepDraw(ImmediateContext, AlphaBlendState, GetWorldConstants(mesh.LocalToWorld));
        ImmediateContext.InputAssembler.InputLayout = LEVertexDefaultInputLayout;
        ImmediateContext.VertexShader.Set(LEVertexDefaultVertexShader);
        DefaultEffect.RenderObject(ImmediateContext, mesh, (int)section.StartIndex, (int)section.TriangleCount * 3,
            Wireframe ? null : diffuseTexture ?? DefaultTextureView);
    }

    public override void EmptyCaches()
    {
        PackageCache?.ReleasePackages();
        lock (SeekFreeShaderCaches)
        {
            SeekFreeShaderCaches.Clear();
        }
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Clear();
        }
        TextureCache?.ExpungeStaleCacheItems();
        BlendStateCache.DisposeValuesAndClear();
        lock (VertexShaderCache)
        {
            VertexShaderCache.DisposeValuesAndClear();
            InputLayoutCache.DisposeValuesAndClear();
        }
        lock (PixelShaderCache)
        {
            PixelShaderCache.DisposeValuesAndClear();
        }
    }

    private System.Drawing.Point mouseDownPos;
    public override bool MouseDown(MouseButtons button, int x, int y)
    {
        if (PressedMouseButton is MouseButtons.None)
        {
            mouseDownPos = new System.Drawing.Point(x, y);
            PressedMouseButton = button;
        }
        return false;
    }

    public override bool MouseUp(MouseButtons button, int x, int y)
    {
        PressedMouseButton = MouseButtons.None;

        //if it moved any significant amount, we count it as a drag
        return Math.Abs(x - mouseDownPos.X) > 3 || Math.Abs(y - mouseDownPos.Y) > 3;
    }

    private System.Drawing.Point lastMouse;
    public override bool MouseMove(int x, int y)
    {
        bool handled = false;
        int xDiff = (x - lastMouse.X);
        int yDiff = (y - lastMouse.Y);
        if (Camera.IsOrthographic)
        {
            switch (PressedMouseButton)
            {
                case MouseButtons.Left:
                case MouseButtons.Middle:
                    float worldPerPixel = Camera.OrthoWidth / Width;
                    Camera.Position += new Vector3(-xDiff * worldPerPixel, yDiff * worldPerPixel, 0);
                    handled = true;
                    break;
                case MouseButtons.Right:
                    Camera.OrthoWidth *= MathF.Pow(1.01f, yDiff);
                    Camera.OrthoWidth = MathF.Max(Camera.OrthoWidth, 1f);
                    handled = true;
                    break;
            }
        }
        else if (Camera.FirstPerson)
        {
            switch (PressedMouseButton)
            {
                case MouseButtons.Left:
                    var camFwd = (Camera.CameraForward with { Z = 0 }).Normal();
                    Camera.Position += camFwd * -yDiff * (CameraSpeed / FPS);
                    Camera.Yaw += xDiff * 0.01f;
                    handled = true;
                    break;
                case MouseButtons.Middle:
                    Camera.Position += Camera.CameraRight * -xDiff * (CameraSpeed / FPS);
                    Camera.Position += Camera.CameraUp * yDiff * (CameraSpeed / FPS);
                    handled = true;
                    break;
                case MouseButtons.Right:
                    Camera.Yaw += xDiff * 0.01f;
                    Camera.Pitch = (Camera.Pitch - yDiff * 0.01f).Clamp(-MathF.PI / 2 + 0.01f, MathF.PI / 2 - 0.01f);
                    handled = true;
                    break;
            }
        }
        else
        {
            switch (PressedMouseButton)
            {
                //orbiting
                case MouseButtons.Left:
                    Camera.Yaw += xDiff * 0.01f;
                    Camera.Pitch = (Camera.Pitch - yDiff * 0.01f).Clamp(-MathF.PI / 2 + 0.01f, MathF.PI / 2 - 0.01f);
                    handled = true;
                    break;
                //panning
                case MouseButtons.Middle:
                    Camera.Position -= Camera.CameraRight * xDiff * Camera.FocusDepth * 0.004f;
                    Camera.Position += Camera.CameraUp * yDiff * Camera.FocusDepth * 0.004f;
                    handled = true;
                    break;
                //zooming
                case MouseButtons.Right:
                    Camera.FocusDepth += yDiff * Camera.FocusDepth * 0.1f * 0.1f;
                    if (Camera.FocusDepth < 0.1) Camera.FocusDepth = 0.1f;
                    handled = true;
                    break;
            }
        }
        lastMouse = new System.Drawing.Point(x, y);
        return handled;
    }

    public override bool MouseScroll(int delta)
    {
        if (Camera.IsOrthographic)
        {
            Camera.OrthoWidth *= MathF.Pow(1.2f, -Math.Sign(delta));
            Camera.OrthoWidth = MathF.Max(Camera.OrthoWidth, 1f);
        }
        else if (Camera.FirstPerson)
        {
            Camera.Position += Camera.CameraForward * (CameraSpeed / FPS ) * (delta / 10f);
        }
        else
        {
            Camera.FocusDepth *= MathF.Pow(1.2f, -Math.Sign(delta)); // kinda hacky because this moves in constant increments regardless of how far the user scrolls.
        }
        return true;
    }

    /// <summary>
    /// Handles key down events. Returns true if the key was accepted.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public override bool KeyDown(Key key)
    {
        switch (key)
        {
            case Key.W:
                PressedKeys |= KeyStates.W;
                return true;
            case Key.S:
                PressedKeys |= KeyStates.S;
                return true;
            case Key.A:
                PressedKeys |= KeyStates.A;
                return true;
            case Key.D:
                PressedKeys |= KeyStates.D;
                return true;
            case Key.Q:
                PressedKeys |= KeyStates.Q;
                return true;
            case Key.E:
                PressedKeys |= KeyStates.E;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Handles key up events. Returns true if the key was accepted.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public override bool KeyUp(Key key)
    {
        switch (key)
        {
            case Key.W:
                PressedKeys &= ~KeyStates.W;
                return true;
            case Key.S:
                PressedKeys &= ~KeyStates.S;
                return true;
            case Key.A:
                PressedKeys &= ~KeyStates.A;
                return true;
            case Key.D:
                PressedKeys &= ~KeyStates.D;
                return true;
            case Key.Q:
                PressedKeys &= ~KeyStates.Q;
                return true;
            case Key.E:
                PressedKeys &= ~KeyStates.E;
                return true;
            default:
                return false;
        }
    }

    public override bool LostKeyboardFocus()
    {
        bool handled = PressedKeys is not KeyStates.None;

        PressedKeys = KeyStates.None;

        return handled;
    }

    public override bool LostMouseFocus()
    {
        bool handled = PressedMouseButton is not MouseButtons.None;

        PressedMouseButton = MouseButtons.None;

        return handled;
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

    /// <summary>
    /// A texture loaded from an Unreal texture, and the formats its views should be created with.
    /// </summary>
    /// <param name="ViewFormat">Format for a view that reads the raw texel values (what LEX's shader expects)</param>
    /// <param name="SRGBViewFormat">If the texture holds sRGB-encoded color, a format for a view that converts to linear (what the game's shaders expect). Otherwise null.</param>
    public readonly record struct LoadedTexture(Texture2D Texture, Format ViewFormat, Format? SRGBViewFormat);

    public LoadedTexture LoadUnrealTexture(ExportEntry texture2DExport)
    {
        if (texture2DExport.ClassName is "TextureRenderTarget2D" or "TextureMovie")
        {
            return new LoadedTexture(CreateWhiteTexture(), Format.R8G8B8A8_UNorm, null);
        }
        var unrealTexture = new LECTexture2D(texture2DExport);
        var pixelFormat = LegendaryExplorerCore.Textures.Image.getPixelFormatType(unrealTexture.Export.GetProperty<EnumProperty>("Format").Value.Name);
        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        var srgbFormats = IsSRGB(texture2DExport) ? RenderContextExtensions.GetSRGBFormats(format) : null;
        Texture2D texture = this.LoadUnrealMipChain(unrealTexture.Mips, unrealTexture.GetTopMip(), pixelFormat, typelessResource: srgbFormats is not null);
        return new LoadedTexture(texture, srgbFormats?.UNorm ?? format, srgbFormats?.SRGB);
    }

    public LoadedTexture LoadUnrealTextureCube(ExportEntry textureCubeExport, PackageCache packageCache = null)
    {
        if (textureCubeExport.ClassName != "TextureCube") throw new ArgumentException("Expected a TextureCube export.", nameof(textureCubeExport));

        var props = textureCubeExport.GetProperties();
        var faceTextures = new Fixed6<LECTexture2D>();
        Span<string> facePropNames = ["FacePosX", "FaceNegX", "FacePosY", "FaceNegY", "FacePosZ", "FaceNegZ"];
        for (int i = 0; i < 6; i++)
        {
            ObjectProperty faceProp = props.GetProp<ObjectProperty>(facePropNames[i]);
            if (faceProp is null)
            {
                return new LoadedTexture(CreateWhiteTextureCube(), Format.R8G8B8A8_UNorm, null);
            }
            faceTextures[i] = new(faceProp.ResolveToExport(textureCubeExport.FileRef, packageCache));
        }
        var pixelData = new Fixed6<byte[]>();

        //should be the same for all textures
        uint size = (uint)faceTextures[0].GetTopMip().width;
        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(
            LegendaryExplorerCore.Textures.Image.getPixelFormatType(faceTextures[0].Export.GetProperty<EnumProperty>("Format").Value.Name));
        for (int i = 0; i < 6; i++)
        {
            pixelData[i] = LECTexture2D.GetTextureData(faceTextures[i].GetTopMip(), textureCubeExport.Game);
        }
        var srgbFormats = IsSRGB(faceTextures[0].Export) ? RenderContextExtensions.GetSRGBFormats(format) : null;
        Texture2D texture = this.LoadTextureCube(size, format, pixelData, srgbFormats?.Typeless);
        return new LoadedTexture(texture, srgbFormats?.UNorm ?? format, srgbFormats?.SRGB);
    }

    //UTexture.SRGB defaults to true
    private static bool IsSRGB(ExportEntry textureExport) => textureExport.GetProperty<BoolProperty>("SRGB")?.Value ?? true;

    private Texture2D CreateWhiteTexture()
    {
        var tex = new Texture2D(Device, new Texture2DDescription { Width = 1, Height = 1, MipLevels = 1, ArraySize = 1, Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0), BindFlags = BindFlags.ShaderResource });
        int white = -1;
        ImmediateContext.UpdateSubresource(ref white, tex, rowPitch: 4);
        return tex;
    }

    private Texture2D CreateWhiteTextureCube()
    {
        var whiteCubeData = new Fixed6<byte[]>();
        whiteCubeData[0] = whiteCubeData[1] = whiteCubeData[2] = whiteCubeData[3] = whiteCubeData[4] = whiteCubeData[5] = [255, 255, 255, 255];
        return this.LoadTextureCube(1, Format.R8G8B8A8_UNorm, whiteCubeData);
    }
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