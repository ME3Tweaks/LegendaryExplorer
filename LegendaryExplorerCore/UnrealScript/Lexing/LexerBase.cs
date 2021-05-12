using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Lexing.Matching;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing
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
