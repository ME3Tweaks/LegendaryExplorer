using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DoUntilLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;

        public DoUntilLoop(Expression cond, CodeBody body,
            int start = -1, int end = -1)
            : base(ASTNodeType.DoUntilLoop, start, end)
        {
            Condition = cond;
            Body = body;
            cond.Outer = this;
            body.Outer = this;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Body;
                yield return Condition;
            }
        }
    }
}
