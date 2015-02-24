using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class StringLiteralMatcher : TokenMatcherBase<String>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data)
        {
            String value = null;
            if (data.CurrentItem == "\"")
            {
                data.Advance();
                String prev = "";
                while (!data.AtEnd())
                {
                    if (data.CurrentItem == "\"" && prev != "\\")
                        break;
                    value += data.CurrentItem;
                    prev = data.CurrentItem;
                    data.Advance();
                }

                if (data.CurrentItem == "\"")
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
