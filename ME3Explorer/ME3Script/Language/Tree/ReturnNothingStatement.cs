using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class ReturnNothingStatement : ReturnStatement
    {
        public ReturnNothingStatement(SourcePosition start = null, SourcePosition end = null, Expression value = null) : base(start, end, value)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
