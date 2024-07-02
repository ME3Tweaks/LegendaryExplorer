using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class BreakStatement : Statement
    {
        public BreakStatement(int start = -1, int end = -1) 
            : base(ASTNodeType.BreakStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
