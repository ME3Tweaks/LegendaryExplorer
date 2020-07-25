using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableType : ASTNode
    {
        public string Name;
        public ASTNode Declaration;
        public ASTNodeType NodeType => Declaration?.Type ?? ASTNodeType.INVALID;

        public VariableType(string name, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableType, start, end) 
        {
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Declaration;
            }
        }
    }
}
