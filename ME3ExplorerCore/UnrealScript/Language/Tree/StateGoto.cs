using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
