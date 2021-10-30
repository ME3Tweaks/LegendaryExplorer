using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public sealed class WhiteSpaceMatcher : TokenMatcherBase
    {
        public override ScriptToken Match(CharDataStream data, ref SourcePosition streamPos, MessageLog log)
        {
            return MatchWhiteSpace(data, ref streamPos);
        }

        public static ScriptToken MatchWhiteSpace(CharDataStream data, ref SourcePosition streamPos)
        {
            bool whiteSpace = false;
            int newlines = 0;
            int column = streamPos.Column;
            while (!data.AtEnd() && char.IsWhiteSpace(data.CurrentItem))
            {
                whiteSpace = true;
                if (data.CurrentItem == '\n')
                {
                    newlines++;
                    column = 0;
                }
                else
                {
                    ++column;
                }
                data.Advance();
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
