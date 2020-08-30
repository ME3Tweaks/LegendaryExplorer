using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public class CaseInsensitiveDictionary<V> : Dictionary<string, V>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
