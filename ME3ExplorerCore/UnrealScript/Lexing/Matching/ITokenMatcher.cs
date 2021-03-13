using Unrealscript.Compiling.Errors;
using Unrealscript.Lexing.Tokenizing;
using Unrealscript.Utilities;

namespace Unrealscript.Lexing.Matching
{
    public interface ITokenMatcher<T> where T : class
    {
        Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);
    }
}
