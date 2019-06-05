using System.Collections.Generic;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using SharpDX.Windows;
using System.Windows.Forms;
using System;

namespace ME3Explorer.Scene3D
{
    /// <summary>
    /// A robust control for rendering 3D graphics.
    /// </summary>
    public class SceneRenderControl : RenderControl
    {
        public SharpDX.Direct3D11.Device Device { get; private set; } = null;
        public SwapChain SwapChain { get; private set; } = null;
        public DeviceContext ImmediateContext { get; private set; } = null;
        public Texture2D BackBuffer { get; private set; } = null;
        public RenderTargetView BackBufferView { get; private set; } = null;
        public Texture2D DepthBuffer { get; private set; } = null; // also called Depth-Stencil, but we don't use stencil at the moment.
        public DepthStencilView DepthBufferView { get; private set; } = null;
        private SharpDX.WIC.ImagingFactory ImageFactory = new SharpDX.WIC.ImagingFactory();
        public Effect<WorldConstants, WorldVertex> DefaultEffect { get; private set; } = null;
        private Texture2D DefaultTexture = null;
        public ShaderResourceView DefaultTextureView { get; private set; } = null;
        private RasterizerState FillRasterizerState = null;
        private RasterizerState WireframeRasterizerState = null;
        public SamplerState SampleState { get; private set; } = null;
        public PreviewTextureCache TextureCache { get; private set; } = null;
        public SceneCamera Camera = new SceneCamera();
        private bool wireframe = false;
        public bool Wireframe
        {
            get
            {
                return wireframe;
            }
            set
            {
                wireframe = value;
                if (Device != null)
                {
                    ImmediateContext.Rasterizer.State = wireframe ? WireframeRasterizerState : FillRasterizerState;
                }
            }
        }
        public bool KeyW = false;
        public bool KeyS = false;
        public bool KeyA = false;
        public bool KeyD = false;
        public bool Orbiting { get; private set; } = false;
        public bool Panning { get; private set; } = false;
        public bool Zooming { get; private set; } = false;
        public float Time { get; private set; } = 0;
        public bool Ready
        {
            get
            {
                return Device != null;
            }
        }

        public SceneRenderControl()
        {
            Camera.FocusDepth = 100;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public void LoadDirect3D()
        {
            // Set up description of swap chain
            SwapChainDescription scd = new SwapChainDescription();
            scd.BufferCount = 1;
            scd.ModeDescription = new ModeDescription(Width, Height, new Rational(60, 1), Format.B8G8R8A8_UNorm);
            scd.Usage = Usage.RenderTargetOutput;
            scd.OutputHandle = Handle;
            scd.SampleDescription.Count = 1;
            scd.SampleDescription.Quality = 0;
            scd.IsWindowed = true;
            scd.ModeDescription.Width = Width;
            scd.ModeDescription.Height = Height;

            // Create device and swap chain according to the description above
            SharpDX.Direct3D11.Device d;
            SwapChain sc;
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, flags, scd, out d, out sc);
            this.SwapChain = sc; // we have to use these temp variables
            this.Device = d; // because properties can't be passed as out parameters. =(

            // Set up the rendering context and buffers and stuff
            ImmediateContext = Device.ImmediateContext;
            BuildBuffers();

            // Build a custom rasterizer state that doesn't cull backfaces
            RasterizerStateDescription frs = new RasterizerStateDescription();
            frs.CullMode = CullMode.None;
            frs.FillMode = FillMode.Solid;
            FillRasterizerState = new RasterizerState(Device, frs);
            ImmediateContext.Rasterizer.State = FillRasterizerState;
            // Build a custom rasterizer state for wireframe drawing
            RasterizerStateDescription wrs = new RasterizerStateDescription();
            wrs.CullMode = CullMode.None;
            wrs.FillMode = FillMode.Wireframe;
            wrs.IsAntialiasedLineEnabled = false;
            wrs.DepthBias = -10;
            WireframeRasterizerState = new RasterizerState(Device, wrs);

            // Set texture sampler state
            SamplerStateDescription ssd = new SamplerStateDescription();
            ssd.AddressU = TextureAddressMode.Wrap;
            ssd.AddressV = TextureAddressMode.Wrap;
            ssd.AddressW = TextureAddressMode.Wrap;
            ssd.Filter = Filter.MinMagMipLinear;
            ssd.MaximumAnisotropy = 1;
            SampleState = new SamplerState(Device, ssd);
            ImmediateContext.PixelShader.SetSampler(0, SampleState);

            // Load the default texture
            System.Drawing.Bitmap deftex = new System.Drawing.Bitmap(System.Environment.CurrentDirectory + "\\exec\\Default.bmp");
            DefaultTexture = LoadTexture(deftex);
            deftex.Dispose();
            DefaultTextureView = new ShaderResourceView(Device, DefaultTexture);

            // Load the default position-texture shader
            DefaultEffect = new Effect<WorldConstants, WorldVertex>(Device, Properties.Resources.StandardShader);

            TextureCache = new PreviewTextureCache(Device);
        }

        private void BuildBuffers()
        {
            BackBuffer = Texture2D.FromSwapChain<Texture2D>(SwapChain, 0);
            BackBufferView = new RenderTargetView(Device, BackBuffer);
            DepthBuffer = new Texture2D(Device, new Texture2DDescription() { ArraySize = 1, BindFlags = BindFlags.DepthStencil, CpuAccessFlags = CpuAccessFlags.None, Format = Format.D32_Float, Height = Height, Width = Width, MipLevels = 1, OptionFlags = ResourceOptionFlags.None, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Default });
            DepthBufferView = new DepthStencilView(Device, DepthBuffer);

            // Set the output-merger pipeline state to write to the created back buffer and depth buffer
            ImmediateContext.OutputMerger.SetTargets(DepthBufferView, BackBufferView);
            ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }

        private void DestroyBuffers()
        {
            ImmediateContext.OutputMerger.SetRenderTargets((RenderTargetView) null);
            BackBufferView.Dispose();
            BackBuffer.Dispose();
            DepthBufferView.Dispose();
            DepthBuffer.Dispose();
        }

        public void UpdateSize()
        {
            DestroyBuffers();
            SwapChain.ResizeBuffers(2, Width, Height, Format.Unknown, SwapChainFlags.None);
            BuildBuffers();
        }

        /*
        Private ImagingFactory As New WIC.ImagingFactory2()
        Private Function LoadTexture(t As Drawing.Bitmap) As ShaderResourceView
            Dim ms As New System.IO.MemoryStream()
            t.Save(ms, Drawing.Imaging.ImageFormat.Png)
            ms.Seek(0, System.IO.SeekOrigin.Begin)

            ' Load Texture2D
            Dim decoder As New SharpDX.WIC.BitmapDecoder(ImagingFactory, ms, WIC.DecodeOptions.CacheOnDemand)
            Dim converter As New WIC.FormatConverter(ImagingFactory)
            converter.Initialize(decoder.GetFrame(0), WIC.PixelFormat.Format32bppPRGBA, WIC.BitmapDitherType.None, Nothing, 0, WIC.BitmapPaletteType.Custom)
            Dim bitmap As WIC.BitmapSource = converter
            Dim stride As Integer = bitmap.Size.Width * 4
            Dim buffer As New SharpDX.DataStream(bitmap.Size.Height * stride, True, True)
            bitmap.CopyPixels(stride, buffer)
            Dim texture As New Texture2D(Device, New Texture2DDescription() With {.Width = bitmap.Size.Width, .Height = bitmap.Size.Height, .ArraySize = 1, .BindFlags = BindFlags.ShaderResource,
                                                                                  .Usage = ResourceUsage.Immutable, .CpuAccessFlags = CpuAccessFlags.None, .Format = Format.R8G8B8A8_UNorm,
                                                                                  .MipLevels = 1, .OptionFlags = ResourceOptionFlags.None, .SampleDescription = New DXGI.SampleDescription(1, 0)}, New DataRectangle(buffer.DataPointer, stride))
            buffer.Dispose()
            ms.Dispose()

            ' Create shaderresourceview
            Return New ShaderResourceView(Device, texture)
            'Return result
        End Function
        */
        public Texture2D LoadTexture(System.Drawing.Bitmap b)
        {
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            b.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Seek(0, System.IO.SeekOrigin.Begin);

            SharpDX.WIC.BitmapDecoder decoder = new SharpDX.WIC.BitmapDecoder(ImageFactory, ms, SharpDX.WIC.DecodeOptions.CacheOnDemand);
            SharpDX.WIC.FormatConverter converter = new SharpDX.WIC.FormatConverter(ImageFactory);
            converter.Initialize(decoder.GetFrame(0), SharpDX.WIC.PixelFormat.Format32bppPRGBA, SharpDX.WIC.BitmapDitherType.None, null, 0.0, SharpDX.WIC.BitmapPaletteType.Custom);
            SharpDX.WIC.BitmapSource bitmap = converter;
            int stride = bitmap.Size.Width * 4;
            DataStream buffer = new DataStream(bitmap.Size.Height * stride, true, true);
            bitmap.CopyPixels(stride, buffer);
            Texture2D texture = new Texture2D(Device, new Texture2DDescription() { Width = bitmap.Size.Width, Height = bitmap.Size.Height, ArraySize = 1, BindFlags = BindFlags.ShaderResource, Usage = ResourceUsage.Immutable, CpuAccessFlags = CpuAccessFlags.None, Format = Format.R8G8B8A8_UNorm, MipLevels = 1, OptionFlags = ResourceOptionFlags.None, SampleDescription = new SampleDescription(1, 0) }, new DataRectangle(buffer.DataPointer, stride));
            bitmap.Dispose();
            buffer.Dispose();
            ms.Dispose();
            return texture;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Ready)
                RenderScene();
        }

        public void UpdateScene()
        {
            float TimeStep = 0.1f;
            Time += TimeStep;
            OnUpdate(TimeStep);
            Update?.Invoke(null, TimeStep);
        }

        protected virtual void OnUpdate(float TimeStep)
        {
            if (Camera.FirstPerson)
            {
                if (KeyW)
                {
                    Camera.Position += Camera.CameraForward * TimeStep * 5;
                }
                if (KeyS)
                {
                    Camera.Position += -Camera.CameraForward * TimeStep * 5;
                }
                if (KeyA)
                {
                    Camera.Position += Camera.CameraLeft * TimeStep * 5;
                }
                if (KeyD)
                {
                    Camera.Position += -Camera.CameraLeft * TimeStep * 5;
                }
            }
        }

        public new event System.EventHandler<float> Update;

        public void RenderScene()
        {
            // Clear the color and depth buffers
            ImmediateContext.ClearDepthStencilView(DepthBufferView, DepthStencilClearFlags.Depth, 1.0f, 0);
            ImmediateContext.ClearRenderTargetView(BackBufferView, new Color(1.0f, 1.0f, 1.0f, 1.0f));

            // Do whatever a derived class wants
            OnRender();

            // Do whatever event handlers want
            Render?.Invoke(null, null);

            // Show the final image in the backbuffer by swapping it with the front buffer.
            SwapChain.Present(0, PresentFlags.None);
        }

        protected virtual void OnRender()
        {

        }

        public event System.EventHandler Render;

        public void UnloadDirect3D()
        {
            if (!Ready)
                return;

            TextureCache.Dispose();

            DefaultTextureView.Dispose();
            DefaultTexture.Dispose();
            SampleState.Dispose();
            DefaultEffect.Dispose();
            BackBufferView.Dispose();
            BackBuffer.Dispose();
            DepthBufferView.Dispose();
            DepthBuffer.Dispose();
            SwapChain.Dispose();
            Device.Dispose();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            switch (e.Button)
            {
                case MouseButtons.Left:
                    if (!Panning && !Zooming) Orbiting = true;
                    break;
                case MouseButtons.Middle:
                    if (!Orbiting && !Zooming) Panning = true;
                    break;
                case MouseButtons.Right:
                    if (!Orbiting && !Panning) Zooming = true;
                    break;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Orbiting = false;
            Panning = false;
            Zooming = false;
        }

        private System.Drawing.Point lastMouse;
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Orbiting)
            {
                Camera.Yaw += (e.Location.X - lastMouse.X) * -0.01f;
                Camera.Pitch = (float) Math.Min(Math.PI / 2, Math.Max(-Math.PI / 2, Camera.Pitch + (e.Location.Y - lastMouse.Y) * -0.01f));
            }
            if (Panning)
            {
                Camera.Position += Camera.CameraLeft * (e.Location.X - lastMouse.X) * Camera.FocusDepth * 0.004f;
                Camera.Position += Camera.CameraUp * (e.Location.Y - lastMouse.Y) * Camera.FocusDepth * 0.004f;
            }
            if (Zooming)
            {
                Camera.FocusDepth += (e.Location.Y - lastMouse.Y) * Camera.FocusDepth * 0.1f * 0.1f;
                if (Camera.FocusDepth < 0.1) Camera.FocusDepth = 0.1f;
                if (float.IsNaN(Camera.FocusDepth)) System.Diagnostics.Debugger.Break();
            }

            lastMouse = e.Location;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Camera.FocusDepth *= (float) Math.Pow(1.2, -Math.Sign(e.Delta)); // kinda hacky because this moves in constant increments regardless of how far the user scrolls.
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        public struct WorldConstants
        {
            Matrix Projection;
            Matrix View;
            Matrix Model;

            public WorldConstants(Matrix Projection, Matrix View, Matrix Model)
            {
                this.Projection = Projection;
                this.View = View;
                this.Model = Model;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Device != null) UpdateSize();
            Camera.aspect = (float) Width / Height;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.W:
                    KeyW = true;
                    break;
                case Keys.S:
                    KeyS = true;
                    break;
                case Keys.A:
                    KeyA = true;
                    break;
                case Keys.D:
                    KeyD = true;
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            switch (e.KeyCode)
            {
                case Keys.W:
                    KeyW = false;
                    break;
                case Keys.S:
                    KeyS = false;
                    break;
                case Keys.A:
                    KeyA = false;
                    break;
                case Keys.D:
                    KeyD = false;
                    break;
            }
        }
    }
}
