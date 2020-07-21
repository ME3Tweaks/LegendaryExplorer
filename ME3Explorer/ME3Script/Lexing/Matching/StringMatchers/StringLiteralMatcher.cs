using ME3Script.Compiling.Errors;
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
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
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
                    {
                        break;
                    }
                    else if (data.CurrentItem == "\n")
                    {
                        streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                        log.LogError("String Literals can not contain line breaks!", start, new SourcePosition(streamPos));
                        return null;
                    }
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
                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                    log.LogError("String Literal was not terminated properly!", start, new SourcePosition(streamPos));
                    return null;
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
