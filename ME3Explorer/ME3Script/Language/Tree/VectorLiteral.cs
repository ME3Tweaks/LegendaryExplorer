using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class VectorLiteral : Expression
    {
        public float X;
        public float Y;
        public float Z;

        public VectorLiteral(float x, float y, float z, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.VectorLiteral, start, end)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override VariableType ResolveType()
        {
            return new VariableType(Keywords.VECT);
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
