using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class ReturnStatement : Statement
    {
        public Expression Value;

        public ReturnStatement(Expression value = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.ReturnStatement, start, end)
        {
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Value;
            }
        }
    }
}
