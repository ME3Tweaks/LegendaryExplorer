using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Utilities;
using Unrealscript.Compiling.Errors;

namespace Unrealscript.Lexing.Matching
{
    public interface ITokenMatcher<T> where T : class
    {
        Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);
    }
}
