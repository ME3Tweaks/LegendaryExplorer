using ME3Script.Lexing.Matching;
using ME3Script.Lexing.Matching.StringMatchers;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing
{
    public class StringLexer : LexerBase<String>
    {
        private SourcePosition StreamPosition;

        public StringLexer(String code, List<KeywordMatcher> delimiters = null, List<KeywordMatcher> keywords = null) 
            : base(new StringTokenizer(code))
        {
            delimiters = delimiters ?? GlobalLists.Delimiters;
            keywords = keywords ?? GlobalLists.Keywords;

            TokenMatchers = new List<ITokenMatcher<String>>();

            TokenMatchers.Add(new StringLiteralMatcher());
            TokenMatchers.Add(new NameLiteralMatcher());
            TokenMatchers.AddRange(delimiters);
            TokenMatchers.AddRange(keywords);
            TokenMatchers.Add(new WhiteSpaceMatcher());
            TokenMatchers.Add(new NumberMatcher(delimiters));
            TokenMatchers.Add(new WordMatcher(delimiters));

            StreamPosition = new SourcePosition(0, 0, 0);
        }

        public override Token<String> GetNextToken()
        {
            if (Data.AtEnd())
            {
                return new Token<String>(TokenType.EOF);
            }

            Token<String> result =
                (from matcher in TokenMatchers
                 let token = matcher.MatchNext(Data, ref StreamPosition)
                 where token != null
                 select token).FirstOrDefault();

            if (result == null)
            {
                Data.Advance();
                return new Token<String>(TokenType.INVALID);
            }
            return result;
        }

        public override IEnumerable<Token<string>> LexData()
        {
            StreamPosition = new SourcePosition(0, 0, 0);
            var token = GetNextToken();
            while (token.Type != TokenType.EOF)
            {
                if (token.Type != TokenType.WhiteSpace)
                    yield return token;

                token = GetNextToken();
            }
        }
    }
}
