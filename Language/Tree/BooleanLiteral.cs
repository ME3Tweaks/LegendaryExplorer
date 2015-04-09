using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class BooleanLiteral : Expression
    {
        public bool Value;

        public BooleanLiteral(bool val, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.BooleanLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
