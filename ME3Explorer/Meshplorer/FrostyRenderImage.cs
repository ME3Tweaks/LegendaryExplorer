/**
 * This code was donated by GalaxyMan2015 from the Frosty Tool Suite
 * https://frostytoolsuitedev.gitlab.io/downloads.html
 */

using System;
using System.Windows.Interop;
using D3D9 = SharpDX.Direct3D9;
using D3D11 = SharpDX.Direct3D11;
using System.Windows;
using System.Runtime.InteropServices;

namespace ME3Explorer
{
    public class FrostyRenderImage : D3DImage, IDisposable
    {
        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();

        private static readonly object d3dLock = new object();

        private static D3D9.Direct3DEx d3d9;
        private static D3D9.Device device;

        private D3D9.Texture backBuffer;
        private D3D9.Texture prevBackBuffer;
        private static int refCount;
        private bool disposed;

        public FrostyRenderImage()
        {
            lock (d3dLock)
            {
                refCount++;
                if (refCount == 1)
                {
                    try
                    {
                        d3d9 = new D3D9.Direct3DEx();
                        D3D9.PresentParameters pp = new D3D9.PresentParameters()
                        {
                            Windowed = true,
                            SwapEffect = D3D9.SwapEffect.Discard,
                            PresentationInterval = D3D9.PresentInterval.Default,
                            BackBufferFormat = D3D9.Format.Unknown,
                            BackBufferHeight = 1,
                            BackBufferWidth = 1,

                            DeviceWindowHandle = GetDesktopWindow()
                        };

                        device = new D3D9.Device(d3d9, 0, D3D9.DeviceType.Hardware, IntPtr.Zero,
                            D3D9.CreateFlags.HardwareVertexProcessing | D3D9.CreateFlags.Multithreaded | D3D9.CreateFlags.FpuPreserve, pp);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message.ToString() + "\n\n" + e.StackTrace);
                        throw e;
                    }
                }
            }
        }

        ~FrostyRenderImage()
        {
            Dispose(false);
        }

        public void SetBackBuffer(D3D11.Texture2D texture)
        {
            if (prevBackBuffer != null)
                prevBackBuffer.Dispose();
            prevBackBuffer = backBuffer;

            if (texture != null)
            {
                using (SharpDX.DXGI.Resource resource = texture.QueryInterface<SharpDX.DXGI.Resource>())
                {
                    IntPtr handle = resource.SharedHandle;
                    backBuffer = new D3D9.Texture(device, texture.Description.Width, texture.Description.Height, 1, D3D9.Usage.RenderTarget, D3D9.Format.A8R8G8B8, D3D9.Pool.Default, ref handle);

                    if (backBuffer != null)
                    {
                        using (D3D9.Surface surface = backBuffer.GetSurfaceLevel(0))
                        {
                            if (TryLock(new Duration(default(TimeSpan))))
                            {
                                SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
                            }
                            Unlock();
                        }
                    }
                    else
                    {
                        if (TryLock(new Duration(default(TimeSpan))))
                        {
                            SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                        }
                        Unlock();
                    }
                }
            }
            else
            {
                if (TryLock(new Duration(default(TimeSpan))))
                {
                    SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                }
                Unlock();
            }
        }

        public void Invalidate()
        {
            if (backBuffer != null)
            {
                if (TryLock(new Duration(default(TimeSpan))))
                {
                    AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
                }
                Unlock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    SetBackBuffer(null);
                    if (prevBackBuffer != null)
                    {
                        prevBackBuffer.Dispose();
                        prevBackBuffer = null;
                    }
                    if (backBuffer != null)
                    {
                        backBuffer.Dispose();
                        backBuffer = null;
                    }
                }

                lock (d3dLock)
                {
                    refCount--;
                    if (refCount == 0)
                    {
                        device.Dispose();
                        d3d9.Dispose();
                    }
                }

                disposed = true;
            }
        }
    }
}