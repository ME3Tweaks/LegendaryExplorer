using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class InOpReference : Expression
    {
        public InOpDeclaration Operator;
        public Expression LeftOperand;
        public Expression RightOperand;

        public InOpReference(InOpDeclaration op, Expression lhs, Expression rhs, int start = -1, int end = -1)
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
