using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
