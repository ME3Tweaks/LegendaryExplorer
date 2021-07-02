using ME3ExplorerCore.UnrealScript.Analysis.Symbols;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;
using static ME3ExplorerCore.UnrealScript.Utilities.Keywords;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
