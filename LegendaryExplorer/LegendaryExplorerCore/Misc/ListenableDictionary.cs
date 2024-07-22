using LegendaryExplorerCore.Packages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LegendaryExplorerCore.Misc
{
    public enum DictChangeType
    {
        AddItem,
        RemoveItem,
        Change
    }

    public class DictionaryChangedEvent<K, V> : EventArgs
    {
        public DictChangeType Type { get; set; }

        public K Key { get; set; }

        public V Value { get; set; }
    }

    /// <summary>
    /// A dictionary that can have a OnDictionaryChanged event handler attached.
    /// From https://stackoverflow.com/a/28039942
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class ListenableDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        public delegate void DictionaryChanged(object sender, DictionaryChangedEvent<TKey, TValue> e);

        public event DictionaryChanged OnDictionaryChanged;

        private IDictionary<TKey, TValue> innerDict;

        public ICollection<TKey> Keys => innerDict.Keys;

        public ICollection<TValue> Values => innerDict.Values;

        public int Count => innerDict.Count;

        public bool IsReadOnly => innerDict.IsReadOnly;

        public TValue this[TKey key]
        {
            get => innerDict[key];
            set
            {
                var count = innerDict.Count;
                innerDict[key] = value;
                if (OnDictionaryChanged != null)
                {
                    OnDictionaryChanged(this, new DictionaryChangedEvent<TKey, TValue>() { Type = count < innerDict.Count ? DictChangeType.AddItem : DictChangeType.Change, Key = key, Value = value });
                }
            }
        }

        public ListenableDictionary()
        {
            innerDict = new Dictionary<TKey, TValue>();
        }

        public ListenableDictionary(IDictionary<TKey, TValue> existingDictionary)
        {
            innerDict = new Dictionary<TKey, TValue>(existingDictionary);
        }

        public void Add(TKey key, TValue value)
        {
            innerDict.Add(key, value);
            OnDictionaryChanged?.Invoke(this, new DictionaryChangedEvent<TKey, TValue>() { Type = DictChangeType.AddItem, Key = key, Value = value });
        }

        public bool ContainsKey(TKey key)
        {
            return innerDict.ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            if (TryGetValue(key, out var removedItem))
            {
                OnDictionaryChanged?.Invoke(this, new DictionaryChangedEvent<TKey, TValue>() { Type = DictChangeType.RemoveItem, Key = key, Value = removedItem });
                return innerDict.Remove(key);
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return innerDict.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            innerDict.Add(item);
            OnDictionaryChanged?.Invoke(this, new DictionaryChangedEvent<TKey, TValue>() { Type = DictChangeType.AddItem, Key = item.Key, Value = item.Value });
        }

        public void Clear()
        {
            innerDict.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return innerDict.Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            innerDict.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            var removed = innerDict.Remove(item);
            OnDictionaryChanged?.Invoke(this, new DictionaryChangedEvent<TKey, TValue>() { Type = DictChangeType.RemoveItem, Key = item.Key, Value = item.Value });
            return removed;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return innerDict.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)innerDict).GetEnumerator();
        }

        // ...
        // ...
    }
}
