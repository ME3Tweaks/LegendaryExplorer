using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching
{
    public interface ITokenMatcher<T> where T : class
    {
        Token<T> MatchNext(TokenizableDataStream<T> data, ref SourcePosition streamPos, MessageLog log);
    }
}
