using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class RotatorLiteral : Expression
    {
        public int Pitch;
        public int Yaw;
        public int Roll;

        public RotatorLiteral(int pitch, int yaw, int roll, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.RotatorLiteral, start, end)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }

        public override VariableType ResolveType()
        {
            return  new VariableType(Keywords.ROT);
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
