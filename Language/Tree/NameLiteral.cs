using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class NameLiteral : Expression
    {
        public String Value;

        public NameLiteral(String val, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.NameLiteral, start, end)
        {
            Value = val;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
