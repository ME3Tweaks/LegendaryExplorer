using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class DoUntilLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;

        public DoUntilLoop(Expression cond, CodeBody body,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.WhileLoop, start, end)
        {
            Condition = cond;
            Body = body;
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
