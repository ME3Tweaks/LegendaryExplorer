using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CaseStatement : Statement
    {
        public Expression Value;

        public CaseStatement(Expression expr, int start, int end) 
            : base(ASTNodeType.CaseStatement, start, end) 
        {
            Value = expr;
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

        public ushort LocationOfNextCase; //only used during decomp
    }
}
