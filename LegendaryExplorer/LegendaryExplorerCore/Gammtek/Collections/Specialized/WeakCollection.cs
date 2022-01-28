using System;
using System.Collections;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Gammtek.Collections.Specialized
{
    /// <summary>
    /// An unordered collection of <typeparamref name="T"/>s, held weakly. (Using <see cref="WeakReference{T}"/>s)
    /// Enumerating this collection will automatically remove garbage collected items.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    //adapted from https://stackoverflow.com/a/66367524
    public class WeakCollection<T> : ICollection<T> where T : class
    {
        private readonly List<WeakReference<T>> list = new();

        public void Add(T item) => list.Add(new WeakReference<T>(item));
        public void Clear() => list.Clear();
        public int Count => list.Count;
        public bool IsReadOnly => false;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(T item)
        {
            foreach (T element in this)
            {
                if (EqualityComparer<T>.Default.Equals(element, item))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            foreach (T element in this)
            {
                array[arrayIndex] = element;
                arrayIndex++;
            }
        }

        public bool Remove(T item)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].TryGetTarget(out T target))
                {
                    //may as well remove dead refs while we're at it
                    list.RemoveAt(i);
                }
                else if (Equals(target, item))
                {
                    list.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].TryGetTarget(out T element))
                {
                    yield return element;
                }
                else
                {
                    list.RemoveAt(i);
                }
            }
        }
    }
}
