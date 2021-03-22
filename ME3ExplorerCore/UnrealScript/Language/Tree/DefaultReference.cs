using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class DefaultReference : SymbolReference
    {
        public DefaultReference(ASTNode symbol, string name = "", SourcePosition start = null, SourcePosition end = null) : base(symbol, name, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
