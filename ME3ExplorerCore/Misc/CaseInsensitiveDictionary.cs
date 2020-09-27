using System;
using System.Collections.Generic;

namespace ME3ExplorerCore.Misc
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
}
