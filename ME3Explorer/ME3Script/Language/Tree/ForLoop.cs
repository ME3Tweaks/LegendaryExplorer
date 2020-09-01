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

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Init;
                yield return Condition;
                yield return Update;
                yield return Body;
            }
        }
    }
}
