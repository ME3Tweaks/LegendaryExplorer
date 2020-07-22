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
    public class StringRefLiteral : Expression
    {
        public int Value;

        public StringRefLiteral(int val, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.StringRefLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return new VariableType("stringRef", null, null);
        }
    }
}
