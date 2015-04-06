using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class WhileLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;

        public WhileLoop(Expression cond, CodeBody body,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.WhileLoop, start, end)
        {
            Condition = cond;
            Body = body;
        }
    }
}
