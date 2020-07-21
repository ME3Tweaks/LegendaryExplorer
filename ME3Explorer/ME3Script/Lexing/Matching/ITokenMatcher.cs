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
    public interface ITokenMatcher<T> where T : class
    {
        Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);
    }
}
