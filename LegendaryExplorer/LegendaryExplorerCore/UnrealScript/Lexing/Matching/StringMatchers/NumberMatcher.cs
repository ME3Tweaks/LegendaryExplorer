using System;
using System.Globalization;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers
{
    public static class NumberMatcher
    {
        public static ScriptToken MatchNumber(CharDataStream data, ref SourcePosition streamPos)
        {
            string first = SubNumberDec(data);
            if (first == null)
                return null;

            TokenType type;
            string value;
            char peek = data.CurrentItem;
            if (peek == 'x')
            {
                if (first != "0")
                    return null;

                data.Advance();
                string hex = SubNumberHex(data);
                peek = data.CurrentItem;
                if (hex == null || peek is '.' or 'x')
                    return null;

                type = TokenType.IntegerNumber;
                value = Convert.ToInt32(hex, 16).ToString("D", CultureInfo.InvariantCulture);
            }
            else if (peek == '.' || peek.CaseInsensitiveEquals('e') || peek.CaseInsensitiveEquals('d'))
            {
                type = TokenType.FloatingNumber;
                string second = null;
                if (peek == '.')
                {
                    data.Advance();
                    second = SubNumberDec(data);
                    peek = data.CurrentItem;
                }
                if (peek.CaseInsensitiveEquals('e') || peek.CaseInsensitiveEquals('d'))
                {
                    data.Advance();
                    string exponent = SubNumberDec(data);
                    peek = data.CurrentItem;
                    if (exponent == null || peek is '.' or 'x')
                        return null;
                    value = $"{first}.{second ?? "0"}e{exponent}";
                }
                else if (second == null && peek == 'f')
                {
                    data.Advance();
                    peek = data.CurrentItem;
                    value = $"{first}.0";
                }
                else
                {
                    if (second == null && peek == 'f')
                    {
                        data.Advance();
                        peek = data.CurrentItem;
                    }
                    if (second == null || peek is '.' or 'x')
                        return null;

                    value = $"{first}.{second}";
                }

                if (data.CurrentItem == 'f')
                {
                    data.Advance();
                    peek = data.CurrentItem;
                }
            }
            else
            {
                type = TokenType.IntegerNumber;
                value = first;
            }

            if (!peek.IsNullOrWhiteSpace() && !GlobalLists.IsDelimiterChar(peek))
            {
                return null;
            }
            var start = new SourcePosition(streamPos);
            streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
            var end = new SourcePosition(streamPos);
            return new ScriptToken(type, value, start, end) { SyntaxType = EF.Number };
        }

        private static string SubNumberHex(CharDataStream data)
        {
            string number = null;
            char peek = data.CurrentItem;
            
            while (!data.AtEnd() && Uri.IsHexDigit(peek))
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            return number;
        }

        private static string SubNumberDec(CharDataStream data)
        {
            string number = null;
            char peek = data.CurrentItem;

            while (!data.AtEnd() && peek.IsDigit())
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            return number;
        }
    }
}
