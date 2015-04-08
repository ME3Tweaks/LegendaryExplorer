using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class FloatLiteral : Expression
    {
        public float Value;

        public FloatLiteral(float val, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.FloatLiteral, start, end)
        {
            Value = val;
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
