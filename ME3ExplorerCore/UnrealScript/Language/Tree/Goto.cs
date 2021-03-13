using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Decompiling;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
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
