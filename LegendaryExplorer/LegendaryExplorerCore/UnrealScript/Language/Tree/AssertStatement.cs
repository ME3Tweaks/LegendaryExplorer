using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class AssertStatement : Statement
    {
        public Expression Condition;
        public AssertStatement(Expression condition, int start = -1, int end = -1) : base(ASTNodeType.AssertStatement, start, end)
        {
            Condition = condition;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
    }
}
