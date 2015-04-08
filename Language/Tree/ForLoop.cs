using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ForLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;
        public Statement Init;
        public Statement Update;

        public ForLoop(Expression cond, CodeBody body,
            Statement init, Statement update,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.WhileLoop, start, end)
        {
            Condition = cond;
            Body = body;
            Init = init;
            Update = update;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
