using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;
using static Unrealscript.Utilities.Keywords;

namespace Unrealscript.Language.Tree
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
