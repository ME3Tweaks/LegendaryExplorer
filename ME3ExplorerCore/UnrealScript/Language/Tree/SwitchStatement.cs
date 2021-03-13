using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
