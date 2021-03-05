using ME3Script.Analysis.Visitors;
using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class SingleLineCommentMatcher : TokenMatcherBase<string>
    {
        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);
            string comment = null;
            if (data.CurrentItem == "/")
            {
                data.Advance();
                if (data.CurrentItem == "/")
                {
                    data.Advance();
                    while (!data.AtEnd())
                    {
                        if (data.CurrentItem == "\n")
                        {
                            break;
                        }

                        comment += data.CurrentItem;
                        data.Advance();
                    }


                    comment ??= "";


                    streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                    SourcePosition end = new SourcePosition(streamPos);
                    return new Token<string>(TokenType.SingleLineComment, comment, start, end) {SyntaxType = EF.Comment};
                }
            }

            return null;
        }
    }
}
