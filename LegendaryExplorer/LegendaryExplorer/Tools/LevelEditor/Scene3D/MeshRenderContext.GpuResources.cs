using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
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
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public partial class MeshRenderContext
{
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
    //ResolveConstants in LevelEditorShader.hlsl
    private SharpDX.Direct3D11.Buffer ResolveConstantBuffer;
    private const int RESOLVE_CONSTANT_BUFFER_SLOT = 4;
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
            decalRasterizers[key] = state = CreateRasterizerState(cull, fill, bias, -decal.SlopeBias);
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

    /// <summary>
    /// Sets every pixel shader sampler slot to <see cref="SampleState"/>, which LEX's shaders expect
    /// </summary>
    public void RestoreDefaultSamplers() => LEEffect.PixelShaderResources.ApplySamplers(ImmediateContext.PixelShader, SampleState);

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

    private readonly ConcurrentDictionary<Guid, (VertexShader Shader, InputLayout InputLayout)> VertexShaderCache = new();
    private readonly ConcurrentDictionary<Guid, PixelShader> PixelShaderCache = new();
    //compiled for shadow depth vertex shaders, which material instances without a static permutation share with their parent. A null value is one that can't be matched
    private readonly ConcurrentDictionary<Guid, Lazy<byte[]>> DepthPixelShaderByteCodes = new();

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

    private RasterizerState CreateRasterizerState(CullMode cullMode, FillMode fillMode = FillMode.Solid, int depthBias = 0, float slopeScaledDepthBias = 0)
    {
        return new RasterizerState(Device, new RasterizerStateDescription
        {
            CullMode = cullMode,
            FillMode = fillMode,
            //matches UE3's D3D10/11 RHI, whose CM_CW cull mode is D3D11_CULL_BACK
            IsFrontCounterClockwise = true,
            //(a new description leaves it off, and unclipped geometry in front of the near plane is clamped to the nearest depth and drawn over
            //everything: the top view's ceilings above its clip height)
            IsDepthClipEnabled = true,
            IsMultisampleEnabled = SampleCount > 1,
            IsAntialiasedLineEnabled = false,
            DepthBias = depthBias,
            SlopeScaledDepthBias = slopeScaledDepthBias
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
        //(constant buffers are a multiple of 16 bytes)
        ResolveConstantBuffer = new SharpDX.Direct3D11.Buffer(Device, 16, ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        //create fallback textures
        WhiteTextureCube = CreateWhiteTextureCube();
        WhiteTextureCubeView = new ShaderResourceView(Device, WhiteTextureCube);
        WhiteTex = CreateWhiteTexture();
        WhiteTexView = new ShaderResourceView(Device, WhiteTex);

        LEEffect = new LEEffect(Device, EmbeddedResources.LevelEditorShader);
        DefaultEffect.BeforeDraw = LEEffect.PixelShaderResources.Reset;
        RestoreDefaultSamplers();
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
        ResolveConstantBuffer?.Dispose();
        LEEffect?.Dispose();
        LightAttenuations.Dispose();
        FillRasterizerState?.Dispose();
        WireframeRasterizerState?.Dispose();
        EmptyCaches();
        base.DisposeResources();
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
