using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public sealed class SymbolMatcher
    {
        public readonly string Keyword;
        private readonly TokenType Type;

        public SymbolMatcher(string keyword, TokenType type)
        {
            Type = type;
            Keyword = keyword;
        }

        public ScriptToken Match(CharDataStream data, ref SourcePosition streamPos)
        {
            foreach (char c in Keyword)
            {
                if (data.CurrentItem != c)
                {
                    return null;
                }
                data.Advance();
            }

            var start = new SourcePosition(streamPos);
            streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
            var end = new SourcePosition(streamPos);
            return new ScriptToken(Type, Keyword, start, end);
        }
    }
}
