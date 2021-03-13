using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class DefaultCaseStatement : Statement
    {
        public DefaultCaseStatement(SourcePosition start, SourcePosition end)
            : base(ASTNodeType.DefaultStatement, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
