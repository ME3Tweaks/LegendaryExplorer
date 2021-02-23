using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class StateGoto : Statement
    {
        public Expression LabelExpression;

        public StateGoto(Expression labelExpr, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.Goto, start, end)
        {
            LabelExpression = labelExpr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
