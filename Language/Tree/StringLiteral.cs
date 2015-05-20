using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StringLiteral : Expression
    {
        public String Value;

        public StringLiteral(String val, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.StringLiteral, start, end)
        {
            Value = val;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public override VariableType ResolveType()
        {
            return new VariableType("string", null, null);
        }
    }
}
