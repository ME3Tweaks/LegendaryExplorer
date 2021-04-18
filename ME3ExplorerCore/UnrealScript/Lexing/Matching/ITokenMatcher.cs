using ME3ExplorerCore.UnrealScript.Compiling.Errors;
using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Lexing.Matching
{
    public interface ITokenMatcher<T> where T : class
    {
        Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);
    }
}
