using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class NoneLiteral : Expression
    {
        public bool IsDelegateNone;

        public NoneLiteral(SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.NoneLiteral, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return null;
        }
    }
}
