using System;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
	public static class IntPtrExtensions
	{
		public static UIntPtr ToUIntPtr(this IntPtr ptr)
		{
			return unchecked((UIntPtr) (ulong) (long) ptr);
		}
	}
}
