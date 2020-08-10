using ME3Script.Compiling.Errors;
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
    public class StringLexer : LexerBase<string>
    {
        private SourcePosition StreamPosition;
        private readonly MessageLog Log;

        public StringLexer(string code, MessageLog log = null, List<KeywordMatcher> delimiters = null, List<KeywordMatcher> keywords = null) 
            : base(new StringTokenizer(code))
        {
            delimiters ??= GlobalLists.Delimiters;
            //keywords ??= GlobalLists.Keywords;
            Log = log ?? new MessageLog();

            TokenMatchers = new List<ITokenMatcher<string>>();

            TokenMatchers.Add(new SingleLineCommentMatcher());
            TokenMatchers.Add(new StringLiteralMatcher());
            TokenMatchers.Add(new NameLiteralMatcher());
            TokenMatchers.AddRange(delimiters);
            //TokenMatchers.AddRange(keywords);
            TokenMatchers.Add(new WhiteSpaceMatcher());
            TokenMatchers.Add(new NumberMatcher(delimiters));
            TokenMatchers.Add(new WordMatcher(delimiters));

            StreamPosition = new SourcePosition(1, 0, 0);
        }

        public override Token<string> GetNextToken()
        {
            if (Data.AtEnd())
            {
                return new Token<string>(TokenType.EOF);
            }

            Token<string> result = null;
            foreach (ITokenMatcher<string> matcher in TokenMatchers)
            {
                Token<string> token = matcher.MatchNext(Data, ref StreamPosition, Log);
                if (token != null)
                {
                    result = token;
                    break;
                }
            }

            if (result == null)
            {
                Log.LogError("Could not lex '" + Data.CurrentItem + "'",
                    StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1));
                Data.Advance();
                return new Token<string>(TokenType.INVALID);
            }
            return result;
        }

        public override IEnumerable<Token<string>> LexData()
        {
            StreamPosition = new SourcePosition(0, 0, 0);
            var token = GetNextToken();
            while (token.Type != TokenType.EOF)
            {
                if (token.Type != TokenType.WhiteSpace 
                 && token.Type != TokenType.SingleLineComment 
                 && token.Type != TokenType.MultiLineComment)
                    yield return token;

                token = GetNextToken();
            }
        }

        public IEnumerable<Token<string>> LexSubData(SourcePosition start, SourcePosition end)
        {
            StreamPosition = start;
            Data.Advance(start.CharIndex);
            var token = GetNextToken();
            // TODO: this assumes well-formed subdata, fix?
            while (!token.StartPosition.Equals(end))
            {
                if (token.Type != TokenType.WhiteSpace)
                    yield return token;

                token = GetNextToken();
            }
        }
    }
}
