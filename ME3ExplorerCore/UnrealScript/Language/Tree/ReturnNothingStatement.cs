using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class ReturnNothingStatement : ReturnStatement
    {
        public ReturnNothingStatement(Expression value = null, SourcePosition start = null, SourcePosition end = null) : base(value, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
