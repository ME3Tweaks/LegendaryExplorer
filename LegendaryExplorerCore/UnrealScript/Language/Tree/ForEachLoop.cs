using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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
