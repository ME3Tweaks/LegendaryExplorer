using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MassEffect3.AudioExtract
{
	internal static class NativeHelper
	{
		[DllImport("shlwapi.dll", ExactSpelling = true, EntryPoint = "StrFormatByteSizeW", CharSet = CharSet.Unicode)]
		private static extern IntPtr StrFormatByteSizeW(
			long dw,
			[MarshalAs(UnmanagedType.LPTStr)] StringBuilder pszBuf,
			int cchBuf);

		public static string FormatByteSize(long size)
		{
			var builder = new StringBuilder(128);
			StrFormatByteSizeW(size, builder, builder.Capacity);
			return builder.ToString();
		}
	}
}