using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Matching
{
    public abstract class TokenMatcherBase
    {
        public abstract ScriptToken Match(CharDataStream data, ref SourcePosition streamPos, MessageLog log);
    }
}
