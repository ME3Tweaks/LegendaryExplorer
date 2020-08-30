using System.Collections.Generic;

namespace ME3ExplorerCore.Gammtek.Collections.Generic
{
	public class HeapSort<T>
	{
		public static void Sort(IList<T> list, IComparer<T> comparer)
		{
			var heap = new Heap<T>(list, list.Count, comparer);

			while (heap.Count > 0)
			{
				heap.PopRoot();
			}
		}
	}
}
