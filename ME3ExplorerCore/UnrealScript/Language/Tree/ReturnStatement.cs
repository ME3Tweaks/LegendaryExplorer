using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class ReturnStatement : Statement
    {
        public Expression Value;

        public ReturnStatement(Expression value = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.ReturnStatement, start, end)
        {
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Value;
            }
        }
    }
}
