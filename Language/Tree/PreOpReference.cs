using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class PreOpReference : Expression
    {
        PreOpDeclaration Operator;
        Expression Operand;

        public PreOpReference(PreOpDeclaration op, Expression oper, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.InOpRef, start, end)
        {
            Operator = op;
            Operand = oper;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return Operator.ReturnType;
        }
    }
}
