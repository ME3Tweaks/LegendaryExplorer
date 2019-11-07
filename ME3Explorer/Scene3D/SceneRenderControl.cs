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
    /// Hosts a <see cref="SceneRenderContext"/> in a Windows Forms control.
    /// </summary>
    public class SceneRenderControl : RenderControl
    {
        public SceneRenderContext Context { get; }
        public SwapChain SwapChain { get; private set; } = null;

        public SceneRenderControl()
        {
            Context = new SceneRenderContext();
            Context.Camera.FocusDepth = 100;
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

            Context.LoadDirect3D(d);
            UpdateSize();
        }

        public void DestroyDirect3D()
        {
            Context.UnloadDirect3D();
            SwapChain.Dispose();
        }

        /*private void BuildBuffers()
        {
            BackBuffer = Texture2D.FromSwapChain<Texture2D>(SwapChain, 0);
            BackBufferView = new RenderTargetView(Device, BackBuffer);
            DepthBuffer = new Texture2D(Device, new Texture2DDescription() { ArraySize = 1, BindFlags = BindFlags.DepthStencil, CpuAccessFlags = CpuAccessFlags.None, Format = Format.D32_Float, Height = Height, Width = Width, MipLevels = 1, OptionFlags = ResourceOptionFlags.None, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Default });
            DepthBufferView = new DepthStencilView(Device, DepthBuffer);

            // Set the output-merger pipeline state to write to the created back buffer and depth buffer
            ImmediateContext.OutputMerger.SetTargets(DepthBufferView, BackBufferView);
            ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);
            ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        }*/

        /*private void DestroyBuffers()
        {
            ImmediateContext.OutputMerger.SetRenderTargets((RenderTargetView) null);
            BackBufferView.Dispose();
            BackBuffer.Dispose();
            DepthBufferView.Dispose();
            DepthBuffer.Dispose();
        }*/

        public void UpdateSize()
        {
            //DestroyBuffers();
            //SwapChain.ResizeBuffers(2, Width, Height, Format.Unknown, SwapChainFlags.None);
            //BuildBuffers();
            Context.UpdateSize(Width, Height, (int width, int height) => {
                SwapChain.ResizeBuffers(2, width, height, Format.Unknown, SwapChainFlags.None);
                return Texture2D.FromSwapChain<Texture2D>(SwapChain, 0);
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Context.Ready)
                RenderScene();
        }

        public event System.EventHandler Render;

        public void RenderScene()
        {
            // Do whatever a derived class wants
            OnRender();

            // Do whatever event handlers want
            Render?.Invoke(null, null);

            // Show the final image in the backbuffer by swapping it with the front buffer.
            SwapChain.Present(0, PresentFlags.None);
        }

        protected virtual void OnRender()
        {
            Context.RenderScene();
        }

        public new event System.EventHandler<float> Update;

        public void UpdateScene(float dt)
        {
            OnUpdate(dt);

            Update?.Invoke(this, dt);
        }

        protected virtual void OnUpdate(float dt)
        {
            Context.UpdateScene(dt);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            SceneRenderContext.MouseButtons buttons;
            if (e.Button == MouseButtons.Left)
                buttons = SceneRenderContext.MouseButtons.Left;
            else if (e.Button == MouseButtons.Middle)
                buttons = SceneRenderContext.MouseButtons.Middle;
            else if (e.Button == MouseButtons.Right)
                buttons = SceneRenderContext.MouseButtons.Right;
            else
                return;
            Context.MouseDown(buttons, e.X, e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            SceneRenderContext.MouseButtons buttons;
            if (e.Button == MouseButtons.Left)
                buttons = SceneRenderContext.MouseButtons.Left;
            else if (e.Button == MouseButtons.Middle)
                buttons = SceneRenderContext.MouseButtons.Middle;
            else if (e.Button == MouseButtons.Right)
                buttons = SceneRenderContext.MouseButtons.Right;
            else
                return;
            Context.MouseUp(buttons, e.X, e.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Context.MouseMove(e.X, e.Y);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Context.MouseScroll(e.Delta);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }


        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Context.Device != null) UpdateSize();
            Context.Camera.aspect = (float) Width / Height;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Context.KeyDown(e.KeyCode);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            Context.KeyUp(e.KeyCode);
        }
    }
}
