using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
