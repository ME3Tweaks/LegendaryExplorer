using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class ExpressionOnlyStatement : Statement
    {
        public Expression Value;

        public ExpressionOnlyStatement(Expression value, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.ExpressionStatement, start, end)
        {
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get { yield return Value; }
        }
    }
}
