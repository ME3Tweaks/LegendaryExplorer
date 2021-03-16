using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class BreakStatement : Statement
    {
        public BreakStatement(SourcePosition start = null, SourcePosition end = null) 
            : base(ASTNodeType.BreakStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
