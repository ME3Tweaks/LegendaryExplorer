using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Be.Windows.Forms
{
	internal static partial class NativeMethods
	{
		// Caret definitions
		[LibraryImport("user32.dll", SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

		[LibraryImport("user32.dll", SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool ShowCaret(IntPtr hWnd);

		[LibraryImport("user32.dll", SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool DestroyCaret();

		[LibraryImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool SetCaretPos(int X, int Y);

		// Key definitions
		public const int WM_KEYDOWN = 0x100;
		public const int WM_KEYUP = 0x101;
		public const int WM_CHAR = 0x102;
	}
}
