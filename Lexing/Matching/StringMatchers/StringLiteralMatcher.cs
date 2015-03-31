using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class StringLiteralMatcher : TokenMatcherBase<String>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos)
        {
            SourcePosition start = new SourcePosition(streamPos);
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

            if (value != null)
            {
                streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                SourcePosition end = new SourcePosition(streamPos);
                return new Token<String>(TokenType.String, value, start, end);
            }
            return null;
        }
    }
}
