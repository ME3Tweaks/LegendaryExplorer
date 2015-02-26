using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace ME3Script.Lexing.Tokenizing
{
    public class StringTokenizer : TokenizableDataStream<String>
    {
        public StringTokenizer(String data) : base(
            () => data.ToCharArray()
                .Select(i => i.ToString(CultureInfo.InvariantCulture))
                .ToList())
        {

        }
    }
}
