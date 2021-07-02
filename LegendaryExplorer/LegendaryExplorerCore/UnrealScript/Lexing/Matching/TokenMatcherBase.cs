using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching
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
