using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Decompiling;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Goto : UnconditionalJump
    {
        public string LabelName;

        public Label Label;

        public ForEachLoop ContainingForEach;

        public Goto(string labelName, int start = -1, int end = -1, ushort jumpLoc = 0) : base(jumpLoc)
        {
            Type = ASTNodeType.Goto;
            StartPos = start;
            EndPos = end;
            LabelName = labelName;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
