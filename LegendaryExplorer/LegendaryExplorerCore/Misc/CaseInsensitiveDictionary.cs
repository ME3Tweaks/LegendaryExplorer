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

        /// <summary>
        /// Initializes a case insensitive dictionary with a specific capacity
        /// </summary>
        /// <param name="capacity"></param>
        public CaseInsensitiveDictionary(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase)
        {
        }

        public CaseInsensitiveDictionary(IDictionary<string, TValue> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    public class CaseInsensitiveConcurrentDictionary<V>() : ConcurrentDictionary<string, V>(StringComparer.OrdinalIgnoreCase);
}
