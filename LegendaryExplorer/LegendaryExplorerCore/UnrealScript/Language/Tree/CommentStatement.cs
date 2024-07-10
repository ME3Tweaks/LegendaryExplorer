using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using System.Collections.Generic;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CommentStatement() : Statement(ASTNodeType.SingleLineComment, -1, -1)
    {
        public readonly List<string> CommentLines = [];
        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
