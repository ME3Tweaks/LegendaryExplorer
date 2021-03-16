using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class StopStatement : Statement
    {
        public StopStatement(SourcePosition start, SourcePosition end)
            : base(ASTNodeType.StopStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
