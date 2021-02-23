using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class AssertStatement : Statement
    {
        public Expression Condition;
        public AssertStatement(Expression condition, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.AssertStatement, start, end)
        {
            Condition = condition;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
