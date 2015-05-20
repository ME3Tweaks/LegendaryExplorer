using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class InOpReference : Expression
    {
        InOpDeclaration Operator;
        Expression LeftOperand;
        Expression RightOperand;

        public InOpReference(InOpDeclaration op, Expression lhs, Expression rhs, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.InOpRef, start, end) 
        {
            Operator = op;
            LeftOperand = lhs;
            RightOperand = rhs;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public override VariableType ResolveType()
        {
            return Operator.ReturnType;
        }
    }
}
