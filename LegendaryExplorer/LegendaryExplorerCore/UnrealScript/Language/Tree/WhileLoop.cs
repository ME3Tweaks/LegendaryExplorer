using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class WhileLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;

        public WhileLoop(Expression cond, CodeBody body,
            int start = -1, int end = -1)
            : base(ASTNodeType.WhileLoop, start, end)
        {
            Condition = cond;
            Body = body;
            cond.Outer = this;
            body.Outer = this;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Condition;
                yield return Body;
            }
        }
    }
}
