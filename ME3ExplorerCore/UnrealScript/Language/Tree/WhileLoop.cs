using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class WhileLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;

        public WhileLoop(Expression cond, CodeBody body,
            SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.WhileLoop, start, end)
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
                yield return Condition;
                yield return Body;
            }
        }
    }
}
