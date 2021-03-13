using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class ForEachLoop : Statement
    {
        public Expression IteratorCall;
        public CodeBody Body;

        public int iteratorPopPos;

        public ForEachLoop(Expression iterator, CodeBody body, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.ForEachLoop, start, end)
        {
            IteratorCall = iterator;
            Body = body;
            iterator.Outer = this;
            body.Outer = this;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return IteratorCall;
                yield return Body;
            }
        }
    }
}
