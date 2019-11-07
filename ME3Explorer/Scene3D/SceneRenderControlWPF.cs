using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Controls;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using System.Windows;

namespace ME3Explorer.Scene3D
{
    /// <summary>
    /// Hosts a <see cref="SceneRenderContext"/> in a WPF control.
    /// </summary>
    public class SceneRenderControlWPF : System.Windows.Controls.ContentControl, IDisposable
    {
        private Microsoft.Wpf.Interop.DirectX.D3D11Image D3DImage = null;
        private Image Image = null;
        private bool initialized;

        public SceneRenderContext Context { get; } = new SceneRenderContext();

        public int RenderWidth => (int)RenderSize.Width;
        public int RenderHeight => (int)RenderSize.Height;

        public SceneRenderControlWPF()
        {
            InitializeComponent();
            Context.Camera.FocusDepth = 100.0f;
        }

        private void InitializeComponent()
        {
            D3DImage = new Microsoft.Wpf.Interop.DirectX.D3D11Image();
            
            D3DImage.OnRender = this.D3DImage_OnRender;
            Image = new Image();
            Image.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            Image.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            Image.Source = D3DImage;
            this.AddChild(Image);

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            this.SizeChanged += SceneRenderControlWPF_SizeChanged;
            this.PreviewMouseDown += SceneRenderControlWPF_PreviewMouseDown;
            this.PreviewMouseMove += SceneRenderControlWPF_PreviewMouseMove;
            this.PreviewMouseUp += SceneRenderControlWPF_PreviewMouseUp;
            this.PreviewMouseWheel += SceneRenderControlWPF_PreviewMouseWheel;
            // TODO: Hook up keyboard events
            // TODO: Hook up some sort of dispose event
        }

        /// <summary>
        /// Call this method at once, once the window hosting this control has been fully loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void InitializeD3D()
        {
            if (initialized) return;
            initialized = true;
            D3DImage.WindowOwner = (new System.Windows.Interop.WindowInteropHelper(System.Windows.Window.GetWindow(this))).Handle;
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            Context.LoadDirect3D(new SharpDX.Direct3D11.Device(DriverType.Hardware, flags));
        }

        private void SceneRenderControlWPF_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            Context.MouseScroll(e.Delta);
        }

        private void SceneRenderControlWPF_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SceneRenderContext.MouseButtons buttons;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                buttons = SceneRenderContext.MouseButtons.Left;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
                buttons = SceneRenderContext.MouseButtons.Middle;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
                buttons = SceneRenderContext.MouseButtons.Right;
            else
                return;
            System.Windows.Point position = e.GetPosition(this);
            Context.MouseUp(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);
            Context.MouseMove((int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SceneRenderContext.MouseButtons buttons;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                buttons = SceneRenderContext.MouseButtons.Left;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
                buttons = SceneRenderContext.MouseButtons.Middle;
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
                buttons = SceneRenderContext.MouseButtons.Right;
            else
                return;
            System.Windows.Point position = e.GetPosition(this);
            Context.MouseDown(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            D3DImage.SetPixelSize(RenderWidth, RenderHeight);
            Context.Camera.aspect = (float)RenderWidth / RenderHeight;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (Context.Ready)
            {
                D3DImage.RequestRender();
            }
        }

        public event EventHandler Render;

        private void D3DImage_OnRender(IntPtr surface, bool isNewSurface)
        {
            if (isNewSurface)
            {
                Context.DestroyBuffers();

                // Yikes - from https://github.com/microsoft/WPFDXInterop/blob/master/samples/D3D11Image/D3D11Visualization/D3DVisualization.cpp#L384
                ComObject res = ComObject.FromPointer<ComObject>(surface);
                SharpDX.DXGI.Resource resource = res.QueryInterface<SharpDX.DXGI.Resource>();
                IntPtr sharedHandle = resource.SharedHandle;
                resource.Dispose();
                SharpDX.Direct3D11.Resource d3dres = Context.Device.OpenSharedResource<SharpDX.Direct3D11.Resource>(sharedHandle);
                Context.UpdateSize(RenderWidth, RenderHeight, (int width, int height) => d3dres.QueryInterface<Texture2D>());
                d3dres.Dispose();

            }
            Context.UpdateScene(0.1f); // TODO: Measure elapsed time!
            Context.RenderScene();
            Render?.Invoke(this, new EventArgs());
            Context.ImmediateContext.Flush();
        }

        public void Dispose()
        {

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            Context.TextureCache?.Dispose();
            this.SizeChanged -= SceneRenderControlWPF_SizeChanged;
            this.PreviewMouseDown -= SceneRenderControlWPF_PreviewMouseDown;
            this.PreviewMouseMove -= SceneRenderControlWPF_PreviewMouseMove;
            this.PreviewMouseUp -= SceneRenderControlWPF_PreviewMouseUp;
            this.PreviewMouseWheel -= SceneRenderControlWPF_PreviewMouseWheel;
        }
    }
}
