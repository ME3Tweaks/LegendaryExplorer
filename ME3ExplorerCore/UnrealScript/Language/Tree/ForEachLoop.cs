using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
