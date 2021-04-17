using System;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Decompiling;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class Goto : UnconditionalJump
    {
        public string LabelName;

        public Label Label;

        public ForEachLoop ContainingForEach;

        public Goto(string labelName, SourcePosition start = null, SourcePosition end = null, ushort jumpLoc = 0) : base(jumpLoc)
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
