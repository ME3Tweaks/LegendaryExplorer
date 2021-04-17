using ME3ExplorerCore.UnrealScript.Analysis.Symbols;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
