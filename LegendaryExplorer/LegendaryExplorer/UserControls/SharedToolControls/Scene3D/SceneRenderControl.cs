using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using FontAwesome5;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Point = System.Windows.Point;
using Resource = SharpDX.Direct3D11.Resource;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using Texture2D = SharpDX.Direct3D11.Texture2D;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

    public enum MouseButtons
    {
        Left,
        Middle,
        Right,
    }

    public static class RenderContextExtensions
    {
        public static unsafe Texture2D LoadTexture(this RenderContext renderContext, uint width, uint height, Format format, byte[] pixelData)
        {
            Texture2DDescription texture2DDescription = GetTextureDescription(width, height, format, false, out int pitch);
            fixed (byte* pixelDataPointer = pixelData)
            {
                return new Texture2D(renderContext.Device, texture2DDescription, new DataRectangle((IntPtr)pixelDataPointer, pitch));
            }
        }

        private static Texture2DDescription GetTextureDescription(uint width, uint height, Format format, bool isCube, out int pitch)
        {
            pitch = (int)(format.SizeOfInBits() * width / 8);
            if (format.IsCompressed())
            {
                // Pitch calculation for compressed formats from https://docs.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide
                int blockSize = 16;
                if (format is Format.BC1_UNorm or Format.BC1_UNorm_SRgb or Format.BC4_SNorm or Format.BC4_UNorm)
                {
                    blockSize = 8;
                }
                pitch = (int)(Math.Max(1, ((width + 3) / 4)) * blockSize);
            }

            var texture2DDescription = new Texture2DDescription
            {
                Width = (int)width,
                Height = (int)height,
                ArraySize = isCube ? 6 : 1,
                BindFlags = BindFlags.ShaderResource,
                Usage = isCube ? ResourceUsage.Default : ResourceUsage.Immutable,

                CpuAccessFlags = CpuAccessFlags.None,
                Format = format,
                MipLevels = 1,
                OptionFlags = isCube ? ResourceOptionFlags.TextureCube : ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0),
            };
            return texture2DDescription;
        }

        public static Texture2D LoadTextureCube(this RenderContext renderContext, uint size, Format format, Fixed6<byte[]> faceData)
        {
            Texture2DDescription texture2DDescription = GetTextureDescription(size, size, format, true, out int pitch);
            var tex = new Texture2D(renderContext.Device, texture2DDescription);

            for (int i = 0; i < faceData.Length; i++)
            {
                renderContext.Device.ImmediateContext.UpdateSubresource(faceData[i], tex, i, pitch);
            }

            return tex;
        }

        public static unsafe Texture2D LoadFile(this RenderContext renderContext, string filename)
        {
            var pixelFormat = LegendaryExplorerCore.Textures.PixelFormat.ARGB;
            byte[] pixelData = LegendaryExplorerCore.Textures.TexConverter.LoadTexture(filename, out uint width, out uint height, ref pixelFormat); // NEEDS WAY TO HAVE ALPHA AS BLACK!
            Format format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
            return renderContext.LoadTexture(width, height, format, pixelData);
        }

        public static Texture2D LoadUnrealMip(this RenderContext renderContext, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip, LegendaryExplorerCore.Textures.PixelFormat pixelFormat)
        {
            // Todo: Needs way to set black alpha
            var imagebytes = LECTexture2D.GetTextureData(mip, mip.Export.Game);
            uint mipWidth = (uint)mip.width;
            uint mipHeight = (uint)mip.height;
            var mipFormat = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
            if (mipFormat.IsCompressed())
            {
                mipWidth = (mipWidth < 4) ? 4 : mipWidth;
                mipHeight = (mipHeight < 4) ? 4 : mipHeight;
            }
            return renderContext.LoadTexture(mipWidth, mipHeight, mipFormat, imagebytes);
        }

        public static Texture2D LoadUnrealTexture(this RenderContext renderContext, ExportEntry texture2DExport)
        {
            var unrealTexture = new LECTexture2D(texture2DExport);
            return renderContext.LoadUnrealMip(unrealTexture.GetTopMip(), LegendaryExplorerCore.Textures.Image.getPixelFormatType(unrealTexture.Export.GetProperties().GetProp<EnumProperty>("Format").Value.Name));
        }

        public static Texture2D LoadUnrealTextureCube(this RenderContext renderContext, ExportEntry textureCubeExport, PackageCache packageCache = null)
        {
            if (textureCubeExport.ClassName != "TextureCube") throw new ArgumentException("Expected a TextureCube export.", nameof(textureCubeExport));

            var props = textureCubeExport.GetProperties();
            var faceTextures = new Fixed6<LECTexture2D>();
            Span<string> facePropNames = ["FacePosX", "FaceNegX", "FacePosY", "FaceNegY", "FacePosZ", "FaceNegZ"];
            for (int i = 0; i < 6; i++)
            {
                faceTextures[i] = new(props.GetProp<ObjectProperty>(facePropNames[i]).ResolveToExport(textureCubeExport.FileRef, packageCache));
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
            return renderContext.LoadTextureCube(size, format, pixelData);
        }
    }

    public abstract class RenderContext
    {
        public int Width { get; private set; } = 0;
        public int Height { get; private set; } = 0;
        public Device Device { get; private set; }
        public DeviceContext ImmediateContext { get; private set; }
        public Texture2D Backbuffer { get; private set; }
        public BlendState AlphaBlendState { get; private set; } // A BlendState that uses standard alpha blending
        public bool IsReady => Device != null;

        public virtual void CreateResources()
        {
            DeviceCreationFlags deviceFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded;
#if DEBUG
            deviceFlags |= DeviceCreationFlags.Debug;
#endif
            Device = new Device(DriverType.Hardware, deviceFlags);
            ImmediateContext = Device.ImmediateContext;

            var alphaBlendDesc = new BlendStateDescription();
            alphaBlendDesc.RenderTarget[0] = new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All, 
                BlendOperation = BlendOperation.Add, 
                AlphaBlendOperation = BlendOperation.Add, 
                SourceBlend = BlendOption.SourceAlpha, 
                DestinationBlend = BlendOption.InverseSourceAlpha, 
                SourceAlphaBlend = BlendOption.SourceAlpha, 
                DestinationAlphaBlend = BlendOption.InverseSourceAlpha, 
                IsBlendEnabled = true
            };
            AlphaBlendState = new BlendState(Device, alphaBlendDesc);
        }

        public virtual void CreateSizeDependentResources(int width, int height, Texture2D newBackbuffer)
        {
            Width = width;
            Height = height;
            Backbuffer = newBackbuffer;
        }

        public virtual void DisposeSizeDependentResources()
        {
            Backbuffer.Dispose();
            Backbuffer = null;
        }

        public virtual void DisposeResources()
        {
            AlphaBlendState.Dispose();
            AlphaBlendState = null;
            ImmediateContext.Dispose();
            ImmediateContext = null;

#if DEBUG
            var debug = Device.QueryInterface<DeviceDebug>();
            debug.ReportLiveDeviceObjects(ReportingLevel.Detail);
            debug.Dispose();
#endif

            Device.Dispose();
            Device = null;
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

        public virtual void EmptyCaches()
        {
            
        }
    }

    /// <summary>
    /// Hosts a <see cref="RenderContext"/> in a WPF control.
    /// </summary>
    public sealed class SceneRenderControl : ContentControl, IDisposable, INotifyPropertyChanged
    {
        private Microsoft.Wpf.Interop.DirectX.D3D11Image D3DImage;
        private Image Image;
        private readonly Stopwatch Stopwatch = new();
        private bool _shouldRender;
        private RenderContext _context;
        private Action _onImageRendered;
        private bool _captureNextFrame;

        public RenderContext Context
        {
            get => _context;
            set => SetProperty(ref _context, value);
        }

        /// <summary>
        /// Invoked when the D3D11Image object has completed a rendering update
        /// </summary>
        public Action OnImageRendered
        {
            get => _onImageRendered;
            set => SetProperty(ref _onImageRendered, value);
        }

        public int RenderWidth => (int)RenderSize.Width;
        public int RenderHeight => (int)RenderSize.Height;

        public bool CaptureNextFrame
        {
            get => _captureNextFrame;
            set => SetProperty(ref _captureNextFrame, value);
        }

        public SceneRenderControl()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                // only at this point the control is ready
                var window = Window.GetWindow(this); // get the parent window
                //this will obviously always be true at runtime, but is NOT true in the designer.
                if (window is not null)
                {
                    window.Closing += (s1, e1) =>
                    {
                        if (!e1.Cancel)
                        {
                            Dispose();
                        }
                    };
                }
            };
        }

        private void InitializeComponent()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            Loaded += SceneRenderControl_Loaded;
        }

        private bool InitiallyLoaded = false;
        private void SceneRenderControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!InitiallyLoaded)
            {
                // Debug.WriteLine("SceneRenderControl_Loaded");
                D3DImage = new Microsoft.Wpf.Interop.DirectX.D3D11Image
                {
                    OnRender = D3DImage_OnRender,
                };
                Image = new Image
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Source = D3DImage
                };
                Content = Image;

                D3DImage.WindowOwner = new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).Handle; // This needs to be cleared when disposing or it will hold a reference
                Unloaded += SceneRenderControlWPF_Unloaded;

                try
                {
                    Context.CreateResources();
                }
                catch (Exception exception)
                {
                    Content = Image = new ImageAwesome
                    {
                        Icon = EFontAwesomeIcon.Solid_Ban,
                        Foreground = Brushes.DarkRed
                    };
                    return;
                }

                CompositionTarget.Rendering += CompositionTarget_Rendering;
                InitiallyLoaded = true;
            }
            else
            {
                // We are now becoming visible (e.g. tab selection)
                SetShouldRender(true);
            }
            PreviewMouseDown += SceneRenderControlWPF_PreviewMouseDown;
            PreviewMouseMove += SceneRenderControlWPF_PreviewMouseMove;
            PreviewMouseUp += SceneRenderControlWPF_PreviewMouseUp;
            PreviewMouseWheel += SceneRenderControlWPF_PreviewMouseWheel;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            SizeChanged += SceneRenderControlWPF_SizeChanged;
        }

        private void SceneRenderControlWPF_Unloaded(object sender, RoutedEventArgs e)
        {
            SetShouldRender(false);
            PreviewMouseDown -= SceneRenderControlWPF_PreviewMouseDown;
            PreviewMouseMove -= SceneRenderControlWPF_PreviewMouseMove;
            PreviewMouseUp -= SceneRenderControlWPF_PreviewMouseUp;
            PreviewMouseWheel -= SceneRenderControlWPF_PreviewMouseWheel;
            KeyUp -= OnKeyUp;
            KeyDown -= OnKeyDown;
            SizeChanged -= SceneRenderControlWPF_SizeChanged;
        }

        /// <summary>
        /// This is called when the window closes, as we have to dispose of resources that can't be disposed during unload
        /// </summary>
        public void Dispose()
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            Unloaded -= SceneRenderControlWPF_Unloaded;

            if (Context.Backbuffer != null)
                Context.DisposeSizeDependentResources();

            if (Context.Device != null)
                Context.DisposeResources();

            D3DImage?.Dispose();
            D3DImage = null;
            Image = null;
            Content = null;
            
            //Image.Source = null; // Lose reference to D3DImage
            //this.D3DImage.WindowOwner = IntPtr.Zero; // dunno if this is a good idea
            //D3DImage.Dispose();
            //Context = null;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Call this method at once, once the window hosting this control has been fully loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /*public void InitializeD3D()
        {
            D3DImage.WindowOwner = (new System.Windows.Interop.WindowInteropHelper(System.Windows.Window.GetWindow(this))).Handle;
            Context.CreateResources();
        }*/

        private void SceneRenderControlWPF_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            D3DImage.SetPixelSize(RenderWidth, RenderHeight);
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_shouldRender && Context is { IsReady: true })
            {
                //Debug.WriteLine("Rendering");
                D3DImage.RequestRender();
            }
        }

        private void D3DImage_OnRender(IntPtr surface, bool isNewSurface)
        {
            if (isNewSurface)
            {
                // Debug.WriteLine("IsNewSurface");
                if (Context.Backbuffer != null)
                {
                    Context.DisposeSizeDependentResources();
                }

                // Yikes - from https://github.com/microsoft/WPFDXInterop/blob/master/samples/D3D11Image/D3D11Visualization/D3DVisualization.cpp#L384
                var res = CppObject.FromPointer<ComObject>(surface);
                var resource = res.QueryInterface<SharpDX.DXGI.Resource>();
                IntPtr sharedHandle = resource.SharedHandle;
                resource.Dispose();
                var d3dres = Context.Device.OpenSharedResource<Resource>(sharedHandle);
                Context.CreateSizeDependentResources(RenderWidth, RenderHeight, d3dres.QueryInterface<Texture2D>());
                d3dres.Dispose();
            }

            if (isNewSurface || _shouldRender)
            {
                // Debug.WriteLine("_shouldRender");
                Context.Update((float)Stopwatch.Elapsed.Ticks / TimeSpan.TicksPerSecond);
                Stopwatch.Restart();
                bool capturing = false;
                if (CaptureNextFrame && RenderDoc.IsRenderDocAttached())
                {
                    CaptureNextFrame = false;
                    capturing = true;
                    RenderDoc.StartCapture(Context.Device.NativePointer, D3DImage.WindowOwner);
                }

                Context.Render();
                if (capturing)
                {
                    RenderDoc.EndCapture(Context.Device.NativePointer, D3DImage.WindowOwner);
                }

                OnImageRendered?.Invoke();
            }
        }

        public void SetShouldRender(bool shouldRender)
        {
            //if (!_shouldRender && shouldRender && D3DImage != null) // Not rendering, but we should start
            //{
            //    D3DImage.OnRender = D3DImage_OnRender;
            //}
            //else if (_shouldRender && !shouldRender && D3DImage != null) // Currently rendering, but we should stop
            //{
            //    D3DImage.OnRender = null;
            //}

            _shouldRender = shouldRender;
        }

        #region Input Events
        private void SceneRenderControlWPF_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            MouseButtons buttons;
            if (e.ChangedButton == MouseButton.Left)
                buttons = MouseButtons.Left;
            else if (e.ChangedButton == MouseButton.Middle)
                buttons = MouseButtons.Middle;
            else if (e.ChangedButton == MouseButton.Right)
                buttons = MouseButtons.Right;
            else
                return;
            Point position = e.GetPosition(this);
            e.Handled = Context.MouseDown(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            MouseButtons buttons;
            if (e.ChangedButton == MouseButton.Left)
                buttons = MouseButtons.Left;
            else if (e.ChangedButton == MouseButton.Middle)
                buttons = MouseButtons.Middle;
            else if (e.ChangedButton == MouseButton.Right)
                buttons = MouseButtons.Right;
            else
                return;
            Point position = e.GetPosition(this);
            e.Handled = Context.MouseUp(buttons, (int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point position = e.GetPosition(this);
            e.Handled = Context.MouseMove((int)position.X, (int)position.Y);
        }

        private void SceneRenderControlWPF_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = Context.MouseScroll(e.Delta);
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11 && RenderDoc.IsRenderDocAttached())
            {
                CaptureNextFrame = true;
                e.Handled = true;
            }
            else
            {
                e.Handled = Context.KeyDown(e.Key);
            }
        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = Context.KeyUp(e.Key);
        }
        #endregion

        // MEMORY GC
        ~SceneRenderControl()
        {
            Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
