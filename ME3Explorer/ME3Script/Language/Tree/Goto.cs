using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class Goto : Statement
    {
        public string LabelName;

        public Label Label;

        public ForEachLoop ContainingForEach;

        public Goto(string labelName, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.Goto, start, end)
        {
            LabelName = labelName;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
