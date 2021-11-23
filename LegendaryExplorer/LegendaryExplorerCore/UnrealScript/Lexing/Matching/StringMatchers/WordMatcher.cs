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
            char peek = data.CurrentItem;
            string word = null;
            loopStart:
            while (!data.AtEnd() && !char.IsWhiteSpace(peek) && !GlobalLists.IsDelimiterChar(peek) && peek != '"' && peek != '\'')
            {
                word += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            //HACK: there are variable names that include the c++ scope operator '::' for some godforsaken reason
            if (peek == ':' && data.LookAhead(1) == ':')
            {
                word += peek;
                data.Advance();
                peek = data.CurrentItem;
                word += peek;
                data.Advance();
                peek = data.CurrentItem;
                goto loopStart;
            }

            if (word != null)
            {
                var start = new SourcePosition(streamPos);
                streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
                var end = new SourcePosition(streamPos);
                return new ScriptToken(TokenType.Word, word, start, end);
            }
            return null;
        }
    }
}
