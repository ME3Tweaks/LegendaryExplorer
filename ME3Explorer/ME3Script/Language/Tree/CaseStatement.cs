using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class CaseStatement : Statement
    {
        public Expression Value;

        public CaseStatement(Expression expr, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.CaseStatement, start, end) 
        {
            Value = expr;
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
