using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using Win32 = TerraFX.Interop.Windows.Windows;

namespace LegendaryExplorer.Startup
{
    internal static class DPIAwareSplashScreen
    {
#if NIGHTLY
        private const string SplashImagePath = "LegendaryExplorer.Resources.Images.LEX_Splash_Nightly.png";
#else
        private const string SplashImagePath = "LegendaryExplorer.Resources.Images.LEX_Splash.png";
#endif
        public static unsafe void Show()
        {
            Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            var wcex = new WNDCLASSEXW
            {
                style = CS.CS_NOCLOSE,
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hIcon = Win32.LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION),
                hCursor = Win32.LoadCursorW(HINSTANCE.NULL, IDC.IDC_APPSTARTING),
                hIconSm = HICON.NULL,
                hbrBackground = (HBRUSH)COLOR.COLOR_WINDOW,
                lpszMenuName = null
            };
            fixed (char* className = "LEXSplashScreen")
            {
                wcex.lpszClassName = className;
                if (Win32.RegisterClassExW(&wcex) == 0)
                {
                    return;
                }
                _splashHwnd = Win32.CreateWindowExW(0, className, null, WS.WS_VISIBLE | WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, HINSTANCE.NULL, null);
                if (_splashHwnd == HWND.NULL)
                {
                    return;
                }
            }
            var msg = new MSG();
            //pump messages until the window has painted
            while (Win32.GetMessageW(&msg, HWND.NULL, 0, 0) > 0)
            {
                Win32.TranslateMessage(&msg);
                Win32.DispatchMessageW(&msg);
                if (msg.message is WM.WM_PAINT)
                {
                    break;
                }
            }
        }

        private static HWND _splashHwnd;
        private static Bitmap _splashBitmap;
        public static void DestroySplashScreen()
        {
            Win32.DestroyWindow(_splashHwnd);
            _splashBitmap = null;
        }

        [UnmanagedCallersOnly]
        private static unsafe LRESULT WndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
        {
            switch (message)
            {
                case WM.WM_CREATE:
                    {
                        HMONITOR monitor = Win32.MonitorFromWindow(hWnd, MONITOR.MONITOR_DEFAULTTOPRIMARY);
                        DEVICE_SCALE_FACTOR scaleEnum = default;
                        Win32.GetScaleFactorForMonitor(monitor, &scaleEnum);
                        int scaleFactor = (int)scaleEnum;
                        int width = 912 * scaleFactor / 100;
                        int height = 400 * scaleFactor / 100;
                        int screenWidth = Win32.GetSystemMetrics(SM.SM_CXSCREEN);
                        int x = (screenWidth - width) / 2;
                        int screenHeight = Win32.GetSystemMetrics(SM.SM_CYSCREEN);
                        int y = (screenHeight - height) / 2;
                        Win32.SetWindowPos(hWnd, HWND.HWND_TOP, x, y, width, height, 0);
                        Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SplashImagePath);
                        _splashBitmap = manifestResourceStream is not null ? new Bitmap(manifestResourceStream) : new Bitmap(10,10);
                        return 0;
                    }
                case WM.WM_PAINT:
                    {
                        var ps = new PAINTSTRUCT();
                        var rect = new RECT();
                        Win32.GetClientRect(hWnd, &rect);
                        HDC hdc = Win32.BeginPaint(hWnd, &ps);
                        Win32.SetStretchBltMode(hdc, Win32.COLORONCOLOR);
                        HDC hdcMem = Win32.CreateCompatibleDC(hdc);
                        var hbitmap = (HBITMAP)_splashBitmap.GetHbitmap();
                        Win32.SelectObject(hdcMem, hbitmap);
                        Win32.StretchBlt(hdc, 0, 0, rect.right, rect.bottom, hdcMem, 0, 0, _splashBitmap.Width, _splashBitmap.Height, Win32.SRCCOPY);
                        Win32.DeleteDC(hdcMem);
                        Win32.DeleteObject(hbitmap);
                        Win32.EndPaint(hWnd, &ps);
                        return 0;
                    }
                default:
                    return Win32.DefWindowProcW(hWnd, message, wParam, lParam);
            }
        }
    }
}