using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class WhiteSpaceMatcher : TokenMatcherBase<string>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);
            bool whiteSpace = false;
            int newlines = 0;
            int column = streamPos.Column;
            while (!data.AtEnd() && string.IsNullOrWhiteSpace(data.CurrentItem))
            {
                whiteSpace = true;
                if (data.CurrentItem == "\n")
                {
                    newlines++;
                    column = 0;
                }
                else
                    column++;
                data.Advance();
            }

            if (whiteSpace)
            {
                streamPos = new SourcePosition(start.Line + newlines, column, data.CurrentIndex);
                SourcePosition end = new SourcePosition(streamPos);
                return new Token<string>(TokenType.WhiteSpace, null, start, end);
            }
            return null;
        }
    }
}
