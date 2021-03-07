using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ME3ExplorerCore.Helpers;
using ME3Script.Analysis.Visitors;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class NumberMatcher : TokenMatcherBase<string>
    {
        private readonly List<KeywordMatcher> Delimiters;
        private readonly Regex digits = new Regex("[0-9]", RegexOptions.Compiled);
        private readonly Regex hexDigits = new Regex("[0-9a-fA-F]", RegexOptions.Compiled);

        public NumberMatcher(List<KeywordMatcher> delimiters)
        {
            Delimiters = delimiters ?? new List<KeywordMatcher>();
        }

        protected override Token<string> Match(TokenizableDataStream<string> data, ref SourcePosition streamPos, MessageLog log)
        {
            SourcePosition start = new SourcePosition(streamPos);
            TokenType type;
            string value;

            string first = SubNumber(data, digits);
            if (first == null)
                return null;
            
            if (data.CurrentItem == "x")
            {
                if (first != "0")
                    return null;

                data.Advance();
                string hex = SubNumber(data, hexDigits);
                if (hex == null || data.CurrentItem == "." || data.CurrentItem == "x")
                    return null;

                hex = Convert.ToInt32(hex, 16).ToString("D");
                type = TokenType.IntegerNumber;
                value = hex;
            } 
            else if (data.CurrentItem == "." || data.CurrentItem.CaseInsensitiveEquals("e") || data.CurrentItem.CaseInsensitiveEquals("d"))
            {
                type = TokenType.FloatingNumber;
                string second = null;
                if (data.CurrentItem == ".")
                {
                    data.Advance();
                    second = SubNumber(data, digits);
                }
                if (data.CurrentItem.CaseInsensitiveEquals("e") || data.CurrentItem.CaseInsensitiveEquals("d"))
                {
                    data.Advance();
                    string exponent = SubNumber(data, digits);
                    if (exponent == null || data.CurrentItem == "." || data.CurrentItem == "x")
                        return null;
                    value = $"{first}.{second ?? "0"}e{exponent}";
                }
                else if (second == null && data.CurrentItem == "f")
                {
                    data.Advance();
                    value = $"{first}.0";
                }
                else
                {
                    if (second == null && data.CurrentItem == "f")
                    {
                        data.Advance();
                    }
                    if (second == null || data.CurrentItem == "." || data.CurrentItem == "x")
                        return null;

                    value = $"{first}.{second}";
                }

                if (data.CurrentItem == "f")
                {
                    data.Advance();
                }
            }
            else
            {
                type = TokenType.IntegerNumber;
                value = first;
            }

            string peek = data.CurrentItem;
            bool hasDelimiter = string.IsNullOrWhiteSpace(peek) || Delimiters.Any(c => c.Keyword == peek);
            if (!hasDelimiter)
            {
                return null;
            }
            streamPos = streamPos.GetModifiedPosition(0, data.CurrentIndex - start.CharIndex, data.CurrentIndex - start.CharIndex);
            SourcePosition end = new SourcePosition(streamPos);
            return new Token<string>(type, value, start, end) {SyntaxType = EF.Number};
        }

        private static string SubNumber(TokenizableDataStream<string> data, Regex regex)
        {
            string number = null;
            string peek = data.CurrentItem;
            
            while (!data.AtEnd() && regex.IsMatch(peek))
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            return number;
        }
    }
}
