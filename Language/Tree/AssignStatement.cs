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
        public Variable Target;
        public Expression Value;
        public AssignStatement(Variable target, Expression value,
            SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.AssignStatement, start, end) 
        {
            Target = target;
            Value = value;
        }
    }
}
