using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class StateGoto : Statement
    {
        public Expression LabelExpression;

        public StateGoto(Expression labelExpr, int start = -1, int end = -1) : base(ASTNodeType.Goto, start, end)
        {
            LabelExpression = labelExpr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
