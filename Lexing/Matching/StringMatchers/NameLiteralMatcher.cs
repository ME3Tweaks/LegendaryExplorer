using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class NameLiteralMatcher : TokenMatcherBase<String>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data)
        {
            String value = null;
            Regex regex = new Regex("[0-9a-zA-Z_]");
            if (data.CurrentItem == "'")
            {
                data.Advance();
                while (!data.AtEnd() && regex.IsMatch(data.CurrentItem))
                {
                    value += data.CurrentItem;
                    data.Advance();
                }

                if (data.CurrentItem == "'")
                {
                    data.Advance();
                    if (value == null)
                        value = "";
                }
                else
                {
                    value = null;
                }
            }

            return value == null ? null : new Token<String>(TokenType.String, value);
        }
    }
}
