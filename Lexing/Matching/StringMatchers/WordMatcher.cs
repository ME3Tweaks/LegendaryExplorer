using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching.StringMatchers
{
    public class WordMatcher : TokenMatcherBase<String>
    {
        private List<KeywordMatcher> Delimiters;

        public WordMatcher(List<KeywordMatcher> delimiters)
        {
            Delimiters = delimiters == null ? new List<KeywordMatcher>() : delimiters;
        }

        protected override Token<String> Match(TokenizableDataStream<String> data)
        {
            String peek = data.CurrentItem;
            String word = null;
            while (!data.AtEnd() && !String.IsNullOrWhiteSpace(peek) 
                && Delimiters.All(d => d.Keyword != peek)
                && peek != "\"" && peek != "'")
            {
                word += peek;
                data.Advance();
                peek = data.CurrentItem;
            }

            return word != null ? new Token<String>(TokenType.Word, word) : null;
        }
    }
}
