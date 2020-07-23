using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Language.Tree;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class Const : VariableType
    {
        public string Value;
        public Const(string name, string value, SourcePosition start, SourcePosition end) : base(name, start, end)
        {
            Type = ASTNodeType.Const;
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
