//#define FPS_OVERLAY
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Media;
using LegendaryExplorer.UserControls.ExportLoaderControls.TextureViewer;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using SharpDX.D3DCompiler;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.Mathematics.Interop;
using Texture2D = SharpDX.Direct3D11.Texture2D;
#if FPS_OVERLAY
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
#endif

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D;

/// <summary>
/// Handles rendering of mesh data
/// </summary>
public class MeshRenderContext : RenderContext
{
    /// <summary>
    /// The current flags for rendering textures. This renderer does not support 'SetAlphaAsBlack' or 'ReconstructZ'
    /// </summary>
    public TextureRenderContext.TextureViewFlags CurrentTextureViewFlags = TextureRenderContext.TextureViewFlags.EnableRedChannel | TextureRenderContext.TextureViewFlags.EnableGreenChannel | TextureRenderContext.TextureViewFlags.EnableBlueChannel | TextureRenderContext.TextureViewFlags.EnableAlphaChannel;

    public struct WorldConstants
    {
        public Matrix4x4 Projection;
        public Matrix4x4 View;
        public Matrix4x4 Model;
        public TextureRenderContext.TextureViewFlags Flags;
        public int Padding1;
        public int Padding2;
        public int Padding3; // Aligns on 16 byte boundary

        public WorldConstants(Matrix4x4 Projection, Matrix4x4 View, Matrix4x4 Model, TextureRenderContext.TextureViewFlags flags)
        {
            this.Projection = Projection;
            this.View = View;
            this.Model = Model;
            this.Flags = flags;
            Padding1 = Padding2 = Padding3 = 0;
        }
    }

    public Color BackgroundColor = Color.FromArgb(255,255,255,255); //Default

    #region Size-Dependent Resources
    public RenderTargetView BackbufferView { get; private set; }
    public Texture2D DepthBuffer { get; private set; } // also called Depth-Stencil, but we don't use stencil at the moment.
    public DepthStencilView DepthBufferView { get; private set; }

#if FPS_OVERLAY
    private D2D.RenderTarget renderTarget2D;
    private DW.TextFormat textFormat;
    private D2D.SolidColorBrush defaultForegroundBrush;
#endif
    #endregion
    public GenericEffect<WorldConstants, WorldVertex> DefaultEffect { get; private set; }
    public LEEffect LEEffect { get; private set; }
    private Texture2D DefaultTexture;
    private Texture2D WhiteTextureCube;
    private Texture2D WhiteTex;
    public ShaderResourceView DefaultTextureView { get; private set; }
    public ShaderResourceView WhiteTextureCubeView { get; private set; }
    public ShaderResourceView WhiteTexView { get; private set; }
    private RasterizerState FillRasterizerState;
    private RasterizerState WireframeRasterizerState;
    public SamplerState SampleState { get; private set; }
    public PreviewTextureCache TextureCache { get; private set; }
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
                ImmediateContext.Rasterizer.State = wireframe ? WireframeRasterizerState : FillRasterizerState;
            }
        }
    }
    public bool KeyW;
    public bool KeyS;
    public bool KeyA;
    public bool KeyD;
    public bool Orbiting { get; private set; }
    public bool Panning { get; private set; }
    public bool Zooming { get; private set; }
    public float CameraSpeed { get; set; } = 50.0f; // Units per second
    public float Time { get; private set; }
    public uint NumFrames { get; private set; }

    private float FPS;

    public event EventHandler<float> UpdateScene;
    public event EventHandler RenderScene;

    private readonly Dictionary<RenderTargetBlendDescription, BlendState> BlendStateCache = new(new BlendDescComparer());
    private readonly Dictionary<Guid, VertexShader> VertexShaderCache = [];
    private readonly Dictionary<Guid, InputLayout> InputLayoutCache = [];
    private readonly Dictionary<Guid, PixelShader> PixelShaderCache = [];

    public MeshRenderContext()
    {
        this.Camera.FocusDepth = 100.0f;
    }

    public override void Update(float timestep)
    {
        Time += timestep;
        FPS = 1f / timestep;

        if (Camera.FirstPerson)
        {
            if (KeyW)
            {
                Camera.Position += Camera.CameraForward * timestep * CameraSpeed;
            }
            if (KeyS)
            {
                Camera.Position += -Camera.CameraForward * timestep * CameraSpeed;
            }
            if (KeyA)
            {
                Camera.Position += Camera.CameraLeft * timestep * CameraSpeed;
            }
            if (KeyD)
            {
                Camera.Position += -Camera.CameraLeft * timestep * CameraSpeed;
            }
        }

        UpdateScene?.Invoke(null, timestep);
    }

    public override void Render()
    {
        NumFrames++;
        // Clear the color and depth buffers
        if (DepthBufferView != null && BackbufferView != null)
        {
            ImmediateContext.ClearDepthStencilView(DepthBufferView, DepthStencilClearFlags.Depth, 1.0f, 0);
            ImmediateContext.ClearRenderTargetView(BackbufferView, new RawColor4(BackgroundColor.R / 255.0f, BackgroundColor.G / 255.0f, BackgroundColor.B / 255.0f, BackgroundColor.A / 255.0f));

            // Do whatever event handlers want
            RenderScene?.Invoke(null, EventArgs.Empty);

#if FPS_OVERLAY
            //render D2D overlay
            renderTarget2D.BeginDraw();
            {
                var size = renderTarget2D.Size;
                renderTarget2D.DrawText($"{FPS} fps", textFormat, new RawRectangleF(0, 0, size.Width, size.Height), defaultForegroundBrush);
            }
            renderTarget2D.EndDraw();
#endif
        }

        base.Render();
    }

    public override void CreateResources()
    {
        TextureCache = new PreviewTextureCache(this);
        base.CreateResources();

        // Build a custom rasterizer state that doesn't cull backfaces
        var frs = new RasterizerStateDescription
        {
            CullMode = CullMode.None,
            FillMode = FillMode.Solid
        };
        FillRasterizerState = new RasterizerState(Device, frs);
        ImmediateContext.Rasterizer.State = FillRasterizerState;
        // Build a custom rasterizer state for wireframe drawing
        var wrs = new RasterizerStateDescription
        {
            CullMode = CullMode.None,
            FillMode = FillMode.Wireframe,
            IsAntialiasedLineEnabled = false,
            DepthBias = -10
        };
        WireframeRasterizerState = new RasterizerState(Device, wrs);

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
        //just set all the sample state slots.
        const int numSampleStates = 16;
        for (int i = 0; i < numSampleStates; i++)
        {
            ImmediateContext.PixelShader.SetSampler(i, SampleState);
        }

        // Load the default texture
        DefaultTexture = this.LoadFile(Path.Combine(AppDirectories.ExecFolder, "Default.png"));
        DefaultTextureView = new ShaderResourceView(Device, DefaultTexture);

        // Load the default position-texture shader
        DefaultEffect = new GenericEffect<WorldConstants, WorldVertex>(Device, EmbeddedResources.StandardShader);

        //create fallback textures
        var whiteCubeData = new Fixed6<byte[]>();
        whiteCubeData[0] = whiteCubeData[1] = whiteCubeData[2] = whiteCubeData[3] = whiteCubeData[4] = whiteCubeData[5] = [255, 255, 255, 255];
        WhiteTextureCube = this.LoadTextureCube(1, Format.R8G8B8A8_UNorm, whiteCubeData);
        WhiteTextureCubeView = new ShaderResourceView(Device, WhiteTextureCube);
        WhiteTex = new Texture2D(Device, new Texture2DDescription{ Width = 1, Height = 1, MipLevels = 1, ArraySize = 1, Format = Format.R8G8B8A8_UNorm, SampleDescription = new SampleDescription(1, 0), BindFlags = BindFlags.ShaderResource});
        int white = int.MaxValue;
        Device.ImmediateContext.UpdateSubresource(ref white, WhiteTex, rowPitch: 8);
        WhiteTexView = new ShaderResourceView(Device, WhiteTex);

        LEEffect = new LEEffect(Device);
    }

    public override void CreateSizeDependentResources(int width, int height, Texture2D newBackBuffer)
    {
        base.CreateSizeDependentResources(width, height, newBackBuffer);
        BackbufferView = new RenderTargetView(Device, Backbuffer);
        DepthBuffer = new Texture2D(Device, new Texture2DDescription
        {
            ArraySize = 1,
            BindFlags = BindFlags.DepthStencil,
            CpuAccessFlags = CpuAccessFlags.None,
            Format = Format.D32_Float,
            Height = Height,
            Width = Width,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default
        });
        DepthBufferView = new DepthStencilView(Device, DepthBuffer);

        // Set the output-merger pipeline state to write to the created back buffer and depth buffer
        ImmediateContext.OutputMerger.SetRenderTargets(DepthBufferView, BackbufferView);
        ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);
        ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

        Camera.aspect = (float)Width / Height;


#if FPS_OVERLAY
        using var factory = new D2D.Factory(D2D.FactoryType.SingleThreaded, App.IsDebug ? D2D.DebugLevel.Information : D2D.DebugLevel.None);
        renderTarget2D = new D2D.RenderTarget(factory, newBackBuffer.QueryInterface<Surface>(), new D2D.RenderTargetProperties(new D2D.PixelFormat(Format.Unknown, D2D.AlphaMode.Premultiplied)));
        defaultForegroundBrush = new D2D.SolidColorBrush(renderTarget2D, new RawColor4(0, 0, 0, 1), new D2D.BrushProperties { Opacity = 1 });
        using var dwFactory = new DW.Factory(DW.FactoryType.Shared);
        textFormat = new DW.TextFormat(dwFactory, "Verdana", 12);
        textFormat.TextAlignment = DW.TextAlignment.Trailing;
        textFormat.ParagraphAlignment = DW.ParagraphAlignment.Near;
#endif
    }

    public override void DisposeSizeDependentResources()
    {
        ImmediateContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
        BackbufferView.Dispose();
        DepthBufferView.Dispose();
        DepthBuffer.Dispose();
#if FPS_OVERLAY
        renderTarget2D.Dispose();
        textFormat.Dispose();
        defaultForegroundBrush.Dispose();
#endif
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
        DefaultEffect?.Dispose();
        LEEffect?.Dispose();
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

    public (VertexShader, InputLayout) GetCachedVertexShader(Guid id, byte[] shaderBytecode)
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
            inputLayout = new InputLayout(Device, shaderBytecode, LEVertex.InputElements);
            InputLayoutCache.Add(id, inputLayout);
        }
        return (shader, inputLayout);
    }

    public PixelShader GetCachedPixelShader(Guid id, byte[] shaderBytecode)
    {
        if (!PixelShaderCache.TryGetValue(id, out PixelShader shader))
        {
            string code = HLSLDecompiler.DecompileShader(shaderBytecode, false);
            //HACK: LE shaders seem to always output pixels with no alpha (Maybe it's inverted? Investigate transparent mats) 
            code = code.Replace("o0.w = 0;", "o0.w = 1;", StringComparison.Ordinal);
            //3DMigoto outputs "inf" for the infinity constant, but that's not valid HLSL
            code = code.Replace("// 3Dmigoto declarations", "// 3Dmigoto declarations\n" +
                                                            "#define inf 1.#INF");
            try
            {
                shaderBytecode = ShaderBytecode.Compile(code, "main", "ps_5_0");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

            shader = new PixelShader(Device, shaderBytecode);
            PixelShaderCache.Add(id, shader);
        }
        return shader;
    }

    public override void EmptyCaches()
    {
        TextureCache?.ExpungeStaleCacheItems();
        BlendStateCache.DisposeValuesAndClear();
        VertexShaderCache.DisposeValuesAndClear();
        InputLayoutCache.DisposeValuesAndClear();
        PixelShaderCache.DisposeValuesAndClear();
    }

    public override bool MouseDown(MouseButtons button, int x, int y)
    {
        switch (button)
        {
            case MouseButtons.Left:
                if (!Panning && !Zooming)
                {
                    Orbiting = true;
                    return true;
                }
                break;
            case MouseButtons.Middle:
                if (!Orbiting && !Zooming)
                {
                    Panning = true;
                    return true;
                }
                break;
            case MouseButtons.Right:
                if (!Orbiting && !Panning)
                {
                    Zooming = true;
                    return true;
                }
                break;
        }
        return false;
    }

    public override bool MouseUp(MouseButtons button, int x, int y)
    {
        bool handled = Orbiting | Panning | Zooming;

        Orbiting = false;
        Panning = false;
        Zooming = false;

        return handled;
    }

    private System.Drawing.Point lastMouse;
    public override bool MouseMove(int x, int y)
    {
        bool handled = false;
        if (Orbiting)
        {
            Camera.Yaw += (x - lastMouse.X) * -0.01f;
            Camera.Pitch = MathF.Min(MathF.PI / 2, MathF.Max(-MathF.PI / 2, Camera.Pitch + (y - lastMouse.Y) * -0.01f));
            handled = true;
        }
        if (Panning)
        {
            Camera.Position += Camera.CameraLeft * (x - lastMouse.X) * Camera.FocusDepth * 0.004f;
            Camera.Position += Camera.CameraUp * (y - lastMouse.Y) * Camera.FocusDepth * 0.004f;
            handled = true;
        }
        if (Zooming)
        {
            Camera.FocusDepth += (y - lastMouse.Y) * Camera.FocusDepth * 0.1f * 0.1f;
            if (Camera.FocusDepth < 0.1) Camera.FocusDepth = 0.1f;
            handled = true;
        }
        lastMouse = new System.Drawing.Point(x, y);
        return handled;
    }

    public override bool MouseScroll(int delta)
    {
        Camera.FocusDepth *= MathF.Pow(1.2f, -Math.Sign(delta)); // kinda hacky because this moves in constant increments regardless of how far the user scrolls.
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
                KeyW = true;
                return true;
            case Key.S:
                KeyS = true;
                return true;
            case Key.A:
                KeyA = true;
                return true;
            case Key.D:
                KeyD = true;
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
                KeyW = false;
                return true;
            case Key.S:
                KeyS = false;
                return true;
            case Key.A:
                KeyA = false;
                return true;
            case Key.D:
                KeyD = false;
                return true;
            default:
                return false;
        }
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