using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class WhiteSpaceMatcher : TokenMatcherBase<String>
    {
        protected override Token<String> Match(TokenizableDataStream<String> data)
        {
            bool whiteSpace = false;
            while (!data.AtEnd() && String.IsNullOrWhiteSpace(data.CurrentItem))
            {
                whiteSpace = true;
                data.Advance();
            }

            return whiteSpace ? new Token<String>(TokenType.WhiteSpace) : null;
        }
    }
}
