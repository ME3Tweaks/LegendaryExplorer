using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
