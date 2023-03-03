using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Misc
{
    //todo: make a better implementation
    /// <summary>
    /// Embarrassingly bad implementation of a Dictionary where things are ordered like a list and there can be more than one value per key
    /// (It's just a List&lt;KeyValuePair&lt;TKey,TValue&gt;&gt;)
    /// </summary>
    [Obsolete("Use UMultiMap instead.", false)]
    [JsonArray]
    public class OrderedMultiValueDictionary<TKey, TValue> : List<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>
    {
        private readonly IEqualityComparer<TKey> KeyComparer = EqualityComparer<TKey>.Default;

        public OrderedMultiValueDictionary() { }
        public OrderedMultiValueDictionary(int capacity) : base(capacity) { }
        public OrderedMultiValueDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection) : base(collection) { }

        public bool ContainsKey(TKey key)
        {
            foreach (var kvp in this)
            {
                if (KeyComparer.Equals(kvp.Key, key))
                {
                    return true;
                }
            }
            return false;
        }

        public bool Remove(TKey key)
        {
            if (Count > 0)
            {
                int idx = Keys().IndexOf(key);
                if (idx >= 0)
                {
                    RemoveAt(idx);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            foreach (var kvp in this)
            {
                if (KeyComparer.Equals(kvp.Key, key))
                {
                    value = kvp.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public bool TryGetValue(Predicate<TKey> predicate, out TValue value)
        {
            foreach (var kvp in this)
            {
                if (predicate(kvp.Key))
                {
                    value = kvp.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public TValue this[TKey key]
        {
            get
            {
                foreach (var kvp in this)
                {
                    if (KeyComparer.Equals(kvp.Key, key))
                    {
                        return kvp.Value;
                    }
                }
                throw new ArgumentOutOfRangeException();
            }
            set
            {
                int idx = Count > 0 ? Keys().IndexOf(key) : -1;
                if (idx == -1)
                {
                    Add(key, value);
                }
                else
                {
                    RemoveAt(idx);
                    Insert(idx, new KeyValuePair<TKey, TValue>(key, value));
                }
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys().ToList();

        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values().ToList();

        public void Add(TKey key, TValue value)
        {
            Add(new KeyValuePair<TKey, TValue>(key, value));
        }

        public IEnumerable<TValue> Values()
        {
            foreach (var kvp in this)
            {
                yield return kvp.Value;
            }
        }

        public IEnumerable<TKey> Keys()
        {
            foreach (var kvp in this)
            {
                yield return kvp.Key;
            }
        }
    }
}
