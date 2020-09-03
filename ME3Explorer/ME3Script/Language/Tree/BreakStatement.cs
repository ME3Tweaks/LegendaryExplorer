using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
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
