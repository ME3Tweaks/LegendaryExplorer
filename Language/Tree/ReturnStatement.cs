using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ReturnStatement : Statement
    {
        public Expression Value;

        public ReturnStatement(SourcePosition start, SourcePosition end, Expression value = null)
            : base(ASTNodeType.ReturnStatement, start, end)
        {
            Value = value;
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
