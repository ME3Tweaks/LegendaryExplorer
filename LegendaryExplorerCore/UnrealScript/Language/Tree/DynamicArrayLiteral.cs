using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DynamicArrayLiteral : Expression
    {
        public List<Expression> Values;

        public string ElementType;
        public DynamicArrayLiteral(string elementType, List<Expression> values, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.DynamicArrayLiteral, start, end)
        {
            ElementType = elementType;
            Values = values;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return new VariableType(ElementType, null, null);
        }
        public override IEnumerable<ASTNode> ChildNodes => Values;
    }
}
