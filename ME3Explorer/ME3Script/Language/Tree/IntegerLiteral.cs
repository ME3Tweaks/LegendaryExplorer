using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using ME3Script.Analysis.Symbols;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Language.Tree
{
    public class IntegerLiteral : Expression
    {
        public int Value;

        public string NumType = INT;

        public IntegerLiteral(int val, SourcePosition start = null, SourcePosition end = null) 
            : base(ASTNodeType.IntegerLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType() =>
            NumType switch
            {
                BYTE => SymbolTable.ByteType,
                BIOMASK4 => SymbolTable.BioMask4Type,
                _ => SymbolTable.IntType
            };
    }
}
