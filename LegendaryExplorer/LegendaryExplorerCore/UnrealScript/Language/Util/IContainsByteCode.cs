using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript.Language.Util
{
    public interface IContainsByteCode
    {
        public CodeBody Body { get; set; }

        public TokenStream Tokens { get; }
    }
}
