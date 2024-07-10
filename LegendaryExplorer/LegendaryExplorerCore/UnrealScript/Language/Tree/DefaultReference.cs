using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DefaultReference : SymbolReference
    {
        public DefaultReference(ASTNode symbol, string name = "", int start = -1, int end = -1) : base(symbol, name, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
