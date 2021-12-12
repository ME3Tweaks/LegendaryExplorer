using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    public static class RenderDoc
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RENDERDOC_API_1_0_0
        {
            public IntPtr GetAPIVersion;

            public IntPtr SetCaptureOptionU32;
            public IntPtr SetCaptureOptionF32;

            public IntPtr GetCaptureOptionU32;
            public IntPtr GetCaptureOptionF32;

            public IntPtr SetFocusToggleKeys;
            public IntPtr SetCaptureKeys;

            public IntPtr GetOverlayBits;
            public IntPtr MaskOverlayBits;

            public IntPtr Shutdown;
            public IntPtr UnloadCrashHandler;

            public IntPtr SetLogFilePathTemplate;
            public IntPtr GetLogFilePathTemplate;

            public IntPtr GetNumCaptures;
            public IntPtr GetCapture;

            public IntPtr TriggerCapture;

            public IntPtr IsRemoteAccessConnected;
            public IntPtr LaunchReplayUI;

            public IntPtr SetActiveWindow;

            public StartFrameCapture StartFrameCapture;
            public IntPtr IsFrameCapturing;
            public EndFrameCapture EndFrameCapture;
        }

        //typedef void (RENDERDOC_CC *pRENDERDOC_StartFrameCapture)(RENDERDOC_DevicePointer device, RENDERDOC_WindowHandle wndHandle);
        private delegate void StartFrameCapture(IntPtr device, IntPtr window);

        //typedef uint32_t (RENDERDOC_CC *pRENDERDOC_EndFrameCapture)(RENDERDOC_DevicePointer device, RENDERDOC_WindowHandle wndHandle);
        private delegate int EndFrameCapture(IntPtr device, IntPtr window);

        [DllImport("renderdoc.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int RENDERDOC_GetAPI(int version, out IntPtr outAPIPointers);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static int eRENDERDOC_API_Version_1_0_0 = 10000;

        public static bool IsRenderDocAttached()
        {
            return GetModuleHandle("renderdoc.dll") != IntPtr.Zero;
        }

        public static void StartCapture(IntPtr device, IntPtr windowHandle)
        {
            IntPtr pAPI = new IntPtr();
            int ret = RENDERDOC_GetAPI(eRENDERDOC_API_Version_1_0_0, out pAPI);
            RENDERDOC_API_1_0_0 api = (RENDERDOC_API_1_0_0)Marshal.PtrToStructure(pAPI, typeof(RENDERDOC_API_1_0_0));
            api.StartFrameCapture(device, /*windowHandle*/IntPtr.Zero);
        }

        public static void EndCapture(IntPtr device, IntPtr windowHandle)
        {
            IntPtr pAPI = new IntPtr();
            int ret = RENDERDOC_GetAPI(eRENDERDOC_API_Version_1_0_0, out pAPI);
            RENDERDOC_API_1_0_0 api = (RENDERDOC_API_1_0_0)Marshal.PtrToStructure(pAPI, typeof(RENDERDOC_API_1_0_0));
            api.EndFrameCapture(device, /*windowHandle*/IntPtr.Zero);
        }
    }
}
