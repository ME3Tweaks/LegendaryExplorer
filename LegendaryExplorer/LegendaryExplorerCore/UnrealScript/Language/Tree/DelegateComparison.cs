using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DelegateComparison : Expression
    {
        public bool IsEqual;
        public Expression LeftOperand;
        public Expression RightOperand;

        public int Precedence => IsEqual ? 24 : 26;

        public DelegateComparison(bool isEqual, Expression lhs, Expression rhs, int start = -1, int end = -1) : base(ASTNodeType.InfixOperator, start, end)
        {
            IsEqual = isEqual;
            LeftOperand = lhs;
            RightOperand = rhs;
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.BoolType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
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
