using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class CaseStatement : Statement
    {
        public Expression Value;

        public CaseStatement(Expression expr, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.BreakStatement, start, end) 
        {
            Value = expr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
