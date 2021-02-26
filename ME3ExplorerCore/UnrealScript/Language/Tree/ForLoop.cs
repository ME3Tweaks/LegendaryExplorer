using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class ForLoop : Statement
    {
        public Expression Condition;
        public CodeBody Body;
        public Statement Init;
        public Statement Update;

        public ForLoop(Statement init, Expression cond, Statement update,
                       CodeBody body,
                       SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.WhileLoop, start, end)
        {
            Condition = cond;
            Body = body;
            Init = init;
            Update = update;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if (Init != null) yield return Init;
                if (Condition != null) yield return Condition;
                if (Update != null) yield return Update;
                if (Body != null) yield return Body;
            }
        }
    }
}
