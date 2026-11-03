using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Concurrent;
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
public class MeshRenderContext : RenderContext
{
    // Set by views that opt into culling; null keeps the existing preview behavior.
    public ViewFrustum CullingFrustum { get; set; }

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
    //for reading the depth buffer in shaders, while it isn't bound as the depth target (see LightAttenuationRenderer)
    private ShaderResourceView DepthBufferResourceView;
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
    private D2D.SolidColorBrush statsShadowBrush;
    private D2D.SolidColorBrush errorTextBrush;
    private D2D.SolidColorBrush labelTextBrush;
    private D2D.SolidColorBrush labelBackgroundBrush;
    #endregion
    public GenericEffect<WorldConstants> DefaultEffect { get; private set; }
    //lets LEVertex meshes be drawn with DefaultEffect's pixel shader
    private VertexShader LEVertexDefaultVertexShader;
    private InputLayout LEVertexDefaultInputLayout;
    private VertexShader TerrainVertexShader;
    private PixelShader TerrainPixelShader;
    private InputLayout TerrainInputLayout;
    //index buffers of terrain sections without holes or flipped quads, by section size
    private readonly Dictionary<(int, int), (SharpDX.Direct3D11.Buffer Buffer, Format Format, int Count)> RegularTerrainIndices = [];
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
    private readonly Dictionary<(int, float, CullMode, FillMode), RasterizerState> decalRasterizers = [];

    internal void SetDecalRasterizerState(DecalProjection decal, bool twoSided, bool reverse)
    {
        // Reversed depth reverses UE3's signed bias. D3D11 applies the integer bias at the depth format's precision.
        int bias = (int)Math.Clamp(-decal.DepthBias * (1 << 24), -100000, 100000);
        var cull = twoSided ? CullMode.None : reverse ? CullMode.Front : CullMode.Back;
        var fill = Wireframe ? FillMode.Wireframe : FillMode.Solid;
        var key = (bias, -decal.SlopeBias, cull, fill);
        if (!decalRasterizers.TryGetValue(key, out var state))
        {
            var description = new RasterizerStateDescription
            {
                CullMode = cull, FillMode = fill,
                IsFrontCounterClockwise = true, IsMultisampleEnabled = SampleCount > 1,
                DepthBias = bias, SlopeScaledDepthBias = -decal.SlopeBias
            };
            decalRasterizers[key] = state = new RasterizerState(Device, description);
        }
        ImmediateContext.Rasterizer.State = state;
    }
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
                MaximumAnisotropy = 8,
                MaximumLod = float.MaxValue // The zero-initialized default clamps sampling to mip 0.
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
    private readonly ConcurrentDictionary<Guid, (VertexShader Shader, InputLayout InputLayout)> VertexShaderCache = new();
    private readonly ConcurrentDictionary<Guid, PixelShader> PixelShaderCache = new();
    //compiled for shadow depth vertex shaders, which material instances without a static permutation share with their parent. A null value is one that can't be matched
    private readonly ConcurrentDictionary<Guid, Lazy<byte[]>> DepthPixelShaderByteCodes = new();
    //parsing a package's ShaderCache is expensive, and every material in the package needs it
    private readonly Dictionary<IMEPackage, ShaderCache> SeekFreeShaderCaches = [];
    public readonly PreviewTextureCache TextureCache;
    public readonly PreviewPackageCache PackageCache;

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
    //replaced under LightsLock rather than modified, like Lights
    private LightAssignmentHistory assignmentHistory = LightAssignmentHistory.Empty;
    internal LightAssignmentHistory AssignmentHistory => Volatile.Read(ref assignmentHistory);
    private LightAssignmentPolicy assignmentPolicy = LightAssignmentPolicy.RecomputeAssignments;
    public LightAssignmentPolicy AssignmentPolicy
    {
        get => assignmentPolicy;
        set
        {
            if (assignmentPolicy == value) return;
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            lock (LightsLock)
            {
                assignmentPolicy = value;
                LightsVersion++;
            }
            // Preparation will run on the editor's background worker before rendering resumes.
            lock (StaticLightings)
                foreach (var lighting in StaticLightings) lighting.InvalidatePreviewPreparation();
        }
    }

    /// <summary>Whether ForgetLevel dropped the level since it was last opened, and how many times either happened</summary>
    private sealed class LevelCaptureState
    {
        public bool Forgotten;
        public int Generation;
    }

    //per level (weakly, so closed levels aren't kept alive), so that a capture that was reading a level that was closed, or closed and reopened,
    //doesn't add the old records back; and how many times ClearLights dropped every level. Guarded by LightsLock
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IMEPackage, LevelCaptureState> LevelCaptures = new();
    private int LightsClears;

    /// <summary>
    /// Records the level's light assignments as they are now, unless it has them already
    /// </summary>
    /// <param name="opening">The level is being opened, so it's captured even if it was forgotten before (an open package can be reopened)</param>
    internal void CaptureAssignments(IMEPackage package, bool opening = false)
    {
        int clears, generation;
        LevelCaptureState state;
        lock (LightsLock)
        {
            state = LevelCaptures.GetValue(package, static _ => new LevelCaptureState());
            if (opening && state.Forgotten)
            {
                state.Forgotten = false;
                state.Generation++;
            }
            if (assignmentHistory.Contains(package) || state.Forgotten) return;
            (clears, generation) = (LightsClears, state.Generation);
        }
        //parsing the package's primitives takes a while, so it's done outside the lock
        var records = LightAssignmentHistory.Read(package);
        lock (LightsLock)
        {
            //closed (and maybe reopened) meanwhile, or captured by another thread
            if (clears != LightsClears || state.Generation != generation || assignmentHistory.Contains(package)) return;
            Volatile.Write(ref assignmentHistory, assignmentHistory.With(records));
            LightsVersion++;
        }
    }

    /// <summary>
    /// Select level lighting, a neutral preview light, or material colors without lighting.
    /// </summary>
    public ViewportLightingMode LightingMode { get; set; } = ViewportLightingMode.Level;

    public bool UseDynamicLighting { get; set; } = true;
    public bool UseLightMaps { get; set; } = true;
    public bool IsPreviewLighting => LightingMode == ViewportLightingMode.Preview;
    public bool UsesLevelLighting => LightingMode is ViewportLightingMode.Level or ViewportLightingMode.Preview;
    public bool IsUnlit => LightingMode == ViewportLightingMode.Unlit;
    public bool AreLightMapsActive => LightingMode == ViewportLightingMode.Level && UseLightMaps;
    public bool IsDynamicLightingActive => LightingMode is ViewportLightingMode.Neutral or ViewportLightingMode.Preview
        || (LightingMode == ViewportLightingMode.Level && UseDynamicLighting);

    /// <summary>
    /// Whether the level has lights to render in additive passes.
    /// </summary>
    public bool IsLevelLightingActive => UsesLevelLighting && (IsPreviewLighting || UseDynamicLighting) && Lights.Length > 0;

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
            Volatile.Write(ref assignmentHistory, LightAssignmentHistory.Empty);
            LightsClears++;
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

    /// <summary>
    /// Replaces one level's lights atomically, invalidating mesh interactions and light environments once.
    /// Other open levels keep their lights. Thread-safe.
    /// </summary>
    public void ReplaceLights(IEnumerable<SceneLight> previous, IEnumerable<SceneLight> replacement)
    {
        var toRemove = new HashSet<SceneLight>(previous);
        SceneLight[] newLights = replacement.ToArray();
        lock (LightsLock)
        {
            Lights = [.. Lights.Where(light => !toRemove.Contains(light)), .. newLights];
            LightsVersion++;
        }
    }

    internal readonly LightAttenuationRenderer LightAttenuations;

    /// <summary>
    /// Whether the actor is shown in this view, so that its meshes cast their dynamic shadows
    /// </summary>
    public virtual bool IsActorVisible(ActorProxy actor) => true;

    /// <summary>Whether a component is shown, including its owning actor's visibility.</summary>
    public virtual bool IsComponentVisible(PrimitiveComponentProxy component)
        => component.IsVisible && (component.Actor is null || IsActorVisible(component.Actor));

    //light passes queued during RenderPass.Lighting, drawn by EndLightingPass
    private readonly List<(SceneLight Light, Action<ShaderResourceView> Draw)> PendingLightPasses = [];

    /// <summary>
    /// For <see cref="RenderPass.Lighting"/>: queues a light pass, which <see cref="EndLightingPass"/> draws with the light's attenuation (null if it has none).
    /// The draw sets up all its own render state
    /// </summary>
    internal void QueueLightPass(SceneLight light, Action<ShaderResourceView> draw) => PendingLightPasses.Add((light, draw));

    /// <summary>Draw a decal's light immediately after its base pass, before the next sorted decal.
    /// Attenuation uses the opaque receiver's scene depth; decals never write depth or cast shadows.</summary>
    internal void DrawDecalLightPass(SceneLight light, Action<ShaderResourceView> draw) => draw(GetLightAttenuation(light));

    //light passes that the modulated shadows don't darken, drawn after them by EndLightingPass
    private readonly List<Action> PendingPostModulatedShadowPasses = [];

    /// <summary>
    /// Queues a light pass to be drawn by <see cref="EndLightingPass"/> after the modulated shadows, so that they don't darken it. Only call if
    /// <see cref="AreModulatedShadowsActive"/>. The draw sets up all its own render state
    /// </summary>
    internal void QueuePostModulatedShadowPass(Action draw) => PendingPostModulatedShadowPasses.Add(draw);

    /// <summary>
    /// Whether light environments' modulated shadows are rendered (by <see cref="EndLightingPass"/>)
    /// </summary>
    internal bool AreModulatedShadowsActive => UseGameShaders && IsLevelLightingActive && DepthBufferResourceView is not null && !Camera.IsOrthographic && !Wireframe
                                               && !ModulatedShadowsFailed;

    /// <summary>
    /// Call after <see cref="RenderPass.Lighting"/>, before <see cref="RenderPass.Translucent"/>. Draws the queued light passes grouped by light, as UE3 does,
    /// so each light's dynamic shadows and light function are rendered once, then the light environments' modulated shadows, then the passes they don't darken.
    /// The scene's render targets are restored.
    /// </summary>
    public void EndLightingPass()
    {
        try
        {
            //GroupBy keeps the order lights were first queued in, and each light's passes in order
            foreach (IGrouping<SceneLight, (SceneLight Light, Action<ShaderResourceView> Draw)> lightPasses in PendingLightPasses.GroupBy(pass => pass.Light))
            {
                ShaderResourceView attenuation = GetLightAttenuation(lightPasses.Key);
                foreach ((_, Action<ShaderResourceView> draw) in lightPasses)
                {
                    draw(attenuation);
                }
            }
            RenderModulatedShadows();
            foreach (Action draw in PendingPostModulatedShadowPasses)
            {
                draw();
            }
        }
        finally
        {
            PendingLightPasses.Clear();
            PendingPostModulatedShadowPasses.Clear();
        }
    }

    /// <summary>
    /// Renders the light's dynamic shadows and light function into a texture of its attenuation across the screen. Changes the render state, then restores
    /// the scene's render targets and default states
    /// </summary>
    /// <returns>Null if the light has neither, or they can't be rendered. Only valid until the next light's is rendered</returns>
    private ShaderResourceView GetLightAttenuation(SceneLight light)
    {
        //positions are reconstructed from the scene depth with a perspective projection
        if (!LightAttenuationRenderer.MayHaveAttenuation(light, IsPreviewLighting) || DepthBufferResourceView is null || Camera.IsOrthographic)
        {
            return null;
        }
        try
        {
            return LightAttenuations.GetAttenuation(light, DepthBufferResourceView);
        }
        catch (Exception e)
        {
            //(the renderer won't retry it)
            System.Diagnostics.Debug.WriteLine($"Could not render the attenuation of {light.Export.InstancedFullPath}: {e.Message}");
            return null;
        }
        finally
        {
            RestoreSceneRenderState();
        }
    }

    private static readonly ShaderResourceView[] NoShaderResources = new ShaderResourceView[CommonShaderStage.InputResourceSlotCount];

    /// <summary>
    /// Restores what rendering lights' attenuations or modulated shadows changes, even if it failed partway:
    /// the scene's render targets and viewport, and the default depth, blend, rasterizer and sampler states
    /// </summary>
    private void RestoreSceneRenderState()
    {
        ImmediateContext.PixelShader.SetShaderResources(0, NoShaderResources);
        LEEffect.PixelShaderResources.Reset();
        ImmediateContext.OutputMerger.SetRenderTargets(DepthBufferView, SceneColorView, HitBufferView);
        ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);
        ImmediateContext.OutputMerger.SetDepthStencilState(DefaultDepthState);
        ImmediateContext.OutputMerger.SetBlendState(null);
        RestoreRasterizerState();
        RestoreDefaultSamplers();
    }

    private bool ModulatedShadowsFailed;

    /// <summary>
    /// For the modulated shadows of light environments: multiplies them into the scene color. The scene's render targets are restored.
    /// </summary>
    private void RenderModulatedShadows()
    {
        if (!AreModulatedShadowsActive)
        {
            return;
        }
        DynamicLightEnvironment[] lightEnvironments = GetLightEnvironments();
        if (lightEnvironments.Length == 0)
        {
            return;
        }
        try
        {
            LightAttenuations.RenderModulatedShadows(lightEnvironments, DepthBufferResourceView, SceneColorView);
        }
        catch (Exception e)
        {
            //not retried every frame. EmptyCaches and ForgetLevel reset it, for when the level changes
            ModulatedShadowsFailed = true;
            System.Diagnostics.Debug.WriteLine($"Could not render light environment shadows: {e.Message}");
        }
        finally
        {
            RestoreSceneRenderState();
        }
    }

    //light environments that have primitives, by their component (and the actor that owns them, since actors can share an archetype's)
    private readonly Dictionary<(ExportEntry owner, ExportEntry lightEnvironment), DynamicLightEnvironment> LightEnvironments = [];

    private readonly record struct WorldLightEnvironmentSettings(float CharacterLightingContrastFactor, bool AllowSphericalHarmonicLights,
        bool AllowModulateBetterShadows);

    //each level's persistent WorldInfo settings that light environments use. Guarded by LightEnvironments
    private readonly Dictionary<IMEPackage, WorldLightEnvironmentSettings> CachedWorldLightEnvironmentSettings = [];
    //Each game's installed environment defaults; guarded by LightEnvironments
    private readonly Dictionary<MEGame, LightEnvironmentSettings.InstalledDefaults> InstalledEnvironmentDefaults = [];

    /// <summary>
    /// Whether any of the game's light environment strategies are implemented; each environment's mode is checked separately
    /// </summary>
    internal static bool SupportsLightEnvironments(MEGame game) => GameLightingProfile.ForGame(game).SupportsLightEnvironments;

    /// <summary>
    /// Finds the light environment a primitive component is lit by: its LightEnvironment, unless that's disabled.
    /// Characters whose light environment can't be found (since it's set by a class's defaults that can't be read) get one with the class's defaults
    /// </summary>
    /// <param name="condensedProps">The component's properties, including inherited ones</param>
    /// <param name="owner">The actor that owns the component</param>
    /// <param name="lightEnvironment">The light environment, if it's used and its game and mode are supported.
    /// The component joins it with <see cref="AddLightEnvironmentPrimitive"/></param>
    /// <returns>Whether the component is lit by a light environment. False if its light environment can't be read</returns>
    internal bool ResolveLightEnvironment(ExportEntry componentExport, PropertyCollection condensedProps, ExportEntry owner, out DynamicLightEnvironment lightEnvironment) =>
        ResolveLightEnvironment(componentExport, condensedProps, owner, out lightEnvironment, out _);

    /// <param name="unsupportedReason">Why the component's light environment isn't rendered, when it uses one and <paramref name="lightEnvironment"/> is null</param>
    internal bool ResolveLightEnvironment(ExportEntry componentExport, PropertyCollection condensedProps, ExportEntry owner, out DynamicLightEnvironment lightEnvironment,
        out string unsupportedReason)
    {
        lightEnvironment = null;
        unsupportedReason = null;
        if (!EffectiveLightingProperties.UsesLightEnvironment(componentExport, condensedProps, owner, PackageCache, out ExportEntry lightEnvironmentExport))
        {
            return false;
        }
        if (!SupportsLightEnvironments(componentExport.Game))
        {
            unsupportedReason = "light environments aren't implemented for this game";
            return true;
        }
        try
        {
            //a shared light environment is its actor's. An inherited one with no instance of its own is still this actor's alone
            ExportEntry lightEnvironmentOwner = lightEnvironmentExport?.Parent is ExportEntry outer && outer != owner
                && EffectiveLightingProperties.IsPlacedActor(outer) ? outer : owner;
            lightEnvironment = GetLightEnvironment(lightEnvironmentOwner, lightEnvironmentExport, out unsupportedReason);
            return true;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the light environment of {componentExport.InstancedFullPath}, so it's lit like other meshes: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// The light environment whose LightEnvironment component is <paramref name="lightEnvironmentExport"/>: the existing one, or a new one, which is only kept once
    /// a primitive joins it (<see cref="AddLightEnvironmentPrimitive"/>). Null if the game or the environment's mode isn't supported
    /// </summary>
    /// <param name="owner">The actor that owns the light environment</param>
    /// <param name="lightEnvironmentExport">Null for a character with the class's default light environment</param>
    private DynamicLightEnvironment GetLightEnvironment(ExportEntry owner, ExportEntry lightEnvironmentExport, out string unsupportedReason)
    {
        unsupportedReason = null;
        if ((owner ?? lightEnvironmentExport) is not { } entry || !SupportsLightEnvironments(entry.Game))
        {
            unsupportedReason = "light environments aren't implemented for this game";
            return null;
        }
        IMEPackage level = entry.FileRef;
        WorldLightEnvironmentSettings worldSettings;
        bool hasWorldSettings;
        //the lock is held briefly, since the render thread takes it every frame: the settings are read (from packages, on the loading thread) outside it
        lock (LightEnvironments)
        {
            if (LightEnvironments.TryGetValue((owner, lightEnvironmentExport), out DynamicLightEnvironment lightEnvironment))
            {
                return lightEnvironment;
            }
            hasWorldSettings = CachedWorldLightEnvironmentSettings.TryGetValue(level, out worldSettings);
        }
        if (!hasWorldSettings)
        {
            worldSettings = ReadWorldLightEnvironmentSettings(level);
            lock (LightEnvironments)
            {
                CachedWorldLightEnvironmentSettings[level] = worldSettings;
            }
        }
        LightEnvironmentSettings settings = lightEnvironmentExport is null ? LightEnvironmentSettings.CreateDefaults(entry.Game, isBio: true)
            : LightEnvironmentSettings.Read(lightEnvironmentExport, PackageCache);
        GetInstalledEnvironmentDefaults(entry.Game).ApplyTo(settings);
        settings.AllowModulateBetterShadows = worldSettings.AllowModulateBetterShadows;
        //Unimplemented modes fall back with their reason rather than borrowing another game's strategy
        if (GameLightingProfile.GetEnvironmentSupport(settings) is { } reason)
        {
            unsupportedReason = reason;
            return null;
        }
        //(if another thread made one for the same component meanwhile, AddLightEnvironmentPrimitive keeps whichever is added first)
        return new DynamicLightEnvironment(this, owner, lightEnvironmentExport, settings, worldSettings.CharacterLightingContrastFactor,
            worldSettings.AllowSphericalHarmonicLights);
    }

    private LightEnvironmentSettings.InstalledDefaults GetInstalledEnvironmentDefaults(MEGame game)
    {
        lock (LightEnvironments)
        {
            if (InstalledEnvironmentDefaults.TryGetValue(game, out var cached)) return cached;
        }
        //(read outside the lock; the first stored wins)
        var defaults = LightEnvironmentSettings.InstalledDefaults.Load(game, PackageCache);
        lock (LightEnvironments) return InstalledEnvironmentDefaults.TryAdd(game, defaults) ? defaults : InstalledEnvironmentDefaults[game];
    }

    /// <summary>
    /// The WorldInfo settings that light environments in a level use: CharacterLightingContrastFactor, bAllowLightEnvSphericalHarmonicLights,
    /// and bAllowModulateBetterShadows.
    /// The game reads them from the persistent level's WorldInfo (GWorld->GetWorldInfo(TRUE)), not the streamed-in level's own
    /// </summary>
    private WorldLightEnvironmentSettings ReadWorldLightEnvironmentSettings(IMEPackage level)
    {
        //Default__WorldInfo's values
        WorldLightEnvironmentSettings settings = new(1.5f, true, true);
        try
        {
            if (FindWorldInfo(FindPersistentLevel(level) ?? level) is { } worldInfo)
            {
                PropertyCollection worldProps = EffectiveLightingProperties.Read(worldInfo, PackageCache);
                settings = new WorldLightEnvironmentSettings(worldProps.GetProp<FloatProperty>("CharacterLightingContrastFactor")?.Value ?? 1.5f,
                    worldProps.GetProp<BoolProperty>("bAllowLightEnvSphericalHarmonicLights")?.Value ?? true,
                    worldProps.GetProp<BoolProperty>("bAllowModulateBetterShadows")?.Value ?? true);
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the WorldInfo of {level.FileNameNoExtension}'s persistent level, so the defaults are used: {e.Message}");
        }
        return settings;

        //LE3's levels have a BioWorldInfo
        static ExportEntry FindWorldInfo(IMEPackage pcc) =>
            pcc.Exports.FirstOrDefault(export => !export.IsDefaultObject && GlobalUnrealObjectInfo.IsA(export.ClassName, "WorldInfo", export.Game));
    }

    /// <summary>
    /// The persistent level that streams in <paramref name="level"/>: BioWare names it BioP_{map}, for the levels named Bio?_{map}_..., and its WorldInfo's
    /// StreamingLevels lists them. Null if it can't be found, or <paramref name="level"/> is a persistent level
    /// </summary>
    private IMEPackage FindPersistentLevel(IMEPackage level)
    {
        string levelName = level.FileNameNoExtension;
        string[] nameParts = levelName.Split('_');
        if (levelName.StartsWith("BioP_", StringComparison.OrdinalIgnoreCase) || nameParts.Length < 2
            || !MELoadedFiles.GetFilesLoadedInGame(level.Game).TryGetValue($"BioP_{nameParts[1]}.pcc", out string persistentPath)
            || PackageCache.GetCachedPackage(persistentPath) is not { } persistent)
        {
            return null;
        }
        //it's only the persistent level if it streams this one in
        ExportEntry worldInfo = persistent.Exports.FirstOrDefault(export => !export.IsDefaultObject && GlobalUnrealObjectInfo.IsA(export.ClassName, "WorldInfo", export.Game));
        if (worldInfo?.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") is not { } streamingLevels)
        {
            return null;
        }
        foreach (ObjectProperty streamingLevelProp in streamingLevels)
        {
            if (streamingLevelProp.ResolveToEntry(persistent) is ExportEntry streamingLevel
                && streamingLevel.GetProperty<NameProperty>("PackageName")?.Value.Instanced.Equals(levelName, StringComparison.OrdinalIgnoreCase) is true)
            {
                return persistent;
            }
        }
        return null;
    }

    /// <summary>
    /// Adds a primitive to a light environment from <see cref="ResolveLightEnvironment"/>, keeping the light environment
    /// </summary>
    /// <returns>The light environment the primitive joined: <paramref name="lightEnvironment"/>, unless another primitive's has been kept for the same component meanwhile</returns>
    internal DynamicLightEnvironment AddLightEnvironmentPrimitive(DynamicLightEnvironment lightEnvironment, LightEnvironmentPrimitive primitive)
    {
        lock (LightEnvironments)
        {
            if (!LightEnvironments.TryGetValue((lightEnvironment.Owner, lightEnvironment.Export), out DynamicLightEnvironment kept))
            {
                LightEnvironments.Add((lightEnvironment.Owner, lightEnvironment.Export), kept = lightEnvironment);
            }
            kept.AddPrimitive(primitive);
            return kept;
        }
    }

    internal DynamicLightEnvironment[] GetLightEnvironments()
    {
        lock (LightEnvironments) return [.. LightEnvironments.Values];
    }

    /// <summary>
    /// Removes a primitive from its light environment, which is forgotten once it has none
    /// </summary>
    internal void RemoveLightEnvironmentPrimitive(DynamicLightEnvironment lightEnvironment, LightEnvironmentPrimitive primitive)
    {
        //under the same lock as adding, so a primitive can't join a light environment that's being forgotten
        lock (LightEnvironments)
        {
            if (lightEnvironment.RemovePrimitive(primitive) == 0
                && LightEnvironments.TryGetValue((lightEnvironment.Owner, lightEnvironment.Export), out DynamicLightEnvironment kept) && kept == lightEnvironment)
            {
                LightEnvironments.Remove((lightEnvironment.Owner, lightEnvironment.Export));
            }
        }
    }

    private int levelGeometryVersion;
    private readonly Lock LevelGeometryLock = new();
    private LevelGeometry LevelGeometry;
    private int BuiltLevelGeometryVersion = -1;
    //how many times the geometry has been built, which light environments' cached lighting depends on
    private int LevelGeometryGeneration;
    private long LastLevelGeometryBuildTimestamp;
    //While meshes are loading, unloading or being dragged, the geometry changes every frame. It's rebuilt at most this often, so that the
    //light environments that trace against it aren't all recomputed every frame
    private static readonly TimeSpan MinLevelGeometryRebuildInterval = TimeSpan.FromSeconds(0.25);
    //The triangles of each mesh, so that its instances share them. Weak, since the instances own them: they're freed once every instance is unloaded.
    //Meshes with no triangles are only remembered by key. Both are guarded by LevelGeometryLock
    private readonly Dictionary<string, WeakReference<LevelGeometry.TriangleMesh>> LevelGeometryMeshes = [];
    private readonly HashSet<string> MeshesWithoutLevelGeometry = [];
    //each open level's BSP, which also blocks visibility traces. Guarded by LevelGeometryLock
    private readonly Dictionary<IMEPackage, Model> LevelModels = [];

    /// <summary>
    /// Sets the BSP of a level, which blocks light environments' visibility traces. Only read if light environments are supported for the game
    /// </summary>
    /// <param name="modelUIndex">The level's Model</param>
    public void SetLevelModel(IMEPackage level, int modelUIndex)
    {
        Model model = null;
        if (SupportsLightEnvironments(level.Game) && level.TryGetUExport(modelUIndex, out ExportEntry modelExport))
        {
            try
            {
                model = modelExport.GetBinaryData<Model>();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read {modelExport.InstancedFullPath}, so it won't block light environments' visibility: {e.Message}");
            }
        }
        lock (LevelGeometryLock)
        {
            if (model is null)
            {
                LevelModels.Remove(level);
            }
            else
            {
                LevelModels[level] = model;
            }
        }
        InvalidateLevelGeometry();
    }

    /// <summary>
    /// Call when a static mesh that blocks light environments' visibility traces moves, so that <see cref="GetLevelGeometry"/> is rebuilt
    /// </summary>
    public void InvalidateLevelGeometry() => Interlocked.Increment(ref levelGeometryVersion);

    /// <summary>
    /// The level's geometry, for light environments' visibility traces. Rebuilt when it's needed after it changes, at most every <see cref="MinLevelGeometryRebuildInterval"/>
    /// </summary>
    /// <param name="generation">Changes whenever the returned geometry does</param>
    /// <param name="current">Rebuilt if it changed, however recently it was: for a one-off question (where a click meets the level), not a frame's</param>
    internal LevelGeometry GetLevelGeometry(out int generation, bool current = false)
    {
        lock (LevelGeometryLock)
        {
            int version = levelGeometryVersion;
            if (LevelGeometry is null || BuiltLevelGeometryVersion != version
                && (current || System.Diagnostics.Stopwatch.GetElapsedTime(LastLevelGeometryBuildTimestamp) >= MinLevelGeometryRebuildInterval))
            {
                LevelGeometry = new LevelGeometry(GetStaticLightings(), [.. LevelModels.Values]);
                BuiltLevelGeometryVersion = version;
                LevelGeometryGeneration++;
                LastLevelGeometryBuildTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                PruneLevelGeometryMeshes();
            }
            generation = LevelGeometryGeneration;
            return LevelGeometry;
        }
    }

    //forgets the meshes whose instances have all been unloaded. Call under LevelGeometryLock
    private void PruneLevelGeometryMeshes()
    {
        List<string> freed = null;
        foreach ((string key, WeakReference<LevelGeometry.TriangleMesh> mesh) in LevelGeometryMeshes)
        {
            if (!mesh.TryGetTarget(out _))
            {
                (freed ??= []).Add(key);
            }
        }
        freed?.ForEach(key => LevelGeometryMeshes.Remove(key));
    }

    /// <summary>
    /// Forget collision triangles for edited exports before rebuilding their component previews.
    /// Other instances can then share the new geometry instead of recovering the old weakly cached mesh.
    /// </summary>
    public void InvalidateLevelGeometryMeshes(IMEPackage package, IReadOnlySet<int> updatedExports)
    {
        bool changed = false;
        lock (LevelGeometryLock)
        {
            foreach (int index in updatedExports)
            {
                string key = $"{package.FilePath}|{index}";
                changed |= LevelGeometryMeshes.Remove(key);
                changed |= MeshesWithoutLevelGeometry.Remove(key);
            }
        }
        if (changed) InvalidateLevelGeometry();
    }

    /// <summary>
    /// The triangles a mesh blocks line checks with, read by <paramref name="readTriangles"/> the first time the mesh is seen. Null if it has none, or they can't be read
    /// </summary>
    /// <param name="key">Identifies the mesh</param>
    internal LevelGeometry.TriangleMesh GetLevelGeometryMesh(string key, Func<(Vector3[] Positions, int[] Indices)> readTriangles, kDOPTreeCompact collisionTree = null)
    {
        LevelGeometry.TriangleMesh mesh;
        lock (LevelGeometryLock)
        {
            if (LevelGeometryMeshes.TryGetValue(key, out WeakReference<LevelGeometry.TriangleMesh> cached) && cached.TryGetTarget(out mesh))
            {
                return mesh;
            }
            if (MeshesWithoutLevelGeometry.Contains(key))
            {
                return null;
            }
        }
        //read outside the lock, since it can be slow. Another thread may read the same mesh meanwhile; the first one stored wins
        try
        {
            (Vector3[] positions, int[] indices) = readTriangles();
            mesh = LevelGeometry.TriangleMesh.Create(positions, indices, collisionTree);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the collision of {key}, so it won't block light environments' visibility: {e.Message}");
            mesh = null;
        }
        lock (LevelGeometryLock)
        {
            if (mesh is null)
            {
                MeshesWithoutLevelGeometry.Add(key);
                return null;
            }
            if (LevelGeometryMeshes.TryGetValue(key, out WeakReference<LevelGeometry.TriangleMesh> cached) && cached.TryGetTarget(out LevelGeometry.TriangleMesh stored))
            {
                return stored;
            }
            LevelGeometryMeshes[key] = new WeakReference<LevelGeometry.TriangleMesh>(mesh);
            return mesh;
        }
    }

    /// <summary>
    /// Forgets the lights' light function materials and failed attenuations, so they're reloaded. For when a package edit may have changed them
    /// </summary>
    public void InvalidateLightFunctions()
    {
        LightAttenuations.ClearMaterials();
    }

    /// <summary>
    /// Forgets what's cached for a level that's been closed: its WorldInfo's light environment settings, and its own meshes' collision triangles, in case it's
    /// edited and reopened. (Other packages' meshes' triangles are freed once all their instances are unloaded)
    /// </summary>
    public void ForgetLevel(IMEPackage level)
    {
        //the level's imports key the class defaults
        EffectiveLightingProperties.InvalidateDefaults(PackageCache);
        lock (LightsLock)
        {
            Volatile.Write(ref assignmentHistory, assignmentHistory.Without(level));
            var state = LevelCaptures.GetValue(level, static _ => new LevelCaptureState());
            state.Forgotten = true;
            state.Generation++;
            LightsVersion++;
        }
        lock (LightEnvironments)
        {
            CachedWorldLightEnvironmentSettings.Remove(level);
        }
        //its lights' light function materials and failures, and the meshes that cast shadows from them, which would keep the level loaded
        LightAttenuations.ClearMaterials();
        ModulatedShadowsFailed = false;
        string keyPrefix = $"{level.FilePath}|";
        lock (LevelGeometryLock)
        {
            //it may hold the level's meshes and BSP. Rebuilt the next time it's needed
            LevelGeometry = null;
            LevelModels.Remove(level);
            MeshesWithoutLevelGeometry.RemoveWhere(key => key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase));
            foreach (string key in LevelGeometryMeshes.Keys.Where(key => key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                LevelGeometryMeshes.Remove(key);
            }
            PruneLevelGeometryMeshes();
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

    internal MeshStaticLighting[] GetStaticLightings()
    {
        lock (StaticLightings)
        {
            return [.. StaticLightings];
        }
    }

    internal void UnregisterStaticLighting(MeshStaticLighting staticLighting)
    {
        lock (StaticLightings)
        {
            StaticLightings.Remove(staticLighting);
        }
        if (staticLighting.GeometryMesh is not null)
        {
            //it no longer blocks visibility. (The old geometry keeps it alive until it's rebuilt, or the level is forgotten)
            InvalidateLevelGeometry();
        }
    }

    internal void AddPendingGameShaderLoad(MaterialRenderProxy material)
    {
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Add(material);
        }
    }

    internal void RemovePendingGameShaderLoad(MaterialRenderProxy material)
    {
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Remove(material);
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
    /// Whether <see cref="LoadPendingGameShaders"/> has anything to do for a lighting mode
    /// </summary>
    public bool NeedsGameShaderPreparation(ViewportLightingMode mode)
    {
        if (HasPendingGameShaderLoads) return true;
        if (mode is not (ViewportLightingMode.Level or ViewportLightingMode.Preview)) return false;
        lock (StaticLightings)
        {
            return StaticLightings.Any(staticLighting => !staticLighting.IsPrepared(mode == ViewportLightingMode.Preview));
        }
    }

    /// <summary>
    /// Loads the game shaders of every material created since the last call, and creates their D3D shaders, and prepares their shadow depth shaders.
    /// Then loads the light shaders that the level's lights need (see <see cref="MeshStaticLighting.GetLightInteractions"/>).
    /// Loading them can take seconds for a whole level, so call this on a background thread before turning on <see cref="UseGameShaders"/>,
    /// and after adding a level's lights. Otherwise shaders are loaded the first time they're needed.
    /// </summary>
    /// <param name="reportProgress">Called with the number of items loaded so far, and the total</param>
    /// <param name="lightingModeToPrepare">Load light shaders for this mode before switching to it. Defaults to the current mode.</param>
    /// <exception cref="AggregateException">Some materials' game shaders failed to load, one inner exception each. Everything else was still done,
    /// and those materials are retried when they're rendered, falling back to LEX's shader</exception>
    public void LoadPendingGameShaders(Action<int, int> reportProgress = null, ViewportLightingMode? lightingModeToPrepare = null)
    {
        MaterialRenderProxy[] materials;
        lock (PendingGameShaderLoads)
        {
            materials = [.. PendingGameShaderLoads];
            PendingGameShaderLoads.Clear();
        }
        try
        {
            MeshStaticLighting[] staticLightings = [];
            ViewportLightingMode mode = lightingModeToPrepare ?? LightingMode;
            if (mode is ViewportLightingMode.Level or ViewportLightingMode.Preview)
            {
                lock (StaticLightings)
                {
                    staticLightings = [.. StaticLightings];
                }
            }
            int total = materials.Length + staticLightings.Length;
            //one material failing doesn't stop the others, which were taken off the queue: its failure is reported after them
            List<Exception> failures = null;
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialRenderProxy material = materials[i];
                try
                {
                    material.LoadGameShaders();
                    if (Device is not null && material.CanRenderWithGameShaders)
                    {
                        GetCachedVertexShader(material.UnrealVertexShader.Guid, material.UnrealVertexShader.ShaderByteCode, material.VertexInputElements);
                        GetCachedPixelShader(material.UnrealPixelShader.Guid, material.UnrealPixelShader.ShaderByteCode);
                        if (material.NoLightMapVertexShader is not null)
                        {
                            GetCachedVertexShader(material.NoLightMapVertexShader.Guid, material.NoLightMapVertexShader.ShaderByteCode, material.VertexInputElements);
                            GetCachedPixelShader(material.NoLightMapPixelShader.Guid, material.NoLightMapPixelShader.ShaderByteCode);
                        }
                        if (material.PreviewVertexShader is not null)
                        {
                            GetCachedVertexShader(material.PreviewVertexShader.Guid, material.PreviewVertexShader.ShaderByteCode, material.VertexInputElements);
                            GetCachedPixelShader(material.PreviewPixelShader.Guid, material.PreviewPixelShader.ShaderByteCode);
                        }
                    }
                }
                catch (Exception e)
                {
                    //usually creating its D3D shaders failed, after its shadow shaders were prepared. (If loading them threw, they stay pending until they're tried again, when it's drawn)
                    (failures ??= []).Add(new Exception($"{material.InstancedFullPath}: {e.Message}", e));
                }
                reportProgress?.Invoke(i + 1, total);
            }
            for (int i = 0; i < staticLightings.Length; i++)
            {
                staticLightings[i].PrepareLightShaders(mode == ViewportLightingMode.Preview);
                reportProgress?.Invoke(materials.Length + i + 1, total);
            }
            if (failures is not null)
            {
                throw new AggregateException("Some materials couldn't load their game shaders", failures);
            }
        }
        finally
        {
            // The snapshot is no longer in the queue. Release every material even if progress reporting or light preparation throws.
            foreach (MaterialRenderProxy material in materials) material.Dequeued();
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
                    if (UseGameShaders) LEEffect.BeginFrame(this);
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
            MaximumAnisotropy = 8,
            MaximumLod = float.MaxValue // Allow sampling the full uploaded mip chain.
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
        using (ShaderBytecode terrainVSBytecode = ShaderBytecode.Compile(EmbeddedResources.LevelEditorShader, "VSMainTerrain", "vs_5_0").Bytecode)
        {
            TerrainVertexShader = new VertexShader(Device, terrainVSBytecode);
            TerrainInputLayout = new InputLayout(Device, terrainVSBytecode, TerrainVertex.InputElements);
        }
        using (ShaderBytecode terrainPSBytecode = ShaderBytecode.Compile(EmbeddedResources.LevelEditorShader, "PSMainTerrain", "ps_5_0").Bytecode)
        {
            TerrainPixelShader = new PixelShader(Device, terrainPSBytecode);
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
        //typeless, so that it can also be read as a texture
        DepthBuffer = CreateTarget(Format.R32_Typeless, BindFlags.DepthStencil | BindFlags.ShaderResource, SampleCount);
        DepthBufferView = new DepthStencilView(Device, DepthBuffer, new DepthStencilViewDescription
        {
            Format = Format.D32_Float,
            Dimension = SampleCount > 1 ? DepthStencilViewDimension.Texture2DMultisampled : DepthStencilViewDimension.Texture2D
        });
        DepthBufferResourceView = new ShaderResourceView(Device, DepthBuffer, new ShaderResourceViewDescription
        {
            Format = Format.R32_Float,
            Dimension = SampleCount > 1 ? SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DMultisampled : SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
            Texture2D = { MipLevels = 1, MostDetailedMip = 0 }
        });
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
        statsTextBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(1, 1, 1, 1), new D2D.BrushProperties { Opacity = 1 });
        statsShadowBrush = new D2D.SolidColorBrush(RenderTarget2D, new RawColor4(0, 0, 0, 1), new D2D.BrushProperties { Opacity = 1 });
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
        LightAttenuations.DisposeSizeDependentResources();
        DepthBufferResourceView?.Dispose();
        DepthBufferResourceView = null;
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
        statsShadowBrush?.Dispose();
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
        foreach (var state in decalRasterizers.Values) state.Dispose();
        decalRasterizers.Clear();
        DefaultEffect?.Dispose();
        LEVertexDefaultVertexShader?.Dispose();
        LEVertexDefaultInputLayout?.Dispose();
        TerrainVertexShader?.Dispose();
        TerrainPixelShader?.Dispose();
        TerrainInputLayout?.Dispose();
        foreach (var indices in RegularTerrainIndices.Values) indices.Buffer.Dispose();
        RegularTerrainIndices.Clear();
        ResolveVertexShader?.Dispose();
        ResolvePixelShader?.Dispose();
        LEEffect?.Dispose();
        LightAttenuations.Dispose();
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
        ShaderFlags flags = IsUnlit ? RenderFlags | ShaderFlags.Unlit : RenderFlags;
        return new WorldConstants(Matrix4x4.Transpose(Camera.ProjectionMatrix), Matrix4x4.Transpose(Camera.ViewMatrix), Matrix4x4.Transpose(localToWorld), flags, CurrentHitTestId);
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

    /// <summary>
    /// <see cref="LightAttenuationRenderer.DepthPixelShaderByteCode"/> for a shadow depth vertex shader, compiled once per shader.
    /// Failures are cached too, and rethrown until the shader caches are invalidated.
    /// </summary>
    internal byte[] GetDepthPixelShaderByteCode(LegendaryExplorerCore.Unreal.BinaryConverters.Shaders.Shader vertexShader)
    {
        return DepthPixelShaderByteCodes.GetOrAdd(vertexShader.Guid,
            _ => new Lazy<byte[]>(() => LightAttenuationRenderer.DepthPixelShaderByteCode(vertexShader.ShaderByteCode))).Value;
    }

    // Cache hits do not take a lock. Creation is serialized to avoid duplicate D3D resources
    // when the background shader loader and render thread request the same shader.
    /// <param name="inputElements">The vertex streams the shader reads. By default <see cref="LEVertex"/> and <see cref="MeshStaticLighting"/>'s streams,
    /// which FLocalVertexFactory shaders take. A shader is always given the layout it was first cached with</param>
    public (VertexShader, InputLayout) GetCachedVertexShader(Guid id, byte[] shaderBytecode, InputElement[] inputElements = null)
    {
        if (VertexShaderCache.TryGetValue(id, out var cached)) return cached;
        lock (VertexShaderCache)
        {
            if (!VertexShaderCache.TryGetValue(id, out cached))
            {
                var shader = new VertexShader(Device, shaderBytecode);
                InputLayout inputLayout;
                try
                {
                    inputLayout = new InputLayout(Device, shaderBytecode, inputElements ?? [.. LEVertex.InputElements, .. MeshStaticLighting.InputElements]);
                }
                catch
                {
                    shader.Dispose();
                    throw;
                }
                cached = (shader, inputLayout);
                VertexShaderCache[id] = cached;
            }
            return cached;
        }
    }

    public PixelShader GetCachedPixelShader(Guid id, byte[] shaderBytecode)
    {
        if (PixelShaderCache.TryGetValue(id, out PixelShader shader)) return shader;
        lock (PixelShaderCache)
        {
            if (!PixelShaderCache.TryGetValue(id, out shader))
            {
                //The game's bytecode is used as-is. Base pass shaders write 0 to alpha; materials mask that out with their blend state.
                shader = new PixelShader(Device, shaderBytecode);
                PixelShaderCache[id] = shader;
            }
            return shader;
        }
    }

    /// <summary>
    /// Renders a section of a game-shader mesh with LEX's default shader. Used for materials that can't be rendered with game shaders.
    /// </summary>
    /// <summary>The shared index buffer of a terrain section of this size with no holes or flipped quads</summary>
    internal (SharpDX.Direct3D11.Buffer Buffer, Format Format, int Count) GetRegularTerrainIndices(int trueSizeX, int trueSizeY)
    {
        if (!RegularTerrainIndices.TryGetValue((trueSizeX, trueSizeY), out var entry))
        {
            var indices = Lightmaps.Game.TerrainHeightField.RegularIndices(trueSizeX, trueSizeY);
            (SharpDX.Direct3D11.Buffer buffer, Format format) = TerrainSectionMesh.CreateIndexBuffer(Device, indices, (trueSizeX + 1) * (trueSizeY + 1));
            RegularTerrainIndices[(trueSizeX, trueSizeY)] = entry = (buffer, format, indices.Count);
        }
        return entry;
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

    /// <summary>Optional per-preview mip reader. Null results use the package's usual texture lookup.</summary>
    public Func<LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo, byte[]> TextureDataOverride { get; set; }

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

        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(
            LegendaryExplorerCore.Textures.Image.getPixelFormatType(faceTextures[0].Export.GetProperty<EnumProperty>("Format").Value.Name));
        // D3D requires equal faces, but cooked faces can differ (LE2's Bch_OCEAN_CUBEA has one 256px face).
        int size = faceTextures[0].Mips
            .Where(mip => mip.storageType != StorageTypes.empty && mip.width > 0 && mip.width == mip.height)
            .Select(mip => mip.width)
            .OrderByDescending(width => width)
            .FirstOrDefault(width => Enumerable.Range(0, 6).All(i => faceTextures[i].Mips
                .Any(mip => mip.storageType != StorageTypes.empty && mip.width == width && mip.height == width)));
        if (size == 0)
        {
            throw new InvalidDataException($"Cubemap {textureCubeExport.InstancedFullPath} has no square mip size shared by all six faces.");
        }
        for (int i = 0; i < 6; i++)
        {
            var faceFormat = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(
                LegendaryExplorerCore.Textures.Image.getPixelFormatType(faceTextures[i].Export.GetProperty<EnumProperty>("Format").Value.Name));
            if (faceFormat != format)
            {
                throw new InvalidDataException($"Cubemap {textureCubeExport.InstancedFullPath} has inconsistent face formats.");
            }
            var mip = faceTextures[i].Mips.First(mip => mip.storageType != StorageTypes.empty && mip.width == size && mip.height == size);
            pixelData[i] = this.ReadTextureData(mip);
        }
        var srgbFormats = IsSRGB(faceTextures[0].Export) ? RenderContextExtensions.GetSRGBFormats(format) : null;
        uint textureSize = (uint)(format.IsCompressed() ? Math.Max(4, size) : size);
        Texture2D texture = this.LoadTextureCube(textureSize, format, pixelData, srgbFormats?.Typeless);
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
