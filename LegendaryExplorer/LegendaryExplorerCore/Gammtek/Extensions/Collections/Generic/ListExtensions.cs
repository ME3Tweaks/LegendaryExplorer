using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;

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

        public static void MoveElement<T>(this IList<T> list, int fromIndex, int toIndex)
        {
            if (list == null || fromIndex == toIndex || fromIndex >= list.Count) return;
            var item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
        }

        // IDK how to do this with .ToDictionary()
        public static CaseInsensitiveDictionary<TOutObjType> ToCaseInsensitiveDictionary<TOutObjType, TInObjType>(
            this IList<TInObjType> collection, Func<TInObjType, string> keySelector,
            Func<TInObjType, TOutObjType> valueSelector)
        {
            var cid = new CaseInsensitiveDictionary<TOutObjType>(collection.Count);
            foreach (var item in collection)
            {
                cid.Add(keySelector(item), valueSelector(item));
            }

            return cid;
        }
    }
}