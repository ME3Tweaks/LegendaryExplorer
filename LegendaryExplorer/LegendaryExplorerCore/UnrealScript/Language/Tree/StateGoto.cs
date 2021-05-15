using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class StateGoto : Statement
    {
        public Expression LabelExpression;

        public StateGoto(Expression labelExpr, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.Goto, start, end)
        {
            LabelExpression = labelExpr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
