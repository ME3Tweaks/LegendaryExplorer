using System;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    public enum MouseButtons
    {
        Left,
        Middle,
        Right,
    }

    public static class RenderContextExtensions
    {
        public unsafe static Texture2D LoadTexture(this RenderContext renderContext, uint width, uint height, SharpDX.DXGI.Format format, byte[] pixelData)
        {
            fixed (byte* pixelDataPointer = pixelData)
            {
                return new Texture2D(renderContext.Device, new Texture2DDescription { Width = (int)width, Height = (int)height, ArraySize = 1, BindFlags = BindFlags.ShaderResource, Usage = ResourceUsage.Immutable, CpuAccessFlags = CpuAccessFlags.None, Format = format, MipLevels = 1, OptionFlags = ResourceOptionFlags.None, SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0) }, new DataRectangle((IntPtr)pixelDataPointer, (int)(width * SharpDX.DXGI.FormatHelper.SizeOfInBits(format) / 8)));
            }
        }

        public unsafe static Texture2D LoadFile(this RenderContext renderContext, string filename)
        {
            LegendaryExplorerCore.Textures.PixelFormat pixelFormat = LegendaryExplorerCore.Textures.PixelFormat.ARGB;
            byte[] pixelData = LegendaryExplorerCore.Textures.TexConverter.LoadTexture(filename, out uint width, out uint height, ref pixelFormat);
            SharpDX.DXGI.Format format = (SharpDX.DXGI.Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
            return renderContext.LoadTexture(width, height, format, pixelData);
        }
    }

    public abstract class RenderContext
    {
        public int Width { get; private set; } = 0;
        public int Height { get; private set; } = 0;
        public Device Device { get; private set; }
        public DeviceContext ImmediateContext { get; private set; }
        public Texture2D Backbuffer { get; private set; }
        public bool IsReady => Device != null;

        public virtual void CreateResources()
        {
            DeviceCreationFlags deviceFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded;
#if DEBUG
            deviceFlags |= DeviceCreationFlags.Debug;
#endif
            this.Device = new Device(DriverType.Hardware, deviceFlags);
            this.ImmediateContext = this.Device.ImmediateContext;
        }

        public virtual void CreateSizeDependentResources(int width, int height, Texture2D newBackbuffer)
        {
            this.Width = width;
            this.Height = height;
            this.Backbuffer = newBackbuffer;
        }

        public virtual void DisposeSizeDependentResources()
        {
            this.Backbuffer.Dispose();
            this.Backbuffer = null;
        }

        public virtual void DisposeResources()
        {
            this.ImmediateContext.Dispose();
            this.ImmediateContext = null;

#if DEBUG
            DeviceDebug debug = this.Device.QueryInterface<DeviceDebug>();
            debug.ReportLiveDeviceObjects(ReportingLevel.Detail);
            debug.Dispose();
#endif

            this.Device.Dispose();
            this.Device = null;
        }

        public abstract void Update(float timestep);
        public virtual void Render()
        {
            ImmediateContext.Flush();
        }

        public virtual bool MouseDown(MouseButtons button, int x, int y)
        {
            return false;
        }

        public virtual bool MouseUp(MouseButtons button, int x, int y)
        {
            return false;
        }

        public virtual bool MouseMove(int x, int y)
        {
            return false;
        }

        public virtual bool MouseScroll(int delta)
        {
            return false;
        }

        public virtual bool KeyDown(Key key)
        {
            return false;
        }

        public virtual bool KeyUp(Key key)
        {
            return false;
        }
    }

    /// <summary>
    /// Hosts a <see cref="RenderContext"/> in a WPF control.
    /// </summary>
    public class SceneRenderControl : System.Windows.Controls.ContentControl
    {
        private Microsoft.Wpf.Interop.DirectX.D3D11Image D3DImage = null;
        private Image Image;
        private System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();

        public RenderContext Context { get; set; }

        public int RenderWidth => (int)RenderSize.Width;
        public int RenderHeight => (int)RenderSize.Height;

        public SceneRenderControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            D3DImage = new Microsoft.Wpf.Interop.DirectX.D3D11Image
            {
                OnRender = this.D3DImage_OnRender
            };
            Image = new Image
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Source = D3DImage
            };
            this.AddChild(Image);

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            this.SizeChanged += SceneRenderControlWPF_SizeChanged;
            this.PreviewMouseDown += SceneRenderControlWPF_PreviewMouseDown;
            this.PreviewMouseMove += SceneRenderControlWPF_PreviewMouseMove;
            this.PreviewMouseUp += SceneRenderControlWPF_PreviewMouseUp;
            this.PreviewMouseWheel += SceneRenderControlWPF_PreviewMouseWheel;
            this.Unloaded += SceneRenderControlWPF_Unloaded;
            this.KeyDown += OnKeyDown;
            this.KeyUp += OnKeyUp;
        }

        private void SceneRenderControlWPF_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            this.SizeChanged -= SceneRenderControlWPF_SizeChanged;
            this.PreviewMouseDown -= SceneRenderControlWPF_PreviewMouseDown;
            this.PreviewMouseMove -= SceneRenderControlWPF_PreviewMouseMove;
            this.PreviewMouseUp -= SceneRenderControlWPF_PreviewMouseUp;
            this.PreviewMouseWheel -= SceneRenderControlWPF_PreviewMouseWheel;
            this.Unloaded -= SceneRenderControlWPF_Unloaded;
            this.KeyUp -= OnKeyUp;
            this.KeyDown -= OnKeyDown;
            this.Context.DisposeSizeDependentResources();
            this.Context.DisposeResources();
        }

        /// <summary>
        /// Call this method at once, once the window hosting this control has been fully loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void InitializeD3D()
        {
            D3DImage.WindowOwner = (new System.Windows.Interop.WindowInteropHelper(System.Windows.Window.GetWindow(this))).Handle;
            Context.CreateResources();
        }

        private void SceneRenderControlWPF_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            D3DImage.SetPixelSize(RenderWidth, RenderHeight);
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (Context.IsReady)
            {
                D3DImage.RequestRender();
            }
        }

        DateTime LastUpdatedTime = DateTime.Now;
        private void D3DImage_OnRender(IntPtr surface, bool isNewSurface)
        {
            if (isNewSurface)
            {
                if (Context.Backbuffer != null)
                {
                    Context.DisposeSizeDependentResources();
                }

                // Yikes - from https://github.com/microsoft/WPFDXInterop/blob/master/samples/D3D11Image/D3D11Visualization/D3DVisualization.cpp#L384
                ComObject res = CppObject.FromPointer<ComObject>(surface);
                SharpDX.DXGI.Resource resource = res.QueryInterface<SharpDX.DXGI.Resource>();
                IntPtr sharedHandle = resource.SharedHandle;
                resource.Dispose();
                SharpDX.Direct3D11.Resource d3dres = Context.Device.OpenSharedResource<SharpDX.Direct3D11.Resource>(sharedHandle);
                Context.CreateSizeDependentResources(RenderWidth, RenderHeight, d3dres.QueryInterface<Texture2D>());
                d3dres.Dispose();

            }
            Context.Update((float)Stopwatch.Elapsed.TotalSeconds);
            Stopwatch.Restart();
            LastUpdatedTime = DateTime.Now;
            Context.Render();
        }

        private bool _shouldRender = true;

        public void SetShouldRender(bool shouldRender)
        {
            if (!_shouldRender && shouldRender) // Not rendering, but we should start
            {
                D3DImage.OnRender = D3DImage_OnRender;
            }
            else if (_shouldRender && !shouldRender) // Currently rendering, but we should stop
            {
                D3DImage.OnRender = null;
            }

            _shouldRender = shouldRender;
        }

        #region Input Events
        private void SceneRenderControlWPF_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MouseButtons buttons;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                buttons = MouseButtons.Left;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
                buttons = MouseButtons.Middle;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
                buttons = MouseButtons.Right;
            else
                return;
            System.Windows.Point position = e.GetPosition(this);
            e.Handled = Context.MouseDown(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MouseButtons buttons;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                buttons = MouseButtons.Left;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
                buttons = MouseButtons.Middle;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
                buttons = MouseButtons.Right;
            else
                return;
            System.Windows.Point position = e.GetPosition(this);
            e.Handled = Context.MouseUp(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);
            e.Handled = Context.MouseMove((int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = Context.MouseScroll(e.Delta);
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = Context.KeyDown(e.Key);
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = Context.KeyUp(e.Key);
        }
        #endregion
    }
}
