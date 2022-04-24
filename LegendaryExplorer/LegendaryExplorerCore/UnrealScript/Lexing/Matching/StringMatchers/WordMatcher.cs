using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public sealed class WordMatcher : TokenMatcherBase
    {
        public override ScriptToken Match(CharDataStream data, ref SourcePosition streamPos, MessageLog log)
        {
            return MatchWord(data, ref streamPos);
        }

        public static ScriptToken MatchWord(CharDataStream data, ref SourcePosition streamPos)
        {
            var startIndex = data.CurrentIndex;
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
            var length = data.CurrentIndex - startIndex;
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
