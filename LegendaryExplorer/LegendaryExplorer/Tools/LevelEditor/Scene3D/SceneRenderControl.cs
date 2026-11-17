using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using FontAwesome5;
using SharpDX.Direct3D;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using Device = SharpDX.Direct3D11.Device;
using Point = System.Windows.Point;
using Resource = SharpDX.Direct3D11.Resource;
using LegendaryExplorerCore.Packages;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Libraries;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

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
                SetShouldRender(false);
                Content = Image = new ImageAwesome
                {
                    Icon = EFontAwesomeIcon.Solid_Ban,
                    Foreground = Brushes.DarkRed
                };
                new ExceptionHandlerDialog(exception).Show();
                return;
            }

            FramePacer.Add(this);
            InitiallyLoaded = true;
            D3DImage.SetPixelSize(RenderWidth, RenderHeight);
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
        LostKeyboardFocus += SceneRenderControl_LostKeyboardFocus;
        LostMouseCapture += SceneRenderControl_LostMouseCapture;
        MouseLeave += SceneRenderControl_MouseLeave;
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
        LostKeyboardFocus -= SceneRenderControl_LostKeyboardFocus;
        LostMouseCapture -= SceneRenderControl_LostMouseCapture;
        MouseLeave -= SceneRenderControl_MouseLeave;
        SizeChanged -= SceneRenderControlWPF_SizeChanged;
    }

    /// <summary>
    /// This is called when the window closes, as we have to dispose of resources that can't be disposed during unload
    /// </summary>
    public void Dispose()
    {
        //(a round of frames already asked for draws nothing for it)
        FramePacer.Remove(this);
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

    /// <summary>
    /// Asks for every viewport's frame, as one round, each time the desktop is composed (once a refresh), if the last round has been drawn. Frames
    /// aren't drawn from CompositionTarget.Rendering: WPF paces those itself, and once frames at Render priority have kept its Input-priority check
    /// from running for half a second, it drops the next one to Input priority. Input-priority work only runs while no input is waiting, so a mouse
    /// that keeps moving then holds frames back until it stops. Rounds asked for here stay at Render priority (updating the image has WPF draw it
    /// at Render priority too), and the next isn't asked for until the refresh after one finishes, so input is processed in between.
    /// The viewports share one round: paced apart, one's frame was asked for while another's was drawing, so with two open there was nearly always
    /// a frame waiting at Render priority, and the windows stopped responding until one closed or frames got cheaper
    /// </summary>
    private static class FramePacer
    {
        //the viewports drawn each round, in the order they were loaded. Locked, and waited on while there are none
        private static readonly List<SceneRenderControl> viewports = [];
        private static Thread thread;
        //1 from when a round is asked for until it has been drawn
        private static int roundPending;

        public static void Add(SceneRenderControl viewport)
        {
            lock (viewports)
            {
                if (viewports.Count > 0 && viewports[0].Dispatcher != viewport.Dispatcher)
                    throw new InvalidOperationException("Viewports are drawn on one UI thread.");
                viewports.Add(viewport);
                if (thread is null)
                {
                    thread = new Thread(Pace) { IsBackground = true, Name = "Viewport frame pacing" };
                    thread.Start();
                }
                Monitor.PulseAll(viewports);
            }
        }

        public static void Remove(SceneRenderControl viewport)
        {
            lock (viewports) viewports.Remove(viewport);
        }

        private static void Pace()
        {
            while (true)
            {
                Dispatcher dispatcher;
                lock (viewports)
                {
                    while (viewports.Count == 0) Monitor.Wait(viewports);
                    dispatcher = viewports[0].Dispatcher;
                }
                long start = Stopwatch.GetTimestamp();
                //returns at the next composition, or at once if composition is off
                if (WindowsAPI.DwmFlush() < 0 || Stopwatch.GetElapsedTime(start).TotalMilliseconds < 1) Thread.Sleep(15);
                if (Interlocked.CompareExchange(ref roundPending, 1, 0) == 0)
                {
                    dispatcher.InvokeAsync(DrawRound, DispatcherPriority.Render);
                }
            }
        }

        private static void DrawRound()
        {
            try
            {
                SceneRenderControl[] round;
                lock (viewports) round = [.. viewports];
                foreach (var viewport in round) viewport.DrawFrame();
            }
            finally
            {
                Volatile.Write(ref roundPending, 0);
            }
        }
    }

    private void DrawFrame()
    {
        //(not once disposed, which removes it from rounds, though one drawing before it may have disposed it)
        if (D3DImage is not null && _shouldRender && Context is { IsReady: true })
        {
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
            var res = SharpDX.CppObject.FromPointer<SharpDX.ComObject>(surface);
            var resource = res.QueryInterface<SharpDX.DXGI.Resource>();
            IntPtr sharedHandle = resource.SharedHandle;
            resource.Dispose();
            var d3dres = Context.Device.OpenSharedResource<Resource>(sharedHandle);
            try
            {
                Context.CreateSizeDependentResources(RenderWidth, RenderHeight, d3dres.QueryInterface<Texture2D>());
            }
            catch (Exception exception)
            {
                SetShouldRender(false);
                Content = Image = new ImageAwesome
                {
                    Icon = EFontAwesomeIcon.Solid_Ban,
                    Foreground = Brushes.DarkRed
                };
                new ExceptionHandlerDialog(exception).Show();
            }
            d3dres.Dispose();
        }

        if (_shouldRender)
        {
            // Debug.WriteLine("_shouldRender");
            Context.Update((float)Stopwatch.Elapsed.Ticks / TimeSpan.TicksPerSecond);
            Stopwatch.Restart();
            //(after the update, which resolves what's hovered; pressing or releasing a key over the viewport shows by the next frame)
            Cursor = Context.ViewportCursor;
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
        SyncMouseCapture();
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
        SyncMouseCapture();
    }

    private void SceneRenderControlWPF_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        Point position = e.GetPosition(this);
        e.Handled = Context.MouseMove((int)position.X, (int)position.Y);
        SyncMouseCapture();
    }

    /// <summary>
    /// Captures the mouse while the context holds it, so a drag's release outside the viewport still reaches it.
    /// Call after ending a drag from outside the input events
    /// </summary>
    public void SyncMouseCapture()
    {
        if (Context?.HoldsMouseCapture == true)
        {
            if (!IsMouseCaptured) CaptureMouse();
        }
        else if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void SceneRenderControlWPF_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = Context.MouseScroll(e.Delta, Keyboard.Modifiers);
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
            e.Handled = Context.KeyDown(InputKey(e));
            SyncMouseCapture();
        }
    }

    public void OnKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = Context.KeyUp(InputKey(e));
        SyncMouseCapture();
    }

    /// <summary>
    /// While Alt is held WPF reports keys as <see cref="Key.System"/>. During a drag the context gets the real key, so Escape still cancels;
    /// otherwise Alt combinations stay the window's (menu access keys, Alt+F4)
    /// </summary>
    private Key InputKey(KeyEventArgs e) => e.Key is Key.System && Context.ClaimsAltKeys ? e.SystemKey : e.Key;

    private void SceneRenderControl_LostMouseCapture(object sender, MouseEventArgs e)
    {
        e.Handled = Context.LostMouseFocus();
    }

    private void SceneRenderControl_MouseLeave(object sender, MouseEventArgs e) => Context.MouseLeft();

    private void SceneRenderControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        e.Handled = Context.LostKeyboardFocus();
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
