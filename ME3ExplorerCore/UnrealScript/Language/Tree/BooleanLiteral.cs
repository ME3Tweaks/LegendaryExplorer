using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
