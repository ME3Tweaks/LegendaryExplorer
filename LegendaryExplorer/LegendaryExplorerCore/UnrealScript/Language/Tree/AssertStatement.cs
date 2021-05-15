using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class AssertStatement : Statement
    {
        public Expression Condition;
        public AssertStatement(Expression condition, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.AssertStatement, start, end)
        {
            Condition = condition;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
