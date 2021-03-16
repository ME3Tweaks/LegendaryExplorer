using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
