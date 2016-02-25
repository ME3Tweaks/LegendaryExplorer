using System;
using System.ComponentModel;
using System.Linq;

namespace MassEffect3.SaveEdit
{
	internal static class BindingListHelpers
	{
		public static void RemoveAll<T>(this BindingList<T> list, Func<T, bool> predicate)
		{
			foreach (var item in list.Where(predicate).ToList())
			{
				list.Remove(item);
			}
		}
	}
}