using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    //todo: make a better implementation
    /// <summary>
    /// Embarrassingly bad implementation of a Dictionary where things are ordered like a list and there can be more than one value per key
    /// (It's just a List&lt;KeyValuePair&lt;TKey,TValue&gt;&gt;)
    /// </summary>
    public class OrderedMultiValueDictionary<TKey, TValue> : List<KeyValuePair<TKey, TValue>>
    {
        public OrderedMultiValueDictionary() : base() { }
        public OrderedMultiValueDictionary(int capacity) : base(capacity) { }
        public OrderedMultiValueDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection) : base(collection) { }

        public bool ContainsKey(TKey key)
        {
            foreach (var kvp in this)
            {
                if (EqualityComparer<TKey>.Default.Equals(kvp.Key, key))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            foreach (var kvp in this)
            {
                if (EqualityComparer<TKey>.Default.Equals(kvp.Key, key))
                {
                    value = kvp.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

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
    }
}
