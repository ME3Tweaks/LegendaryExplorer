using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class KeywordMatcher : TokenMatcherBase<String>
    {
        public String Keyword { get; private set; }
        private TokenType Type;
        private List<KeywordMatcher> Delimiters;
        private bool SubString;

        public KeywordMatcher(String keyword, TokenType type, List<KeywordMatcher> delims, bool allowSubString = true)
        {
            Type = type;
            Keyword = keyword;
            Delimiters = delims == null ? new List<KeywordMatcher>() : delims;
            SubString = allowSubString;
        }

        protected override Token<String> Match(TokenizableDataStream<String> data)
        {
            foreach (char c in Keyword)
            {
                if (data.CurrentItem != c.ToString(CultureInfo.InvariantCulture))
                    return null;
                data.Advance();
            }

            String peek = data.CurrentItem;
            bool hasDelimiter = String.IsNullOrWhiteSpace(peek) || Delimiters.Any(c => c.Keyword == peek);
            if (SubString || (!SubString && hasDelimiter))
            {
                return new Token<String>(Type, Keyword);
            }
            return null;
        }
    }
}
