using System;
using System.Diagnostics.CodeAnalysis;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
	public static class StringComparisonExtensions
	{
		[return: NotNull]
		public static StringComparer GetStringComparer(this StringComparison comparison)
		{
			switch (comparison)
			{
				case StringComparison.CurrentCulture:
				{
					return StringComparer.CurrentCulture;
				}
				case StringComparison.CurrentCultureIgnoreCase:
				{
					return StringComparer.CurrentCultureIgnoreCase;
				}
				case StringComparison.InvariantCulture:
				{
					return StringComparer.InvariantCulture;
				}
				case StringComparison.InvariantCultureIgnoreCase:
				{
					return StringComparer.InvariantCultureIgnoreCase;
				}
				case StringComparison.Ordinal:
				{
					return StringComparer.Ordinal;
				}
				case StringComparison.OrdinalIgnoreCase:
				{
					return StringComparer.OrdinalIgnoreCase;
				}
				default:
				{
					throw new ArgumentOutOfRangeException(nameof(comparison));
				}
			}
		}
	}
}
