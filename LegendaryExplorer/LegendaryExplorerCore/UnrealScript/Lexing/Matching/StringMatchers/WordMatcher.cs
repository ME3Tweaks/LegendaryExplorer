using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public static class WordMatcher
    {
        public static ScriptToken MatchWord(CharDataStream data, ref SourcePosition streamPos)
        {
            int startIndex = data.CurrentIndex;
            char peek = data.CurrentItem;
            loopStart:
            while (!data.AtEnd() && !char.IsWhiteSpace(peek) && !GlobalLists.IsDelimiterChar(peek) && peek != '"' && peek != '\'')
            {
                data.Advance();
                peek = data.CurrentItem;
            }

            //HACK: there are variable names that include the c++ scope operator '::' for some godforsaken reason
            if (peek == ':' && data.LookAhead(1) == ':')
            {
                data.Advance(2);
                peek = data.CurrentItem;
                goto loopStart;
            }
            int length = data.CurrentIndex - startIndex;
            if (length != 0)
            {
                var start = new SourcePosition(streamPos);
                streamPos = streamPos.GetModifiedPosition(0, length, length);
                var end = new SourcePosition(streamPos);
                return new ScriptToken(TokenType.Word, data.Slice(startIndex, length), start, end);
            }
            return null;
        }
    }
}
