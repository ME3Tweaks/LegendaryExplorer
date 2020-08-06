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
    public class DynamicArrayType : VariableType
    {
        public VariableType ElementType;

        public DynamicArrayType(VariableType elementType, SourcePosition start = null, SourcePosition end = null) : base("array", start, end)
        {
            ElementType = elementType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if (Declaration != null) yield return Declaration;
                yield return ElementType;
            }
        }
    }
}
