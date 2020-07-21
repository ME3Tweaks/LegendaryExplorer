using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
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
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);
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
                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                    log.LogError("Name Literal was not terminated properly!", start, new SourcePosition(streamPos));
                    return null;
                }
            }

            if (value != null)
            {
                streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                SourcePosition end = new SourcePosition(streamPos);
                return new Token<String>(TokenType.Name, value, start, end);
            }
            return null;
        }
    }
}
