using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class SwitchStatement : Statement
    {
        public Expression Expression;
        public CodeBody Body;

        public SwitchStatement(Expression expr, CodeBody body,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.SwitchStatement, start, end)
        {
            Expression = expr;
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
                yield return Expression;
                yield return Body;
            }
        }
    }
}
