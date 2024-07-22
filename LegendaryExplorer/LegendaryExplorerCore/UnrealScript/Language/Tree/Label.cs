using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Label : Statement
    {
        public ushort StartOffset;
        public string Name;

        public Label(string name, ushort offset, int start = -1, int end = -1)
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
