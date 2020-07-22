using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ConditionalExpression : Expression
    {
        public Expression Condition;
        public Expression TrueExpression;
        public Expression FalseExpression;

        public ConditionalExpression(Expression cond, Expression first, Expression second, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.ConditionalExpression, start, end)
        {
            Condition = cond;
            TrueExpression = first;
            FalseExpression = second;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return TrueExpression.ResolveType();
        }
    }
}
