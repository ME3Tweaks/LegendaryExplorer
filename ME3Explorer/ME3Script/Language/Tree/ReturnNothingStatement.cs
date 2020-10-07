using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class ReturnNothingStatement : ReturnStatement
    {
        public ReturnNothingStatement(Expression value = null, SourcePosition start = null, SourcePosition end = null) : base(value, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
