using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class VariableType : ASTNode
    {
        public string Name;
        public ASTNode Declaration;
        public virtual ASTNodeType NodeType => Declaration?.Type ?? ASTNodeType.INVALID;

        public EPropertyType PropertyType;

        public VariableType(string name, SourcePosition start = null, SourcePosition end = null, EPropertyType propType = EPropertyType.None)
            : base(ASTNodeType.VariableType, start, end) 
        {
            Name = name;
            PropertyType = propType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if(Declaration != null) yield return Declaration;
            }
        }
    }
}
