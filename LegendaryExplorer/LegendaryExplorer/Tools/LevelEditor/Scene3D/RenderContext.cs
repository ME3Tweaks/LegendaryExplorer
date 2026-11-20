using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using SharpDX.Direct3D11;
using SharpDX.Direct3D;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using System.Numerics;
using LegendaryExplorer.Libraries;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public enum MouseButtons
{
    None,
    Left,
    Middle,
    Right
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
        /// Primitives are drawn over the scene (the transform widget and gizmos). Their hit test pixels are marked, so the selection outline isn't drawn around them
        /// </summary>
        Overlay = 1 << 25,
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

        //the hit test target (if bound) is overwritten: its alpha marks overlay pixels (see ShaderFlags.Overlay), so it isn't a blend factor
        var alphaBlendDesc = new BlendStateDescription { IndependentBlendEnable = true };
        alphaBlendDesc.RenderTarget[1] = new RenderTargetBlendDescription
        {
            RenderTargetWriteMask = ColorWriteMaskFlags.All,
            IsBlendEnabled = false
        };
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
