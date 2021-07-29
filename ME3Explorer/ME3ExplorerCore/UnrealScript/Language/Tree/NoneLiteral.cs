using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class NoneLiteral : Expression
    {
        public bool IsDelegateNone;

        public NoneLiteral(SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.NoneLiteral, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return null;
        }
    }
}
