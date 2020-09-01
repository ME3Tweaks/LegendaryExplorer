using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using ME3Script.Analysis.Symbols;

namespace ME3Script.Language.Tree
{
    public class BooleanLiteral : Expression
    {
        public bool Value;

        public BooleanLiteral(bool val, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.BooleanLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.BoolType;
        }
    }
}
