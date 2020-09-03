using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class PreOpReference : Expression
    {
        public PreOpDeclaration Operator;
        public Expression Operand;

        public PreOpReference(PreOpDeclaration op, Expression oper, SourcePosition start = null, SourcePosition end = null)
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
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Operand;
            }
        }
    }
}
