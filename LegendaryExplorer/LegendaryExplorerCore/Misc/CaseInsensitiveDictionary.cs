using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Misc
{
    /// <summary>
    /// A dictionary with string keys that performs case-insensitive comparisons.
    /// This class wraps <see cref="Dictionary{TKey, TValue}"/> with <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    public class CaseInsensitiveDictionary<TValue> : Dictionary<string, TValue>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CaseInsensitiveDictionary{TValue}"/> class that is empty.
        /// </summary>
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CaseInsensitiveDictionary{TValue}"/> class with a specific initial capacity.
        /// </summary>
        /// <param name="capacity">The initial number of elements that the dictionary can contain.</param>
        public CaseInsensitiveDictionary(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CaseInsensitiveDictionary{TValue}"/> class that contains elements copied from the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose elements are copied to the new dictionary.</param>
        public CaseInsensitiveDictionary(IDictionary<string, TValue> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    /// <summary>
    /// A thread-safe dictionary with string keys that performs case-insensitive comparisons.
    /// This class wraps <see cref="ConcurrentDictionary{TKey, TValue}"/> with <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <typeparam name="V">The type of values in the dictionary.</typeparam>
    public class CaseInsensitiveConcurrentDictionary<V>() : ConcurrentDictionary<string, V>(StringComparer.OrdinalIgnoreCase);
}
