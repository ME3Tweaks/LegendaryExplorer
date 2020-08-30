using System;

namespace ME3ExplorerCore.Gammtek.Extensions
{
	public static class IntPtrExtensions
	{
		public static UIntPtr ToUIntPtr(this IntPtr ptr)
		{
			return unchecked((UIntPtr) (ulong) (long) ptr);
		}
	}
}
