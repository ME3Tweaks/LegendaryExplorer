using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class DefaultStatement : Statement
    {
        public DefaultStatement(SourcePosition start, SourcePosition end)
            : base(ASTNodeType.BreakStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
