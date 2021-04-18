using System.Globalization;
using System.Linq;

namespace ME3ExplorerCore.UnrealScript.Lexing.Tokenizing
{
    public class StringTokenizer : TokenizableDataStream<string>
    {
        public StringTokenizer(string data) : base(
            () => data.ToCharArray()
                .Select(i => i.ToString(CultureInfo.InvariantCulture))
                .ToList())
        {

        }
    }
}
