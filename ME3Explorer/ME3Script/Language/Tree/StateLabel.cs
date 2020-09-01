using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class StateLabel : Statement
    {
        public int StartOffset;
        public string Name;

        public StateLabel(string name, int offset, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.StateLabel, start, end)
        {
            StartOffset = offset;
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
