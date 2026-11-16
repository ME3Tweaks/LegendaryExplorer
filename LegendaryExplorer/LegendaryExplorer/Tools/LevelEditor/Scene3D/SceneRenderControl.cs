using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using FontAwesome5;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Point = System.Windows.Point;
using Resource = SharpDX.Direct3D11.Resource;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using LegendaryExplorer.Dialogs;
using System.Numerics;
using LegendaryExplorer.Libraries;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

public enum MouseButtons
{
    None,
    Left,
    Middle,
    Right
}

public static class RenderContextExtensions
{
    /// <param name="resourceFormat">Format to create the texture with, if different from <paramref name="format"/>. (eg. a typeless format, so that it can have both UNORM and sRGB views)</param>
    public static unsafe Texture2D LoadTexture(this RenderContext renderContext, uint width, uint height, Format format, byte[] pixelData, Format? resourceFormat = null)
    {
        Texture2DDescription texture2DDescription = GetTextureDescription(width, height, format, false, out int pitch);
        texture2DDescription.Format = resourceFormat ?? format;
        fixed (byte* pixelDataPointer = pixelData)
        {
            return new Texture2D(renderContext.Device, texture2DDescription, new SharpDX.DataRectangle((IntPtr)pixelDataPointer, pitch));
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

    public static Texture2D LoadTextureCube(this RenderContext renderContext, uint size, Format format, Fixed6<byte[]> faceData, Format? resourceFormat = null)
    {
        if (size == 0 || size > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(size));
        int requiredBytes = GetMipByteSize(format, (int)size, (int)size, out _);
        // UpdateSubresource takes a pointer, so a short face would read past the array.
        for (int i = 0; i < faceData.Length; i++)
        {
            if (faceData[i] is null || faceData[i].Length < requiredBytes)
            {
                throw new ArgumentException($"Cubemap face {i} needs {requiredBytes} bytes for {size}x{size} {format}, but has {faceData[i]?.Length ?? 0}.", nameof(faceData));
            }
        }
        Texture2DDescription texture2DDescription = GetTextureDescription(size, size, format, true, out int pitch);
        texture2DDescription.Format = resourceFormat ?? format;
        var tex = new Texture2D(renderContext.Device, texture2DDescription);
        try
        {
            for (int i = 0; i < faceData.Length; i++)
            {
                renderContext.Device.ImmediateContext.UpdateSubresource(faceData[i], tex, i, pitch);
            }
            return tex;
        }
        catch
        {
            tex.Dispose();
            throw;
        }
    }

    public static unsafe Texture2D LoadTextureFromFile(this RenderContext renderContext, string filename)
    {
        var pixelFormat = LegendaryExplorerCore.Textures.PixelFormat.ARGB;
        byte[] pixelData = LegendaryExplorerCore.Textures.TexConverter.LoadTexture(filename, out uint width, out uint height, ref pixelFormat); // NEEDS WAY TO HAVE ALPHA AS BLACK!
        Format format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        return renderContext.LoadTexture(width, height, format, pixelData);
    }

    public static byte[] ReadTextureData(this RenderContext renderContext, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip)
        => renderContext is MeshRenderContext meshContext ? meshContext.ReadTextureMip(mip) : LECTexture2D.GetTextureData(mip, mip.Export.Game);

    public static Texture2D LoadUnrealMip(this RenderContext renderContext, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip, LegendaryExplorerCore.Textures.PixelFormat pixelFormat, bool typelessResource = false)
    {
        // Todo: Needs way to set black alpha
        var imagebytes = renderContext.ReadTextureData(mip);
        uint mipWidth = (uint)mip.width;
        uint mipHeight = (uint)mip.height;
        var mipFormat = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        if (mipFormat.IsCompressed())
        {
            mipWidth = (mipWidth < 4) ? 4 : mipWidth;
            mipHeight = (mipHeight < 4) ? 4 : mipHeight;
        }
        Format? resourceFormat = typelessResource ? GetSRGBFormats(mipFormat)?.Typeless : null;
        return renderContext.LoadTexture(mipWidth, mipHeight, mipFormat, imagebytes, resourceFormat);
    }

    /// <summary>
    /// Loads <paramref name="topMip"/> and as many of the mips below it as are available, so the texture can be minified without aliasing.
    /// </summary>
    /// <param name="mips">All of the texture's mips, largest first</param>
    public static Texture2D LoadUnrealMipChain(this RenderContext renderContext, IReadOnlyList<LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo> mips,
        LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo topMip, LegendaryExplorerCore.Textures.PixelFormat pixelFormat, bool typelessResource = false)
    {
        var format = (Format)LegendaryExplorerCore.Textures.TexConverter.GetDXGIFormatForPixelFormat(pixelFormat);
        bool isCompressed = format.IsCompressed();
        int topWidth = topMip.width;
        int topHeight = topMip.height;
        //block-compressed textures must have dimensions that are multiples of 4. Tiny ones are padded, and can't have mips below them
        if (isCompressed && (topWidth % 4 != 0 || topHeight % 4 != 0))
        {
            return renderContext.LoadUnrealMip(topMip, pixelFormat, typelessResource);
        }

        int topIndex = -1;
        for (int i = 0; i < mips.Count; i++)
        {
            if (mips[i] == topMip) topIndex = i;
        }
        List<byte[]> levels = ReadMipChain(format, topWidth, topHeight, topIndex < 0 ? 0 : mips.Count - topIndex, level =>
        {
            var mip = mips[topIndex + level];
            return IsChainLevel(mip, level, topMip) ? renderContext.ReadTextureData(mip) : null;
        });
        if (levels.Count == 0)
        {
            return renderContext.LoadUnrealMip(topMip, pixelFormat, typelessResource);
        }
        return CreateMipChain(renderContext.Device, format, typelessResource ? GetSRGBFormats(format)?.Typeless : null, topWidth, topHeight, levels);
    }

    /// <summary>
    /// Whether a mip is the given level of the chain below <paramref name="top"/>
    /// </summary>
    internal static bool IsChainLevel(LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo mip, int level, LegendaryExplorerCore.Unreal.Classes.Texture2DMipInfo top) =>
        mip.storageType is not StorageTypes.empty && mip.width == Math.Max(1, top.width >> level) && mip.height == Math.Max(1, top.height >> level);

    /// <summary>
    /// Reads as many levels of a mip chain as are available. The top level is what a draw shows up close, so a missing
    /// or bad level below it only costs the levels from there on, which only matters when the texture is minified.
    /// </summary>
    /// <param name="read">Reads a level of the chain. Null ends the chain</param>
    /// <returns>The complete levels, largest first. Empty if the top level's data is missing or too short. An exception reading it isn't caught</returns>
    internal static List<byte[]> ReadMipChain(Format format, int topWidth, int topHeight, int levelCount, Func<int, byte[]> read)
    {
        var levels = new List<byte[]>(levelCount);
        for (int level = 0; level < levelCount; level++)
        {
            byte[] data;
            try
            {
                data = read(level);
            }
            catch (Exception e) when (level > 0 && e is not OperationCanceledException)
            {
                Debug.WriteLine($"Could not read level {level} of a {topWidth}x{topHeight} mip chain: {e}");
                break;
            }
            if (data is null || data.Length < GetMipByteSize(format, Math.Max(1, topWidth >> level), Math.Max(1, topHeight >> level), out _))
            {
                break;
            }
            levels.Add(data);
        }
        return levels;
    }

    /// <summary>
    /// Creates a texture from complete mips, largest first. Only uses the device, so this is safe on a worker thread.
    /// </summary>
    /// <param name="resourceFormat">Format to create the texture with, if different from <paramref name="format"/></param>
    internal static Texture2D CreateMipChain(Device device, Format format, Format? resourceFormat, int width, int height, IReadOnlyList<byte[]> levels)
    {
        Texture2DDescription desc = GetTextureDescription((uint)width, (uint)height, format, false, out _);
        desc.MipLevels = levels.Count;
        desc.Format = resourceFormat ?? format;

        var handles = new System.Runtime.InteropServices.GCHandle[levels.Count];
        var rects = new SharpDX.DataRectangle[levels.Count];
        try
        {
            for (int level = 0; level < levels.Count; level++)
            {
                handles[level] = System.Runtime.InteropServices.GCHandle.Alloc(levels[level], System.Runtime.InteropServices.GCHandleType.Pinned);
                GetMipByteSize(format, Math.Max(1, width >> level), Math.Max(1, height >> level), out int pitch);
                rects[level] = new SharpDX.DataRectangle(handles[level].AddrOfPinnedObject(), pitch);
            }
            return new Texture2D(device, desc, rects);
        }
        finally
        {
            foreach (var handle in handles)
            {
                if (handle.IsAllocated) handle.Free();
            }
        }
    }

    internal static int GetMipByteSize(Format format, int width, int height, out int pitch)
    {
        if (format.IsCompressed())
        {
            int blockSize = format is Format.BC1_UNorm or Format.BC1_UNorm_SRgb or Format.BC4_SNorm or Format.BC4_UNorm ? 8 : 16;
            int blocksWide = Math.Max(1, (width + 3) / 4);
            int blocksHigh = Math.Max(1, (height + 3) / 4);
            pitch = blocksWide * blockSize;
            return pitch * blocksHigh;
        }
        pitch = format.SizeOfInBits() * width / 8;
        return pitch * height;
    }

    /// <summary>
    /// For formats that have an sRGB variant, returns the typeless format a texture must be created with to have both UNORM and sRGB views.
    /// Returns null for formats without an sRGB variant (eg. BC4/BC5, which are only used for non-color data).
    /// </summary>
    public static (Format Typeless, Format UNorm, Format SRGB)? GetSRGBFormats(Format format) => format switch
    {
        Format.BC1_UNorm or Format.BC1_UNorm_SRgb => (Format.BC1_Typeless, Format.BC1_UNorm, Format.BC1_UNorm_SRgb),
        Format.BC2_UNorm or Format.BC2_UNorm_SRgb => (Format.BC2_Typeless, Format.BC2_UNorm, Format.BC2_UNorm_SRgb),
        Format.BC3_UNorm or Format.BC3_UNorm_SRgb => (Format.BC3_Typeless, Format.BC3_UNorm, Format.BC3_UNorm_SRgb),
        Format.BC7_UNorm or Format.BC7_UNorm_SRgb => (Format.BC7_Typeless, Format.BC7_UNorm, Format.BC7_UNorm_SRgb),
        Format.R8G8B8A8_UNorm or Format.R8G8B8A8_UNorm_SRgb => (Format.R8G8B8A8_Typeless, Format.R8G8B8A8_UNorm, Format.R8G8B8A8_UNorm_SRgb),
        Format.B8G8R8A8_UNorm or Format.B8G8R8A8_UNorm_SRgb => (Format.B8G8R8A8_Typeless, Format.B8G8R8A8_UNorm, Format.B8G8R8A8_UNorm_SRgb),
        Format.B8G8R8X8_UNorm or Format.B8G8R8X8_UNorm_SRgb => (Format.B8G8R8X8_Typeless, Format.B8G8R8X8_UNorm, Format.B8G8R8X8_UNorm_SRgb),
        _ => null
    };

    public static Mesh<WorldVertex> GetMeshFromAggGeom(this RenderContext renderContext, StructProperty aggGeom)
    {
        if (aggGeom?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") is ArrayProperty<StructProperty> convexElems)
        {
            var vertices = new List<WorldVertex>();
            var triangles = new List<Triangle>();
            int vertTotal = 0;
            foreach (StructProperty convexElem in convexElems)
            {
                var faceTriData = convexElem.GetProp<ArrayProperty<IntProperty>>("FaceTriData");
                for (int i = 0; i < faceTriData.Count; i += 3)
                {
                    triangles.Add(new Triangle((uint)(faceTriData[i].Value + vertTotal), (uint)(faceTriData[i + 1].Value + vertTotal), (uint)(faceTriData[i + 2].Value + vertTotal)));
                }

                var vertexData = convexElem.GetProp<ArrayProperty<StructProperty>>("VertexData");
                foreach (StructProperty vertex in vertexData)
                {
                    float x = vertex.GetProp<FloatProperty>("X").Value;
                    float y = vertex.GetProp<FloatProperty>("Y").Value;
                    float z = vertex.GetProp<FloatProperty>("Z").Value;
                    vertices.Add(new WorldVertex(new Vector3(x, y, z), Vector4.Zero, Vector2.Zero));
                    ++vertTotal;
                }
            }

            return new Mesh<WorldVertex>(renderContext.Device, triangles, vertices);
        }

        return null;
    }
}

public abstract class RenderContext
{
    [Flags]
    public enum ShaderFlags : int
    {
        None = 0,
        /// <summary>
        /// If normals should have the third color channel populated
        /// </summary>
        ReconstructNormalZ = 1 << 0,
        /// <summary>
        /// If alpha channel should be set to black, so textures can properly be viewed
        /// </summary>
        AlphaAsBlack = 1 << 1,
        EnableRedChannel = 1 << 2,
        EnableGreenChannel = 1 << 3,
        EnableBlueChannel = 1 << 4,
        EnableAlphaChannel = 1 << 5,

        //level editor flags
        /// <summary>
        /// Primitives are textured sprites, tinted by their vertex color. Transparent texels are discarded
        /// </summary>
        Sprite = 1 << 26,
        /// <summary>
        /// The mesh is eligible for the selected light, so it's tinted
        /// </summary>
        Highlighted = 1 << 27,
        Unlit = 1 << 28,
        Wireframe = 1 << 29,
        Selected = 1 << 30,
        PrimitiveRendering = 1 << 31,
    }

    public int Width { get; private set; } = 0;
    public int Height { get; private set; } = 0;
    public Device Device { get; private set; }
    public DeviceContext ImmediateContext { get; private set; }
    public Texture2D Backbuffer { get; private set; }
    public BlendState AlphaBlendState { get; private set; } // A BlendState that uses standard alpha blending
    public bool IsReady => Device != null;

    public virtual void CreateResources()
    {
        // Keep device resource creation thread-safe; the immediate context remains render-thread owned.
        DeviceCreationFlags deviceFlags = DeviceCreationFlags.BgraSupport;
#if D3D11_DEBUG
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

#if D3D11_DEBUG
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

    public virtual bool MouseScroll(int delta, ModifierKeys modifiers)
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

    public virtual bool LostKeyboardFocus()
    {
        return false;
    }

    public virtual bool LostMouseFocus()
    {
        return false;
    }

    /// <summary>The mouse left the viewport (not while it's captured: a drag keeps reporting it outside)</summary>
    public virtual void MouseLeft() { }

    /// <summary>
    /// Whether the context owns the mouse (such as during a drag), so the control captures it and keeps reporting it outside the viewport
    /// </summary>
    public virtual bool HoldsMouseCapture => false;

    /// <summary>
    /// Whether keys pressed while Alt is held reach the context as themselves (so Escape is Escape, and Alt's own release can be held back from the
    /// window's menu). Otherwise Alt combinations stay the window's
    /// </summary>
    public virtual bool ClaimsAltKeys => HoldsMouseCapture;

    /// <summary>The cursor over the viewport, or null for the usual one. Read each frame</summary>
    public virtual Cursor ViewportCursor => null;

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
