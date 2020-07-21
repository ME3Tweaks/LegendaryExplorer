using ME3Script.Compiling.Errors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Matching
{
    public abstract class TokenMatcherBase<T> : ITokenMatcher<T> where T : class
    {
        protected abstract Token<T> Match(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);

        public Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log)
        {
            data.PushSnapshot();

            Token<T> token = Match(data, ref streamPos, log);
            if (token == null)
                data.PopSnapshot();
            else 
                data.DiscardSnapshot();

            return token;
        }
    }
}
