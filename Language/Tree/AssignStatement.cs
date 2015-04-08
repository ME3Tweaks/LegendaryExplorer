using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class AssignStatement : Statement
    {
        public SymbolReference Target;
        public Expression Value;
        public AssignStatement(SymbolReference target, Expression value,
            SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.AssignStatement, start, end) 
        {
            Target = target;
            Value = value;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
