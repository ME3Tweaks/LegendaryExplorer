using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic
{
    public static class ListExtensions
    {
        public static void ReplaceFirstOrAdd<T>(this IList<T> collection, Func<T, bool> predicate, T item)
        {
            int index = collection.FindIndex(predicate);
            if (index == -1)
            {
                collection.Add(item);
            }
            else
            {
                collection[index] = item;
            }
        }
    }
}
