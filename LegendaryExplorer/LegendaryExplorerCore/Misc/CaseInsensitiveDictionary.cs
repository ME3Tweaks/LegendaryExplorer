using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Misc
{
    public class CaseInsensitiveDictionary<TValue> : Dictionary<string, TValue>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public CaseInsensitiveDictionary(IDictionary<string, TValue> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    public class CaseInsensitiveConcurrentDictionary<V> : ConcurrentDictionary<string, V>
    {
        public CaseInsensitiveConcurrentDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
