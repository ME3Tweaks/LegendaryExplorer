using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class InOpReference : Expression
    {
        public InOpDeclaration Operator;
        public Expression LeftOperand;
        public Expression RightOperand;

        public InOpReference(InOpDeclaration op, Expression lhs, Expression rhs, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.InOpRef, start, end) 
        {
            Operator = op;
            LeftOperand = lhs;
            RightOperand = rhs;
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
                yield return LeftOperand;
                yield return RightOperand;
            }
        }
    }
}
