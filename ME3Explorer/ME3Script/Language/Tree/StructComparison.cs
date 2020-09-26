using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Symbols;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class StructComparison : Expression
    {
        public bool IsEqual;
        public Expression LeftOperand;
        public Expression RightOperand;

        public Struct Struct;

        public int Precedence => IsEqual ? 24 : 26;

        public StructComparison(bool isEqual, Expression lhs, Expression rhs, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.InfixOperator, start, end)
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
