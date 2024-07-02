using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DynamicArrayLiteral : Expression
    {
        public readonly List<Expression> Values;

        public readonly DynamicArrayType ArrayType;
        public DynamicArrayLiteral(DynamicArrayType arrayType, List<Expression> values, int start = -1, int end = -1) : base(ASTNodeType.DynamicArrayLiteral, start, end)
        {
            ArrayType = arrayType;
            Values = values;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType() => ArrayType;

        public override IEnumerable<ASTNode> ChildNodes => Values;
    }
}
