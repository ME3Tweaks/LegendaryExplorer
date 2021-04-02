using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class StringRefLiteral : Expression
    {
        public int Value;

        public StringRefLiteral(int val, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.StringRefLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType() => SymbolTable.StringRefType;
    }
}
