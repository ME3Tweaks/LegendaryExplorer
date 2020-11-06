using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using ME3Script.Analysis.Symbols;

namespace ME3Script.Language.Tree
{
    public class NameLiteral : Expression
    {
        public string Value;

        public NameLiteral(string val, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.NameLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType() => SymbolTable.NameType;
    }
}
