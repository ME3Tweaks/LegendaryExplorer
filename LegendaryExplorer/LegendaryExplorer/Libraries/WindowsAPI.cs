using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Libraries
{
    internal static unsafe partial class WindowsAPI
    {
        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        public static partial long SendMessage(IntPtr hWnd, uint msg, nint wParam, long lParam);

        [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessage(IntPtr hWnd, uint msg, nint wParam, long lParam);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsIconic(IntPtr hwnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "FindWindowW")]
        public static partial IntPtr FindWindow(string className, string windowName);

        [LibraryImport("user32.dll")]
        public static partial int GetWindowRect(IntPtr hwnd, out Rectangle rect);

        [LibraryImport("Shell32.dll")]
        public static partial int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        [LibraryImport("gdi32.dll")]
        public static partial IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, in uint pcFonts);

        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "GetModuleHandleW")]
        public static partial IntPtr GetModuleHandle(string lpModuleName);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            void* lpBuffer,
            ulong dwSize,
            out IntPtr lpNumberOfBytesRead);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            void* lpBuffer,
            ulong nSize,
            out IntPtr lpNumberOfBytesWritten);

        [LibraryImport("kernel32.dll")]
        public static partial IntPtr OpenProcess(
            uint processAccess,
            [MarshalAs(UnmanagedType.Bool)]
            bool bInheritHandle,
            uint processId
        );

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CloseHandle(IntPtr hObject);
    }
}
