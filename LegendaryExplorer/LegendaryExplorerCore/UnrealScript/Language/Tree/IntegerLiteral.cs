using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class IntegerLiteral : Expression
    {
        public int Value;

        public string NumType = INT;

        public IntegerLiteral(int val, int start = -1, int end = -1) 
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
