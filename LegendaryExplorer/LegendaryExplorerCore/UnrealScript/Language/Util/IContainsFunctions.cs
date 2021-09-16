using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Language.Util
{
    public interface IContainsFunctions
    {
        public List<Function> Functions { get; }
    }
}
