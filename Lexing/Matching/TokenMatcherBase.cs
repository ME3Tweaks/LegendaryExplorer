using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script
{
    public abstract class TokenMatcherBase<T> : ITokenMatcher<T> where T : class
    {
        protected abstract Token<T> Match(TokenizableDataStream<T> data);

        public Token<T> MatchNext(TokenizableDataStream<T> data)
        {
            data.PushSnapshot();

            Token<T> token = Match(data);
            if (token == null)
                data.PopSnapshot();
            else 
                data.DiscardSnapshot();

            return token;
        }
    }
}
