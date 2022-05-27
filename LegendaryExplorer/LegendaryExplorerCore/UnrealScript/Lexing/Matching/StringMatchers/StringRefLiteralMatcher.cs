using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public static class StringRefLiteralMatcher
    {
        public static ScriptToken MatchStringRef(CharDataStream data, ref SourcePosition streamPos)
        {
            char peek = data.CurrentItem;
            if (peek != '$')
            {
                return null;
            }
            data.Advance();
            peek = data.CurrentItem;
            bool isNegative = false;
            if (peek == '-')
            {
                isNegative = true;
                data.Advance();
                peek = data.CurrentItem;
            }
            string number = null;
            while (!data.AtEnd() && peek.IsDigit())
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            if (number == null)
            {
                return null;
            }

            if (isNegative)
            {
                number = $"-{number}";
            }

            var start = new SourcePosition(streamPos);
            streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
            var end = new SourcePosition(streamPos);
            return new ScriptToken(TokenType.StringRefLiteral, number, start, end) { SyntaxType = EF.Number };
        }
    }
}
