using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
