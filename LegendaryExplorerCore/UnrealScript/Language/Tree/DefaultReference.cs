using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DefaultReference : SymbolReference
    {
        public DefaultReference(ASTNode symbol, string name = "", SourcePosition start = null, SourcePosition end = null) : base(symbol, name, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
