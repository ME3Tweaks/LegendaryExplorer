using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
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
