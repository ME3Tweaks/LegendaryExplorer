using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class StringRefLiteralMatcher : TokenMatcherBase<string>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);

            string peek = data.CurrentItem;
            if (peek != "$")
            {
                return null;
            }
            data.Advance();
            string number = null;
            var regex = new Regex("[0-9]");
            while (!data.AtEnd() && regex.IsMatch(peek))
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            if (number == null)
            {
                return null;
            }

            streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
            SourcePosition end = new SourcePosition(streamPos);
            return new Token<string>(TokenType.StringRefLiteral, number, start, end);
        }
    }
}
