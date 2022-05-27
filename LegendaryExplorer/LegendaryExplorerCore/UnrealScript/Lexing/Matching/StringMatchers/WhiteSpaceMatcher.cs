using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public static class WhiteSpaceMatcher
    {
        public static ScriptToken MatchWhiteSpace(CharDataStream data, ref SourcePosition streamPos)
        {
            bool whiteSpace = false;
            int newlines = 0;
            int column = streamPos.Column;
            char peek = data.CurrentItem;
            while (!data.AtEnd() && char.IsWhiteSpace(peek))
            {
                whiteSpace = true;
                if (peek == '\n')
                {
                    newlines++;
                    column = 0;
                }
                else
                {
                    ++column;
                }
                data.Advance();
                peek = data.CurrentItem;
            }

            if (whiteSpace)
            {
                var start = new SourcePosition(streamPos);
                streamPos = new SourcePosition(start.Line + newlines, column, data.CurrentIndex);
                var end = new SourcePosition(streamPos);
                return new ScriptToken(TokenType.WhiteSpace, null, start, end);
            }
            return null;
        }
    }
}
