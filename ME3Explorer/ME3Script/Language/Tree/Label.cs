using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class Label : Statement
    {
        public ushort StartOffset;
        public string Name;

        public Label(string name, ushort offset, SourcePosition start = null, SourcePosition end = null)
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
