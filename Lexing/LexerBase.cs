using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script
{
    public abstract class LexerBase<T> where T : class
    {
        protected List<ITokenMatcher<T>> TokenMatchers;
        protected TokenizableDataStream<T> Data;

        public LexerBase(TokenizableDataStream<T> data)
        {
            Data = data;
        }

        public abstract IEnumerable<Token<T>> LexData();
        public abstract Token<T> GetNextToken();
    }
}
