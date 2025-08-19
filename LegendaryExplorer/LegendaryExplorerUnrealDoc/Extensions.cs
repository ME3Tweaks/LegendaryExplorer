using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerUnrealDoc
{
    public static class Extensions
    {
        public static IEnumerable<Enum> GetFlags(this Enum input)
        {
            foreach (Enum value in Enum.GetValues(input.GetType()))
                if (Convert.ToUInt64(value) != 0 && input.HasFlag(value))
                    yield return value;
        }
    }
}
