using System.Collections.Generic;

namespace LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic
{
	public static class DictionaryExtensions
	{
		public static void ChangeKey<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey oldValue, TKey newValue)
		{
			var value = source[oldValue];
			source.Remove(oldValue);
			source[newValue] = value;
		}
	}
}
