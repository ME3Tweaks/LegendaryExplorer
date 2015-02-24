using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class NumberMatcher : TokenMatcherBase<String>
    {
        private List<KeywordMatcher> Delimiters;

        public NumberMatcher(List<KeywordMatcher> delimiters)
        {
            Delimiters = delimiters == null ? new List<KeywordMatcher>() : delimiters;
        }

        protected override Token<String> Match(TokenizableDataStream<String> data)
        {
            String first = SubNumber(data, new Regex("[0-9]"));
            if (first == null)
                return null;
            
            if (data.CurrentItem == "x")
            {
                if (first != "0")
                    return null;

                data.Advance();
                String hex = SubNumber(data, new Regex("[0-9a-fA-F]"));
                if (hex == null || data.CurrentItem == "." || data.CurrentItem == "x")
                    return null;

                hex = Convert.ToInt32(hex, 16).ToString("D");
                return new Token<String>(TokenType.IntegerNumber, hex);
            } 
            else if (data.CurrentItem == ".")
            {
                data.Advance();
                String second = SubNumber(data, new Regex("[0-9]"));
                if (second == null || data.CurrentItem == "." || data.CurrentItem == "x")
                    return null;

                return new Token<String>(TokenType.FloatingNumber, first + "." + second);
            }

            return new Token<String>(TokenType.IntegerNumber, first);
        }

        private String SubNumber(TokenizableDataStream<String> data, Regex regex)
        {
            String number = null;
            String peek = data.CurrentItem;
            
            while (!data.AtEnd() && regex.IsMatch(peek))
            {
                number += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            peek = data.CurrentItem;
            bool hasDelimiter = String.IsNullOrWhiteSpace(peek) || Delimiters.Any(c => c.Keyword == peek)
                || peek == "x" || peek == ".";
            return number != null && hasDelimiter ? number : null;
        }
    }
}
