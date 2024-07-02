using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class AssertStatement : Statement
    {
        public Expression Condition;
        public AssertStatement(Expression condition, int start = -1, int end = -1) : base(ASTNodeType.AssertStatement, start, end)
        {
            Condition = condition;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
