using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching.StringMatchers;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public class StringLexer : LexerBase<string>
    {
        private SourcePosition StreamPosition;
        private readonly MessageLog Log;

        public StringLexer(string code, MessageLog log = null, List<KeywordMatcher> delimiters = null, List<KeywordMatcher> keywords = null) 
            : base(new StringTokenizer(code))
        {
            delimiters ??= GlobalLists.Delimiters;
            Log = log ?? new MessageLog();

            TokenMatchers = new List<ITokenMatcher<string>>();

            TokenMatchers.Add(new SingleLineCommentMatcher());
            TokenMatchers.Add(new StringLiteralMatcher());
            TokenMatchers.Add(new NameLiteralMatcher());
            TokenMatchers.Add(new StringRefLiteralMatcher());
            TokenMatchers.AddRange(delimiters);
            TokenMatchers.Add(new WhiteSpaceMatcher());
            TokenMatchers.Add(new NumberMatcher(delimiters));
            TokenMatchers.Add(new WordMatcher(delimiters));

            StreamPosition = new SourcePosition(1, 0, 0);
        }

        public override Token<string> GetNextToken()
        {
            if (Data.AtEnd())
            {
                return new Token<string>(TokenType.EOF, null, StreamPosition, StreamPosition);
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
                return new Token<string>(TokenType.INVALID, Data.CurrentItem, StreamPosition, StreamPosition.GetModifiedPosition(0, 1, 1)) { SyntaxType = EF.ERROR };
            }
            return result;
        }

        public override IEnumerable<Token<string>> LexData()
        {
            StreamPosition = new SourcePosition(1, 0, 0);
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

        public override IEnumerable<Token<string>> LexSubData(SourcePosition start, SourcePosition end)
        {
            StreamPosition = start;
            Data.Advance(start.CharIndex);
            var token = GetNextToken();
            // TODO: this assumes well-formed subdata, fix?
            while (!token.StartPos.Equals(end))
            {
                if (token.Type != TokenType.WhiteSpace
                 && token.Type != TokenType.SingleLineComment
                 && token.Type != TokenType.MultiLineComment)
                    yield return token;

                token = GetNextToken();
            }
        }
    }
}
