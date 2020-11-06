using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using ME3Script.Analysis.Symbols;

namespace ME3Script.Language.Tree
{
    public class FloatLiteral : Expression
    {
        public float Value;

        public FloatLiteral(float val, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.FloatLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType() => SymbolTable.FloatType;
    }
}
