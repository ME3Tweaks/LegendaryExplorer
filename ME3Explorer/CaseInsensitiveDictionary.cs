using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
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
