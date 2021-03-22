using System.Collections.Generic;
using Unrealscript.Lexing.Matching;
using Unrealscript.Lexing.Tokenizing;
using Unrealscript.Utilities;

namespace Unrealscript.Lexing
{
    public abstract class LexerBase<T> where T : class
    {
        protected List<ITokenMatcher<T>> TokenMatchers;
        protected TokenizableDataStream<T> Data;

        protected LexerBase(TokenizableDataStream<T> data)
        {
            Data = data;
        }

        public abstract IEnumerable<Token<T>> LexData();
        public abstract IEnumerable<Token<T>> LexSubData(SourcePosition start, SourcePosition end);
        public abstract Token<T> GetNextToken();
    }
}
